#nullable enable

using System;
using AbilityKit.GameplayTags;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Config;

namespace AbilityKit.Samples.Logic.Samples.Tags
{
    /// <summary>
    /// TagRequirements 概述 - 演示标签需求检查
    /// </summary>
    [Sample]
    public sealed class TagRequirementsOverview : SampleBase
    {
        public override string Title => "Tag Requirements Overview";
        public override string Description => "使用 GameplayTagRequirements 进行条件判断";
        public override SampleCategory Category => SampleCategory.Tags;

        private static GameplayTag Tag(string name) => GameplayTagManager.Instance.RequestTag(name);

        protected override void OnRun()
        {
            Log("=== Tag Requirements 概述 ===");
            Output.Divider();

            // 确保标签已注册
            SampleConfig.LoadAndRegisterTags();

            Log("【1】GameplayTagRequirements 概念");
            Output.Bullet("Required: 必须包含的标签（All 匹配）");
            Output.Bullet("Blocked: 不能包含的标签（Any 匹配）");
            Output.Bullet("Exact: 是否精确匹配（默认 false，支持层级匹配）");
            Log("");

            Log("【2】创建需求条件");
            var requiredContainer = new GameplayTagContainer();
            requiredContainer.Add(Tag("Buff.AttackSpeed"));
            requiredContainer.Add(Tag("Unit.Hero"));

            var blockedContainer = new GameplayTagContainer();
            blockedContainer.Add(Tag("Status.Dead"));

            var requirements = new GameplayTagRequirements(requiredContainer, blockedContainer);
            KeyValue("TagRequirements.RequiredCount", requiredContainer.Count.ToString());
            KeyValue("TagRequirements.BlockedCount", blockedContainer.Count.ToString());
            KeyValue("TagRequirements.Predicate", "Require Buff.AttackSpeed + Unit.Hero, Block Status.Dead");

            Log("需求: 需要 Buff.AttackSpeed + Unit.Hero，阻止 Status.Dead");
            Log("");

            // 测试场景 A
            Log("【3】测试场景");
            var containerA = new GameplayTagContainer();
            containerA.Add(Tag("Buff.AttackSpeed"));
            containerA.Add(Tag("Buff.MoveSpeed"));
            containerA.Add(Tag("Unit.Hero"));

            Log("场景 A - 角色拥有: Buff.AttackSpeed, Buff.MoveSpeed, Unit.Hero");
            var sceneA = requirements.IsSatisfiedBy(containerA);
            KeyValue("TagRequirements.SceneA", sceneA.ToString());
            Log($"IsSatisfiedBy: {sceneA}");
            Log("");

            // 测试场景 B - 缺少 Required
            var containerB = new GameplayTagContainer();
            containerB.Add(Tag("Buff.MoveSpeed"));
            containerB.Add(Tag("Unit.Hero"));

            Log("场景 B - 角色拥有: Buff.MoveSpeed, Unit.Hero (缺少 AttackSpeed)");
            var sceneB = requirements.IsSatisfiedBy(containerB);
            KeyValue("TagRequirements.SceneB", sceneB.ToString());
            Log($"IsSatisfiedBy: {sceneB}");
            Log("");

            // 测试场景 C - 包含 Blocked
            var containerC = new GameplayTagContainer();
            containerC.Add(Tag("Buff.AttackSpeed"));
            containerC.Add(Tag("Unit.Hero"));
            containerC.Add(Tag("Status.Dead"));

            Log("场景 C - 角色拥有: Buff.AttackSpeed, Unit.Hero, Status.Dead");
            var sceneC = requirements.IsSatisfiedBy(containerC);
            KeyValue("TagRequirements.SceneC", sceneC.ToString());
            Log($"IsSatisfiedBy: {sceneC}");
            Log("");

            // 4. 快捷方法
            Log("【4】快捷构造方法");
            Log("  GameplayTagRequirements.Require(tag1, tag2)");
            Log("    -> 只有 Required，无 Blocked");
            Log("");
            Log("  GameplayTagRequirements.Block(tag1, tag2)");
            Log("    -> 只有 Blocked，无 Required");

            var quickRequire = GameplayTagRequirements.Require(
                Tag("Skill.Ultimate"),
                Tag("Cost.Mana"));

            var quickBlock = GameplayTagRequirements.Block(
                Tag("Status.Silenced"));

            KeyValue("TagRequirements.QuickRequire", quickRequire.Required.Count.ToString());
            KeyValue("TagRequirements.QuickBlock", quickBlock.Blocked.Count.ToString());
            Log($"快捷 Require: {quickRequire.Required.Count} 个必需标签");
            Log($"快捷 Block: {quickBlock.Blocked.Count} 个阻止标签");
            Log("");

            // 5. 典型应用场景
            Log("【5】典型应用场景");
            Output.Bullet("技能释放条件: 需要某些 Buff 才能释放");
            Output.Bullet("伤害修正: 对特定目标生效");
            Output.Bullet("Buff 叠加规则: 互斥的 Debuff");
            Output.Bullet("目标过滤: 选择特定类型的单位");

            Output.Divider();
        }
    }
}
