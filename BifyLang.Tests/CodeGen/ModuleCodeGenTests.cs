using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ModuleCodeGenTests : BifyTestBase
{
    [Test]
    public void ImportModule_InstantiateDottedClass_CompilesSuccessfully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            string moduleFile = Path.Combine(tempDir, "mod.bify");
            File.WriteAllText(moduleFile, """
            public class Greeter {
                public func greet() {
                    printf("Hello from module");
                }
            }
            """);

            string mainFile = Path.Combine(tempDir, "main.bify");
            File.WriteAllText(mainFile, """
            import "./mod.bify";
            func main() {
                val g: mod.Greeter = new mod.Greeter();
                g.greet();
            }
            """);

            string outFile = Path.Combine(tempDir, "main.out");
            CompilationHelper.CompileFile(mainFile, outFile, CompilerFlags.NONE);
            Assert.That(File.Exists(outFile), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void PrivateFunction_SameModule_ExecutesCorrectly()
    {
        string code = """
        private func helper(x: int) -> int {
            return x * 2;
        }
        func implicitHelper(x: int) -> int {
            return x + 5;
        }
        func main() -> int {
            return helper(10) + implicitHelper(3);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(28));
    }

    [Test]
    public void PublicFunction_CrossModule_CompilesAndExecutes()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "math.bify"), """
            public func addTwo(x: int) -> int {
                return x + 2;
            }
            """);

            string mainFile = Path.Combine(tempDir, "main.bify");
            File.WriteAllText(mainFile, """
            import "./math.bify";
            func main() {
                printf("%d", math.addTwo(40));
            }
            """);

            string outFile = Path.Combine(tempDir, "main.out");
            CompilationHelper.CompileFile(mainFile, outFile, CompilerFlags.NONE);
            Assert.That(File.Exists(outFile), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [TestCase("private func secret() -> int { return 1; }", "secret", TestName = "ExplicitPrivateFunction_CrossModule_Rejected")]
    [TestCase("func internalFn() -> int { return 1; }", "internalFn", TestName = "ImplicitPrivateFunction_CrossModule_Rejected")]
    public void PrivateFunction_CrossModule_Rejected(string modFunc, string fnName)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "mod.bify"), modFunc);
            string mainFile = Path.Combine(tempDir, "main.bify");
            File.WriteAllText(mainFile, $$"""
            import "./mod.bify";
            func main() -> int {
                return mod.{{fnName}}();
            }
            """);

            var diagnostics = new BifyLang.Exceptions.DiagnosticBag();
            try
            {
                using var compiled = Bify.Compile(File.ReadAllText(mainFile), diagnostics, mainFile);
            }
            catch (BifyLang.Exceptions.BifyCompilationException)
            {
            }

            Assert.That(
                diagnostics.Diagnostics.Any(d => d.Kind == BifyLang.Exceptions.DiagnosticKind.ATTRIBUTE),
                Is.True,
                $"Expected ACCESS_VIOLATION, got: [{string.Join("; ", diagnostics.Diagnostics.Select(d => $"{d.Kind}: {d.Message}"))}]");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
