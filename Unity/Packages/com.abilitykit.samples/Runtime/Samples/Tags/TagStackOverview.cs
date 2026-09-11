#nullable enable

using AbilityKit.GameplayTags;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Config;

namespace AbilityKit.Samples.Logic.Samples.Tags
{
    /// <summary>
    /// Tag Stack 概述 - 标签栈与叠加系统
    /// </summary>
    [Sample]
    public sealed class TagStackOverview : SampleBase
    {
        public override string Title => "Tag Stack Overview";
        public override string Description => "GameplayTagStack 标签叠加系统";
        public override SampleCategory Category => SampleCategory.Tags;

        protected override void OnRun()
        {
            Section("Tag Stack 概述");
            Info("标签栈系统，支持标签叠加计数");
            Line();

            // 确保标签已注册
            SampleConfig.LoadAndRegisterTags();
            var manager = GameplayTagManager.Instance;

            // 1. 基本概念
            Section("1. 基本概念");
            Bullet("GameplayTagStack: 单个标签 + 计数");
            Bullet("GameplayTagStackContainer: 多个标签栈的容器");
            Bullet("应用场景: Buff 叠加、Dot 伤害层数");
            Line();

            // 2. 单个标签栈
            Section("2. GameplayTagStack 用法");
            ShowSingleStack(manager);
            Line();

            // 3. 标签栈容器
            Section("3. GameplayTagStackContainer 用法");
            ShowStackContainer(manager);
            Line();

            // 4. 典型应用场景
            Section("4. 典型应用场景");
            ShowRealWorldScenarios(manager);
            Line();

            // 5. API 总结
            Section("5. API 总结");
            Bullet("container.GetStackCount(tag)   // 获取层数");
            Bullet("container.AddStack(tag, count)  // 添加层数");
            Bullet("container.RemoveStack(tag, count) // 移除层数");
            Bullet("container.SetStackCount(tag, count) // 设置层数");
            Bullet("container.HasTag(tag)          // 是否有该标签（层数>0）");
            Bullet("container.ToContainer()        // 转为标签容器");
        }

        private void ShowSingleStack(GameplayTagManager manager)
        {
            var poisonTag = manager.RequestTag("Debuff.Poison");
            var stack = new GameplayTagStack(poisonTag, 1);

            Info("创建标签栈:");
            KeyValue("stack.Tag", stack.Tag.TagName);
            KeyValue("stack.Count", stack.Count.ToString());
            KeyValue("stack.IsValid", stack.IsValid.ToString());
            KeyValue("stack.IsEmpty", stack.IsEmpty.ToString());
            Line();

            Info("栈操作:");
            stack.Increment(3);
            KeyValue("Increment(3)", $"Count = {stack.Count}");

            stack.Decrement(2);
            KeyValue("Decrement(2)", $"Count = {stack.Count}");

            stack.SetCount(10);
            KeyValue("SetCount(10)", $"Count = {stack.Count}");
        }

        private void ShowStackContainer(GameplayTagManager manager)
        {
            var container = new GameplayTagStackContainer();

            // 添加多个不同标签的栈
            var poisonTag = manager.RequestTag("Debuff.Poison");
            var burnTag = manager.RequestTag("Debuff.Burning");
            var attackTag = manager.RequestTag("Buff.AttackSpeed");

            container.AddStack(poisonTag, 5);
            container.AddStack(burnTag, 3);
            container.AddStack(attackTag, 2);

            Info("初始状态:");
            KeyValue("Count", container.Count.ToString());
            KeyValue("TotalCount", container.TotalCount.ToString());
            KeyValue("GetStackCount(poison)", container.GetStackCount(poisonTag).ToString());
            Line();

            Info("添加层数:");
            container.AddStack(poisonTag, 3);
            KeyValue("AddStack(poison, 3)", $"Poison层数 = {container.GetStackCount(poisonTag)}");
            Line();

            Info("移除层数:");
            container.RemoveStack(poisonTag, 4);
            KeyValue("RemoveStack(poison, 4)", $"Poison层数 = {container.GetStackCount(poisonTag)}");
            Line();

            Info("设置层数:");
            container.SetStackCount(burnTag, 1);
            KeyValue("SetStackCount(burn, 1)", $"Burn层数 = {container.GetStackCount(burnTag)}");
            Line();

            Info("HasTag 检查:");
            KeyValue("HasTag(poison)", container.HasTag(poisonTag).ToString());
            KeyValue("HasTag(unknown)", container.HasTag(manager.RequestTag("Unknown")).ToString());
            Line();

            Info("遍历所有栈:");
            foreach (var stack in container)
            {
                Bullet($"{stack.Tag.TagName}: x{stack.Count}");
            }
        }

        private void ShowRealWorldScenarios(GameplayTagManager manager)
        {
            var container = new GameplayTagStackContainer();

            Info("场景1: DOT 伤害叠加");
            var poisonTag = manager.RequestTag("Debuff.Poison");
            container.AddStack(poisonTag, 1);
            Bullet("每次_tick 添加一层 Poison");
            Bullet($"层数越多，伤害越高: Damage = BaseDamage * StackCount");
            KeyValue("当前Poison层数", container.GetStackCount(poisonTag).ToString());
            Line();

            Info("场景2: Buff 效果叠加");
            var speedTag = manager.RequestTag("Buff.MoveSpeed");
            container.AddStack(speedTag, 3);
            Bullet("攻速/移速 Buff 叠加");
            KeyValue("当前MoveSpeed层数", container.GetStackCount(speedTag).ToString());
            Bullet($"Speed = BaseSpeed * (1 + 0.1 * StackCount)");
            Line();

            Info("场景3: 叠加上限");
            container.SetStackCount(speedTag, 10);
            KeyValue("SetStackCount(speed, 10)", $"移速层数 = {container.GetStackCount(speedTag)}");
            Bullet("大多数游戏会有最大叠加层数限制");
            Line();

            Info("场景4: 转换为标签容器");
            var tagContainer = container.ToContainer();
            KeyValue("ToContainer()", $"{tagContainer.Count} 个标签");
            Bullet("用于与 GameplayTagContainer 互操作");
        }
    }
}
