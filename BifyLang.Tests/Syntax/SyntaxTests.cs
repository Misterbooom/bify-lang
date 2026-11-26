#nullable enable
using BifyLang.Ast;
using BifyLang.Exceptions;
using BifyLang.Lexer;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.Syntax;

[TestFixture]
public class SyntaxTests : BifyTestBase
{
    private static void AssertIdentifier(AstNode? node, string expectedName)
    {
        Assert.That(node, Is.InstanceOf<AstIdentifier>());
        Assert.That(((AstIdentifier)node!).Name, Is.EqualTo(expectedName));
    }

    private static void AssertInteger(AstNode? node, int expectedValue)
    {
        Assert.That(node, Is.InstanceOf<AstInteger>());
        Assert.That(((AstInteger)node!).Value, Is.EqualTo(expectedValue));
    }

    [Test]
    public void VarDecl_PrimitiveTypes()
    {
        string code = @"
func test() {
    val a: int = 5;
    mut b: float = 3.14;
    val c: string = ""hello"";
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.GreaterThanOrEqualTo(1));
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        AssertIdentifier(v1.Type, "int");
        AssertIdentifier(v1.AssignmentNode.Left, "a");
        AssertInteger(v1.AssignmentNode.Right, 5);
        Assert.That(v1.Flag.HasFlag("val"), Is.True);
        AssertIdentifier(v2.Type, "float");
        Assert.That(v2.Flag.HasFlag("mut"), Is.True);
        AssertIdentifier(v3.Type, "string");
    }

    [Test]
    public void VarDecl_UnknownTypes()
    {
        string code = @"
func test() {
    val x: MyCustomType = new MyCustomType();
    val pos: Vector3 = getOrigin();
    val z: UnregisteredType = x;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        AssertIdentifier(v1.Type, "MyCustomType");
        Assert.That(v1.AssignmentNode.Right, Is.InstanceOf<AstNew>());
        AssertIdentifier(v2.Type, "Vector3");
        Assert.That(v2.AssignmentNode.Right, Is.InstanceOf<AstCall>());
        AssertIdentifier(v3.Type, "UnregisteredType");
    }

    [Test]
    public void VarDecl_Uninitialized()
    {
        string code = @"
func test() {
    mut a: int;
    mut b: CustomType;
    mut p: CustomType*;
    mut arr: int[5];
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(4));

        for (int i = 0; i < 4; i++)
        {
            var v = (AstVarDecl)fn.BlockNode.ChildNodes[i];
            Assert.That(v.AssignmentNode.Right, Is.Null);
        }

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];
        var v4 = (AstVarDecl)fn.BlockNode.ChildNodes[3];

        AssertIdentifier(v1.Type, "int");
        AssertIdentifier(v2.Type, "CustomType");
        Assert.That(v3.Type, Is.InstanceOf<AstUnaryOperator>());
        var uop = (AstUnaryOperator)v3.Type;
        Assert.That(uop.Token.Type, Is.EqualTo(TokenType.POINTER));
        Assert.That(v4.Type, Is.InstanceOf<AstIndexOperator>());
        var idx = (AstIndexOperator)v4.Type;
        AssertInteger(idx.IndexNode, 5);
    }

