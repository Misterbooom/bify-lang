#nullable enable
using BifyLang.Ast;
using BifyLang.CodeGen.Core.Context;
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.Semantics;

[TestFixture]
public class FunctionPathCheckerTests : BifyTestBase
{
    private static void ParseAndCheck(string code, CodeGenContext ctx)
    {
        var module = Bify.Parse(code, ctx.Diagnostics);
        foreach (var child in module.ChildNodes)
        {
            if (child is AstFunctionDecl fn)
            {
                _ = new FunctionPathChecker(fn, ctx);
            }
            else if (child is AstClass cls && cls.BodyNode is AstBlock block)
            {
                foreach (var member in block.ChildNodes)
                {
                    if (member is AstFunctionDecl method)
                        _ = new FunctionPathChecker(method, ctx);
                }
            }
        }
    }

    private static void AssertSuccess(string code)
    {
        using var ctx = new CodeGenContext();
        ParseAndCheck(code, ctx);
        Assert.That(ctx.Diagnostics.HasErrors, Is.False,
            $"Expected no error, but got: [{string.Join("; ", ctx.Diagnostics.Diagnostics.Select(d => $"{d.Kind}: {d.Message}"))}]");
    }

    private static void AssertTypeError(string code, string? expectedSubstring = null)
    {
        using var ctx = new CodeGenContext();
        BifyAssert.ActionThrows(DiagnosticKind.TYPE, () => ParseAndCheck(code, ctx), expectedSubstring, ctx.Diagnostics);
    }

    [Test]
    public void PathCheck_SimpleReturn()
    {
        AssertSuccess("func getVal() -> int { return 42; }");
    }

    [Test]
    public void PathCheck_IfElseBothReturn()
    {
        AssertSuccess("func max(a: int, b: int) -> int { if (a > b) { return a; } else { return b; } }");
    }

    [Test]
    public void PathCheck_IfElseIfElseAllReturn()
    {
        AssertSuccess("func sign(x: int) -> int { if (x > 0) { return 1; } else if (x < 0) { return -1; } else { return 0; } }");
    }

    [Test]
    public void PathCheck_VoidNoReturn()
    {
        AssertSuccess("func doWork() { val a: int = 1; val b: int = 2; }");
    }

    [Test]
    public void PathCheck_VoidEmptyReturn()
    {
        AssertSuccess("func doWork(a: int) { if (a > 0) { return; } val b: int = 2; }");
    }

    [Test]
    public void PathCheck_EarlyReturnDeadCodePruned()
    {
        AssertSuccess("func compute() -> int { return 100; val dead: int = 50; }");
    }

    [Test]
    public void PathCheck_InfiniteWhileReturns()
    {
        AssertSuccess("func loopForever() -> int { while (true) { return 1; } }");
    }

    [Test]
    public void PathCheck_InfiniteForIfElseReturns()
    {
        AssertSuccess("func loopForever() -> int { for (mut i: int = 0; true; i = i + 1) { if (i > 5) { return 1; } else { return 2; } } }");
    }

    [Test]
    public void PathCheck_InfiniteForTrueConditionReturns()
    {
        AssertSuccess("func loopForever() -> int { for (mut i: int = 0; true; i = i + 1) { return 1; } }");
    }

    [Test]
    public void PathCheck_ConstructorVoidAllowed()
    {
        AssertSuccess("class TestClass { mut x: int; constructor(v: int) { this.x = v; return; } }");
    }

    [Test]
    public void PathCheck_SwitchAllReturn()
    {
        AssertSuccess("func getVal(x: int) -> int { switch (x) { case 1: return 10; case 2: return 20; default: return 0; } }");
    }

    [Test]
    public void PathCheck_SwitchAllReturnWithDeadBreakAfterReturn()
    {
        AssertSuccess("func getVal(x: int) -> int { switch (x) { case 1: return 10; break; default: return 0; break; } }");
    }

    [Test]
    public void PathCheck_InfiniteWhileReturns_CompilesToValidLlvmIr()
    {
        AssertValidFunction("func loopForever() -> int { while (true) { return 1; } }", "loopForever");
    }

    [Test]
    public void PathCheck_InfiniteForReturns_CompilesToValidLlvmIr()
    {
        AssertValidFunction("func loopForever() -> int { for (mut i: int = 0; true; i++) { return 1; } }", "loopForever");
    }

    [Test]
    public void PathCheck_ClassMethodMissingReturn_ReportsDiagnosticAndRecovers()
    {
        BifyAssert.CompileError("""
        class Calc {
            public func bad() -> int {
                val x: int = 1;
            }
            public func good() -> int {
                return 2;
            }
        }
        func main() -> int { return 0; }
        """, DiagnosticKind.TYPE, "missing return value of type 'int'");
    }

    [TestCase("func missing() -> int { val a: int = 5; }", "missing return value of type 'int'", TestName = "PathCheck_MissingReturnNonVoid")]
    [TestCase("func check(a: int) -> int { if (a > 0) { return a; } }", "missing return value of type 'int'", TestName = "PathCheck_IfWithoutElseFails")]
    [TestCase("func check(a: int) -> int { if (a > 0) { val x: int = 1; } else { return 2; } }", "missing return value of type 'int'", TestName = "PathCheck_IfElseBranchMissingReturn")]
    [TestCase("func getValue() -> int { return; }", "expected type 'int', found 'void'", TestName = "PathCheck_EmptyReturnInNonVoid")]
    [TestCase("func doWork() { return 42; }", "cannot return value from 'void' function", TestName = "PathCheck_ValueReturnInVoidFails")]
    [TestCase("func compute() -> int { return 10; return; }", "expected type 'int', found 'void'", TestName = "PathCheck_InvalidDeadReturnAfterValidReturnFails")]
    [TestCase("class BadCtor { constructor() { return 1; } }", "cannot return value from 'void' function", TestName = "PathCheck_ValueReturnInConstructorFails")]
    [TestCase("func loop() -> int { while (true) { break; } }", "missing return value of type 'int'", TestName = "PathCheck_WhileTrueWithBreakDoesNotGuaranteeReturn")]
    [TestCase("func loop() -> int { for (mut i: int = 0; true; i = i + 1) { break; } }", "missing return value of type 'int'", TestName = "PathCheck_ForInfiniteWithBreakDoesNotGuaranteeReturn")]
    [TestCase("func loop() -> int { for (mut i: int = 0; i < 10; i = i + 1) { return 1; } }", "missing return value of type 'int'", TestName = "PathCheck_FiniteForDoesNotGuaranteeReturn")]
    [TestCase("func getVal(x: int) -> int { switch (x) { case 1: return 10; } }", "missing return value of type 'int'", TestName = "PathCheck_SwitchMissingDefaultFails")]
    [TestCase("func getVal(x: int, y: int) -> int { switch (x) { case 1: if (y > 0) { break; } return 10; default: return 0; } }", "missing return value of type 'int'", TestName = "PathCheck_SwitchConditionalBreakBeforeReturnFails")]
    public void PathCheck_FailureCases(string code, string expectedSubstring)
    {
        AssertTypeError(code, expectedSubstring);
    }
}
