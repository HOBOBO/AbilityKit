using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using NUnit.Framework;

public sealed class SkillWaitConditionCatalogTests
{
    [Test]
    public void Catalog_ResolvesBuiltInsCaseInsensitively()
    {
        Assert.IsTrue(SkillWaitConditionCatalog.TryGet("observedslotsidle", out var observedSlotsIdle));
        Assert.AreEqual("ObservedSlotsIdle", observedSlotsIdle.Id);

        Assert.IsTrue(SkillWaitConditionCatalog.TryGet("INPUTRELEASED", out var inputReleased));
        Assert.AreEqual("InputReleased", inputReleased.Id);
    }

    [Test]
    public void Catalog_RejectsUnknownCondition()
    {
        var specification = new SkillWaitUntilPhaseDTO { Condition = "MissingCondition" };

        Assert.IsFalse(SkillWaitConditionCatalog.TryValidate(specification, out var error));
        StringAssert.Contains("unsupported wait condition", error);
    }

    [Test]
    public void ObservedSlotsIdle_WithoutObservedSlotsCompletesForCompatibility()
    {
        Assert.IsTrue(SkillWaitConditionCatalog.TryGet("ObservedSlotsIdle", out var condition));

        Assert.IsTrue(condition.IsMet(new SkillPipelineContext(), new SkillWaitUntilPhaseDTO
        {
            Condition = "ObservedSlotsIdle"
        }));
    }

    [Test]
    public void InputReleased_CompletesOnlyAfterContextReceivesRelease()
    {
        Assert.IsTrue(SkillWaitConditionCatalog.TryGet("InputReleased", out var condition));
        var context = new SkillPipelineContext();
        var specification = new SkillWaitUntilPhaseDTO { Condition = "InputReleased" };

        Assert.IsFalse(condition.IsMet(context, specification));

        context.MarkInputReleased();

        Assert.IsTrue(condition.IsMet(context, specification));
    }

    [Test]
    public void InputReleased_RejectsUnexpectedArguments()
    {
        var specification = new SkillWaitUntilPhaseDTO
        {
            Condition = "InputReleased",
            Arguments = new[]
            {
                new SkillWaitConditionArgumentDTO { Name = "unused", Value = "true" }
            }
        };

        Assert.IsFalse(SkillWaitConditionCatalog.TryValidate(specification, out var error));
        StringAssert.Contains("does not accept arguments", error);
    }
}
