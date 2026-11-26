#nullable enable
using System.Linq;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.Exceptions;
using LLVMSharp.Interop;

namespace BifyLang.CodeGen.Core.Validators;

public class FunctionPathChecker
{
    public bool AllPathsReturn { get; }

    private readonly BifyType _functionType;
    private readonly CodeGenContext? _ctx;
    private readonly bool _isVoid;

    public FunctionPathChecker(BifyType functionType, AstNode? node, AstNode nameNode,CodeGenContext? ctx = null)
    {
        _functionType = functionType;
        _ctx = ctx;
        _isVoid = _functionType.Type.Kind == LLVMTypeKind.LLVMVoidTypeKind;

        if (node == null) return;

        AllPathsReturn = Check(node);
        if (!_isVoid && !AllPathsReturn)
        {
            _ctx?.Fail($"missing return value of type '{_functionType.Name}'", DiagnosticKind.TYPE, nameNode);
        }
    }

    public FunctionPathChecker(AstFunctionDecl functionDecl, CodeGenContext ctx)
        : this(TypeResolver.Resolve(functionDecl.TypeNode, ctx), functionDecl.BlockNode,functionDecl.FunctionNameNode, ctx)
    {
    }

    public bool Check(AstNode? node)
    {
        if (node == null) return false;

        if (_ctx != null && node.Token is { Line: > 0 })
            _ctx.CurrentLine = node.Token.Line;

        return node switch
        {
            AstLine line => Check(line.Child),
            AstReturn astReturn => CheckReturn(astReturn),
            AstBlock astBlock => CheckBlock(astBlock),
            AstIf astIf => CheckIf(astIf),
            AstWhile astWhile => CheckLoop(astWhile.ConditionNode, astWhile.BlockNode, allowNullCondition: false),
            AstFor astFor => CheckLoop(astFor.ConditionNode, astFor.BlockNode, allowNullCondition: true),
            AstSwitch astSwitch => CheckSwitch(astSwitch),
            _ => false
        };
    }

    private bool CheckReturn(AstReturn astReturn)
    {
        bool hasArgs = astReturn.ArgumentsNode != null;

        if (!hasArgs && !_isVoid)
        {
            _ctx?.Fail($"expected type '{_functionType.Name}', found 'void'", DiagnosticKind.TYPE, astReturn);
        }
        else if (hasArgs && _isVoid)
        {
            _ctx?.Fail("cannot return value from 'void' function", DiagnosticKind.TYPE, astReturn);
        }

        return true;
    }

    private bool CheckBlock(AstBlock astBlock)
    {
        bool blockReturns = false;

        foreach (var child in astBlock.ChildNodes)
        {
            blockReturns |= Check(child);
        }

        return blockReturns;
    }

    private bool CheckIf(AstIf astIf)
    {
        bool allReturn = Check(astIf.BlockNode);

        foreach (var elseIf in astIf.ElseIfNodes)
        {
            allReturn &= Check(elseIf.BlockNode);
        }

        if (astIf.ElseNode?.BlockNode == null)
            return false;

        return Check(astIf.ElseNode.BlockNode) && allReturn;
    }

    private bool CheckLoop(AstNode? conditionNode, AstNode? blockNode, bool allowNullCondition)
    {
        Check(blockNode);

        return IsInfiniteCondition(conditionNode, allowNullCondition) && !HasBreak(blockNode);
    }

    private bool CheckSwitch(AstSwitch astSwitch)
    {
        bool allCasesReturn = true;

        foreach (var c in astSwitch.Cases)
        {
            bool caseReturns = Check(c.BlockNode);
            bool caseBreaks = HasBreak(c.BlockNode);
            allCasesReturn &= caseReturns && !caseBreaks;
        }

        if (astSwitch.DefaultNode == null)
            return false;

        bool defaultReturns = Check(astSwitch.DefaultNode.BlockNode) && !HasBreak(astSwitch.DefaultNode.BlockNode);
        return allCasesReturn && defaultReturns;
    }

    private static bool IsInfiniteCondition(AstNode? conditionNode, bool allowNull) =>
        conditionNode switch
        {
            null => allowNull,
            AstIdentifier { Name: "true" or "1" } => true,
            AstBoolean { Value: true } => true,
            AstInteger { Value: not 0 } => true,
            _ => false
        };

    private static bool HasBreak(AstNode? node) =>
        node switch
        {
            null or AstWhile or AstFor or AstSwitch => false,
            AstBreak => true,
            AstLine line => HasBreak(line.Child),
            AstBlock block => BlockHasReachableBreak(block),
            AstIf ifNode => HasBreak(ifNode.BlockNode)
                            || ifNode.ElseIfNodes.Any(e => HasBreak(e.BlockNode))
                            || HasBreak(ifNode.ElseNode?.BlockNode),
            _ => false
        };

    private static bool BlockHasReachableBreak(AstBlock block)
    {
        foreach (var child in block.ChildNodes)
        {
            var stmt = child is AstLine line ? line.Child : child;
            if (stmt is AstReturn)
                return false;
            if (HasBreak(stmt))
                return true;
        }
        return false;
    }
}
