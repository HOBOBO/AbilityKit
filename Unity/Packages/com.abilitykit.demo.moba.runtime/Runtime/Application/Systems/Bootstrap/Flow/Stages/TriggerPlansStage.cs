using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering.Json;
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
                var jsonLoader = new UnityResourcesTextLoader(textAssetLoader);
                return new PlanTextLoaderAdapter(jsonLoader);
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

            var db = new TriggerPlanJsonDatabase();
            IMobaPresentationCueResolver cueResolver = null;
            try
            {
                cueResolver = resolver.Resolve<IMobaPresentationCueResolver>();
            }
            catch
            {
                cueResolver = new MobaPresentationCueResolver();
            }

            db.CueFactory = new MobaPresentationCueFactory(resolver.Resolve<MobaPresentationCueSnapshotService>(), cueResolver);
            var textAssetLoader = resolver.Resolve<ITextAssetLoader>();
            var fsAdapter = new EtFileSystemAdapter(textAssetLoader);
            var directoryLoader = new TriggerPlanDirectoryLoader(fsAdapter);
            var profile = resolver.Resolve<MobaTriggerPlanLoadProfile>() ?? MobaTriggerPlanLoadProfile.Default;

            LoadEntries(db, fsAdapter, directoryLoader, profile.Entries, db.CueFactory, profile.FailFastOnDirectoryLoad);
            db.InitializeBlackboards(resolver.Resolve<IBlackboardResolver>(), replaceExisting: true);
            db.ConfigureOwnerBlackboards(resolver.Resolve<IOwnerBlackboardStore>(), releaseExisting: true);
            return db;
        }

        private static void LoadEntries(TriggerPlanJsonDatabase db, EtFileSystemAdapter fsAdapter, TriggerPlanDirectoryLoader directoryLoader, TriggerPlanLoadEntry[] entries, TriggerPlanJsonDatabase.ICueFactory cueFactory, bool failFastOnDirectoryLoad)
        {
            if (entries == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrEmpty(entry.Path)) continue;

                if (entry.IsDirectory)
                {
                    LoadDirectory(db, directoryLoader, entry, cueFactory, failFastOnDirectoryLoad);
                }
                else
                {
                    LoadFile(db, fsAdapter, entry, cueFactory);
                }
            }
        }

        private static void LoadFile(TriggerPlanJsonDatabase db, EtFileSystemAdapter fsAdapter, TriggerPlanLoadEntry entry, TriggerPlanJsonDatabase.ICueFactory cueFactory)
        {
            MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanDatabaseFactory), $"Loading {entry.Name} from {entry.Path}");
            try
            {
                var loadedDb = new TriggerPlanJsonDatabase { CueFactory = cueFactory };
                loadedDb.Load(fsAdapter, entry.Path);
                db.MergeFrom(loadedDb, replaceExisting: true);
                MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanDatabaseFactory), $"{entry.Name} merged. total records={db.Records?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(ex, MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Exception, nameof(MobaTriggerPlanDatabaseFactory), $"Failed to load required trigger plan file. name={entry.Name}, path={entry.Path}");
                throw;
            }
        }

        private static void LoadDirectory(TriggerPlanJsonDatabase db, TriggerPlanDirectoryLoader directoryLoader, TriggerPlanLoadEntry entry, TriggerPlanJsonDatabase.ICueFactory cueFactory, bool failFastOnDirectoryLoad)
        {
            var pattern = string.IsNullOrEmpty(entry.Pattern) ? "**/*.json" : entry.Pattern;
            MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanDatabaseFactory), $"Loading {entry.Name} from {entry.Path} directory");
            try
            {
                var options = new TriggerPlanDirectoryLoadOptions
                {
                    CueFactory = cueFactory,
                    ThrowOnFileParseError = failFastOnDirectoryLoad
                };
                var loadedDb = directoryLoader.LoadDirectory(entry.Path, pattern, options);
                if (loadedDb != null && loadedDb.Records != null)
                {
                    // 目录中的触发器文件是当前维护的事实来源，必须覆盖陈旧的编译记录。
                    db.MergeFrom(loadedDb, replaceExisting: true);
                    MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanDatabaseFactory), $"{entry.Name} merged with override enabled. total records={db.Records?.Count ?? 0}");
                }
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(ex, MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Exception, nameof(MobaTriggerPlanDatabaseFactory), $"Failed to load required trigger plan directory. name={entry.Name}, path={entry.Path}, pattern={pattern}");
                throw;
            }
        }

        /// <summary>
        /// 用于加载触发器计划的文本资源适配器。
        /// </summary>
        private sealed class EtFileSystemAdapter :
            AbilityKit.Ability.Triggering.Json.ITextLoader,
            IFileSystemTextLoader
        {
            private readonly ITextAssetLoader _textAssetLoader;
            private readonly ITextAssetDirectoryLoader _directoryLoader;

            public EtFileSystemAdapter(ITextAssetLoader textAssetLoader)
            {
                _textAssetLoader = textAssetLoader ?? throw new ArgumentNullException(nameof(textAssetLoader));
                _directoryLoader = textAssetLoader as ITextAssetDirectoryLoader;
            }

            bool AbilityKit.Ability.Triggering.Json.ITextLoader.TryLoad(string id, out string text)
            {
                text = null;
                if (string.IsNullOrEmpty(id)) return false;
                return _textAssetLoader.TryLoadText(id, out text);
            }

            bool AbilityKit.Triggering.Runtime.Plan.Json.TriggerPlanJsonDatabase.ITextLoader.TryLoad(string id, out string text)
            {
                return ((AbilityKit.Ability.Triggering.Json.ITextLoader)this).TryLoad(id, out text);
            }

            public IEnumerable<string> GetFiles(string directory, string pattern)
            {
                if (_directoryLoader == null) return Enumerable.Empty<string>();
                return _directoryLoader.GetTextAssetPaths(directory, pattern) ?? Enumerable.Empty<string>();
            }
        }

    }
}
