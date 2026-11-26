using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BifyLang.Exceptions;
using BifyLang.Lexer;

namespace BifyLang.Ast;

public class AstNode(Token token, AstNode left = null, AstNode right = null)
{
    public Token Token { get; set; } = token;
    public AstNode Left { get; set; } = left;
    public AstNode Right { get; set; } = right;
    public bool IsPoison { get; init; }

    private int _lineNumber;
    private int _columnNumber;

    public int LineNumber
    {
        get => _lineNumber == 0 ? (Token?.Line ?? 0) : _lineNumber;
        set => _lineNumber = value;
    }

    public int ColumnNumber
    {
        get => _columnNumber == 0 ? (Token?.Column ?? 0) : _columnNumber;
        set => _columnNumber = value;
    }

    protected virtual IEnumerable<(string Label, AstNode Node)> GetChildren()
    {
        if (Left != null) yield return ("left:", Left);
        if (Right != null) yield return ("right:", Right);
    }

    public override string ToString() => StrHelper();

    public virtual string StrHelper(int level = 0, string note = "", bool isLeft = true)
    {
        var sb = new StringBuilder();
        sb.Append(' ', 4 * level)
          .Append("|- ")
          .Append(note)
          .Append($"{GetType().Name}('{Token?.Value ?? ""}',Line - {LineNumber})\n");

        foreach (var (label, child) in GetChildren())
        {
            if (child != null)
                sb.Append(child.StrHelper(level + 1, label));
        }

        return sb.ToString();
    }

    public virtual int Len(int level = 1) => Math.Max(
        Left?.Len(level + 1) ?? level,
        Right?.Len(level + 1) ?? level
    );
}

public class AstPoison : AstNode
{
    public AstPoison(Token token) : base(token) => IsPoison = true;
}

public class AstBinaryOp(Token token, AstNode left = null, AstNode right = null) : AstNode(token, left, right);

public abstract class AstConstant(Token token, object value) : AstNode(token)
{
    public object Value { get; protected set; } = value;
}

public class AstInteger(Token token, int value) : AstConstant(token, value);
public class AstString(Token token, string value) : AstConstant(token, value);
public class AstBoolean(Token token, bool value) : AstConstant(token, value);
public class AstNull(Token token) : AstConstant(token, null);
public class AstFloat(Token token, float value) : AstConstant(token, value);

public class AstAssignment(Token token, AstNode left = null, AstNode right = null) : AstNode(token, left, right)
{
    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Var name: ", Left), ("Var Value: ", Right)];
}

public class AstVarDecl(Token token, AstAssignment assignmentNode, AstNode type = null, AstFlag flagNode = null, AstNode left = null, AstNode right = null)
    : AstNode(token, left, right)
{
    public readonly AstAssignment AssignmentNode = assignmentNode;
    public readonly AstNode Type = type;
    public readonly AstFlag Flag = flagNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        base.GetChildren().Concat([("Assignment:", AssignmentNode), ("Type: ", Type), ("Flag: ", Flag)]);
}

public class AstLine(AstNode child) : AstNode(new Token(TokenType.EOL, ";")
{
    Line = child?.LineNumber ?? 0,
    Column = child?.ColumnNumber ?? 0
})
{
    public readonly AstNode Child = child;
    protected override IEnumerable<(string, AstNode)> GetChildren() => [("Child: ", Child)];
}

public class AstIdentifier(Token token, string name) : AstNode(token)
{
    public readonly string Name = name;
}

public class AstBracket(Token token) : AstNode(token);
public class AstEol(Token token) : AstNode(token);
public class AstBreak(Token token) : AstNode(token);
public class AstContinue(Token token) : AstNode(token);
public class AstRangeOperator(Token token) : AstBinaryOp(token);
public class AstMemberAccess(Token token, AstNode left, AstNode right) : AstNode(token, left, right);

public class AstBlock(List<AstNode> childsNodes) : AstNode(new Token(TokenType.BLOCK, "Block")
{
    Line = childsNodes is { Count: > 0 } ? childsNodes[0]?.LineNumber ?? 0 : 0,
    Column = childsNodes is { Count: > 0 } ? childsNodes[0]?.ColumnNumber ?? 0 : 0
})
{
    public readonly List<AstNode> ChildNodes = childsNodes;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        ChildNodes.Select(n => ("Statement: ", n));

    public override int Len(int level = 1) => ChildNodes.Count;
}

public class AstIf(Token token, AstNode conditionNode, AstNode blockNode, AstElse elseNode = null) : AstNode(token)
{
    public readonly AstNode ConditionNode = conditionNode;
    public readonly AstNode BlockNode = blockNode;
    public AstElse ElseNode = elseNode;
    public readonly List<AstElseIf> ElseIfNodes = [];

    public void AddElseIfNode(AstElseIf astElseIf) => ElseIfNodes.Add(astElseIf);

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("condition: ", ConditionNode), ("block: ", BlockNode), ("else: ", ElseNode), .. ElseIfNodes.Select(e => ("else if: ", (AstNode)e))];
}

