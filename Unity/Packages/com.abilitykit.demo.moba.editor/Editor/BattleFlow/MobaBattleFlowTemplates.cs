#if UNITY_EDITOR
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using AbilityKit.Scenario;
using UnityEditor;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>
    /// MOBA 常用套路模板（复合积木）：把"生成施法者+目标 → 施放 → 断言"封装成一个可复用宏。
    /// 策划/测试从调色板拖一个模板即得到一份完整流程，无需懂细颗粒积木怎么拼。
    /// </summary>
    [InitializeOnLoad]
    public static class MobaBattleFlowTemplates
    {
        static MobaBattleFlowTemplates()
        {
            BattleBlockPalette.Register("模板", new BattleCompositeBlock
            {
                Id = "xiaoqiao-skill1-damage",
                DisplayName = "小乔一技能伤害",
                Children = new BattleBlock[]
                {
                    new SpawnActorBlock { Alias = "caster", HeroId = 1002, AttributeTemplateId = 1002, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
                    new SpawnActorBlock { Alias = "target", HeroId = 1001, AttributeTemplateId = 1001, TeamId = 2, Position = new TestVector3(6, 0, 0) },
                    new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
                    new AssertTraceBlock { Kind = "DamageApply", ConfigId = 10020101 },
                },
            });

            BattleBlockPalette.Register("模板", new BattleCompositeBlock
            {
                Id = "lianpo-skill1-dash",
                DisplayName = "廉颇一技能冲锋",
                Children = new BattleBlock[]
                {
                    new SpawnActorBlock { Alias = "caster", HeroId = 1001, AttributeTemplateId = 1001, PlayerId = "player_1", Position = new TestVector3(0, 0, 0) },
                    new SpawnActorBlock { Alias = "target", HeroId = 1002, AttributeTemplateId = 1002, TeamId = 2, Position = new TestVector3(3, 0, 0) },
                    new TimelineStepBlock { AtMs = 100, Action = "cast_skill", ActorAlias = "caster", TargetAlias = "target", Slot = 1 },
                    new AssertTraceBlock { Kind = "DamageApply", ConfigId = 10010101 },
                },
            });

            // DSL 断言动词 → MOBA 断言积木。
            BattleFlowDslParser.AssertFactory = (verb, args) => verb switch
            {
                "assert" => args.Length >= 2 ? new AssertTraceBlock { Kind = args[0], ConfigId = int.Parse(args[1]) } : null,
                "assert-not" => args.Length >= 2 ? new AssertNoTraceBlock { Kind = args[0], ConfigId = int.Parse(args[1]) } : null,
                "assert-state" => args.Length >= 4 ? new AssertStateBlock { Alias = args[0], Property = args[1], Comparator = args[2], ExpectedValue = args[3] } : null,
                _ => null,
            };
        }
    }
}
#endif
