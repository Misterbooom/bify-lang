#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Module;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Class;

public class ClassLayoutBuilder(CodeGenContext ctx) : LayoutBuilder(ctx)
{
    private readonly List<(ClassType? Type, ClassInfo Info)> _built = [];

    public  override void Declare(ModuleInfo[] modules)
    {
        var dependency = new ClassDependencyResolver(_ctx);
        var classes = dependency.Resolve(modules.Select(i => i.ModuleNode));
        _built.Clear();

        foreach (var info in classes)
        {
            var ownerCtx = _ctx.ModulesMap[info.ModuleName].ModuleContext;
            using (_ctx.EnterModule(ownerCtx))
            {
                _ctx.CurrentLine = info.ClassNode.LineNumber;
                try
                {
                    _built.Add((DeclareOpaque(info), info));
                }
                catch (BifyCompilationException)
                {
                    _built.Add((null, info));
                }
            }
        }
    }

    public override void Build(ModuleInfo[] modules)
    {
        foreach (var (type, info) in _built)
        {
            if (type == null) continue;
            var ownerCtx = _ctx.ModulesMap[info.ModuleName].ModuleContext;
            using (_ctx.EnterModule(ownerCtx))
            {
                _ctx.CurrentLine = info.ClassNode.LineNumber;
                BuildBody(type, info);
            }
        }
    }

    private ClassType DeclareOpaque(ClassInfo info)
    {
        var structType = _ctx.Context.CreateNamedStruct(info.Name);
        var pointerType = LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0);

        ClassType? parent = null;
        string parentName = ClassDependencyResolver.ResolveParentName(_ctx, info.ClassNode.ParentNode, info.ModuleName);
        if (!string.IsNullOrEmpty(parentName))
        {
            if (_ctx.TypeRegistry.TryGetType(parentName, out var pType) && pType is ClassType cp)
                parent = cp;
            else
                Fail($"unknown base class '{parentName}'", DiagnosticKind.TYPE,
                    info.ClassNode);
        }

        string mangledName = NameMangler.GenerateTypeName(info.ModuleName, info.Name);

        var classType = new ClassType(info.Name, mangledName, pointerType, structType, info.ModuleName, parent)
        {
            MinType = info.MinType,
            MaxType = info.MaxType,
            Flags = MemberResolver.ResolveTypeFlags(ctx, info.ClassNode),
        };

        if (parent != null)
            Validators.VisibilityChecker.CheckInheritance(_ctx, classType, parent, info.ClassNode.ParentNode);