    [Test]
    public void VarDecl_PointerTypes()
    {
        string code = @"
func test() {
    val p: int* = null;
    mut pp: Foo**;
    mut ppp: Bar***;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        Assert.That(v1.Type, Is.InstanceOf<AstUnaryOperator>());
        AssertIdentifier(((AstUnaryOperator)v1.Type).Operand, "int");

        Assert.That(v2.Type, Is.InstanceOf<AstUnaryOperator>());
        var ptr2 = (AstUnaryOperator)v2.Type;
        Assert.That(ptr2.Operand, Is.InstanceOf<AstUnaryOperator>());
        AssertIdentifier(((AstUnaryOperator)ptr2.Operand).Operand, "Foo");

        Assert.That(v3.Type, Is.InstanceOf<AstUnaryOperator>());
    }

    [Test]
    public void VarDecl_ArrayTypes()
    {
        string code = @"
func test() {
    val a: int[] = [1, 2, 3];
    mut b: int[10];
    mut c: CustomClass[];
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        var arr1 = (AstIndexOperator)v1.Type;
        Assert.That(arr1.IndexNode, Is.Null);
        AssertIdentifier(arr1.TargetNode, "int");

        var arr2 = (AstIndexOperator)v2.Type;
        AssertInteger(arr2.IndexNode, 10);
        AssertIdentifier(arr2.TargetNode, "int");

        var arr3 = (AstIndexOperator)v3.Type;
        AssertIdentifier(arr3.TargetNode, "CustomClass");
    }

    [Test]
    public void VarDecl_PointerAndArrayCombinations()
    {
        string code = @"
func test() {
    mut ptrArr: Foo*[3];
    mut matrix: int[][5];
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(2));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];

        var arr = (AstIndexOperator)v1.Type;
        AssertInteger(arr.IndexNode, 3);
        Assert.That(arr.TargetNode, Is.InstanceOf<AstUnaryOperator>());

        var mtx = (AstIndexOperator)v2.Type;
        AssertInteger(mtx.IndexNode, 5);
        Assert.That(mtx.TargetNode, Is.InstanceOf<AstIndexOperator>());
    }

    [Test]
    public void VarDecl_Modifiers()
    {
        string code = @"
func test() {
    val MAX: int = 100;
    public mut instance: Foo = null;
    private val PI: float = 3.14;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(3));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];
        var v3 = (AstVarDecl)fn.BlockNode.ChildNodes[2];

        Assert.That(v1.Flag.HasFlag("val"), Is.True);
        Assert.That(v2.Flag.HasFlag("public") && v2.Flag.HasFlag("mut"), Is.True);
        Assert.That(v3.Flag.HasFlag("private") && v3.Flag.HasFlag("val"), Is.True);
    }

    [Test]
    public void VarDecl_InferredType()
    {
        string code = @"
func test() {
    val x = 1;
    mut y = x * 2;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(2));

        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];

