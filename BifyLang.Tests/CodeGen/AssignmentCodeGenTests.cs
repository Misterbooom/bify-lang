#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class AssignmentCodeGenTests : BifyTestBase
{
    [TestCase("mut x: int = 5; x = 10;", TestName = "Assign_BasicInt")]
    [TestCase("mut x: int = 10; x += 5;", TestName = "Assign_CompoundAdd")]
    [TestCase("mut x: int = 10; x -= 4;", TestName = "Assign_CompoundSub")]
    [TestCase("mut x: int = 3; x *= 4;", TestName = "Assign_CompoundMul")]
    [TestCase("mut x: int = 20; x /= 5;", TestName = "Assign_CompoundDiv")]
    [TestCase("mut f: float = 2.5; f += 1.5; f -= 0.5; f *= 2.0; f /= 2.0;", TestName = "Assign_FloatCompound")]
    public void Assign_ValidOperations(string body)
    {
        AssertValidFunction($"func test() {{ {body} }}");
    }

    [Test]
    public void Assign_AssignToConstThrows()
    {
        AssertCompileError("func test() { val c: int = 42; c = 100; }", DiagnosticKind.TYPE);
    }

    [Test]
    public void Assign_AssignToValParameterThrows()
    {
        AssertCompileError("func test(a: int) { a = 10; }", DiagnosticKind.TYPE);
        AssertCompileError("func test(val a: int) { a = 10; }", DiagnosticKind.TYPE);
    }

    [Test]
    public void Assign_AssignToMutParameterSucceeds()
    {
        AssertValidFunction("func test(mut a: int) { a = 10; }");
    }

    [Test]
    public void Assign_AssignToUndefinedThrows()
    {
        AssertCompileError("func test() { missingVariable = 10; }", DiagnosticKind.NAME);
    }

    [Test]
    public void Assign_IncompatibleTypeThrows()
    {
        AssertCompileError("func test() { mut x: int = 10; x = \"cannot assign string to int\"; }", DiagnosticKind.TYPE);
    }
}
