#nullable enable
using BifyLang.CodeGen.Core.Types.Data;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class ClassDestructorTests : BifyTestBase
{
    [Test]
    public void ClassDestructor_DefaultSynthesizedAtSlot0()
    {
        string code = """
        class Item {
            public val id: int;
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var item = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Item");
        Assert.That(item.VTable.Slots.Count, Is.EqualTo(1));
        Assert.That(item.VTable.Slots[0].MethodName, Is.EqualTo("destructor"));
        Assert.That(item.VTable.Slots[0].Method.DeclaringType.Name, Is.EqualTo("Item"));
        Assert.That(item.Methods["destructor"].VTableIndex, Is.EqualTo(0));
    }

    [Test]
    public void ClassDestructor_ExplicitDeclarationAtSlot0()
    {
        string code = """
        class Resource {
            destructor() {
            }
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var res = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Resource");
        Assert.That(res.VTable.Slots.Count, Is.EqualTo(1));
        Assert.That(res.VTable.Slots[0].MethodName, Is.EqualTo("destructor"));
        Assert.That(res.VTable.Slots[0].Method.DeclaringType.Name, Is.EqualTo("Resource"));
        Assert.That(res.Methods["destructor"].VTableIndex, Is.EqualTo(0));
        Assert.That(res.Methods["destructor"].Body, Is.Not.Null);
    }

    [Test]
    public void ClassDestructor_DerivedClassOverridesSlot0()
    {
        string code = """
        class BaseResource {
            destructor() {}
        }
        class DerivedResource : BaseResource {
            destructor() {}
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var b = (ClassType)m.Context.TypeRegistry.GetBifyType("main.BaseResource");
        var d = (ClassType)m.Context.TypeRegistry.GetBifyType("main.DerivedResource");

        Assert.That(b.VTable.Slots[0].Method.DeclaringType.Name, Is.EqualTo("BaseResource"));
        Assert.That(d.VTable.Slots[0].Method.DeclaringType.Name, Is.EqualTo("DerivedResource"));
        Assert.That(d.Methods["destructor"].VTableIndex, Is.EqualTo(0));
    }

    [Test]
    public void ClassDestructor_NormalVirtualMethodsShiftedToSlot1AndAbove()
    {
        string code = """
        class Service {
            public virtual func start() {}
            destructor() {}
            public virtual func stop() {}
        }
        func main() {}
        """;

        using var m = Bify.Compile(code);
        m.AssertValid();

        var svc = (ClassType)m.Context.TypeRegistry.GetBifyType("main.Service");
        Assert.That(svc.VTable.Slots.Count, Is.EqualTo(3));
        Assert.That(svc.VTable.Slots[0].MethodName, Is.EqualTo("destructor"));
        Assert.That(svc.Methods["destructor"].VTableIndex, Is.EqualTo(0));

        Assert.That(svc.VTable.Slots[1].MethodName, Is.EqualTo("start"));
        Assert.That(svc.Methods["start"].VTableIndex, Is.EqualTo(1));

        Assert.That(svc.VTable.Slots[2].MethodName, Is.EqualTo("stop"));
        Assert.That(svc.Methods["stop"].VTableIndex, Is.EqualTo(2));
    }

    [Test]
    public void ClassDestructor_MultipleDestructors_Throws() =>
        AssertCompileError("class A { destructor() {} destructor() {} } func main() {}", DiagnosticKind.NAME);

    [Test]
    public void ClassDestructor_WithParameters_Throws() =>
        AssertCompileError("class A { destructor(x: int) {} } func main() {}", DiagnosticKind.SYNTAX);

    [Test]
    public void ClassDestructor_FieldNamedDestructor_Throws() =>
        AssertCompileError("class A { public val destructor: int; } func main() {}", DiagnosticKind.SYNTAX);
}
