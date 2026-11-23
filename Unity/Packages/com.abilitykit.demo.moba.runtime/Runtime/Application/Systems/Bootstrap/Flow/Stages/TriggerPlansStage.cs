using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Demo.Moba.Systems.Bootstrap;
using AbilityKit.Demo.Moba.Systems.Bootstrap.Flow;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    /// <summary>
    /// 触发器计划阶段
    /// 注册触发器计划相关的服务
    /// </summary>
    [MobaBootstrapStage]
    public sealed class TriggerPlansStage : MobaBootstrapStageBase
    {
        public override string Name => MobaBootstrapStageNames.TriggerPlans;

        public override string[] Dependencies => new[]
        {
            MobaBootstrapStageNames.WorldModules,
        };

        protected internal override void Configure(WorldContainerBuilder builder)
        {
            // 注意：ITextAssetLoader 已经通过 ResourcesTextAssetLoader 的 WorldServiceAttribute 自动注册

            // 注册 TriggerPlanJsonDatabase.ITextLoader
            builder.TryRegister<TriggerPlanJsonDatabase.ITextLoader>(WorldLifetime.Singleton, r =>
            {
                var textAssetLoader = r.Resolve<ITextAssetLoader>();
                return new UnityResourcesTextLoader(textAssetLoader);
            });

            builder.TryRegister<MobaTriggerPlanLoadProfile>(WorldLifetime.Singleton, _ => MobaTriggerPlanLoadProfile.Default);
            builder.TryRegister<IMobaTriggerPlanDatabaseFactory>(WorldLifetime.Singleton, _ => new MobaTriggerPlanDatabaseFactory());

            builder.TryRegister<TriggerPlanJsonDatabase>(WorldLifetime.Scoped, r =>
            {
                var factory = r.Resolve<IMobaTriggerPlanDatabaseFactory>() ?? new MobaTriggerPlanDatabaseFactory();
                return factory.Create(r);
            });
        }
    }

    public interface IMobaTriggerPlanDatabaseFactory
    {
        TriggerPlanJsonDatabase Create(IWorldResolver resolver);
    }

    public sealed class MobaTriggerPlanDatabaseFactory : IMobaTriggerPlanDatabaseFactory
    {
        public TriggerPlanJsonDatabase Create(IWorldResolver resolver)
        {
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            IMobaPresentationCueResolver cueResolver = null;
            try
            {
                cueResolver = resolver.Resolve<IMobaPresentationCueResolver>();
            }
            catch
            {
                cueResolver = new MobaPresentationCueResolver();
            }

            var cueFactory = new MobaPresentationCueFactory(resolver.Resolve<MobaPresentationCueSnapshotService>(), cueResolver);
            var textAssetLoader = resolver.Resolve<ITextAssetLoader>();
            var profile = resolver.Resolve<MobaTriggerPlanLoadProfile>() ?? MobaTriggerPlanLoadProfile.Default;

            var db = MobaTriggerPlanLoader.Load(new EtFileSystemAdapter(textAssetLoader), profile, cueFactory);
            db.InitializeBlackboards(resolver.Resolve<IBlackboardResolver>(), replaceExisting: true);
            db.ConfigureOwnerBlackboards(resolver.Resolve<IOwnerBlackboardStore>(), releaseExisting: true);
            return db;
        }

        /// <summary>
        /// 用于加载触发器计划的文本资源适配器。
        /// </summary>
        private sealed class EtFileSystemAdapter :
            IFileSystemTextLoader
        {
            private readonly ITextAssetLoader _textAssetLoader;
            private readonly ITextAssetDirectoryLoader _directoryLoader;

            public EtFileSystemAdapter(ITextAssetLoader textAssetLoader)
            {
                _textAssetLoader = textAssetLoader ?? throw new ArgumentNullException(nameof(textAssetLoader));
                _directoryLoader = textAssetLoader as ITextAssetDirectoryLoader;
            }

            public bool TryLoad(string id, out string text)
            {
                text = null;
                if (string.IsNullOrEmpty(id)) return false;
                return _textAssetLoader.TryLoadText(id, out text);
            }

            public IEnumerable<string> GetFiles(string directory, string pattern)
            {
                if (_directoryLoader == null) return Enumerable.Empty<string>();
                return _directoryLoader.GetTextAssetPaths(directory, pattern) ?? Enumerable.Empty<string>();
            }
        }

    }
}
