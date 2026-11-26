#nullable enable
using BifyLang.CodeGen.Core.Validators;
using BifyLang.Exceptions;
using BifyLang.Tests.Infrastructure;

namespace BifyLang.Tests.CodeGen;

[TestFixture]
public class FlagCheckerTests : BifyTestBase
{
    [TestCase("virtual", "override", TestName = "ConflictingMethodFlags_VirtualOverride")]
    [TestCase("abstract", "override", TestName = "ConflictingMethodFlags_AbstractOverride")]
    [TestCase("abstract", "final", TestName = "ConflictingMethodFlags_AbstractFinal")]
    [TestCase("public", "private", TestName = "ConflictingMethodFlags_MultipleAccess")]
    public void ConflictingMethodFlags_Rejected(string first, string second)
    {
        Assert.That(FlagChecker.CheckMethodFlags([first, second], out var error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("abstract", "final", TestName = "ConflictingClassFlags_AbstractFinal")]
    [TestCase("public", "private", TestName = "ConflictingClassFlags_MultipleAccess")]
    public void ConflictingClassFlags_Rejected(string first, string second)
    {
        Assert.That(FlagChecker.CheckClassFlags([first, second], out var error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }
}
