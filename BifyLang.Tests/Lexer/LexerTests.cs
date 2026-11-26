#nullable enable
using BifyLang.Exceptions;
using BifyLang.Lexer;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.Lexer;

[TestFixture]
public class LexerTests : BifyTestBase
{
    [Test]
    public void Lexer_TokenizesKeywordsCorrectly()
    {
        string code = "class new if else while for switch case default return const constructor destructor break continue";
        var tokens = Bify.Lex(code);

        Assert.That(tokens.Any(t => t.Type == TokenType.CLASS), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.NEW), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.IF), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.ELSE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.WHILE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.FOR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.SWITCH), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CASE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.DEFAULT), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.RETURN), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CONST), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CONSTRUCTOR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.DESTRUCTOR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.BREAK), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.CONTINUE), Is.True);
    }

    [Test]
    public void Lexer_TokenizesRustLikeDeclarationTokens()
    {
        string code = "func main()->int { val a: int = 1; mut b: int = 2; }";
        var tokens = Bify.Lex(code);

        Assert.That(tokens.Any(t => t.Type == TokenType.FUNCTION && t.Value == "func"), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.ARROW && t.Value == "->"), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.VAL && t.Value == "val"), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.MUT && t.Value == "mut"), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.IDENTIFIER && t.Value == "int"), Is.True);
    }

    [Test]
    public void Lexer_TokenizesOperatorsAndPunctuation()
    {
        string code = "== != <= >= += -= *= /= ++ -- && || { } ( ) [ ] ; , . : = + - * / < >";
        var tokens = Bify.Lex(code);

        Assert.That(tokens.Any(t => t.Type == TokenType.EQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.NEQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.LTEQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.GTEQ), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.ADDE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.SUBE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.MULE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.DIVE), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.INCREMENT), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.DECREMENT), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.AND), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.OR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.ASSIGN), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.EOL), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.LCUR), Is.True);
        Assert.That(tokens.Any(t => t.Type == TokenType.RCUR), Is.True);
    }

    [Test]
    public void Lexer_TokenizesLiterals()
    {
        string code = @"42 3.14 ""hello world""";
        var tokens = Bify.Lex(code);

        var numTokens = tokens.Where(t => t.Type == TokenType.NUMBER).ToList();
        Assert.That(numTokens.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(numTokens.Any(t => t.Value == "42"), Is.True);
        Assert.That(numTokens.Any(t => t.Value == "3.14"), Is.True);

        var strToken = tokens.FirstOrDefault(t => t.Type == TokenType.STRING);
        Assert.That(strToken, Is.Not.Null);
        Assert.That(strToken!.Value, Is.EqualTo("hello world"));
    }

    [Test]
    public void Lexer_TracksLineAndColumn()
    {
        string code = "int a = 1;\nint b = 2;";
        var tokens = Bify.Lex(code);

        var firstToken = tokens[0];
        Assert.That(firstToken.Value, Is.EqualTo("int"));
        Assert.That(firstToken.Line, Is.EqualTo(1));
        Assert.That(firstToken.Column, Is.EqualTo(1));

        var secondLineToken = tokens.FirstOrDefault(t => t.Line == 2 && t.Value == "int");
        Assert.That(secondLineToken, Is.Not.Null);
        Assert.That(secondLineToken!.Column, Is.EqualTo(1));
    }

    [Test]
    public void Lexer_TracksExactColumnAfterStringsAndEscapes()
    {
        string code = "\"hi\\n\\\"there\\\"\"; int x = 42;\n    int y = 7;";
        var tokens = Bify.Lex(code);

        var strToken = tokens.First(t => t.Type == TokenType.STRING);
        Assert.That(strToken.Line, Is.EqualTo(1));
        Assert.That(strToken.Column, Is.EqualTo(1));

        var eolToken = tokens.First(t => t.Line == 1 && t.Type == TokenType.EOL);
        Assert.That(eolToken.Column, Is.EqualTo(16));

        var intToken = tokens.First(t => t.Line == 1 && t.Value == "int");
        Assert.That(intToken.Column, Is.EqualTo(18));

        var xToken = tokens.First(t => t.Line == 1 && t.Value == "x");
        Assert.That(xToken.Column, Is.EqualTo(22));

        var line2Int = tokens.First(t => t.Line == 2 && t.Value == "int");
        Assert.That(line2Int.Column, Is.EqualTo(5));

        var line2Y = tokens.First(t => t.Line == 2 && t.Value == "y");
        Assert.That(line2Y.Column, Is.EqualTo(9));
    }

    [Test]
    public void Lexer_RecoversWithPoisonTokensAndReportsMultipleDiagnostics()
    {
        string code = "int a = @;\nint b = (1));\n\"unclosed\nint c = 5;";
        var diagnostics = new DiagnosticBag();
        var tokens = Bify.Lex(code, diagnostics, "lexer_test.bf");

        Assert.That(diagnostics.Diagnostics.Count, Is.EqualTo(3));
        Assert.That(diagnostics.Diagnostics.All(d => d.Kind == DiagnosticKind.SYNTAX), Is.True);
        Assert.That(diagnostics.Diagnostics.All(d => d.Location.FilePath == "lexer_test.bf"), Is.True);

        Assert.That(diagnostics.Diagnostics[0].Location.LineNumber, Is.EqualTo(1));
        Assert.That(diagnostics.Diagnostics[0].Location.ColumnNumber, Is.EqualTo(9));

        Assert.That(diagnostics.Diagnostics[1].Location.LineNumber, Is.EqualTo(2));
        Assert.That(diagnostics.Diagnostics[1].Location.ColumnNumber, Is.EqualTo(12));

        Assert.That(diagnostics.Diagnostics[2].Location.LineNumber, Is.EqualTo(3));
        Assert.That(diagnostics.Diagnostics[2].Location.ColumnNumber, Is.EqualTo(1));

        var poisonTokens = tokens.Where(t => t.IsPoison).ToList();
        Assert.That(poisonTokens.Count, Is.EqualTo(3));
        Assert.That(poisonTokens[0].Type, Is.EqualTo(TokenType.POISON));
        Assert.That(poisonTokens[1].Type, Is.EqualTo(TokenType.POISON));
        Assert.That(poisonTokens[2].Type, Is.EqualTo(TokenType.STRING));

        var recoveredInt = tokens.FirstOrDefault(t => t.Line == 4 && t.Value == "int");
        var recoveredIdent = tokens.FirstOrDefault(t => t.Line == 4 && t.Value == "c");
        var recoveredNum = tokens.FirstOrDefault(t => t.Line == 4 && t.Value == "5");
        Assert.That(recoveredInt, Is.Not.Null);
        Assert.That(recoveredInt!.Column, Is.EqualTo(1));
        Assert.That(recoveredIdent, Is.Not.Null);
        Assert.That(recoveredIdent!.Column, Is.EqualTo(5));
        Assert.That(recoveredNum, Is.Not.Null);
        Assert.That(recoveredNum!.Column, Is.EqualTo(9));
    }
}
