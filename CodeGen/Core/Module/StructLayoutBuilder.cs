using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;
using LLVMSharp;
using LLVMSharp.Interop;
using StructType = BifyLang.CodeGen.Core.Types.Data.StructType;

namespace BifyLang.CodeGen.Core.Module;

public class StructLayoutBuilder(CodeGenContext ctx) : LayoutBuilder(ctx)
{
    private readonly List<(StructType? Type, AstStruct StructNode, ModuleInfo Module)> _built = [];

    public override void Declare(ModuleInfo[] modules)
    {
        var structs = modules.SelectMany(
            module => module.ModuleNode.ChildNodes.OfType<AstStruct>(),
            (module, structNode) => (module, structNode));
        _built.Clear();

        foreach (var (module, structNode) in structs)
        {
            try
            {
                _built.Add((DeclareOpaque(module, structNode), structNode, module));
            }
            catch (BifyCompilationException)
            {
                _built.Add((null, structNode, module));
            }
        }
    }

    public override void Build(ModuleInfo[] modules)
    {
        foreach (var (type, structNode, module) in _built)
        {
            if (type == null) continue;
            using (_ctx.EnterModule(module.ModuleContext))
            {
                ProcessFields(type, structNode);
                ProcessMethods(type, structNode);
            }
        }

        foreach (var (type, structNode, module) in _built)
        {
            if (type == null) continue;
            using (_ctx.EnterModule(module.ModuleContext))
            {
                if (HasCycle(type, [], []))
                {
                    _ctx.TypeRegistry.RegisterPoison(type.FullName);
                    Fail($"Cyclic struct dependency involving '{type.Name}'.", DiagnosticKind.TYPE, structNode);
                }

                FinalizeStructBody(type, structNode.NameNode);
            }
        }
    }

    private static bool HasCycle(StructType current, HashSet<string> visiting, HashSet<string> visited)
    {
        if (visited.Contains(current.FullName))
            return false;
        if (!visiting.Add(current.FullName))
            return true;

        foreach (var field in current.Fields.Values)
        {
            if (field.IsPoison)
                continue;
            if (field.Type is StructType nested && HasCycle(nested, visiting, visited))
                return true;
        }

        visiting.Remove(current.FullName);
        visited.Add(current.FullName);
        return false;
    }

    private StructType DeclareOpaque(ModuleInfo module, AstStruct structNode)
    {
        using (_ctx.EnterModule(module.ModuleContext))
        {
            var rawName = structNode.NameNode.Token.Value;
            var mangledName = NameMangler.GenerateTypeName(module.Name, rawName);
            var parentName = ClassDependencyResolver.ResolveParentName(_ctx, structNode.ParentNode, module.Name);
            if (!string.IsNullOrEmpty(parentName))
            {

                _ctx.TypeRegistry.RegisterPoison(mangledName);
                return Fail<StructType>("Structs cannot inherit from other types.", DiagnosticKind.TYPE, structNode.ParentNode);
            }

            var structType = new StructType(rawName, mangledName, _ctx.Context.CreateNamedStruct(mangledName),
                module.Name)
            {
                Flags = MemberResolver.ResolveTypeFlags(ctx,structNode),
            };
            if (_ctx.TypeRegistry.TryGetType(mangledName, out var _))
            {
                Error($"Struct with name '{rawName}' already exists in module '{module.Name}'.", DiagnosticKind.NAME, structNode.NameNode);
            }
            BifyDebug.Log($"Adding struct with name '{mangledName}'.");
            _ctx.TypeRegistry.Register(mangledName, structType);
            _ctx.ModuleContext.TypesInfo[mangledName] = structType;
            return structType;
        }
    }

    private void ProcessFields(StructType structType, AstStruct structNode)
    {
        var block = (AstBlock)structNode.BodyNode;
        int nextIndex = structType.Fields.Count;

        foreach (var varDecl in block.ChildNodes.OfType<AstVarDecl>())
        {
            _ctx.CurrentLine = varDecl.LineNumber;
            string name = varDecl.AssignmentNode.Left.Token.Value;

            if (structType.Fields.ContainsKey(name))
            {
                Error($"Duplicate field '{name}' in struct '{structType.Name}'.", DiagnosticKind.NAME, varDecl);
                continue;
            }

            if (structType.Methods.ContainsKey(name) || name == DestructorSynthesizer.DestructorName)
            {
                Error($"Field '{name}' in struct '{structType.Name}' conflicts with existing method.",
                    DiagnosticKind.NAME, varDecl);
                continue;
            }

            var field = ClassMemberCollector.CollectFieldOrPoison(varDecl, _ctx, structType, name, nextIndex);
            if (field.Type is ReferenceType)
            {
                Error("Structs cannot contain reference fields.", DiagnosticKind.TYPE, varDecl);
                continue;
            }

            nextIndex++;
            structType.Fields.Add(name, field);
        }
    }

    private void ProcessMethods(StructType structType, AstStruct structNode)
    {
        var block = (AstBlock)structNode.BodyNode;

        var funcDecls = block.ChildNodes.OfType<AstFunctionDecl>().ToList();

        foreach (var funcDecl in funcDecls)
        {
            _ctx.CurrentLine = funcDecl.LineNumber;
            string name = funcDecl.FunctionNameNode.Name;
            if (structType.Fields.ContainsKey(name))
            {
                Error($"Method '{name}' in struct '{structType.Name}' conflicts with existing field.",
                    DiagnosticKind.NAME, funcDecl);
                continue;
            }

            if (name is "destructor" or "constructor")
            {
                Error($"Struct '{structType.Name}' cannot declare a {name}.", DiagnosticKind.TYPE, funcDecl);
                continue;
            }

            TryAddMethod(funcDecl, structType, name);
        }
    }

    private void TryAddMethod(AstFunctionDecl funcDecl, StructType structType, string name)
    {
        try
        {
            var method = ClassMemberCollector.CollectMethod(funcDecl, _ctx, structType);

            if (structType.Methods.ContainsKey(name) && !method.IsOverride)
                Error($"Duplicate method '{name}' in struct '{structType.FullName}'.", DiagnosticKind.NAME, funcDecl);

            structType.Methods[name] = method;
        }
        catch (BifyCompilationException)
        {
            if (!structType.Methods.ContainsKey(name))
                structType.Methods[name] = ClassMemberCollector.CreatePoisonMethod(_ctx, name, structType);
        }
    }

    private void FinalizeStructBody(StructType structType, AstNode callSite)
    {

        List<LLVMTypeRef> elementTypes = [];

        foreach (var field in structType.Fields.Values.OrderBy(f => f.Index))
        {
            elementTypes.Add(field.Type.Type);
        }

        if (elementTypes.Count == 0)
        {
            Error("Structs must have at least one field.", DiagnosticKind.SYNTAX, callSite);
        }
        structType.LlvmStructType.StructSetBody(elementTypes.ToArray(), false);
    }
}
