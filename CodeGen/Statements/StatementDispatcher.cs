using System;
using System.Collections.Generic;
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Expressions;
using BifyLang.CodeGen.Statements.ControlFlow;
using BifyLang.Exceptions;

namespace BifyLang.CodeGen.Statements;

public class StatementDispatcher(CodeGenContext ctx) : CodeGenBase(ctx)
{

    public void EmitBlock(AstBlock blockNode) => EmitBlock(blockNode.ChildNodes);

    public void EmitBlock(List<AstNode> nodeList)
    {
        foreach (var node in nodeList)
        {
            if (_ctx.Builder.InsertBlock.Terminator.Handle != IntPtr.Zero)
                break;

            EmitStatementSafely(node);
        }
    }

    private void EmitStatementSafely(AstNode node)
    {
        try
        {
            EmitStatement(node);
        }
        catch (BifyCompilationException)
        {
        }
    }

    public void EmitStatement(AstNode node)
    {
        if (node is AstLine line)
        {
            node = line.Child;
        }

        if (node == null || node is AstPoison)
            return;

        _ctx.CurrentLine = node.LineNumber;

        switch (node)
        {
            case AstPoison:
                break;
            case AstVarDecl varDecl:
                new VariableDeclGen(_ctx).EmitVariableDecl(varDecl);
                break;
            case AstReturn astReturn:
                new ReturnGen(_ctx).EmitReturn(astReturn);
                break;
            case AstAssignmentOperator astAssignmentOperator:
                using (_ctx.HeapControl.BeginTempScope())
                    new AssignmentGen(_ctx).EmitAssign(astAssignmentOperator);
                break;
            case AstUnaryOperator astUnaryOperator:
                using (_ctx.HeapControl.BeginTempScope())
                    new UnaryOpGen(_ctx).EmitUnaryOperator(astUnaryOperator, true);
                break;
            case AstIf astIf:
                new IfGen(_ctx).EmitIf(astIf);
                break;
            case AstWhile astWhile:
                new WhileGen(_ctx).EmitWhile(astWhile);
                break;
            case AstFor astFor:
                new ForGen(_ctx).EmitFor(astFor);
                break;
            case AstCall or AstNew:
                using (_ctx.HeapControl.BeginTempScope())
                    new ExpressionDispatcher(_ctx).EmitExpression(node);
                break;
            case AstBreak astBreak:
                new JumpGen(_ctx).EmitBreak(astBreak);
                break;
            case AstContinue astContinue:
                new JumpGen(_ctx).EmitContinue(astContinue);
                break;
            case AstSwitch astSwitch:
                new SwitchGen(_ctx).EmitSwitch(astSwitch);
                break;
            default:
                Fail("invalid statement", DiagnosticKind.SYNTAX, node);
                break;
        }
    }
}