public class AstElse(Token token, AstNode blockNode) : AstNode(token)
{
    public readonly AstNode BlockNode = blockNode;
    protected override IEnumerable<(string, AstNode)> GetChildren() => [("block: ", BlockNode)];
}

public abstract class AstConditionalBlock(Token token, AstNode blockNode, AstNode conditionNode) : AstNode(token)
{
    public readonly AstNode BlockNode = blockNode;
    public readonly AstNode ConditionNode = conditionNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Condition: ", ConditionNode), ("Block: ", BlockNode)];
}

public class AstElseIf(Token token, AstNode blockNode, AstNode conditionNode) : AstConditionalBlock(token, blockNode, conditionNode);
public class AstWhile(Token token, AstNode blockNode, AstNode conditionNode) : AstConditionalBlock(token, blockNode, conditionNode);

public class AstFor(Token token, AstNode blockNode, AstNode conditionNode, AstNode incrementNode, AstNode initNode) : AstNode(token)
{
    public readonly AstNode BlockNode = blockNode, ConditionNode = conditionNode, IncrementNode = incrementNode, InitNode = initNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Init: ", InitNode), ("Condition: ", ConditionNode), ("Increment: ", IncrementNode), ("Block: ", BlockNode)];
}

public class AstSwitch(Token token, AstNode expressionNode, List<AstCase> cases = null, AstDefault defaultNode = null) : AstNode(token)
{
    public readonly AstNode ExpressionNode = expressionNode;
    public readonly List<AstCase> Cases = cases ?? [];
    public readonly AstDefault DefaultNode = defaultNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Expression: ", ExpressionNode), .. Cases.Select(c => ("Case: ", (AstNode)c)), ("Default: ", DefaultNode)];
}

public class AstCase(Token token, AstNode valueNode, AstBlock blockNode) : AstNode(token)
{
    public readonly AstNode ValueNode = valueNode;
    public readonly AstBlock BlockNode = blockNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Value: ", ValueNode), ("Block: ", BlockNode)];
}

public class AstDefault(Token token, AstBlock blockNode) : AstNode(token)
{
    public readonly AstBlock BlockNode = blockNode;
    protected override IEnumerable<(string, AstNode)> GetChildren() => [("Block: ", BlockNode)];
}

public class AstCall(Token token, AstNode callableName, AstNode[] arguments = null) : AstNode(token)
{
    public readonly AstNode CallableName = callableName;
    public readonly AstNode[] Arguments = arguments;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Callable: ", CallableName), ..Arguments.Select(f => ("Arg: ", f))];
}

public class AstUnaryOperator(Token token, AstNode value, bool isPrefix = false) : AstNode(token)
{
    public readonly AstNode Operand = value;
    public AstNode Value => Operand;
    public readonly bool IsPrefix = isPrefix;

    public AstUnaryOperator Update(AstNode newOperand) => new(Token, newOperand, IsPrefix);
    protected override IEnumerable<(string, AstNode)> GetChildren() => [("Value: ", Operand)];
}

public class AstFunctionDecl(Token token, AstNode typeNode, AstIdentifier functionNameNode, AstNode argumentsNode, AstBlock blockNode, AstFlag flagNode)
    : AstNode(token)
{
    public readonly AstNode ArgumentsNode = argumentsNode;
    public readonly AstIdentifier FunctionNameNode = functionNameNode;
    public readonly AstBlock BlockNode = blockNode;
    public readonly AstNode TypeNode = typeNode;
    public readonly AstFlag FlagNode = flagNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Type: ", TypeNode), ("Name: ", FunctionNameNode), ("Arguments: ", ArgumentsNode), ("Block: ", BlockNode), ("Flags", FlagNode)];
}

public class AstModule : AstNode
{
    public readonly List<AstNode> ChildNodes;
    public readonly string ModulePath;
    public string[] SourceCode { get; set; } = [];
    public string[] SourceLines => SourceCode;
    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            ValidateIdentifier(value);
            _name = value;
        }
    }

    public AstModule(string moduleName, string modulePath, List<AstNode> nodes)
        : base(new Token(TokenType.IDENTIFIER, moduleName))
    {
        ChildNodes = nodes;
        ModulePath = modulePath;
        Name = ResolveModuleName(moduleName, modulePath);
    }

    public AstModule(string modulePath, List<AstNode> nodes)
        : this(Path.GetFileNameWithoutExtension(modulePath), modulePath, nodes) { }

    private static string ResolveModuleName(string moduleName, string modulePath)
    {
        string rawName = !string.IsNullOrEmpty(modulePath) ? Path.GetFileNameWithoutExtension(modulePath) : moduleName;
        return string.IsNullOrEmpty(rawName) ? "main" : rawName;
    }

    private void ValidateIdentifier(string name)
    {
        if (IsValidIdentifier(name)) return;

        string filePath = string.IsNullOrEmpty(ModulePath) ? "main" : ModulePath;
        throw new BifyCompilationException(new Diagnostic(
            $"Invalid module identifier '{name}'. Module name must be a valid identifier.",
            DiagnosticKind.NAME,
            new SourceLocation(filePath, LineNumber, ColumnNumber)
        ));
    }

    public static bool IsValidIdentifier(string name) =>
        !string.IsNullOrEmpty(name) &&
        (name[0] == '_' || char.IsLetter(name[0])) &&
        name.Skip(1).All(c => c == '_' || char.IsLetterOrDigit(c));

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        ChildNodes.Select(n => ("Child: ", n));
}

