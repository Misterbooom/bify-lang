#nullable enable
using System;
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

public class MemberAccessGen(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public TypedValue EmitMemberAccess(AstMemberAccess memberAccess)
    {
        if (TryEmitModuleMember(memberAccess, out var moduleMember))
            return moduleMember;
        var resolver = new LValueResolver(_ctx);
        if (resolver.TryResolveLValue(memberAccess, out var member))
        {
            var loaded = _ctx.Builder.BuildLoad2(member.Type.Type, member.Pointer);
            return new  TypedValue(loaded, member.Type, member.IsWeak);
        }
        var (target, compositeType, memberName) = ResolveTarget(memberAccess);

        if (compositeType.Fields.TryGetValue(memberName, out var field))
        {
            if (field.IsPoison)
                AbortPoison();
            if (compositeType is StructType)
            {
                MemberResolver.CheckAccess(_ctx, field.DeclaringType ?? compositeType, field.AccessLevel, field.Name);
                var fieldVal = _ctx.Builder.BuildExtractValue(target.Value, (uint)field.Index, field.Name);
                return new TypedValue(fieldVal, field.Type);
            }
            var fieldPtr = ResolveCheckedFieldPtr(target, compositeType, field);
            var loaded = _ctx.Builder.BuildLoad2(field.Type.Type, fieldPtr, field.Name);
            return new TypedValue(loaded, field.Type, field.IsWeak);
        }
        return Fail<TypedValue>($"no member '{memberName}' in '{compositeType.Name}'", DiagnosticKind.ATTRIBUTE, memberAccess);
    }

    private bool TryEmitModuleMember(AstMemberAccess memberAccess, out TypedValue result)
    {
        result = default;
        if (memberAccess.Left is not AstIdentifier modId ||
            !_ctx.ModulesMap.TryGetValue(modId.Name, out var moduleType))
            return false;

        string memberName = ExpectMemberName(memberAccess);
        if (TypeResolver.TryResolveIdentifier(memberName, modId.Name, _ctx, out var identifier) && identifier is ICompositeType comp)
        {

            result = new TypedValue(null, new WrappedCompositeType(comp));
            return true;
        }
        BifyDebug.Log($"ide: {identifier}");

        if (!moduleType.ModuleContext.CurrentScope.TryLookup(memberName, out var member))
            return Fail<bool>($"no member '{memberName}' in module '{modId.Name}'", DiagnosticKind.ATTRIBUTE, memberAccess);

        if (member.IsPoison)
            AbortPoison();

        result = new TypedValue(member.Pointer, member.Type);
        return true;
    }

    private (TypedValue Target, ICompositeType Type, string MemberName) ResolveTarget(AstMemberAccess memberAccess)
    {
        var target = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
        if (target.Type is not ICompositeType compositeType)
            return Fail<(TypedValue, ICompositeType, string)>($"type '{target.Type.Name}' has no members", DiagnosticKind.TYPE, memberAccess);

        string memberName = ExpectMemberName(memberAccess);
        if (target.IsWeak)
        {
            var upgraded = _ctx.HeapControl.BuildUpgradeWeak(target.Value);
            target = new TypedValue(upgraded, target.Type, false);
            _ctx.HeapControl.TrackTemp(target);
        }

        return (target, compositeType, memberName);
    }

    private LLVMValueRef ResolveCheckedFieldPtr(TypedValue target, ICompositeType compositeType, ClassField field)
    {
        MemberResolver.CheckAccess(_ctx, field.DeclaringType ?? compositeType, field.AccessLevel, field.Name);
        return MemberResolver.ResolveFieldPtr(_ctx, compositeType, target.Value, field);
    }


    private string ExpectMemberName(AstMemberAccess memberAccess) =>
        memberAccess.Right is AstIdentifier id
            ? id.Name
            : Fail<string>("expected member identifier", DiagnosticKind.SYNTAX, memberAccess);
}
