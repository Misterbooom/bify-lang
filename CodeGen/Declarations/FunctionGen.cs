#nullable enable
using System;
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Emitters;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Declarations;

public class FunctionGen(CodeGenContext ctx) : CodeGenBase(ctx)
{
    private readonly FunctionBodyEmitter _bodyEmitter = new(ctx);

    public LLVMValueRef DeclareFunction(AstFunctionDecl functionDecl)
    {
        string functionName = functionDecl.FunctionNameNode.Name;
        var mangledName = NameMangler.GenerateFunctionName(_ctx.ModuleContext.Name, functionName);
        AccessLevel accessLevel = functionDecl.FlagNode.HasFlag("public") ? AccessLevel.PUBLIC : AccessLevel.PRIVATE;

        var existingFn = _ctx.Module.GetNamedFunction(mangledName);
        if (existingFn.Handle != 0)
        {
            return existingFn;
        }

        if (!FlagChecker.CheckFunctionFlags(functionDecl.FlagNode.ToStringArray(), out var error))
        {
            _ctx.Error(error, DiagnosticKind.SYNTAX, functionDecl.FlagNode);
        }

        if (functionDecl.BlockNode == null)
        {
            _ctx.Error("Functions must have body.", DiagnosticKind.SYNTAX, functionDecl.FunctionNameNode);
        }
        var returnType = TypeResolver.Resolve(functionDecl.TypeNode, _ctx);
        var parameters = ParameterResolver.Resolve(functionDecl.ArgumentsNode, _ctx);
        var paramTypes = parameters.Select(p => p.Type.Type).ToArray();
        VisibilityChecker.CheckFunctionSignature(_ctx, accessLevel, returnType, parameters, functionDecl);


        var functionType = LLVMTypeRef.CreateFunction(returnType.Type, paramTypes, false);
        var functionValue = _ctx.Module.AddFunction(mangledName, functionType);

        _ctx.CurrentScope.Define(functionName, functionValue, new CallableType(functionName, mangledName, [..parameters], returnType, functionType), accessLevel:accessLevel);

        return functionValue;
    }


    public void EmitFunctionBody(AstFunctionDecl functionDecl)
    {
        string functionName = functionDecl.FunctionNameNode.Name;
        if (functionDecl.IsPoison || (_ctx.CurrentScope.TryLookup(functionName, out var sym) && sym.IsPoison))
            return;

        var mangledName = NameMangler.GenerateFunctionName(_ctx.ModuleContext.Name, functionName);
        var returnType = TypeResolver.Resolve(functionDecl.TypeNode, _ctx);

        _ = new FunctionPathChecker(functionDecl, _ctx);

        var parameters = ParameterResolver.Resolve(functionDecl.ArgumentsNode, _ctx);

        var functionValue = _ctx.Module.GetNamedFunction(mangledName);
        if (functionValue.Handle == 0)
            throw new InvalidOperationException("Can't find function in module.");

        _bodyEmitter.Emit(functionValue, returnType, parameters, functionDecl.BlockNode);
    }

    public void EmitFunction(AstFunctionDecl functionDecl)
    {
        if (functionDecl.IsPoison)
            return;

        EmitFunctionBody(functionDecl);
    }
}
