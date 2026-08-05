using System.Reflection;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class GeneratedPayloadAccessorContractTests
{
    [Fact]
    public void SkillPipelineAccessor_SupportsEveryDeclaredCurrentAndLegacyField()
    {
        foreach (var fieldName in GetConstantFieldValues(typeof(SkillRulePayloadFields)))
        {
            Assert.True(SkillPipelineContextPayloadAccessor.SupportsField(SkillRulePayloadFields.FieldId(fieldName)));
            Assert.True(SkillPipelineContextPayloadAccessor.SupportsField(SkillRulePayloadFields.LegacyFieldId(fieldName)));
        }
    }

    [Fact]
    public void BattlePayloadCatalog_RecognizesEveryDeclaredField()
    {
        foreach (var fieldName in GetConstantFieldValues(typeof(MobaBattlePayloadFields)))
        {
            Assert.True(MobaBattlePayloadFields.IsKnownFieldId(MobaBattlePayloadFields.FieldId(fieldName)));
        }
    }

    private static IEnumerable<string> GetConstantFieldValues(Type catalogType)
    {
        return catalogType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
    }
}
