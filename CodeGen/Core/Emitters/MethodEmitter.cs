#nullable enable
using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Core.Emitters;

public class MethodEmitter(CodeGenContext ctx) : CodeGenBase(ctx)
{
    private readonly FunctionBodyEmitter _bodyEmitter = new(ctx);


    public void EmitMethod(ClassMethod classMethod, AstNode fallback)
    {
        if (classMethod.IsAbstract)
        {
            if (classMethod.Body != null)
            {
                _ctx.Error("abstract method cannot have body", DiagnosticKind.SYNTAX, fallback);
            }
            var entry = classMethod.FunctionValue.AppendBasicBlock("entry");
            _ctx.Builder.PositionAtEnd(entry);
            _ctx.Builder.BuildUnreachable();
            return;
        }
        var parameters = new List<ParameterInfo>
        {
            new("this", (BifyType)classMethod.DeclaringType, false, null)
        };
        parameters.AddRange(classMethod.Parameters);

        var returnType = classMethod.Signature.ReturnType;
        bool isDestructor = classMethod.Name == DestructorSynthesizer.DestructorName;
        bool isConstructor = classMethod.Name == "constructor";
        if (classMethod.FunctionValue.Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Method value is null.");
        }


        _bodyEmitter.Emit(
            classMethod.FunctionValue,
            returnType,
            parameters,
            classMethod.Body,
            isMethod: true,
            isDestructor: isDestructor,
            isConstructor: isConstructor,
            declaringClass: classMethod.DeclaringType);
    }
}
