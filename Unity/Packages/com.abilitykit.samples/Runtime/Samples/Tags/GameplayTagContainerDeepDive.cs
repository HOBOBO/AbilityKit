#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Tags
{
    /// <summary>
    /// GameplayTagContainerDeepDive - GameplayTagContainer 深度示例
    /// 演示 GameplayTagContainer 的高级用法和游戏标签系统
    /// </summary>
    [Sample]
    public sealed class GameplayTagContainerDeepDive : SampleBase
    {
        public override string Title => "GameplayTagContainer Deep Dive";
        public override string Description => "演示 GameplayTag 层级、Container 操作、TagRequirements 等高级特性";
        public override SampleCategory Category => SampleCategory.Tags;

        protected override void OnRun()
        {
            Log("=== GameplayTagContainer 深度示例 ===");
            Output.Divider();

            // 1. 核心概念
            Log("【1】核心概念");
            Output.Bullet("GameplayTag - 游戏标签，类似 UE 的 FGameplayTag");
            Output.Bullet("GameplayTagContainer - 标签容器，类似 UE 的 FGameplayTagContainer");
            Output.Bullet("GameplayTagManager - 标签管理器，处理标签层级匹配");
            Output.Bullet("GameplayTagRequirements - 标签需求检查");
            Output.Bullet("GameplayTagQuery - 标签查询语言");
            Log("");

            // 2. 标签层级
            Log("【2】标签层级结构");
            Output.Bullet("标签使用点分隔的层级路径");
            Output.Bullet("父标签匹配所有子标签");
            Output.Bullet("例如: Skill.Fire 匹配 Skill.Fire.Bolt、Skill.Fire.Area");
            Log("");
            Log("  标签层级示例:");
            Log("    Ability.Damage.Fire");
            Log("    Ability.Damage.Ice");
            Log("    Ability.Heal");
            Log("    Effect.Status.Poison");
            Log("    Effect.Status.Slow");
            Log("    State.Stunned");
            Log("    State.Silenced");
            Log("");

            // 3. Container 基本操作
            Log("【3】Container 基本操作");
            Log("");
            Log("  // 创建容器");
            Log("  var container = new GameplayTagContainer();");
            Log("");
            Log("  // 添加标签");
            Log("  container.Add(tag1);");
            Log("  container.Add(tag2);");
            Log("  container.AddRange(tags);");
            Log("");
            Log("  // 移除标签");
            Log("  container.Remove(tag);");
            Log("  container.RemoveTags(otherContainer);");
            Log("");
            Log("  // 查询");
            Log("  bool has = container.HasTag(tag);");
            Log("  bool hasAny = container.HasAny(otherContainer);");
            Log("  bool hasAll = container.HasAll(otherContainer);");
            Log("");

            // 4. 标签匹配
            Log("【4】标签匹配模式");
            Log("");
            Log("  // Exact 匹配 - 精确匹配");
            Log("  container.HasTagExact(tag);");
            Log("  // 只匹配完全相同的标签");
            Log("");
            Log("  // Parent 匹配 - 父标签匹配子标签（默认）");
            Log("  container.HasTag(tag);");
            Log("  // 如果 container 有 Skill.Damage，tag 是 Skill 则返回 true");
            Log("");

            // 5. Container 组合操作
            Log("【5】Container 组合操作");
            Log("");
            Log("  // 并集 - 合并两个容器");
            Log("  var union = container1.Union(container2);");
            Log("  var union2 = container1 + container2;");
            Log("");
            Log("  // 交集 - 获取共同标签");
            Log("  var intersect = container1.Intersect(container2);");
            Log("  var intersect2 = container1 & container2;");
            Log("");
            Log("  // 差集 - 获取独有的标签");
            Log("  var except = container1.Except(container2);");
            Log("  var except2 = container1 - container2;");
            Log("");

            // 6. 运算符重载
            Log("【6】运算符重载");
            Log("");
            Log("  // 加法 - 添加标签");
            Log("  var result = container + tag;");
            Log("  var result = container1 + container2;");
            Log("");
            Log("  // 减法 - 移除标签");
            Log("  var result = container - tag;");
            Log("  var result = container1 - container2;");
            Log("");
            Log("  // 隐式转换");
            Log("  GameplayTagContainer container = singleTag;");
            Log("");

            // 7. GameplayTagRequirements
            Log("【7】GameplayTagRequirements - 标签需求");
            Log("");
            Log("  // 创建需求");
            Log("  var requirements = new GameplayTagRequirements();");
            Log("  requirements.RequireTags.Add(tag1);");
            Log("  requirements.RequireTags.Add(tag2);");
            Log("  requirements.IgnoreTags.Add(ignoredTag);");
            Log("");
            Log("  // 检查是否满足需求");
            Log("  bool satisfied = requirements.Check(container);");
            Log("");
            Log("  // 需求逻辑:");
            Log("  // (container 包含所有 RequireTags) && (container 不包含任何 IgnoreTags)");
            Log("");

            // 8. ContinuousTagRequirements
            Log("【8】ContinuousTagRequirements - 持续标签需求");
            Output.Bullet("用于检查持续性效果的需求");
            Output.Bullet("支持最小/最大叠加层数");
            Output.Bullet("支持时间相关的需求");
            Log("");
            Log("  var continuousReq = new ContinuousTagRequirements();");
            Log("  continuousReq.MinStacks = 2;");
            Log("  continuousReq.MaxStacks = 5;");
            Log("  continuousReq.RequiredDuration = 2.0f;");
            Log("");

            // 9. 典型使用场景
            Log("【9】典型使用场景");
            Output.Bullet("技能标签 - Ability.Fire, Ability.Ice, Ability.Heal");
            Output.Bullet("状态标签 - State.Stunned, State.Silenced, State.Invincible");
            Output.Bullet("效果标签 - Effect.DOT, Effect.HOT, Effect.Shield");
            Output.Bullet("伤害类型 - Damage.Physical, Damage.Magical, Damage.True");
            Output.Bullet("目标标签 - Target.Ally, Target.Enemy, Target.Self");
            Output.Bullet("Buff 标签 - Buff.Strength, Buff.Speed, Buff.Armor");
            Log("");

            // 10. 技能系统集成
            Log("【10】技能系统集成示例");
            Log("");
            Log("  // 定义技能标签");
            Log("  const string FireMagic = \"Ability.Damage.Fire\";");
            Log("  const string AoE = \"Target.AoE\";");
            Log("  const string Channeling = \"Skill.Channeling\";");
            Log("");
            Log("  // 检查技能标签");
            Log("  if (skillTags.HasTag(FireMagic))");
            Log("  {");
            Log("      // 应用火焰抗性检查");
            Log("  }");
            Log("");
            Log("  // 检查需求");
            Log("  var requirements = new GameplayTagRequirements();");
            Log("  requirements.RequireTags.Add(\"State.NotSilenced\");");
            Log("  requirements.RequireTags.Add(\"State.NotStunned\");");
            Log("  ");
            Log("  if (!requirements.Check(casterTags))");
            Log("  {");
            Log("      // 无法施法");
            Log("  }");
            Log("");

            // 11. 序列化
            Log("【11】序列化支持");
            Output.Bullet("支持 JSON 序列化");
            Output.Bullet("支持网络二进制序列化");
            Output.Bullet("支持 Editor 配置");
            Log("");
            Log("  // JSON 序列化");
            Log("  var json = JsonUtility.ToJson(container);");
            Log("  var restored = JsonUtility.FromJson<GameplayTagContainer>(json);");
            Log("");
            Log("  // 二进制序列化");
            Log("  var writer = new FastBufferWriter();");
            Log("  container.NetSerialize(writer);");
            Log("");

            // 12. API 参考
            Log("【12】关键 API 参考");
            Output.Bullet("AbilityKit.GameplayTags.Core.GameplayTag");
            Output.Bullet("AbilityKit.GameplayTags.Core.GameplayTagContainer");
            Output.Bullet("AbilityKit.GameplayTags.Core.GameplayTagManager");
            Output.Bullet("AbilityKit.GameplayTags.Core.GameplayTagRequirements");
            Output.Bullet("AbilityKit.GameplayTags.Core.ContinuousTagRequirements");
            Output.Bullet("AbilityKit.GameplayTags.Core.GameplayTagStack");
            Log("");

            Output.Divider();
            Log("【总结】GameplayTag 系统提供强大的标签管理能力，是实现技能、状态、效果系统的基石");
        }
    }
}
