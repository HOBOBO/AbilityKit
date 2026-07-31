using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services.Behavior.BTree;
using AbilityKit.Demo.Moba.Services.Search;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    /// <summary>
    /// 创建 Actor Brain 决策时提供给驱动器的稳定上下文。
    /// 决策实例可以持有自己的运行状态，组件仅保存运行时绑定信息。
    /// </summary>
    public readonly struct MobaBrainDecisionCreateContext
    {
        public readonly MobaActorBrainDefinition Definition;
        public readonly MobaActorRegistry Registry;
        public readonly MobaConfigDatabase Config;
        public readonly long OwnerActorId;
        public readonly int SourceKind;
        public readonly int SourceId;
        public readonly SearchTargetService SearchTargets;
        public readonly Func<long> CurrentTimeMsProvider;

        public MobaBrainDecisionCreateContext(
            in MobaActorBrainDefinition definition,
            MobaActorRegistry registry,
            MobaConfigDatabase config,
            long ownerActorId,
            int sourceKind,
            int sourceId,
            SearchTargetService searchTargets = null,
            Func<long> currentTimeMsProvider = null)
        {
            Definition = definition;
            Registry = registry;
            Config = config;
            OwnerActorId = ownerActorId;
            SourceKind = sourceKind;
            SourceId = sourceId;
            SearchTargets = searchTargets;
            CurrentTimeMsProvider = currentTimeMsProvider;
        }
    }

    /// <summary>
    /// Brain 决策驱动器。新决策框架只需实现此接口并注册，无需修改 Brain 服务生命周期。
    /// </summary>
    public interface IMobaBrainDecisionDriver
    {
        MobaBrainDriverKind Kind { get; }

        bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision);
    }

    /// <summary>
    /// 决策驱动器注册表。每个驱动类型只保留一个确定实现，避免服务内出现类型分支。
    /// </summary>
    public sealed class MobaBrainDecisionDriverRegistry
    {
        private readonly Dictionary<MobaBrainDriverKind, IMobaBrainDecisionDriver> _drivers = new();

        public MobaBrainDecisionDriverRegistry(IEnumerable<IMobaBrainDecisionDriver> drivers = null)
        {
            if (drivers == null) return;

            foreach (var driver in drivers)
            {
                Register(driver);
            }
        }

        public static MobaBrainDecisionDriverRegistry CreateDefault()
        {
            return new MobaBrainDecisionDriverRegistry(new IMobaBrainDecisionDriver[]
            {
                new MobaBTreeBrainDecisionDriver(),
            });
        }

        public void Register(IMobaBrainDecisionDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            _drivers[driver.Kind] = driver;
        }

        public bool Contains(MobaBrainDriverKind kind)
        {
            return _drivers.ContainsKey(kind);
        }

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = null;
            return _drivers.TryGetValue(context.Definition.DriverKind, out var driver)
                && driver.TryCreate(in context, out decision)
                && decision != null;
        }
    }

    /// <summary>
    /// BTCore 决策驱动器。定义键对应导出的行为树资源名。
    /// </summary>
    public sealed class MobaBTreeBrainDecisionDriver : IMobaBrainDecisionDriver
    {
        public MobaBrainDriverKind Kind => MobaBrainDriverKind.BTree;

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = null;
            var treeName = context.Definition.DecisionName;
            if (string.IsNullOrWhiteSpace(treeName)
                || !MobaBTreeAssetLoader.TryLoad(treeName, out var json))
            {
                return false;
            }

            decision = MobaBTreeDecision.Create(
                json,
                context.Registry,
                context.Config,
                context.SearchTargets,
                context.CurrentTimeMsProvider,
                context.Definition.SkillSelectionPolicy);
            if (decision == null)
            {
                Log.Warning($"[MobaBrain] behavior tree create failed. brainId={context.Definition.BrainId} tree={treeName}");
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 由外部状态机图或业务状态机注册的决策创建函数。
    /// 返回的决策实例应独占其状态，不能在多个 Actor 之间共享。
    /// </summary>
    public delegate IBehaviorDecision MobaHfsmDecisionFactory(in MobaBrainDecisionCreateContext context);

    /// <summary>
    /// HFSM 驱动器。通过定义键选择已注册的状态机工厂，使状态机实现不依赖 Brain 服务。
    /// </summary>
    public sealed class MobaHfsmBrainDecisionDriver : IMobaBrainDecisionDriver
    {
        private readonly Dictionary<string, MobaHfsmDecisionFactory> _factories = new(StringComparer.Ordinal);

        public MobaBrainDriverKind Kind => MobaBrainDriverKind.Hfsm;

        public void Register(string definitionName, MobaHfsmDecisionFactory factory)
        {
            if (string.IsNullOrWhiteSpace(definitionName))
                throw new ArgumentException("A state-machine definition name is required.", nameof(definitionName));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _factories[definitionName] = factory;
        }

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = null;
            return !string.IsNullOrWhiteSpace(context.Definition.DecisionName)
                && _factories.TryGetValue(context.Definition.DecisionName, out var factory)
                && (decision = factory(in context)) != null;
        }
    }

}
