#nullable enable
using BifyLang.Ast;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.Syntax;

[TestFixture]
public class AstVerificationTests : BifyTestBase
{
    [Test]
    public void AstVerificationTest()
    {
        string testCode = @"
class Entity {
    public mut id: int = 1;
    mut data: UnknownType;
    public constructor(id: int, ptr: UnknownType*) {
        this.id = id;
    }
    public func getData() -> UnknownType* {
        return data;
    }
}

func calculate(x: int, ptr: CustomClass*, list: int[]) -> int {
    mut localVal: int = 42;
    val obj: CustomClass;
    val p: CustomClass* = ptr;
    val dblPtr: int**;
    mut buffer: int[10];
    val classPtrs: CustomClass*[5];
    val limit: int = 100;
    val product = x * localVal;

    for (mut it: CustomClass* = p; it != null; it++) {
        localVal = localVal + 1;
    }

    localVal = x * limit;
    buffer[0] = localVal;
    return localVal;
}

func main() {
    val ent: Entity = new Entity(1, null);
}
";

        var module = Bify.Parse(testCode);

        Assert.That(module.ChildNodes.Count, Is.EqualTo(3));
        Assert.That(module.ChildNodes[0], Is.InstanceOf<AstClass>());

        var cls = (AstClass)module.ChildNodes[0];
        Assert.That(cls.BodyNode, Is.InstanceOf<AstBlock>());
        var clsBody = (AstBlock)cls.BodyNode;
        Assert.That(clsBody.ChildNodes.Count, Is.EqualTo(4));

        Assert.That(module.ChildNodes[1], Is.InstanceOf<AstFunctionDecl>());
        var fn = (AstFunctionDecl)module.ChildNodes[1];
        Assert.That(fn.BlockNode.ChildNodes.Count, Is.EqualTo(12));

        Assert.That(module.ChildNodes[2], Is.InstanceOf<AstFunctionDecl>());
    }

    [Test]
    public void ImportStatementTest()
    {
        string testCode = @"import ""/path/to/file""";
        var module = Bify.Parse(testCode);

        Assert.That(module.ChildNodes.Count, Is.EqualTo(1));
        Assert.That(module.ChildNodes[0], Is.InstanceOf<AstImport>());
        var importNode = (AstImport)module.ChildNodes[0];
        Assert.That(importNode.Path, Is.EqualTo("/path/to/file"));
    }

    [Test]
    public void ImportStatementWithSemicolonTest()
    {
        string testCode = @"
import ""/path/to/file"";
import ""./another/file.bify"";
";
        var module = Bify.Parse(testCode);

        Assert.That(module.ChildNodes.Count, Is.EqualTo(2));
        Assert.That(module.ChildNodes[0], Is.InstanceOf<AstImport>());
        Assert.That(((AstImport)module.ChildNodes[0]).Path, Is.EqualTo("/path/to/file"));
        Assert.That(module.ChildNodes[1], Is.InstanceOf<AstImport>());
        Assert.That(((AstImport)module.ChildNodes[1]).Path, Is.EqualTo("./another/file.bify"));
    }

    [Test]
    public void AstModuleNameFromPathTest()
    {
        var module1 = new AstModule("path/second/test.bify", []);
        Assert.That(module1.Name, Is.EqualTo("test"));

        var module2 = new AstModule("", "path/second/my_module.bify", []);
        Assert.That(module2.Name, Is.EqualTo("my_module"));

        var module3 = new AstModule("custom_name", "", []);
        Assert.That(module3.Name, Is.EqualTo("custom_name"));
    }

    [Test]
    public void AstModuleInvalidIdentifierTest()
    {
        BifyAssert.ActionThrows(DiagnosticKind.NAME, () => new AstModule("path/second/test-module.bify", []));
        BifyAssert.ActionThrows(DiagnosticKind.NAME, () => new AstModule("path/second/test*module.bify", []));
        BifyAssert.ActionThrows(DiagnosticKind.NAME, () => new AstModule("path/second/123test.bify", []));
        BifyAssert.ActionThrows(DiagnosticKind.NAME, () => new AstModule("path/second/test module.bify", []));

        var validModule = new AstModule("valid_mod", []);
        BifyAssert.ActionThrows(DiagnosticKind.NAME, () => validModule.Name = "invalid-name");
    }

    [Test]
    public void AstModuleSourceCodeRememberedTest()
    {
        string testCode = "val a: int = 10;\nval b: int = 20;";
        var module = Bify.Parse(testCode);

        Assert.That(module.SourceCode, Is.Not.Null);
        Assert.That(module.SourceCode.Length, Is.EqualTo(2));
        Assert.That(module.SourceCode[0], Is.EqualTo("val a: int = 10;"));
        Assert.That(module.SourceCode[1], Is.EqualTo("val b: int = 20;"));
        Assert.That(module.SourceLines, Is.EqualTo(module.SourceCode));
    }

    [Test]
    public void DottedTypeDeclarationAndNewTest()
    {
        string testCode = "val pr: testModule.Printer = new testModule.Printer();";
        var module = Bify.Parse(testCode);

        Assert.That(module.ChildNodes.Count, Is.EqualTo(1));
        Assert.That(module.ChildNodes[0], Is.InstanceOf<AstVarDecl>());
        var varDecl = (AstVarDecl)module.ChildNodes[0];
        Assert.That(varDecl.Type, Is.InstanceOf<AstMemberAccess>());
        var typeAccess = (AstMemberAccess)varDecl.Type;
        Assert.That(typeAccess.Left.Token.Value, Is.EqualTo("testModule"));
        Assert.That(typeAccess.Right.Token.Value, Is.EqualTo("Printer"));
        Assert.That(varDecl.AssignmentNode.Right, Is.InstanceOf<AstNew>());
        var astNew = (AstNew)varDecl.AssignmentNode.Right;
        Assert.That(astNew.ValueNode, Is.InstanceOf<AstMemberAccess>());
    }
}
