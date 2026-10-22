using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Share.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Config;

public sealed class MobaAttributeGrowthTests
{
    [Fact]
    public void Attribute_template_growth_is_applied_per_level_to_attributes_and_resources()
    {
        var template = new BattleAttributeTemplateMO(new BattleAttributeTemplateDTO
        {
            Hp = 100,
            HpGrowth = 10,
            MaxHp = 120,
            MaxHpGrowth = 20,
            PhysicsAttack = 50,
            PhysicsAttackGrowth = 5,
            MoveSpeed = 4.5f,
            MoveSpeedGrowth = 0.1f
        });

        var levelOne = new MobaActorAttributeInitializer().Prepare(template, 1);
        var levelFour = new MobaActorAttributeInitializer().Prepare(template, 4);

        Assert.Equal(100f, levelOne.Group.GetValue(MobaAttributeIds.HP));
        Assert.Equal(130f, levelFour.Group.GetValue(MobaAttributeIds.HP));
        Assert.Equal(180f, levelFour.Group.GetValue(MobaAttributeIds.MAX_HP));
        Assert.Equal(65f, levelFour.Group.GetValue(MobaAttributeIds.PHYSICS_ATTACK));
        Assert.Equal(4.8f, levelFour.Group.GetValue(MobaAttributeIds.MOVE_SPEED), 3);
        Assert.Equal(MobaResourceFixedConvert.ToFixed(130).RawValue,
            levelFour.Resources.Map[ResourceType.Hp].Current.RawValue);
        Assert.Equal(MobaResourceFixedConvert.ToFixed(180).RawValue,
            levelFour.Resources.Map[ResourceType.Hp].LastMax.RawValue);
    }
}
