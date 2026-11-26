#nullable enable
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class VisibilityTests : BifyTestBase
{
    [Test]
    public void PrivateFunction_WithPrivateTypes_Allowed()
    {
        string code = """
        private class Secret {}
        private struct Data { mut x: int; }
        func useSecret(s: Secret, d: Data) -> Secret {
            return s;
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void PrivateClassAndStruct_PublicMembersWithPrivateTypes_Allowed()
    {
        string code = """
        private class Inner {}
        private struct InnerVal { mut v: int; }
        class Holder {
            public mut inner: Inner;
            public val data: InnerVal;
            public constructor(i: Inner) {
                this.inner = i;
            }
            public func getInner(d: InnerVal) -> Inner {
                return inner;
            }
        }
        struct Wrapper {
            mut data: InnerVal;
            public func getData(d: InnerVal) -> InnerVal {
                return d;
            }
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void PublicClassAndStruct_PrivateMembersWithPrivateTypes_Allowed()
    {
        string code = """
        private class Secret {}
        private struct SecretVal { mut x: int; }
        public class Api {
            private val s: Secret;
            private func helper(v: SecretVal) -> Secret {
                return s;
            }
        }
        public struct PublicBox {
            private mut v: SecretVal;
            private func getV() -> SecretVal {
                return v;
            }
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void PrivateClass_InheritingFromPrivateBase_Allowed()
    {
        string code = """
        private class Base {}
        class Derived : Base {}
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void PublicFunction_WithPublicTypes_Allowed()
    {
        string code = """
        public class PubClass {}
        public struct PubStruct { mut x: int; }
        public func echo(c: PubClass, s: PubStruct) -> PubClass {
            return c;
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();
    }

    [Test]
    public void PublicFunction_ReturningPrivateClassOrStruct_Rejected()
    {
        AssertCompileError("""
        private class Secret {}
        public func leak() -> Secret { return new Secret(); }
        func main() {}
        """, DiagnosticKind.ACCESS_VIOLATION);

        AssertCompileError("""
        private struct SecretVal { mut x: int; }
        public func leak() -> SecretVal { return new SecretVal(x: 1); }
        func main() {}
        """, DiagnosticKind.ACCESS_VIOLATION);
    }

    [Test]
    public void PublicFunction_TakingPrivateParameter_Rejected()
    {
        AssertCompileError("""
        private class Secret {}
        public func take(s: Secret) {}
        func main() {}
        """, DiagnosticKind.ACCESS_VIOLATION);

        AssertCompileError("""
        private struct SecretVal { mut x: int; }
        public func take(s: SecretVal) {}
        func main() {}
        """, DiagnosticKind.ACCESS_VIOLATION);
    }

    [TestCase("public", TestName = "PublicClass_PublicMethod_ReturningPrivateType_Rejected")]
    [TestCase("protected", TestName = "PublicClass_ProtectedMethod_ReturningPrivateType_Rejected")]
    public void PublicClass_VisibleMethod_ReturningPrivateType_Rejected(string access)
    {
        string code = $$"""
        private class Secret {}
        public class Api {
            {{access}} func leak() -> Secret { return new Secret(); }
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ACCESS_VIOLATION);
    }

    [TestCase("public", TestName = "PublicClass_PublicMethod_PrivateParam_Rejected")]
    [TestCase("protected", TestName = "PublicClass_ProtectedMethod_PrivateParam_Rejected")]
    public void PublicClass_VisibleMethod_TakingPrivateParam_Rejected(string access)
    {
        string code = $$"""
        private struct SecretVal { mut x: int; }
        public class Api {
            {{access}} func take(v: SecretVal) {}
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ACCESS_VIOLATION);
    }

    [Test]
    public void PublicClass_PublicConstructor_TakingPrivateParam_Rejected()
    {
        string code = """
        private class Secret {}
        public class Api {
            public constructor(s: Secret) {}
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ACCESS_VIOLATION);
    }

    [TestCase("public", TestName = "PublicClass_PublicField_PrivateType_Rejected")]
    [TestCase("protected", TestName = "PublicClass_ProtectedField_PrivateType_Rejected")]
    public void PublicClass_VisibleField_PrivateType_Rejected(string access)
    {
        string code = $$"""
        private class Secret {}
        public class Api {
            {{access}} val s: Secret;
        }
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ACCESS_VIOLATION);
    }

    [Test]
    public void PublicStruct_PublicMethodAndField_PrivateType_Rejected()
    {
        AssertCompileError("""
        private struct SecretVal { mut x: int; }
        public struct ApiStruct {
            public mut v: SecretVal;
        }
        func main() {}
        """, DiagnosticKind.ACCESS_VIOLATION);

        AssertCompileError("""
        private struct SecretVal { mut x: int; }
        public struct ApiStruct {
            public func leak() -> SecretVal { return new SecretVal(x: 1); }
        }
        func main() {}
        """, DiagnosticKind.ACCESS_VIOLATION);
    }

    [Test]
    public void PublicClass_InheritingFromPrivateBase_Rejected()
    {
        string code = """
        private class Base {}
        public class Derived : Base {}
        func main() {}
        """;

        AssertCompileError(code, DiagnosticKind.ACCESS_VIOLATION);
    }
}
