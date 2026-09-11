#nullable enable

using AbilityKit.Samples.Abstractions;
using AbilityKit.Samples.Logic.Infrastructure.Config;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Samples.Config
{
    /// <summary>
    /// 配置类型注册系统演示
    /// 展示如何使用 MarkerAttribute 机制进行类型注册和查找
    /// </summary>
    [Sample]
    public sealed class ConfigRegistryBasics : SampleBase
    {
        public override string Title => "配置类型注册系统";
        public override string Description => "展示如何使用 MarkerAttribute 机制进行类型注册和查找";
        public override SampleCategory Category => SampleCategory.Foundation;

        protected override void OnRun()
        {
            Log("=== 配置类型注册系统演示 ===\n");

            // 演示各种 Registry 的使用
            DemonstratePipelinePhaseRegistry();
            DemonstrateStateTypeRegistry();
            DemonstrateCharacterRegistry();
            DemonstrateCharacterTagRegistry();
            DemonstrateBTRegistry();

            Log("\n=== 演示完成 ===");
        }

        private void DemonstratePipelinePhaseRegistry()
        {
            Log("--- Pipeline 阶段类型注册 ---");

            var registry = PipelinePhaseRegistry.Instance;
            Log($"已注册的 Pipeline 阶段数量: {registry.Count}");

            // 通过名称获取类型
            if (registry.TryGet("Casting", out var castingType))
            {
                Log($"找到 'Casting' 类型: {castingType.Name}");
                var phase = registry.CreatePhase("Casting");
                Log($"创建实例: {phase.GetType().Name}");
            }

            // 遍历所有注册的阶段
            Log("所有已注册的 Pipeline 阶段:");
            registry.ForEach((name, type) =>
            {
                Log($"  - {name} -> {type.Name}");
            });

            Log("");
        }

        private void DemonstrateStateTypeRegistry()
        {
            Log("--- 状态机状态类型注册 ---");

            var registry = StateTypeRegistry.Instance;
            Log($"已注册的状态数量: {registry.Count}");

            // 创建状态实例
            if (registry.TryCreateState("Idle", out var idleState))
            {
                Log($"创建 'Idle' 状态成功: {idleState}");
            }

            Log("");
        }

        private void DemonstrateCharacterRegistry()
        {
            Log("--- 角色类型注册 ---");

            var registry = CharacterTypeRegistry.Instance;
            Log($"已注册的角色类型数量: {registry.Count}");

            // 创建角色
            if (registry.TryGet("hero", out var heroType))
            {
                Log($"找到 'hero' 类型: {heroType.Name}");
                var hero = registry.GetOrCreateInstance("hero");
                Log($"创建 hero 实例: {hero}");
            }

            Log("");
        }

        private void DemonstrateCharacterTagRegistry()
        {
            Log("--- 角色标签注册 ---");

            var registry = CharacterTagRegistry.Instance;
            Log($"具有标签的角色类型数量: {registry.Count}");
            Log($"所有已注册的标签: {string.Join(", ", registry.AllTags)}");

            // 通过标签查找角色
            Log("\n查找所有 'Enemy' 标签的角色:");
            foreach (var type in registry.GetTypesByTag("Enemy"))
            {
                Log($"  - {type.Name}");
            }

            Log("\n查找所有 'Tower' 标签的角色:");
            foreach (var type in registry.GetTypesByTag("Tower"))
            {
                Log($"  - {type.Name}");
            }

            Log("");
        }

        private void DemonstrateBTRegistry()
        {
            Log("--- 行为树节点类型注册 ---");

            var nodeRegistry = BTNodeTypeRegistry.Instance;
            var actionRegistry = BTActionTypeRegistry.Instance;
            var conditionRegistry = BTConditionTypeRegistry.Instance;

            Log($"已注册的 BT 节点类型: {nodeRegistry.Count}");
            Log($"已注册的 BT 动作类型: {actionRegistry.Count}");
            Log($"已注册的 BT 条件类型: {conditionRegistry.Count}");

            // 创建行为树节点
            var selector = nodeRegistry.CreateNode("Selector");
            var sequence = nodeRegistry.CreateNode("Sequence");

            Log($"\n创建的节点: {selector.GetType().Name}, {sequence.GetType().Name}");

            // 创建行为树动作
            var moveTo = actionRegistry.CreateAction("MoveTo");
            var lookAt = actionRegistry.CreateAction("LookAt");

            Log($"创建的动作: {moveTo.GetType().Name}, {lookAt.GetType().Name}");

            Log("");
        }
    }
}