public class AstAssignmentOperator(Token token, AstNode identifierNode, AstNode valueNode) : AstNode(token)
{
    public readonly AstNode IdentifierNode = identifierNode;
    public readonly AstNode ValueNode = valueNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Identifier: ", IdentifierNode), ("Value: ", ValueNode)];
}

public class AstReturn(Token token, AstNode argumentsNode) : AstNode(token)
{
    public readonly AstNode ArgumentsNode = argumentsNode;
    protected override IEnumerable<(string, AstNode)> GetChildren() => [("Arguments: ", ArgumentsNode)];
}

public class AstArray(Token token, AstNode argumentsNode) : AstNode(token)
{
    public readonly AstNode ArgumentsNode = argumentsNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        UnpackCommaNode(ArgumentsNode).Select(n => ("Argument: ", n));

    private static IEnumerable<AstNode> UnpackCommaNode(AstNode node) =>
        node is AstBinaryOp { Token.Type: TokenType.COMMA } op
            ? UnpackCommaNode(op.Left).Concat(UnpackCommaNode(op.Right))
            : [node];
}

public class AstIndexOperator(AstNode nodeIndex, AstNode operandNode)
    : AstNode(operandNode?.Token ?? nodeIndex?.Token ?? new Token(TokenType.NUMBER, "UNKNOWN"))
{
    public readonly AstNode IndexNode = nodeIndex;
    public readonly AstNode TargetNode = operandNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("IndexNode: ", IndexNode), ("Operand: ", TargetNode)];
}

public class AstParam(AstNode type, AstNode name, AstNode value, AstNode flag)
    : AstNode(name?.Token ?? type?.Token ?? new Token(TokenType.AND, "Param"))
{
    public readonly AstNode Type = type, Name = name, Flag = flag, Value = value;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Type: ", Type), ("Name: ", Name), ("Flag: ", Flag)];
}

public class AstNamedArg(AstNode name, AstNode value):AstNode(name?.Token ?? value?.Token ?? new Token(TokenType.AND, "Arg"))
{
    public readonly AstNode Name = name, Value = value;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Name: ", Name), ("Value: ", Value)];
}
public class AstConditionStatement(AstNode left = null, AstNode right = null) : AstNode(new Token(TokenType.IDENTIFIER, "Condition statement")
{
    Line = left?.LineNumber ?? right?.LineNumber ?? 0,
    Column = left?.ColumnNumber ?? right?.ColumnNumber ?? 0
}, left, right);

public abstract class AstTypeDecl(Token token, AstNode nameNode, AstNode bodyNode, AstNode parentNode, AstFlag flagNode) : AstNode(token)
{
    public readonly AstNode NameNode = nameNode, BodyNode = bodyNode, ParentNode = parentNode;
    public readonly AstFlag FlagNode = flagNode;


    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Name: ", NameNode), ("Body: ", BodyNode), ("Parent: ", ParentNode), ("Flags: ",  FlagNode)];


}

public class AstClass(Token token, AstNode nameNode, AstNode bodyNode, AstNode parentNode, AstFlag flagNode) : AstTypeDecl(token, nameNode, bodyNode, parentNode,flagNode);
public class AstStruct(Token token, AstNode nameNode, AstNode bodyNode, AstNode parentNode, AstFlag flagNode) : AstTypeDecl(token, nameNode, bodyNode, parentNode,flagNode);


public class AstCast(Token token, AstNode typeNode, AstNode valueNode) : AstNode(token)
{
    public readonly AstNode TypeNode = typeNode;
    public readonly AstNode ValueNode = valueNode;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Type: ", TypeNode), ("Value: ", ValueNode)];
}

public class AstNew(Token token, AstNode valueNode, AstNode[] arguments) : AstNode(token)
{
    public readonly AstNode ValueNode = valueNode;
    public readonly AstNode[] Arguments = arguments;

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        [("Value: ", ValueNode), ..Arguments.Select(i => ("Arg", i))];
}

public class AstUnsafe(Token token, AstBlock body) : AstNode(token)
{
    public readonly AstNode BodyNode = body;
    protected override IEnumerable<(string, AstNode)> GetChildren() => [("Body: ", BodyNode)];
}

public class AstFlag(Token token, List<AstNode> flags) : AstNode(token)
{
    public readonly List<AstNode> Flags = flags;

    public bool HasFlag(string name) => Flags.OfType<AstIdentifier>().Any(i => i.Name == name);

    protected override IEnumerable<(string, AstNode)> GetChildren() =>
        Flags.Select(f => ("Flag: ", f));
    public string[] ToStringArray()
    {
        return [.. Flags.Select(i => i.Token.Value)];
    }
}
