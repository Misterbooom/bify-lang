#nullable enable
using BifyLang.CodeGen;
using BifyLang.CodeGen.Core;
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class StaticFieldTests : BifyTestBase
{
    [Test]
    public void StaticField_PrimitiveReadAndMutate_ExecutesCorrectly()
    {
        string code = """
        class Counter {
            public static mut count: int = 10;
            public static val step: int = 5;
            public static func increment() {
                Counter.count = Counter.count + Counter.step;
            }
        }
        func main() -> int {
            Counter.increment();
            Counter.increment();
            Counter.count = Counter.count + 2;
            return Counter.count;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(22));
    }

    [Test]
    public void StaticField_UninitializedPrimitive_DefaultsToZero()
    {
        string code = """
        class State {
            public static mut value: int;
        }
        func main() -> int {
            val initial = State.value;
            State.value = 17;
            return initial + State.value;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(17));
    }

    [Test]
    public void StaticField_ClassInitializer_InitializesBeforeMainAndCleansUpAfterMain()
    {
        string code = """
        class Resource {
            public mut id: int;
            public constructor(id: int) {
                this.id = id;
                printf("ctor:%d;", id);
            }
            public destructor() {
                printf("dtor:%d;", id);
            }
        }
        class Holder {
            public static val shared: Resource = new Resource(42);
        }
        func main() -> int {
            printf("main:%d;", Holder.shared.id);
            return 0;
        }
        """;

        AssertExecution(code, "ctor:42;main:42;dtor:42;", 0);
    }

    [Test]
    public void StaticField_ReassignClassField_ReleasesOldAndCleansUpNew()
    {
        string code = """
        class Node {
            public mut id: int;
            public constructor(id: int) {
                this.id = id;
            }
            public destructor() {
                printf("dtor:%d;", id);
            }
        }
        class Registry {
            public static mut current: Node = new Node(1);
        }
        func main() -> int {
            printf("first:%d;", Registry.current.id);
            Registry.current = new Node(2);
            printf("second:%d;", Registry.current.id);
            return 0;
        }
        """;

        AssertExecution(code, "first:1;dtor:1;second:2;dtor:2;", 0);
    }

    [Test]
    public void StaticField_InStaticClass_ExecutesCorrectly()
    {
        string code = """
        static class Config {
            public static val basePort: int = 80;
            public static mut offset: int = 8;
        }
        func main() -> int {
            Config.offset = Config.offset + 2;
            return Config.basePort + Config.offset;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(90));
    }

    [Test]
    public void StaticField_InAbstractClass_ExecutesCorrectly()
    {
        string code = """
        abstract class Base {
            public static mut sharedValue: int = 30;
            public abstract func op() -> int;
        }
        func main() -> int {
            Base.sharedValue = Base.sharedValue + 12;
            return Base.sharedValue;
        }
        """;

        var result = NativeRunner.Run(code);
        Assert.That(result.ExitCode, Is.EqualTo(42));
    }

    [Test]
    public void StaticField_LlvmIr_NotInInstanceStructAndEmittedAsGlobal()
    {
        string code = """
        class Metrics {
            public static mut total: int = 99;
            public mut instanceVal: int = 1;
        }
        func main() -> int {
            return Metrics.total;
        }
        """;

        using var compiled = Bify.Compile(code);
        compiled.AssertValid();

        var metricsType = (ClassType)compiled.Context.TypeRegistry.GetBifyType("main.Metrics");
        // Instance layout: vptr (0), strong_count (1), weak_count (2), instanceVal (3)
        Assert.That(metricsType.StructType.StructElementTypesCount, Is.EqualTo(4));
        Assert.That(metricsType.Fields["total"].GlobalValue.Handle, Is.Not.EqualTo(IntPtr.Zero));
    }

    [Test]
    public void PrivateStaticField_SameClassAllowed_ExternalRejected()
    {
        string validCode = """
        class Vault {
            private static mut pin: int = 55;
            public static func readPin() -> int {
                return Vault.pin;
            }
        }
        func main() -> int {
            return Vault.readPin();
        }
        """;

        var result = NativeRunner.Run(validCode);
        Assert.That(result.ExitCode, Is.EqualTo(55));

        string invalidCode = """
        class Vault {
            private static mut pin: int = 55;
        }
        func main() -> int {
            return Vault.pin;
        }
        """;

        AssertCompileError(invalidCode, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void ProtectedStaticField_SubclassAllowed_ExternalRejected()
    {
        string validCode = """
        class Base {
            protected static mut secret: int = 21;
        }
        class Derived : Base {
            public static func doubled() -> int {
                return Base.secret * 2;
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
            protected static mut secret: int = 21;
        }
        func main() -> int {
            return Base.secret;
        }
        """;

        AssertCompileError(invalidCode, DiagnosticKind.ATTRIBUTE);
    }

    [Test]
    public void PublicStaticField_CrossModule_ExecutesCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            string moduleFile = Path.Combine(tempDir, "cfg.bify");
            File.WriteAllText(moduleFile, """
            public class Settings {
                public static mut factor: int = 6;
            }
            """);

            string mainFile = Path.Combine(tempDir, "main.bify");
            File.WriteAllText(mainFile, """
            import "./cfg.bify";
            func main() -> int {
                cfg.Settings.factor = cfg.Settings.factor + 1;
                return cfg.Settings.factor * 6;
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
            Assert.That(process.ExitCode, Is.EqualTo(42));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void StaticValField_Reassignment_Rejected()
    {
        string code = """
        class Constants {
            public static val max: int = 10;
        }
        func main() {
            Constants.max = 20;
        }
        """;

        AssertCompileError(code, DiagnosticKind.TYPE);
    }

    [Test]
    public void InstanceField_AssignedViaClassName_Rejected()
    {
        string code = """
        class Foo {
            public mut x: int = 5;
        }
        func main() {
            Foo.x = 10;
        }
        """;

        AssertCompileError(code, DiagnosticKind.ATTRIBUTE);
    }
}
