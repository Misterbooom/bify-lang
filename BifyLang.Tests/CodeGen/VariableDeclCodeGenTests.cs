#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class VariableDeclCodeGenTests : BifyTestBase
{
    [TestCase("val x = 42;", TestName = "VarDecl_InferredInt")]
    [TestCase("val pi = 3.14;", TestName = "VarDecl_InferredFloat")]
    [TestCase("mut flag = true;", TestName = "VarDecl_InferredBool")]
    [TestCase("val message = \"hello world\";", TestName = "VarDecl_InferredString")]
    public void VarDecl_InferredTypes(string decl)
    {
        AssertValidFunction($"func test() {{ {decl} }}");
    }

    [TestCase("val f: float = 42;", TestName = "VarDecl_ImplicitCast_IntToFloat")]
    [TestCase("val d: double = 3.14;", TestName = "VarDecl_ImplicitCast_FloatToDouble")]
    [TestCase("val i: int = true;", TestName = "VarDecl_ImplicitCast_BoolToInt")]
    [TestCase("val b: bool = 1;", TestName = "VarDecl_ImplicitCast_IntToBool")]
    public void VarDecl_ImplicitCasts(string decl)
    {
        AssertValidFunction($"func test() {{ {decl} }}");
    }

    [TestCase("val x: int = (int)3.14;", TestName = "VarDecl_ExplicitCast_FloatToInt")]
    [TestCase("val x: float = (float)42;", TestName = "VarDecl_ExplicitCast_IntToFloat")]
    [TestCase("val c: char = (char)65; val i: int = (int)c;", TestName = "VarDecl_ExplicitCast_CharAndInt")]
    public void VarDecl_ExplicitCasts(string body)
    {
        AssertValidFunction($"func test() {{ {body} }}");
    }

    [TestCase("val uninit;", TestName = "VarDecl_Error_VarUninitialized")]
    [TestCase("val i: int = 3.14;", TestName = "VarDecl_Error_ImplicitFloatToInt")]
    [TestCase("val x: int = \"cannot convert string to int\";", TestName = "VarDecl_Error_IncompatibleTypes")]
    public void VarDecl_TypeErrors(string decl)
    {
        AssertCompileError($"func test() {{ {decl} }}", DiagnosticKind.TYPE);
    }

    [Test]
    public void VarDeclCodeGen_ExplicitCastIncompatibleThrows()
    {
        AssertCompileError("func test() { val x: float = (float)\"cannot cast string to float\"; }", DiagnosticKind.CAST);
    }

    [Test]
    public void VarDecl_NullFlagNode_DoesNotThrow()
    {
        using var tc = new ExpressionTestContext();
        tc.Ctx.FunctionEntry = tc.Function.FirstBasicBlock;
        var varId = new Ast.AstIdentifier(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.IDENTIFIER, "x"), "x");
        var val = new Ast.AstInteger(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.NUMBER, "7"), 7);
        var assign = new Ast.AstAssignment(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.EQ, "="), varId, val);
        var typeId = new Ast.AstIdentifier(new global::BifyLang.Lexer.Token(global::BifyLang.Lexer.TokenType.IDENTIFIER, "int"), "int");
        var decl = new Ast.AstVarDecl(varId.Token, assign, typeId, flagNode: null);

        Assert.DoesNotThrow(() => new BifyLang.CodeGen.Statements.VariableDeclGen(tc.Ctx).EmitVariableDecl(decl));
    }

    [Test]
    public void VarDecl_PoisonRecovery_SuppressesCascadingUndefinedVariableAndContinues()
    {
        string code = """
        func test() {
            val a: int = ;
            val b: int = a + 1;
            val c: int = "bad";
        }
        """;
        var diagnostics = new DiagnosticBag();
        Bify.Compile(code, diagnostics);

        Assert.That(diagnostics.HasErrors, Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Kind == DiagnosticKind.SYNTAX && d.Location.LineNumber == 2), Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Kind == DiagnosticKind.TYPE && d.Location.LineNumber == 4), Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Message.Contains("is not defined")), Is.False);
    }
}
