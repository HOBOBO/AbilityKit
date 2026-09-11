#nullable enable

using System;
using System.Linq;
using AbilityKit.GameplayTags;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Config;

namespace AbilityKit.Samples.Logic.Samples.Tags
{
    /// <summary>
    /// GameplayTags 概述 - 演示 GameplayTag 核心用法
    /// </summary>
    [Sample]
    public sealed class GameplayTagsOverview : SampleBase
    {
        public override string Title => "Gameplay Tags Overview";
        public override string Description => "GameplayTag 核心概念与配置驱动使用";
        public override SampleCategory Category => SampleCategory.Tags;

        private static GameplayTag Tag(string name) => GameplayTagManager.Instance.RequestTag(name);

        protected override void OnRun()
        {
            Log("=== GameplayTags 概述 ===");
            Output.Divider();

            // 1. 从配置加载并注册标签
            Log("【1】从配置加载标签组");
            var groups = SampleConfig.LoadTagGroups();
            var tags = SampleConfig.LoadAndRegisterTags();

            Log($"已注册 {tags.Count} 个标签");
            Log($"共 {groups.Count} 个标签组");
            KeyValue("RegisteredTagCount", tags.Count.ToString());
            KeyValue("TagGroupCount", groups.Count.ToString());
            Log("");

            // 2. 演示标签层级结构
            Log("【2】标签层级结构");
            Output.Bullet("标签格式: Parent.Child.GrandChild (点分隔)");
            Output.Bullet("支持父子匹配: Damage.Fire 会匹配 Damage");
            Output.Bullet("支持网络同步: NetIndex 用于高效网络传输");
            Log("");

            Log("示例标签:");
            foreach (var group in groups.Take(3))
            {
                Output.Bullet($"[{group.Name}] {string.Join(", ", group.Tags.Take(3))}...");
            }
            Log("");

            // 3. 演示 GameplayTagContainer
            Log("【3】GameplayTagContainer 容器操作");
            var container = new GameplayTagContainer();

            var tag1 = Tag("Damage.Fire");
            var tag2 = Tag("Debuff.Stun");
            var tag3 = Tag("Buff.AttackSpeed");

            container.Add(tag1);
            container.Add(tag2);

            Log($"添加标签: {tag1.TagName}, {tag2.TagName}");
            Log($"容器当前标签数: {container.Count}");
            Log($"HasTag({tag1.TagName}): {container.HasTag(tag1)}");
            Log($"HasTag(Damage): {container.HasTag(Tag("Damage"))}");
            KeyValue("Container.Count", container.Count.ToString());
            KeyValue("Container.Has.Damage", container.HasTag(Tag("Damage")).ToString());
            Log("");

            // 4. 演示层级匹配
            Log("【4】层级匹配演示");
            var fireTag = Tag("Damage.Fire.Burning");
            container.Add(fireTag);

            Log($"添加 {fireTag.TagName} 到容器");
            Log($"HasTag(Damage.Fire): {container.HasTag(Tag("Damage.Fire"))}");
            Log($"HasTagExact(Damage.Fire): {container.HasTagExact(Tag("Damage.Fire"))}");
            KeyValue("Hierarchy.Has.DamageFire", container.HasTag(Tag("Damage.Fire")).ToString());
            KeyValue("Hierarchy.HasExact.DamageFire", container.HasTagExact(Tag("Damage.Fire")).ToString());
            Log("");

            // 5. 容器操作
            Log("【5】容器操作");
            var other = new GameplayTagContainer(tag3);
            var combined = container + other;

            Log($"原始容器 + Buff.AttackSpeed = {combined.Count} 个标签");
            KeyValue("Combined.Count", combined.Count.ToString());

            var removed = combined - tag2;
            Log($"移除 Debuff.Stun 后 = {removed.Count} 个标签");
            KeyValue("Removed.Count", removed.Count.ToString());
            Log("");

            // 6. 快捷 API
            Log("【6】快捷 API");
            Output.Bullet("GameplayTagManager.Instance.RequestTag(\"TagName\")  // 注册并获取标签");
            Output.Bullet("tag.Matches(other)                                 // 层级匹配");
            Output.Bullet("tag.IsChildOf(parent)                              // 是否为子标签");
            Output.Bullet("container.HasTag(tag)                              // 检查标签（支持层级）");
            Output.Bullet("container.HasTagExact(tag)                         // 精确检查");

            var quickTag = Tag("Unit.Hero");
            Log($"RequestTag(\"Unit.Hero\") = {quickTag.TagName}");

            Output.Divider();
            Log("完整 API 文档: AbilityKit.GameplayTags.Core");
        }
    }
}
