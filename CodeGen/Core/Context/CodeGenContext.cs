using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Module;
using BifyLang.CodeGen.Core.Runtime;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Context;

public class CodeGenContext : IDisposable
{
    public LLVMContextRef Context;
    public LLVMBasicBlockRef FunctionEntry;

    public LLVMModuleRef Module { get; private set; }
    public LLVMBuilderRef Builder { get; private set; }
    public ModuleContext ModuleContext;

    public Scope CurrentScope
    {
        get => ModuleContext.CurrentScope;
        private set => ModuleContext.CurrentScope = value;
    }

    public ICompositeType? CurrentCompositeType = null;
    public TypeRegistry TypeRegistry { get; private set; }
    public LoopContext LoopContext = new LoopContext(null, null, null, null);
    public BifyType FunctionReturnType;
    public readonly HeapControl HeapControl;
    public readonly DiagnosticBag Diagnostics;
    public int CurrentLine;
    public readonly Dictionary<string, ModuleType> ModulesMap = [];

    public CodeGenContext(DiagnosticBag? diagnostics = null)
    {
        Diagnostics = diagnostics ?? new DiagnosticBag();
        Context = LLVMContextRef.Create();
        Module = Context.CreateModuleWithName("main");
        Builder = Context.CreateBuilder();
        ModuleContext = new ModuleContext(this, "main", "");
        TypeRegistry = new TypeRegistry(this);
        HeapControl = new HeapControl(this);
        HeapControl.DefineRuntimeFunctions();
    }

    private SourceLocation CreateLocation(AstNode? node)
    {
        int line = node?.LineNumber is > 0 ? node.LineNumber : CurrentLine;
        return new SourceLocation(
            ModuleContext?.FullPath ?? "",
            line,
            node?.ColumnNumber ?? 0,
            ModuleContext?.Name,
            CurrentScope?.FunctionName
        );
    }

    public void Error(string msg, DiagnosticKind kind, AstNode? node = null)
    {
        Diagnostics.Report(msg, kind, CreateLocation(node));
    }

    [DoesNotReturn]
    public void Fail(string msg, DiagnosticKind kind, AstNode? node = null)
    {
        Diagnostics.Fail(msg, kind, CreateLocation(node));
    }

    [DoesNotReturn]
    public T Fail<T>(string msg, DiagnosticKind kind, AstNode? node = null)
    {
        return Diagnostics.Fail<T>(msg, kind, CreateLocation(node));
    }

    [DoesNotReturn]
    public void AbortPoison()
    {
        throw new BifyCompilationException();
    }

    [DoesNotReturn]
    public T AbortPoison<T>()
    {
        throw new BifyCompilationException();
    }

    private sealed class ScopeExitHandle(CodeGenContext ctx) : IDisposable
    {
        public void Dispose()
        {
            ctx.CurrentScope.ReleaseLocals(ctx);
            ctx.CurrentScope = ctx.CurrentScope.Parent
                               ?? throw new InvalidOperationException("Cannot exit root scope");
        }
    }

    private sealed class LoopExitHandle(CodeGenContext ctx) : IDisposable
    {
        public void Dispose()
        {
            ctx.LoopContext = ctx.LoopContext.Parent;
        }
    }

    private sealed class ModuleExitHandle(CodeGenContext ctx, ModuleContext previousContext) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ctx.ModuleContext = previousContext;
        }
    }

    public IDisposable EnterScope()
    {
        CurrentScope = new Scope(parent: CurrentScope);
        return new ScopeExitHandle(this);
    }

    public IDisposable EnterModule(ModuleContext newModuleContext)
    {
        var previous = ModuleContext;
        ModuleContext = newModuleContext;
        BuiltinFunctions.DeclarePrintf(this);

        return new ModuleExitHandle(this, previous);
    }

    public IDisposable EnterModule(string name, string path)
    {
        return EnterModule(new ModuleContext(this, name, path));
    }

    public void ReleaseScopesUpTo(Scope targetScope)
    {
        var scope = CurrentScope;
        while (scope != null && scope != targetScope)
        {
            scope.ReleaseLocals(this);
            scope = scope.Parent;
        }
    }

    /// <summary>
    /// use before EnterScope
    /// </summary>
    public IDisposable EnterLoop(LLVMBasicBlockRef breakBlock, LLVMBasicBlockRef continueBlock)
    {
        LoopContext = new LoopContext(LoopContext, breakBlock, continueBlock, CurrentScope);
        return new LoopExitHandle(this);
    }

    public LLVMValueRef BuildEntryAlloca(LLVMTypeRef type, string name)
    {
        if (FunctionEntry.Handle == IntPtr.Zero)
            throw new InvalidOperationException("BuildEntryAlloca called outside of a function body.");

        var cachedBlock = Builder.InsertBlock;
        var terminator = FunctionEntry.Terminator;
        if (terminator.Handle != IntPtr.Zero)
            Builder.PositionBefore(terminator);
        else
            Builder.PositionAtEnd(FunctionEntry);

        var alloca = Builder.BuildAlloca(type, name);

        if (cachedBlock.Handle != IntPtr.Zero)
            Builder.PositionAtEnd(cachedBlock);
        return alloca;
    }

    public LLVMModuleRef GenerateModule(AstModule rootModule)
    {
        var rootContext = new ModuleContext(this, rootModule.Name, rootModule.ModulePath);
        var rootInfo = new ModuleInfo(rootModule.Name, rootModule.ModulePath, rootModule, rootContext);
        var pool = new ModulePoolCollector(this).Collect(rootModule);
        ModuleInfo[] allModuleInfos = [.. pool, rootInfo];
        new ModuleLayoutBuilder(this).Build(allModuleInfos);
        return Module;
    }

    public void Dispose()
    {
        Builder.Dispose();
        Module.Dispose();
        Context.Dispose();
    }
}
