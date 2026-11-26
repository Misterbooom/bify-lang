#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Expressions;

public class NewGen(CodeGenContext ctx) : CodeGenBase(ctx)
{
    public TypedValue EmitNew(AstNew astNew)
    {

        var type = ResolveType(astNew.ValueNode);

        return type switch
        {
            ClassType classType => EmitClassNew(classType, astNew.Arguments, astNew.ValueNode),
            StructType structType => EmitStructNew(structType, astNew.Arguments, astNew.ValueNode),
            _ => Fail<TypedValue>($"Type '{type.Name}' cannot be instantiated.", DiagnosticKind.TYPE,
                astNew.ValueNode)
        };


    }

    private TypedValue EmitClassNew(ClassType type, AstNode[] argsNode, AstNode callSite)
    {
        if (type.Flags.HasFlag(TypeFlags.ABSTRACT))
        {
            _ctx.Fail($"Cannot instantiate abstract class '{type.Name}'",DiagnosticKind.CANNOT_INSTANTIATE_ABSTRACT_CLASS, callSite);
        }
        var instancePtr = new ClassBuilder(_ctx).BuildInstance(type);
        InvokeConstructor(type, instancePtr, argsNode, callSite);

        return new TypedValue(instancePtr, type);
    }

    private TypedValue EmitStructNew(StructType type, AstNode[] argsNode, AstNode callSite)
    {
        var instancePtr = new ClassBuilder(_ctx).BuildInstance(type);
        InvokeStructConstructor(type, instancePtr, argsNode, callSite);
        var loaded = _ctx.Builder.BuildLoad2(type.LlvmStructType, instancePtr, type.Name + "_loaded");
        return new TypedValue(loaded, type);
    }

    private void InvokeStructConstructor(StructType type, LLVMValueRef instancePtr, AstNode[] argNodes, AstNode callSite)
    {
        var resolvedArgs = ArgumentResolver.Resolve(
            _ctx, argNodes, FieldsToParameters(type.Fields), callSite,
            $"Constructor '{type.Name}'");
        if (resolvedArgs.Any(i => i != null && i.isPositional ))
        {
            Fail("Struct initialization requires named arguments.",DiagnosticKind.SYNTAX, callSite);
        }
        var pairs = type.Fields.GroupJoin(
            resolvedArgs,
            field => field.Key,
            arg => arg?.Name,
            (field, matchedArgs) => (StructField: field.Value, ArgumentInfo: matchedArgs.FirstOrDefault())
        );
        foreach (var (structField, argumentInfo) in pairs)
        {
            MemberResolver.CheckAccess(_ctx, type, structField.AccessLevel, structField.Name);
            var ptr = MemberResolver.ResolveFieldPtr(_ctx, type, instancePtr, structField);
            _ctx.Builder.BuildStore(argumentInfo!.Value.Value, ptr);

        }


    }

    private ParameterInfo[] FieldsToParameters(Dictionary<string, ClassField> fields)
    {
        List<ParameterInfo> parameters = [];
        foreach (var (name, field) in fields)
        {
            var param = new ParameterInfo(field.Name, field.Type, field.IsConst, field.ValueNode);
            parameters.Add(param);
        }

        return parameters.ToArray();
    }

    private ICompositeType ResolveType(AstNode valueNode)
    {
        string? rawName = valueNode switch
        {
            AstIdentifier id => NameMangler.GenerateTypeName(_ctx.ModuleContext.Name, id.Name),
            AstMemberAccess { Left: AstIdentifier mod, Right: AstIdentifier target } => NameMangler.GenerateTypeName(
                mod.Name, target.Name),
            _ => null
        };

        if (rawName == null)
            return Fail<ICompositeType>("Expected type identifier after 'new'", DiagnosticKind.SYNTAX, valueNode);

        if (TypeResolver.Resolve(valueNode, _ctx) is not ICompositeType type)
            return Fail<ICompositeType>($"Type '{rawName}' cannot be instantiated.", DiagnosticKind.TYPE, valueNode);

        return type;
    }

    private void InvokeConstructor(ClassType type, LLVMValueRef instancePtr, AstNode[] argNodes, AstNode callSite)
    {
        if (!type.Methods.TryGetValue("constructor", out var ctor))
        {
            throw new InvalidOperationException(
                "Cannot find constructor in class");
        }

        if (ctor.IsPoison)
            AbortPoison();

        var resolvedArgs =
            ArgumentResolver.Resolve(_ctx, argNodes, ctor.Parameters, callSite, $"Constructor '{type.Name}'");
        var func = ResolveConstructorFn(ctor);
        LLVMValueRef[] args = [instancePtr, .. resolvedArgs.Select(a => a.Value.Value)];
        _ctx.Builder.BuildCall2(ctor.Signature.Type, func, args);
    }

    private LLVMValueRef ResolveConstructorFn(ClassMethod ctor)
    {
        if (ctor.FunctionValue.Handle != IntPtr.Zero)
            return ctor.FunctionValue;

        string symbol = ctor.Signature is CallableType c ? c.FullName : $"{ctor.DeclaringType.FullName}_constructor";
        var func = _ctx.Module.GetNamedFunction(symbol);
        if (func.Handle == IntPtr.Zero)
            Fail($"Constructor '{symbol}' is not defined", DiagnosticKind.NAME);

        return func;
    }
}
