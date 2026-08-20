using System;
using System.Collections.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Ability.Config;
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
        string Kind { get; }

        bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision);
    }

    /// <summary>
    /// Optional driver-owned validation for algorithm-specific resources and definitions.
    /// Adding a new driver does not require a central validation branch.
    /// </summary>
    public interface IMobaBrainDecisionDriverValidator
    {
        void ValidateDefinition(
            in MobaActorBrainDefinition definition,
            ICollection<string> errors);
    }

    /// <summary>
    /// 决策驱动器注册表。每个驱动类型只保留一个确定实现，避免服务内出现类型分支。
    /// </summary>
    public sealed class MobaBrainDecisionDriverRegistry
    {
        private readonly Dictionary<string, IMobaBrainDecisionDriver> _drivers =
            new(StringComparer.Ordinal);

        public MobaBrainDecisionDriverRegistry(IEnumerable<IMobaBrainDecisionDriver> drivers = null)
        {
            if (drivers == null) return;

            foreach (var driver in drivers)
            {
                Register(driver);
            }
        }

        public static MobaBrainDecisionDriverRegistry CreateDefault(ITextAssetLoader textAssetLoader = null)
        {
            return new MobaBrainDecisionDriverRegistry(new IMobaBrainDecisionDriver[]
            {
                new MobaBTreeBrainDecisionDriver(textAssetLoader),
            });
        }

        public void Register(IMobaBrainDecisionDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            var kind = driver.Kind;
            if (string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException("A brain driver key is required.", nameof(driver));
            _drivers[kind] = driver;
        }

        public bool Contains(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind) && _drivers.ContainsKey(kind);
        }

        public bool TryGetDriver(string kind, out IMobaBrainDecisionDriver driver)
        {
            driver = null;
            return !string.IsNullOrWhiteSpace(kind) && _drivers.TryGetValue(kind, out driver);
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
    public sealed class MobaBTreeBrainDecisionDriver :
        IMobaBrainDecisionDriver,
        IMobaBrainDecisionDriverValidator
    {
        private readonly ITextAssetLoader _textAssetLoader;

        public MobaBTreeBrainDecisionDriver(ITextAssetLoader textAssetLoader = null)
        {
            _textAssetLoader = textAssetLoader;
        }

        public string Kind => MobaBrainDriverKeys.BehaviorTree;

        public void ValidateDefinition(
            in MobaActorBrainDefinition definition,
            ICollection<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (!MobaBTreeAssetLoader.TryLoad(_textAssetLoader, definition.DecisionName, out var json))
            {
                errors.Add(
                    $"Brain '{definition.BrainId}' references missing BTree resource '{definition.DecisionName}'.");
                return;
            }

            try
            {
                MobaBTreeDecision.ValidateConfiguration(json);
            }
            catch (Exception ex)
            {
                errors.Add(
                    $"Brain '{definition.BrainId}' BTree '{definition.DecisionName}' is invalid: {ex.Message}");
            }
        }

        public bool TryCreate(in MobaBrainDecisionCreateContext context, out IBehaviorDecision decision)
        {
            decision = null;
            var treeName = context.Definition.DecisionName;
            if (string.IsNullOrWhiteSpace(treeName)
                || !MobaBTreeAssetLoader.TryLoad(_textAssetLoader, treeName, out var json))
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
    public sealed class MobaHfsmBrainDecisionDriver :
        IMobaBrainDecisionDriver,
        IMobaBrainDecisionDriverValidator
    {
        private readonly Dictionary<string, MobaHfsmDecisionFactory> _factories = new(StringComparer.Ordinal);

        public string Kind => MobaBrainDriverKeys.Hfsm;

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

        public void ValidateDefinition(
            in MobaActorBrainDefinition definition,
            ICollection<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (!_factories.ContainsKey(definition.DecisionName))
            {
                errors.Add(
                    $"Brain '{definition.BrainId}' references missing HFSM decision '{definition.DecisionName}'.");
            }
        }
    }

}