        _ctx.TypeRegistry.Register(mangledName, classType);
        _ctx.ModuleContext.TypesInfo[mangledName] = classType;
        return classType;
    }

    private void BuildBody(ClassType type, ClassInfo info)
    {
        var classType = _ctx.TypeRegistry.GetBifyType(type.FullName) as ClassType;
        if (classType == null)
            throw new Exception($"Class: '{type.FullName}' was not registred in TypeRegistry.");

        InheritMembers(classType, info.ClassNode.ParentNode);
        ProcessFields(info, classType);
        ProcessMethods(info, classType);
        FinalizeStructBody(classType);
    }

    private void InheritMembers(ClassType classType, AstNode fallbackNode)
    {

        var parent = classType.Parent;
        classType.VTable = VTableBuilder.Inherit(parent);

        if (parent == null)
            return;
        if (parent.Flags.HasFlag(TypeFlags.FINAL))
        {
            _ctx.Error("cannot inherit from final class", DiagnosticKind.TYPE, fallbackNode);
        }

        foreach (var kvp in parent.Fields)
            classType.Fields.Add(kvp.Key, kvp.Value);

        foreach (var kvp in parent.Methods)
        {
            if (kvp.Key != "constructor" && kvp.Key != DestructorSynthesizer.DestructorName)
                classType.Methods.Add(kvp.Key, kvp.Value);
        }
    }

    private void ProcessFields(ClassInfo info, ClassType classType)
    {
        var block = (AstBlock)info.ClassNode.BodyNode;
        int nextIndex = 3 + classType.Fields.Count;

        foreach (var varDecl in block.ChildNodes.OfType<AstVarDecl>())
        {
            _ctx.CurrentLine = varDecl.LineNumber;
            string name = varDecl.AssignmentNode.Left.Token.Value;

            if (classType.Fields.ContainsKey(name))
            {
                Error($"duplicate field '{name}' in '{info.Name}'", DiagnosticKind.NAME, varDecl);
                continue;
            }

            if (classType.Methods.ContainsKey(name) || name == DestructorSynthesizer.DestructorName)
            {
                Error($"field '{name}' conflicts with method in '{info.Name}'", DiagnosticKind.NAME, varDecl);
                continue;
            }

            classType.Fields.Add(name, ClassMemberCollector.CollectFieldOrPoison(varDecl, _ctx, classType, name, nextIndex++));
        }
    }

    private void ProcessMethods(ClassInfo info, ClassType classType)
    {
        var block = (AstBlock)info.ClassNode.BodyNode;
        var funcDecls = block.ChildNodes.OfType<AstFunctionDecl>().ToList();

        ProcessDestructor(info, classType, funcDecls);
        bool hasConstructor = false;
        foreach (var funcDecl in funcDecls)
        {
            _ctx.CurrentLine = funcDecl.LineNumber;
            string name = funcDecl.FunctionNameNode.Name;
            if (name == DestructorSynthesizer.DestructorName)
                continue;
            if (name == "constructor")
            {
                hasConstructor = true;
            }

            if (classType.Fields.ContainsKey(name))
            {
                Error($"method '{name}' conflicts with field in '{classType.FullName}'", DiagnosticKind.NAME, funcDecl);
                continue;
            }
            TryAddMethod(funcDecl, classType, name);
        }

        if (!hasConstructor)
        {
            SynthesizeConstructor(classType);
        }

        AllAbstractsImplemented(classType, info.ClassNode.NameNode);
    }

    private bool AllAbstractsImplemented(ClassType classType, AstNode fallbackNode)
    {
        if (classType.Flags.HasFlag(TypeFlags.ABSTRACT))
            return true;

        ClassType? parent = classType.Parent;
        if (parent == null)
        {
            return true;
        }
        foreach (var (parentMethodName, parentMethod) in parent.Methods)
        {
            if (!parentMethod.IsAbstract) continue;
            if (classType.Methods.TryGetValue(parentMethodName, out var method) && !method.IsOverride)
            {
                _ctx.Error($"unimplemented abstract method '{method.Name}'", DiagnosticKind.ABSTRACT_METHOD_NOT_IMPLEMENTED, fallbackNode);
            }
        }

        return false;
    }
    private void TryAddMethod(AstFunctionDecl funcDecl, ClassType classType, string name)
    {
        try
        {
            _ = new FunctionPathChecker(funcDecl, _ctx);
            var method = ClassMemberCollector.CollectMethod(funcDecl, _ctx, classType);
            int vtableIndex = VTableBuilder.Apply(_ctx, classType.VTable, method, funcDecl);
            method.VTableIndex = vtableIndex;

            if (classType.Methods.ContainsKey(name) && !method.IsOverride)
                Fail($"duplicate method '{name}' in '{classType.FullName}'", DiagnosticKind.NAME, funcDecl);

            classType.Methods[name] = method;
        }
        catch (BifyCompilationException)
        {
            if (!classType.Methods.ContainsKey(name))
                classType.Methods[name] = ClassMemberCollector.CreatePoisonMethod(_ctx, name, classType);
        }
    }

    private void SynthesizeConstructor(ClassType classType)
    {
        var ctor = ConstructorSynthesizer.CreateDefault(_ctx, classType);
        classType.Methods["constructor"] = ctor;
    }
    private void ProcessDestructor(ClassInfo info, ClassType classType, List<AstFunctionDecl> funcDecls)
    {
        var dtors = funcDecls.Where(f => f.FunctionNameNode.Name == DestructorSynthesizer.DestructorName).ToList();
        if (dtors.Count > 1)
            Error($"duplicate destructor in '{info.Name}'", DiagnosticKind.NAME, dtors[1]);

        var dtor = dtors.FirstOrDefault();
        if (dtor != null)
        {
            try
            {
                _ = new FunctionPathChecker(dtor, _ctx);
            }
            catch (BifyCompilationException)
            {
            }
        }

        var method = ClassMemberCollector.CollectDestructorOrDefault(dtor, _ctx, classType);
        method.VTableIndex = VTableBuilder.ApplyDestructor(classType.VTable, method);
        classType.Methods[DestructorSynthesizer.DestructorName] = method;
    }

    private void FinalizeStructBody(ClassType classType)
    {
        var elementTypes = new List<LLVMTypeRef>
        {
            LLVMTypeRef.CreatePointer(_ctx.Context.Int8Type, 0),
            _ctx.Context.Int32Type,
            _ctx.Context.Int32Type
        };

        foreach (var field in classType.Fields.Values.OrderBy(f => f.Index))
        {
            elementTypes.Add(field.Type.Type);
        }

        classType.StructType.StructSetBody(elementTypes.ToArray(), false);
    }
}
