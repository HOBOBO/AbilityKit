using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow
{
    public static class MobaBootstrapStageNames
    {
        public const string CoreState = "CoreState";
        public const string Config = "Config";
        public const string WorldModules = "WorldModules";
        public const string TriggerPlans = "TriggerPlans";
        public const string TargetingAndSkills = "TargetingAndSkills";
        public const string WorldInit = "Install.WorldInit";
        public const string MapRuntime = "Install.MapRuntime";
        public const string StartGame = "StartGame";
        public const string PlanTriggering = "Install.PlanTriggering";
    }

    /// <summary>
    /// Bootstrap Stage 基类
    /// 所有引导阶段继承此类，实现具体配置逻辑
    /// </summary>
    public abstract class MobaBootstrapStageBase
    {
        /// <summary>
        /// Stage 名称
        /// </summary>
        public virtual string Name => GetType().Name;

        /// <summary>
        /// 依赖的其他 Stage 名称
        /// </summary>
        public virtual string[] Dependencies => Array.Empty<string>();

        /// <summary>
        /// 配置阶段 - 添加服务到容器
        /// </summary>
        /// <param name="builder">世界容器构建器</param>
        protected internal virtual void Configure(WorldContainerBuilder builder)
        {
        }

        /// <summary>
        /// 安装阶段 - 安装系统
        /// </summary>
        /// <param name="contexts">Entitas 上下文</param>
        /// <param name="systems">Entitas 系统</param>
        /// <param name="services">世界解析器</param>
        protected internal virtual void Install(
            Entitas.IContexts contexts,
            Entitas.Systems systems,
            IWorldResolver services)
        {
        }

        /// <summary>
        /// 执行配置阶段
        /// </summary>
        protected internal void ExecuteConfigure(WorldContainerBuilder builder)
        {
            try
            {
                Configure(builder);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaBootstrap] Configure stage failed: {Name}");
                throw;
            }
        }

        /// <summary>
        /// 执行安装阶段
        /// </summary>
        protected internal void ExecuteInstall(
            Entitas.IContexts contexts,
            Entitas.Systems systems,
            IWorldResolver services)
        {
            try
            {
                Install(contexts, systems, services);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[MobaBootstrap] Install stage failed: {Name}");
                throw;
            }
        }
    }

    public sealed class MobaBootstrapStagePlan
    {
        private readonly IReadOnlyList<MobaBootstrapStageBase> _orderedStages;

        private MobaBootstrapStagePlan(IReadOnlyList<MobaBootstrapStageBase> orderedStages)
        {
            _orderedStages = orderedStages;
        }

        public IReadOnlyList<MobaBootstrapStageBase> OrderedStages => _orderedStages;

        public static MobaBootstrapStagePlan Create(IEnumerable<MobaBootstrapStageBase> stages)
        {
            if (stages == null) throw new ArgumentNullException(nameof(stages));

            var stageList = new List<MobaBootstrapStageBase>();
            var definitions = new List<BattleStageDefinition>();
            var byName = new Dictionary<string, MobaBootstrapStageBase>(StringComparer.Ordinal);
            foreach (var stage in stages)
            {
                if (stage == null)
                {
                    throw new InvalidOperationException($"MOBA bootstrap stage at index {stageList.Count} is null.");
                }

                var dependencies = stage.Dependencies ?? Array.Empty<string>();
                stageList.Add(stage);
                definitions.Add(new BattleStageDefinition(stage.Name, prerequisites: dependencies));
                if (!byName.TryAdd(stage.Name, stage))
                {
                    throw new InvalidOperationException($"Duplicate MOBA bootstrap stage name '{stage.Name}'.");
                }
            }

            var graph = BattleStageGraph.Create(definitions);
            var ordered = new MobaBootstrapStageBase[graph.OrderedStages.Count];
            for (int i = 0; i < graph.OrderedStages.Count; i++)
            {
                ordered[i] = byName[graph.OrderedStages[i].Id];
            }

            return new MobaBootstrapStagePlan(Array.AsReadOnly(ordered));
        }
    }

    /// <summary>
    /// Stage 注册表
    /// 管理所有 Bootstrap Stage
    /// </summary>
    public static class MobaBootstrapStageRegistry
    {
        private static readonly List<MobaBootstrapStageBase> _stages = new();
        private static readonly object Sync = new object();
        private static MobaBootstrapStagePlan _plan;
        private static readonly string[] RequiredStages =
        {
            MobaBootstrapStageNames.CoreState,
            MobaBootstrapStageNames.Config,
            MobaBootstrapStageNames.WorldModules,
            MobaBootstrapStageNames.TriggerPlans,
            MobaBootstrapStageNames.TargetingAndSkills,
            MobaBootstrapStageNames.WorldInit,
            MobaBootstrapStageNames.MapRuntime,
            MobaBootstrapStageNames.StartGame,
            MobaBootstrapStageNames.PlanTriggering,
            "Tags"
        };
        /// <summary>
        /// 注册 Stage
        /// </summary>
        public static void Register(MobaBootstrapStageBase stage)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));

            var name = stage.Name;
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("MOBA bootstrap stage has no name.");
            }

            lock (Sync)
            {
                if (_plan != null)
                    throw new InvalidOperationException("MOBA bootstrap stage registry is frozen.");
                _stages.Add(stage);
            }
        }

        internal static void RollbackPendingTo(int count)
        {
            lock (Sync)
            {
                if (_plan != null) return;
                if (count < 0 || count > _stages.Count)
                    throw new InvalidOperationException("Cannot roll back frozen MOBA bootstrap stages.");
                _stages.RemoveRange(count, _stages.Count - count);
            }
        }

        public static MobaBootstrapStagePlan Freeze()
        {
            lock (Sync)
            {
                if (_plan != null) return _plan;
                var plan = MobaBootstrapStagePlan.Create(_stages);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var stage in plan.OrderedStages) names.Add(stage.Name);
                foreach (var required in RequiredStages)
                    if (!names.Contains(required))
                        throw new InvalidOperationException("Required MOBA bootstrap stage is missing: " + required);
                _plan = plan;
                return _plan;
            }
        }

        /// <summary>
        /// 获取所有 Stage
        /// </summary>
        public static IEnumerable<MobaBootstrapStageBase> GetAllStages()
        {
            lock (Sync) return _stages.ToArray();
        }

        /// <summary>
        /// 获取配置阶段的 Stage
        /// </summary>
        public static IEnumerable<MobaBootstrapStageBase> GetConfigureStages()
        {
            return Freeze().OrderedStages;
        }

        /// <summary>
        /// 获取安装阶段的 Stage
        /// </summary>
        public static IEnumerable<MobaBootstrapStageBase> GetInstallStages()
        {
            return Freeze().OrderedStages;
        }

        /// <summary>
        /// 获取 Stage 数量
        /// </summary>
        public static int Count
        {
            get { lock (Sync) return _stages.Count; }
        }
    }

    /// <summary>
    /// Stage 自动注册特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MobaBootstrapStageAttribute : Attribute
    {
    }
}
