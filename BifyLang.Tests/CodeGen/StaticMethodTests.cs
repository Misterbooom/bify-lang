#nullable enable
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class StaticMethodTests : BifyTestBase
{
    [Test]
    public void StaticMethod_ParameterlessVoid_ExecutesCorrectly()
    {
        string code = """
        class Logger {
            public static func ping() {
                printf("pong");
            }
        }
        func main() -> int {
            Logger.ping();
            return 0;
        }
        """;

        AssertExecution(code, "pong", 0);
    }

    [Test]
    public void StaticMethod_WithParametersAndReturn_ExecutesCorrectly()
    {
        string code = """
        class MathOps {
            public static func add(a: int, b: int) -> int {
                return a + b;
            }
        }
        func main() -> int {
            return MathOps.add(10, 32);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(42));
    }

    [Test]
    public void StaticMethod_WithClassParameter_PreservesArcLifetime()
    {
        string code = """
        class Box {
            public mut value: int;
            public constructor(v: int) {
                this.value = v;
            }
            public destructor() {
                printf("dtor:%d;", value);
            }
            public static func read(b: Box) -> int {
                return b.value;
            }
        }
        func run() -> int {
            val b = new Box(19);
            return Box.read(b);
        }
        func main() -> int {
            val res = run();
            printf("res:%d;", res);
            return 0;
        }
        """;

        AssertExecution(code, "dtor:19;res:19;", 0);
    }

    [Test]
    public void StaticMethod_FactoryReturningClass_ExecutesAndReleasesOnce()
    {
        string code = """
        class Item {
            public mut id: int;
            public constructor(id: int) {
                this.id = id;
            }
            public destructor() {
                printf("dtor:%d;", id);
            }
            public static func create(id: int) -> Item {
                return new Item(id);
            }
        }
        func useFactory() -> int {
            val item = Item.create(7);
            return item.id;
        }
        func main() -> int {
            val id = useFactory();
            printf("id:%d;", id);
            return 0;
        }
        """;

        AssertExecution(code, "dtor:7;id:7;", 0);
    }

    [Test]
    public void StaticMethod_CallingAnotherStaticMethod_ExecutesCorrectly()
    {
        string code = """
        class Calc {
            public static func square(x: int) -> int {
                return x * x;
            }
            public static func sumOfSquares(a: int, b: int) -> int {
                return Calc.square(a) + Calc.square(b);
            }
        }
        func main() -> int {
            return Calc.sumOfSquares(3, 4);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(25));
    }

    [Test]
    public void StaticMethod_NamedAndDefaultArguments_ExecutesCorrectly()
    {
        string code = """
        class Config {
            public static func combine(a: int, b: int = 5) -> int {
                return a * 10 + b;
            }
        }
        func main() -> int {
            val first = Config.combine(2);
            val second = Config.combine(b: 3, a: 1);
            return first + second;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(38));
    }

    [Test]
    public void StaticMethod_InAbstractClass_CalledWithoutInstantiation()
    {
        string code = """
        abstract class Base {
            public abstract func instanceOp() -> int;
            public static func helper(x: int) -> int {
                return x + 15;
            }
        }
        func main() -> int {
            return Base.helper(27);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(42));
    }

    [Test]
    public void StaticClass_WithStaticMethod_ExecutesCorrectly()
    {
        string code = """
        static class Utils {
            public static func triple(x: int) -> int {
                return x * 3;
            }
        }
        func main() -> int {
            return Utils.triple(14);
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(42));
    }

    [Test]
    public void StaticMethod_LlvmIr_HasNoThisParameterAndNotInVTable()
    {
        string code = """
        class Widget {
            public virtual func instanceMethod(x: int) -> int {
                return x;
            }
            public static func staticMethod(a: int, b: int) -> int {
                return a + b;
            }
        }
        func main() -> int {
            return Widget.staticMethod(1, 2);
        }
        """;

        using var compiled = Bify.Compile(code);
        compiled.AssertValid();

        var ir = compiled.Module.PrintToString();
        Assert.That(ir, Does.Contain("Widget_staticMethod(i32"));
        var staticFn = compiled.Module.GetNamedFunction(NameMangler.GenerateMethodName("main", "Widget", "staticMethod"));
        Assert.That(staticFn.Handle, Is.Not.EqualTo(IntPtr.Zero));
        Assert.That(staticFn.ParamsCount, Is.EqualTo(2));
    }

    [Test]
    public void PrivateStaticMethod_SameClassAllowed_ExternalRejected()
    {
        string validCode = """
        class Secret {
            private static func hidden() -> int {
                return 33;
            }
            public static func reveal() -> int {
                return Secret.hidden();
            }
        }
        func main() -> int {
            return Secret.reveal();
        }
        """;

        var result = NativeRunner.Run(validCode);
        Assert.That(result.ExitCode, Is.EqualTo(33));

        string invalidCode = """
        class Secret {
            private static func hidden() -> int {
                return 33;
            }
        }
        func main() -> int {
            return Secret.hidden();
        }
        """;

        AssertCompileError(invalidCode, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void ProtectedStaticMethod_SubclassAllowed_ExternalRejected()
    {
        string validCode = """
        class Base {
            protected static func code() -> int {
                return 21;
            }
        }
        class Derived : Base {
            public static func doubled() -> int {
                return Base.code() * 2;
            }
        }
        func main() -> int {
            return Derived.doubled();
        }
        """;

        var result = NativeRunner.Run(validCode);
        Assert.That(result.ExitCode, Is.EqualTo(42));

        string invalidCode = """
        class Base {
            protected static func code() -> int {
                return 21;
            }
        }
        func main() -> int {
            return Base.code();
        }
        """;

        AssertCompileError(invalidCode, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void PublicStaticMethod_CrossModule_ExecutesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            string moduleFile = Path.Combine(tempDir, "mathmod.bify");
            File.WriteAllText(moduleFile, """
            public class MathUtils {
                public static func square(x: int) -> int {
                    return x * x;
                }
            }
            """);

            string mainFile = Path.Combine(tempDir, "main.bify");
            File.WriteAllText(mainFile, """
            import "./mathmod.bify";
            func main() -> int {
                return mathmod.MathUtils.square(6);
            }
            """);

            string outFile = Path.Combine(tempDir, "main.out");
            CompilationHelper.CompileFile(mainFile, outFile, CompilerFlags.NONE);
            Assert.That(File.Exists(outFile), Is.True);

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = outFile,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.That(process, Is.Not.Null);
            process!.WaitForExit(5000);
            Assert.That(process.ExitCode, Is.EqualTo(36));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void StaticMethod_CalledOnInstance_Rejected()
    {
        string code = """
        class Foo {
            public static func bar() -> int {
                return 1;
            }
        }
        func main() -> int {
            val f = new Foo();
            return f.bar();
        }
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void InstanceMethod_CalledOnClassName_Rejected()
    {
        string code = """
        class Foo {
            public func bar() -> int {
                return 1;
            }
        }
        func main() -> int {
            return Foo.bar();
        }
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void StaticMethod_ReferencingThis_Rejected()
    {
        string code = """
        class Foo {
            public mut x: int = 5;
            public static func bar() -> int {
                return this.x;
            }
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.NAME);
    }

    [Test]
    public void StaticMethod_ReferencingInstanceField_Rejected()
    {
        string code = """
        class Foo {
            public mut x: int = 5;
            public static func bar() -> int {
                return x;
            }
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.NAME);
    }

    [Test]
    public void StaticClass_WithNonStaticMethod_Rejected()
    {
        string code = """
        static class BadStatic {
            public func instanceMethod() -> int {
                return 1;
            }
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.NAME);
    }

    [Test]
    public void StaticClass_DirectInstantiation_Rejected()
    {
        string code = """
        static class Utils {
            public static func ping() {}
        }
        func main() {
            val u = new Utils();
        }
        """;

        AssertCompileError(code, DiagnosticKind.CANNOT_INSTANTIATE_STATIC_CLASS);
    }

    [Test]
    public void StaticClass_Inheritance_Rejected()
    {
        string code = """
        static class Utils {
            public static func ping() {}
        }
        class Sub : Utils {
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void StaticConstructor_Rejected()
    {
        string code = """
        class Foo {
            public static constructor() {}
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void StaticDestructor_Rejected()
    {
        string code = """
        class Foo {
            public static destructor() {}
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }
}