        AssertIdentifier(v1.Type, "void");
        AssertIdentifier(v2.Type, "void");
        Assert.That(v2.AssignmentNode.Right, Is.InstanceOf<AstBinaryOp>());
    }

    [Test]
    public void FuncDecl_PrimitiveReturn()
    {
        string code = @"
func add(a: int, b: int) -> int {
    return a + b;
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(1));

        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("add"));
        AssertIdentifier(fn.TypeNode, "int");
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(1));
        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstReturn>());
    }

    [Test]
    public void FuncDecl_UnknownReturn()
    {
        string code = @"
func process(req: Request, opt: Options) -> CustomResult {
    return res;
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("process"));
        AssertIdentifier(fn.TypeNode, "CustomResult");
    }

    [Test]
    public void FuncDecl_VoidNoParams()
    {
        string code = @"
func run() {
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("run"));
        AssertIdentifier(fn.TypeNode, "void");
        Assert.That(fn.ArgumentsNode, Is.Null);
    }

    [Test]
    public void FuncDecl_ColonReturnAndOmittedVoidReturn()
    {
        string code = @"
func f() : int { return 1; }
func g() { }
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(2));

        var fn1 = (AstFunctionDecl)module.ChildNodes[0];
        var fn2 = (AstFunctionDecl)module.ChildNodes[1];

        AssertIdentifier(fn1.TypeNode, "int");
        AssertIdentifier(fn2.TypeNode, "void");
    }

    [Test]
    public void FuncDecl_PointerAndArrayReturn()
    {
        string code = @"
func findNode(tree: Tree*, key: int) -> Node* {
    return null;
}

func getBuffer() -> int[] {
    return buf;
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(2));

        var fn1 = (AstFunctionDecl)module.ChildNodes[0];
        var fn2 = (AstFunctionDecl)module.ChildNodes[1];

        Assert.That(fn1.TypeNode, Is.InstanceOf<AstUnaryOperator>());
        var ptr = (AstUnaryOperator)fn1.TypeNode;
        Assert.That(ptr.Token.Type, Is.EqualTo(TokenType.POINTER));
        AssertIdentifier(ptr.Operand, "Node");

        Assert.That(fn2.TypeNode, Is.InstanceOf<AstIndexOperator>());
        var arr = (AstIndexOperator)fn2.TypeNode;
        AssertIdentifier(arr.TargetNode, "int");
    }

    [Test]
    public void FuncDecl_Modifiers()
    {
        string code = @"
public func execute() {
}

private func calculate() -> int {
    return 0;
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(2));

        var fn1 = (AstFunctionDecl)module.ChildNodes[0];
        var fn2 = (AstFunctionDecl)module.ChildNodes[1];

        Assert.That(fn1.FlagNode.HasFlag("public"), Is.True);
        Assert.That(fn2.FlagNode.HasFlag("private"), Is.True);
    }

    [Test]
    public void FuncDecl_MultipleParameters()
    {
        string code = @"
func render(width: int, tex: Texture*, coords: float[], flags: int) {
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];

        Assert.That(fn.FunctionNameNode.Name, Is.EqualTo("render"));
        Assert.That(fn.ArgumentsNode, Is.InstanceOf<AstBinaryOp>());
    }

    [Test]
    public void FuncDecl_ParameterMutabilityVariants()
    {
        string code = @"
func mix(a: int, val b: int, mut c: int) {
}
";
        var diagnostics = new DiagnosticBag();
        var module = Bify.Parse(code, diagnostics);
        Assert.That(diagnostics.HasErrors, Is.False);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.ArgumentsNode, Is.InstanceOf<AstBinaryOp>());
    }

    [Test]
    public void Class_ConstructorsWithModifiers()
    {
        string code = @"
class Player {
    public constructor(id: int) {
        this.id = id;
    }
    private constructor() {
    }
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(1));

        var cls = (AstClass)module.ChildNodes[0];
        var body = (AstBlock)cls.BodyNode;
        Assert.That(body.ChildNodes.Count, Is.EqualTo(2));

        var ctor1 = (AstFunctionDecl)body.ChildNodes[0];
        var ctor2 = (AstFunctionDecl)body.ChildNodes[1];

        Assert.That(ctor1.FunctionNameNode.Name, Is.EqualTo("constructor"));
        AssertIdentifier(ctor1.TypeNode, "void");
        Assert.That(ctor1.FlagNode.HasFlag("public"), Is.True);
        Assert.That(ctor2.FunctionNameNode.Name, Is.EqualTo("constructor"));
        AssertIdentifier(ctor2.TypeNode, "void");
        Assert.That(ctor2.FlagNode.HasFlag("private"), Is.True);
    }

    [Test]
    public void Class_FieldsAndMethods()
    {
        string code = @"
class Item {
    public mut id: int = 0;
    mut data: CustomData;

    public constructor(id: int, d: CustomData) {
        this.id = id;
        this.data = d;
    }

    public func getData() -> CustomData* {
        return data;
    }
}
";
        var module = Bify.Parse(code);
        var cls = (AstClass)module.ChildNodes[0];
        var body = (AstBlock)cls.BodyNode;
        Assert.That(body.ChildNodes.Count, Is.EqualTo(4));

        Assert.That(body.ChildNodes[0], Is.InstanceOf<AstVarDecl>());
        Assert.That(body.ChildNodes[1], Is.InstanceOf<AstVarDecl>());
        Assert.That(body.ChildNodes[2], Is.InstanceOf<AstFunctionDecl>());
        Assert.That(((AstFunctionDecl)body.ChildNodes[2]).FunctionNameNode.Name, Is.EqualTo("constructor"));
        Assert.That(body.ChildNodes[3], Is.InstanceOf<AstFunctionDecl>());
        Assert.That(((AstFunctionDecl)body.ChildNodes[3]).FunctionNameNode.Name, Is.EqualTo("getData"));
    }

    [Test]
    public void ForLoop_UnknownTypeDecl()
    {
        string code = @"
func test() {
    for (mut it: CustomIterator* = start; it != end; it++) {
        it.step();
    }
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        var forLoop = (AstFor)fn.BlockNode.ChildNodes[0];

        Assert.That(forLoop.InitNode, Is.InstanceOf<AstVarDecl>());
        var varDecl = (AstVarDecl)forLoop.InitNode;
        Assert.That(varDecl.Type, Is.InstanceOf<AstUnaryOperator>());
        var ptr = (AstUnaryOperator)varDecl.Type;
        AssertIdentifier(ptr.Operand, "CustomIterator");
        Assert.That(forLoop.IncrementNode, Is.InstanceOf<AstUnaryOperator>());
        var inc = (AstUnaryOperator)forLoop.IncrementNode;
        Assert.That(inc.Token.Type, Is.EqualTo(TokenType.INCREMENT));
    }

    [Test]
    public void WhileAndIfStatements()
    {
        string code = @"
func test() {
    while (x > 0) {
        if (x == 1) {
            break;
        } else if (x == 2) {
            continue;
        } else {
            x = x - 1;
        }
    }
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        var whileLoop = (AstWhile)fn.BlockNode.ChildNodes[0];
        var whileBlock = (AstBlock)whileLoop.BlockNode;
        var ifStmt = (AstIf)whileBlock.ChildNodes[0];

        Assert.That(ifStmt.ConditionNode, Is.InstanceOf<AstBinaryOp>());
        Assert.That(ifStmt.ElseNode, Is.InstanceOf<AstElse>());
    }

    [Test]
    public void AssignmentsAndExpressions()
    {
        string code = @"
func test() {
    a = b * c;
    arr[0] = 42;
    this.field = value;
    foo(a, b);
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(4));

        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstAssignmentOperator>());
        Assert.That(fn.BlockNode.ChildNodes[1], Is.InstanceOf<AstAssignmentOperator>());
        Assert.That(fn.BlockNode.ChildNodes[2], Is.InstanceOf<AstAssignmentOperator>());
        Assert.That(fn.BlockNode.ChildNodes[3], Is.InstanceOf<AstCall>());
    }

    [Test]
    public void Switch_BasicCasesAndDefault()
    {
        string code = @"
func test(x: int) {
    switch (x) {
        case 1:
            val a: int = 10;
            break;
        case 2:
            val b: int = 20;
            break;
        default:
            val c: int = 0;
            break;
    }
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstSwitch>());
        var sw = (AstSwitch)fn.BlockNode.ChildNodes[0];
        AssertIdentifier(sw.ExpressionNode, "x");
        Assert.That(sw.Cases.Count, Is.EqualTo(2));
        AssertInteger(sw.Cases[0].ValueNode, 1);
        Assert.That(sw.Cases[0].BlockNode.ChildNodes.Count, Is.EqualTo(2));
        AssertInteger(sw.Cases[1].ValueNode, 2);
        Assert.That(sw.DefaultNode, Is.Not.Null);
        Assert.That(sw.DefaultNode!.BlockNode.ChildNodes.Count, Is.EqualTo(2));
    }

    [Test]
    public void Switch_BracedCases()
    {
        string code = @"
func test(x: int) {
    switch (x) {
        case 1: {
            val a: int = 10;
        }
        default: {
            val b: int = 0;
        }
    }
}
";
        var module = Bify.Parse(code);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        Assert.That(fn.BlockNode.ChildNodes[0], Is.InstanceOf<AstSwitch>());
        var sw = (AstSwitch)fn.BlockNode.ChildNodes[0];
        Assert.That(sw.Cases.Count, Is.EqualTo(1));
        Assert.That(sw.DefaultNode, Is.Not.Null);
    }

    [Test]
    public void Negative_InvalidVariableName()
    {
        BifyAssert.AnySyntaxError(@"
func test() {
    val 123: int = 5;
}
");
    }

    [Test]
    public void Negative_EmptyValueAssigned()
    {
        BifyAssert.AnySyntaxError(@"
func test() {
    val a: int = ;
}
");
    }

    [Test]
    public void Negative_VarDecl_EmptyTypeAfterColon()
    {
        BifyAssert.AnySyntaxError(@"
func test() {
    val a: = 5;
}
");
    }

    [Test]
    public void Negative_FuncDecl_EmptyReturnTypeAfterArrow()
    {
        BifyAssert.AnySyntaxError(@"
func test() -> {
}
");
    }

    [Test]
    public void Negative_FuncDecl_MissingParens()
    {
        BifyAssert.AnySyntaxError(@"
func test -> int {
}
");
    }

    [Test]
    public void Negative_Constructor_MissingParens()
    {
        BifyAssert.AnySyntaxError(@"
class A {
    constructor {
    }
}
");
    }

    [Test]
    public void Negative_InvalidStatementBinaryOp()
    {
        BifyAssert.AnySyntaxError(@"
func test() {
    a + b;
}
");
    }

    [Test]
    public void Negative_ClassMethodBeforeField()
    {
        BifyAssert.AnySyntaxError(@"
class Bad {
    func method() {
    }
    val field: int = 1;
}
");
    }

    [Test]
    public void Negative_Switch_MissingExpression()
    {
        BifyAssert.AnySyntaxError(@"
func test() {
    switch () {
        case 1:
            break;
    }
}
");
    }

    [Test]
    public void Negative_Switch_DuplicateDefault()
    {
        BifyAssert.AnySyntaxError(@"
func test(x: int) {
    switch (x) {
        default:
            break;
        default:
            break;
    }
}
");
    }

    [Test]
    public void Negative_Switch_MissingColon()
    {
        BifyAssert.AnySyntaxError(@"
func test(x: int) {
    switch (x) {
        case 1
            break;
    }
}
");
    }

    [Test]
    public void Syntax_PropagatesExactColumnNumbersToAstNodes()
    {
        string code = """
        func test() {
            val alpha: int = 10;
                mut beta: int = alpha;
        }
        """;
        var diagnostics = new DiagnosticBag();
        var module = Bify.Parse(code, diagnostics);

        Assert.That(diagnostics.HasErrors, Is.False);
        var fn = (AstFunctionDecl)module.ChildNodes[0];
        var v1 = (AstVarDecl)fn.BlockNode.ChildNodes[0];
        var v2 = (AstVarDecl)fn.BlockNode.ChildNodes[1];

        Assert.That(v1.Type.LineNumber, Is.EqualTo(2));
        Assert.That(v1.Type.ColumnNumber, Is.EqualTo(16));
        Assert.That(v1.AssignmentNode.Left.LineNumber, Is.EqualTo(2));
        Assert.That(v1.AssignmentNode.Left.ColumnNumber, Is.EqualTo(9));
        Assert.That(v1.AssignmentNode.Right.LineNumber, Is.EqualTo(2));
        Assert.That(v1.AssignmentNode.Right.ColumnNumber, Is.EqualTo(22));

        Assert.That(v2.Type.LineNumber, Is.EqualTo(3));
        Assert.That(v2.Type.ColumnNumber, Is.EqualTo(19));
        Assert.That(v2.AssignmentNode.Left.LineNumber, Is.EqualTo(3));
        Assert.That(v2.AssignmentNode.Left.ColumnNumber, Is.EqualTo(13));
        Assert.That(v2.AssignmentNode.Right.LineNumber, Is.EqualTo(3));
        Assert.That(v2.AssignmentNode.Right.ColumnNumber, Is.EqualTo(25));
    }

    [Test]
    public void Syntax_RecoversWithAstPoisonAndParsesFunctionBodyErrors()
    {
        string code = """
        func broken(int) {
            val y: int = ;
            val z: int = 5;
        }
        func valid() {
            val ok: int = ;
        }
        """;
        var diagnostics = new DiagnosticBag();
        var module = Bify.Parse(code, diagnostics, "syntax_test.bf");

        Assert.That(diagnostics.Diagnostics.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(diagnostics.Diagnostics.Any(d => d.Location.LineNumber == 1), Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Location.LineNumber == 2), Is.True);
        Assert.That(diagnostics.Diagnostics.Any(d => d.Location.LineNumber == 6), Is.True);
        Assert.That(diagnostics.Diagnostics.All(d => d.Location.FilePath == "syntax_test.bf"), Is.True);

        var functions = module.ChildNodes.OfType<AstFunctionDecl>().ToList();
        Assert.That(functions.Count, Is.EqualTo(2));
        Assert.That(functions[0].IsPoison, Is.True);
        Assert.That(functions[0].BlockNode.ChildNodes.Any(n => n.IsPoison), Is.True);
        Assert.That(functions[1].IsPoison, Is.False);
        Assert.That(functions[1].BlockNode.ChildNodes.Any(n => n.IsPoison), Is.True);
    }

    [Test]
    public void Struct_WithInterfaceFieldsAndMethod()
    {
        string code = @"
struct Vector2:Interface {
    val x:float;
    val y:float;
    func method()->type{}
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(1));
        Assert.That(module.ChildNodes[0], Is.InstanceOf<AstStruct>());
        Assert.That(module.ChildNodes[0], Is.Not.InstanceOf<AstClass>());

        var s = (AstStruct)module.ChildNodes[0];
        AssertIdentifier(s.NameNode, "Vector2");
        AssertIdentifier(s.ParentNode, "Interface");

        var body = (AstBlock)s.BodyNode;
        Assert.That(body.ChildNodes.Count, Is.EqualTo(3));

        var xField = (AstVarDecl)body.ChildNodes[0];
        var yField = (AstVarDecl)body.ChildNodes[1];
        var method = (AstFunctionDecl)body.ChildNodes[2];

        AssertIdentifier(xField.AssignmentNode.Left, "x");
        AssertIdentifier(xField.Type, "float");
        AssertIdentifier(yField.AssignmentNode.Left, "y");
        AssertIdentifier(yField.Type, "float");
        Assert.That(method.FunctionNameNode.Name, Is.EqualTo("method"));
        AssertIdentifier(method.TypeNode, "type");
    }

    [Test]
    public void Struct_WithoutInterfaceAndMultipleInterfaces()
    {
        string code = @"
struct Point {
    val x: float;
}
struct Vector3 : I1, I2 {
    mut z: float;
}
";
        var module = Bify.Parse(code);
        Assert.That(module.ChildNodes.Count, Is.EqualTo(2));

        var s1 = (AstStruct)module.ChildNodes[0];
        AssertIdentifier(s1.NameNode, "Point");
        Assert.That(s1.ParentNode, Is.Null);

        var s2 = (AstStruct)module.ChildNodes[1];
        AssertIdentifier(s2.NameNode, "Vector3");
        Assert.That(s2.ParentNode, Is.InstanceOf<AstBinaryOp>());
        var parentList = (AstBinaryOp)s2.ParentNode;
        Assert.That(parentList.Token.Type, Is.EqualTo(TokenType.COMMA));
        AssertIdentifier(parentList.Left, "I1");
        AssertIdentifier(parentList.Right, "I2");
    }

    [Test]
    public void Struct_InvalidBodyAndName_ReportsDiagnostics()
    {
        BifyAssert.ParseError("struct 123 {}", DiagnosticKind.SYNTAX);
        BifyAssert.ParseError("struct Bad { func m() {} val x: int; }", DiagnosticKind.SYNTAX);
        BifyAssert.ParseError("struct Bad { return 1; }", DiagnosticKind.SYNTAX);
    }
}
