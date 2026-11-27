#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Class;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Emitters;

public readonly record struct LValue(
    LLVMValueRef Pointer,
    BifyType Type,
    bool IsConstant,
    bool IsWeak,
    ClassField? Field = null,
    ICompositeType? DeclaringType = null
);

public class LValueResolver(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public bool TryResolveLValue(AstNode node, out LValue lvalue)
    {
        lvalue = default;
        return node switch
        {
            AstIdentifier id => TryResolveIdentifier(id, out lvalue),
            AstMemberAccess memberAccess => TryResolveMemberAccess(memberAccess, strict: false, out lvalue),
            _ => false
        };
    }


    public LValue ResolveLValue(AstNode node)
    {
        if (node is AstIdentifier id)
            return ResolveIdentifierStrict(id);
        if (node is AstMemberAccess memberAccess)
        {
            TryResolveMemberAccess(memberAccess, strict: true, out var lvalue);
            return lvalue;
        }

        return Fail<LValue>($"cannot assign to '{node.Token.Value}'", DiagnosticKind.TYPE, node);
    }

    private bool TryResolveIdentifier(AstIdentifier id, out LValue lvalue)
    {
        lvalue = default;
        string name = id.Name;
        if (name is "true" or "false" or "null")
            return false;
        if (_ctx.CurrentScope.TryLookup(name, out var symbol))
        {
            if (symbol.IsPoison)
                AbortPoison();
            if (symbol.Type.Type.Kind == LLVMTypeKind.LLVMFunctionTypeKind)
                return false;
            lvalue = new LValue(symbol.Pointer, symbol.Type, symbol.IsConstant, symbol.IsWeak);
            return true;
        }

        if (MemberResolver.TryResolveThisField(_ctx, name, out var fieldPtr, out var field))
        {
            bool thisIsConst = _ctx.CurrentScope.TryLookup("this", out var thisSym) &&
                               thisSym.Type is StructType &&
                               thisSym.IsConstant;
            lvalue = new LValue(fieldPtr, field.Type, thisIsConst || field.IsConst, field.IsWeak, field,
                field.DeclaringType);
            return true;
        }

        return false;
    }

    private LValue ResolveIdentifierStrict(AstIdentifier id)
    {
        if (TryResolveIdentifier(id, out var lvalue))
            return lvalue;
        string name = id.Name;
        if (name is "true" or "false" or "null" ||
            _ctx.ModulesMap.ContainsKey(name) ||
            _ctx.CurrentScope.TryLookup(name, out _))
        {
            return Fail<LValue>($"cannot assign to '{name}'", DiagnosticKind.TYPE, id);
        }

        return Fail<LValue>($"undeclared identifier '{name}'", DiagnosticKind.NAME, id);
    }

    private bool TryResolveMemberAccess(AstMemberAccess memberAccess, bool strict, out LValue lvalue)
    {
        lvalue = default;
        if (memberAccess.Left is AstIdentifier modId && _ctx.ModulesMap.ContainsKey(modId.Name))
        {
            if (strict)
                Fail($"cannot assign to module member '{memberAccess.Right.Token.Value}'", DiagnosticKind.TYPE,
                    memberAccess);
            return false;
        }

        if (memberAccess.Left is AstIdentifier classId && TypeResolver.TryResolve(classId, _ctx, out var type)  && type is not ClassType classType )
        {
            if (strict)
                Fail($"cannot assign to  member '{memberAccess.Right.Token.Value}'", DiagnosticKind.TYPE, memberAccess);
            return false;
        }

        if (memberAccess.Right is not AstIdentifier memberId)
        {
            if (strict)
                Fail("expected member identifier", DiagnosticKind.SYNTAX, memberAccess);
            return false;
        }

        if (TryResolveLValue(memberAccess.Left, out var leftLValue))
            return TryResolveFieldFromLValue(memberAccess, leftLValue, memberId.Name, strict, out lvalue);
        if (!strict)
            return false;
        return ResolveFieldFromRValue(memberAccess, memberId.Name, out lvalue);
    }

    private bool TryResolveFieldFromLValue(
        AstMemberAccess memberAccess,
        LValue leftLValue,
        string memberName,
        bool strict,
        out LValue lvalue)
    {
        lvalue = default;
        BifyDebug.Log($"lValie type =  {leftLValue.Type}, {memberName}");
        if (leftLValue.Type is WrappedCompositeType wpt)
        {
            if (TryLookupField(memberAccess, wpt.Type, memberName, strict, out var staticField))
            {
                return true;
            }
        }
        if (leftLValue.Type is not ICompositeType compositeType)
        {
            if (strict)
                Fail($"type '{leftLValue.Type.Name}' has no members", DiagnosticKind.TYPE,
                    memberAccess);
            return false;
        }


        if (!TryLookupField(memberAccess, compositeType, memberName, strict, out var field))
            return false;
        var basePtr = compositeType is StructType
            ? leftLValue.Pointer
            : LoadAndUpgradeClassPtr(leftLValue);
        bool baseIsConst = compositeType is StructType && leftLValue.IsConstant;
        lvalue = BuildFieldLValue(compositeType, basePtr, baseIsConst, field);
        return true;
    }

    private bool ResolveFieldFromRValue(AstMemberAccess memberAccess, string memberName, out LValue lvalue)
    {
        lvalue = default;
        var leftVal = new ExpressionDispatcher(_ctx).EmitExpression(memberAccess.Left);
        if (leftVal.Type is StructType)
            Fail("cannot assign to field of temporary struct", DiagnosticKind.TYPE, memberAccess);
        if (leftVal.Type is WrappedCompositeType wpt)
        {
            if (TryLookupField(memberAccess, wpt.Type, memberName, true, out var staticField) && staticField.IsStatic)
            {
                lvalue = BuildFieldLValue(wpt.Type, default, baseIsConst: false, staticField);
                return true;
            }

            return Fail<bool>($"cannot find static field '{memberName}' in class '{wpt.Type.Name}'",DiagnosticKind.ATTRIBUTE, memberAccess.Left);
        }
        if (leftVal.Type is not ClassType classType)
            return Fail<bool>($"type '{leftVal.Type.Name}' has no members", DiagnosticKind.TYPE,
                memberAccess);
        TryLookupField(memberAccess, classType, memberName, strict: true, out var field);
        var basePtr = leftVal.Value;
        if (leftVal.IsWeak)
        {
            basePtr = _ctx.HeapControl.BuildUpgradeWeak(basePtr);
            _ctx.HeapControl.TrackTemp(new TypedValue(basePtr, classType, false));
        }

        lvalue = BuildFieldLValue(classType, basePtr, baseIsConst: false, field);
        return true;
    }

    private bool TryLookupField(
        AstMemberAccess memberAccess,
        ICompositeType compositeType,
        string memberName,
        bool strict,
        out ClassField field)
    {
        if (compositeType.Fields.TryGetValue(memberName, out field!))
        {
            if (field.IsPoison)
                AbortPoison();
            return true;
        }

        if (strict)
        {
            if (compositeType.Methods.ContainsKey(memberName))
                Fail($"cannot assign to method '{memberName}'", DiagnosticKind.TYPE, memberAccess);
            Fail($"no member '{memberName}' in '{compositeType.Name}'", DiagnosticKind.ATTRIBUTE, memberAccess);
        }

        return false;
    }

    private LValue BuildFieldLValue(ICompositeType compositeType, LLVMValueRef basePtr, bool baseIsConst,
        ClassField field)
    {
        MemberResolver.CheckAccess(_ctx, compositeType, field.AccessLevel, field.Name);
        var fieldPtr = MemberResolver.ResolveFieldPtr(_ctx, compositeType, basePtr, field);
        bool isConst = compositeType is StructType ? baseIsConst || field.IsConst : field.IsConst;
        return new LValue(fieldPtr, field.Type, isConst, field.IsWeak, field, compositeType);
    }

    private LLVMValueRef LoadAndUpgradeClassPtr(LValue classLValue)
    {
        var loaded = _ctx.Builder.BuildLoad2(classLValue.Type.Type, classLValue.Pointer, "obj_ptr");
        if (!classLValue.IsWeak)
            return loaded;
        var upgraded = _ctx.HeapControl.BuildUpgradeWeak(loaded);
        _ctx.HeapControl.TrackTemp(new TypedValue(upgraded, classLValue.Type, false));
        return upgraded;
    }
}
