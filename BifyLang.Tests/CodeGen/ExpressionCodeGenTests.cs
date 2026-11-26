#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.CodeGen.Core.Types.Service;
using BifyLang.CodeGen.Expressions;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using BifyLang.Tests.Infrastructure;
using LLVMSharp.Interop;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ExpressionCodeGenTests : BifyTestBase
{
    [Test]
    public void ExprCodeGen_IntegerLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.NUMBER, "42");
        var node = new AstInteger(token, 42);

        TypedValue result = dispatcher.EmitExpression(node);

        Assert.That(result.Type.Name, Is.EqualTo("int"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(result.Type.Type.IntWidth, Is.EqualTo(32));
        Assert.That(result.Value.ConstIntSExt, Is.EqualTo(42));
    }

    [Test]
    public void ExprCodeGen_FloatLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.NUMBER, "3.14");
        var node = new AstFloat(token, 3.14f);

        TypedValue result = dispatcher.EmitExpression(node);

        Assert.That(result.Type.Name, Is.EqualTo("float"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMFloatTypeKind));
    }

    [Test]
    public void ExprCodeGen_BoolLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);

        var trueNode = new AstBoolean(new Token(TokenType.TRUE, "true"), true);
        TypedValue trueResult = dispatcher.EmitExpression(trueNode);

        var falseNode = new AstBoolean(new Token(TokenType.FALSE, "false"), false);
        TypedValue falseResult = dispatcher.EmitExpression(falseNode);

        Assert.That(trueResult.Type.Name, Is.EqualTo("bool"));
        Assert.That(trueResult.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(trueResult.Type.Type.IntWidth, Is.EqualTo(1));
        Assert.That(trueResult.Value.ConstIntZExt, Is.EqualTo(1));

        Assert.That(falseResult.Type.Name, Is.EqualTo("bool"));
        Assert.That(falseResult.Value.ConstIntZExt, Is.EqualTo(0));
    }

    [Test]
    public void ExprCodeGen_NullLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.NULL, "null");
        var node = new AstNull(token);

        TypedValue result = dispatcher.EmitExpression(node);

        Assert.That(result.Type.Name, Is.EqualTo("null"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMPointerTypeKind));
    }

    [Test]
    public void ExprCodeGen_StringLiteral()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var token = new Token(TokenType.STRING, "hello world");
        var node = new AstString(token, "hello world");

        TypedValue result = dispatcher.EmitExpression(node);

        Assert.That(result.Type.Name, Is.EqualTo("string"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMPointerTypeKind));
        Assert.That(result.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_IdentifierLookup()
    {
        using var h = CreateExprContext("testLookup");
        var intType = BifyType.CreateInt(h.Ctx);
        var alloca = h.Ctx.Builder.BuildAlloca(intType.Type, "x");
        h.Ctx.CurrentScope.Define("x", alloca, intType);

        var idNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "x"), "x");
        TypedValue result = h.Dispatcher.EmitExpression(idNode);

        Assert.That(result.Type.Name, Is.EqualTo("int"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(result.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_UndefinedIdentifierThrows()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var idNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "undefinedVar"), "undefinedVar");

        BifyAssert.ActionThrows(DiagnosticKind.NAME, () => dispatcher.EmitExpression(idNode), diagnostics: ctx.Diagnostics);
    }

    [Test]
    public void ExprCodeGen_BinaryArithmetic()
    {
        using var h = CreateExprContext("testArith");

        var left = new AstInteger(new Token(TokenType.NUMBER, "10"), 10);
        var right = new AstInteger(new Token(TokenType.NUMBER, "20"), 20);
        var addNode = new AstBinaryOp(new Token(TokenType.ADD, "+"), left, right);
        TypedValue addResult = h.Dispatcher.EmitExpression(addNode);

        var fLeft = new AstFloat(new Token(TokenType.NUMBER, "2.5"), 2.5f);
        var fRight = new AstFloat(new Token(TokenType.NUMBER, "4.0"), 4.0f);
        var mulNode = new AstBinaryOp(new Token(TokenType.MUL, "*"), fLeft, fRight);
        TypedValue mulResult = h.Dispatcher.EmitExpression(mulNode);

        Assert.That(addResult.Type.Name, Is.EqualTo("int"));
        Assert.That(addResult.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
        Assert.That(mulResult.Type.Name, Is.EqualTo("float"));
        Assert.That(mulResult.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_BinaryComparison()
    {
        using var h = CreateExprContext("testCmp");

        var left = new AstInteger(new Token(TokenType.NUMBER, "10"), 10);
        var right = new AstInteger(new Token(TokenType.NUMBER, "20"), 20);
        var ltNode = new AstBinaryOp(new Token(TokenType.LT, "<"), left, right);
        TypedValue ltResult = h.Dispatcher.EmitExpression(ltNode);

        var bLeft = new AstBoolean(new Token(TokenType.TRUE, "true"), true);
        var bRight = new AstBoolean(new Token(TokenType.FALSE, "false"), false);
        var eqNode = new AstBinaryOp(new Token(TokenType.EQEQ, "=="), bLeft, bRight);
        TypedValue eqResult = h.Dispatcher.EmitExpression(eqNode);

        Assert.That(ltResult.Type.Name, Is.EqualTo("bool"));
        Assert.That(ltResult.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(eqResult.Type.Name, Is.EqualTo("bool"));
        Assert.That(eqResult.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_InvalidExpressionThrows()
    {
        using var ctx = new CodeGenContext();
        var dispatcher = new ExpressionDispatcher(ctx);
        var invalidNode = new AstBlock([]);

        BifyAssert.ActionThrows(DiagnosticKind.SYNTAX, () => dispatcher.EmitExpression(invalidNode), diagnostics: ctx.Diagnostics);
    }

    [Test]
    public void ExprCodeGen_ExplicitCastFloatToInt()
    {
        using var h = CreateExprContext("testCastFtoI");
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "int"), "int");
        var valNode = new AstFloat(new Token(TokenType.NUMBER, "3.14"), 3.14f);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "int"), typeNode, valNode);

        TypedValue result = h.Dispatcher.EmitExpression(castNode);

        Assert.That(result.Type.Name, Is.EqualTo("int"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(result.Type.Type.IntWidth, Is.EqualTo(32));
        Assert.That(result.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_ExplicitCastIntToFloat()
    {
        using var h = CreateExprContext("testCastItoF");
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "float"), "float");
        var valNode = new AstInteger(new Token(TokenType.NUMBER, "42"), 42);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "float"), typeNode, valNode);

        TypedValue result = h.Dispatcher.EmitExpression(castNode);

        Assert.That(result.Type.Name, Is.EqualTo("float"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMFloatTypeKind));
        Assert.That(result.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_ExplicitCastFloatToDouble()
    {
        using var h = CreateExprContext("testCastFtoD");
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "double"), "double");
        var valNode = new AstFloat(new Token(TokenType.NUMBER, "3.14"), 3.14f);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "double"), typeNode, valNode);

        TypedValue result = h.Dispatcher.EmitExpression(castNode);

        Assert.That(result.Type.Name, Is.EqualTo("double"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMDoubleTypeKind));
        Assert.That(result.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_ExplicitCastFloatToBool()
    {
        using var h = CreateExprContext("testCastFtoB");
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "bool"), "bool");
        var valNode = new AstFloat(new Token(TokenType.NUMBER, "1.5"), 1.5f);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "bool"), typeNode, valNode);

        TypedValue result = h.Dispatcher.EmitExpression(castNode);

        Assert.That(result.Type.Name, Is.EqualTo("bool"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(result.Type.Type.IntWidth, Is.EqualTo(1));
        Assert.That(result.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_ExplicitCastIntToChar()
    {
        using var h = CreateExprContext("testCastItoC");
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "char"), "char");
        var valNode = new AstInteger(new Token(TokenType.NUMBER, "65"), 65);
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "char"), typeNode, valNode);

        TypedValue result = h.Dispatcher.EmitExpression(castNode);

        Assert.That(result.Type.Name, Is.EqualTo("char"));
        Assert.That(result.Type.Type.Kind, Is.EqualTo(LLVMTypeKind.LLVMIntegerTypeKind));
        Assert.That(result.Type.Type.IntWidth, Is.EqualTo(8));
        Assert.That(result.Value.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void ExprCodeGen_ExplicitCastIncompatibleThrows()
    {
        using var h = CreateExprContext("testIncompatCast");
        var typeNode = new AstIdentifier(new Token(TokenType.IDENTIFIER, "float"), "float");
        var valNode = new AstString(new Token(TokenType.STRING, "hello"), "hello");
        var castNode = new AstCast(new Token(TokenType.IDENTIFIER, "float"), typeNode, valNode);

        BifyAssert.ActionThrows(DiagnosticKind.CAST, () => h.Dispatcher.EmitExpression(castNode), diagnostics: h.Ctx.Diagnostics);
    }
}
