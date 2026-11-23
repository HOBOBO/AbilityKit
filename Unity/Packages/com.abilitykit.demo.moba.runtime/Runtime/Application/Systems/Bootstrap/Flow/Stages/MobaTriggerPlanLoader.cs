using System;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow.Stages
{
    /// <summary>
    /// 按指定来源加载触发计划；不解析宿主服务，不绑定世界黑板。
    /// </summary>
    public static class MobaTriggerPlanLoader
    {
        /// <summary>
        /// 按配置顺序合并文件和目录，后加载的同 ID 记录覆盖前者。
        /// </summary>
        public static TriggerPlanJsonDatabase Load(
            IFileSystemTextLoader source,
            MobaTriggerPlanLoadProfile profile,
            TriggerPlanJsonDatabase.ICueFactory cueFactory)
        {
            var database = new TriggerPlanJsonDatabase { CueFactory = cueFactory };
            LoadEntries(database, source, new TriggerPlanDirectoryLoader(source), profile.Entries,
                cueFactory, profile.FailFastOnDirectoryLoad);
            return database;
        }

        /// <summary>
        /// 直接加载必需的聚合包，再按配置合并附加来源；不预先去重聚合包记录。
        /// </summary>
        public static TriggerPlanJsonDatabase LoadWithAggregate(
            IFileSystemTextLoader source,
            string aggregatePath,
            MobaTriggerPlanLoadProfile overlays,
            TriggerPlanJsonDatabase.ICueFactory cueFactory)
        {
            var database = new TriggerPlanJsonDatabase { CueFactory = cueFactory };
            database.Load(source, aggregatePath);
            LoadEntries(database, source, new TriggerPlanDirectoryLoader(source), overlays.Entries,
                cueFactory, overlays.FailFastOnDirectoryLoad);
            return database;
        }

        private static void LoadEntries(TriggerPlanJsonDatabase db, IFileSystemTextLoader source, TriggerPlanDirectoryLoader directoryLoader, TriggerPlanLoadEntry[] entries, TriggerPlanJsonDatabase.ICueFactory cueFactory, bool failFastOnDirectoryLoad)
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
                    LoadFile(db, source, entry, cueFactory);
                }
            }
        }

        private static void LoadFile(TriggerPlanJsonDatabase db, IFileSystemTextLoader source, TriggerPlanLoadEntry entry, TriggerPlanJsonDatabase.ICueFactory cueFactory)
        {
            MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanLoader), $"Loading {entry.Name} from {entry.Path}");
            try
            {
                var loadedDb = new TriggerPlanJsonDatabase { CueFactory = cueFactory };
                loadedDb.Load(source, entry.Path);
                db.MergeFrom(loadedDb, replaceExisting: true);
                MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanLoader), $"{entry.Name} merged. total records={db.Records?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(ex, MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Exception, nameof(MobaTriggerPlanLoader), $"Failed to load required trigger plan file. name={entry.Name}, path={entry.Path}");
                throw;
            }
        }

        private static void LoadDirectory(TriggerPlanJsonDatabase db, TriggerPlanDirectoryLoader directoryLoader, TriggerPlanLoadEntry entry, TriggerPlanJsonDatabase.ICueFactory cueFactory, bool failFastOnDirectoryLoad)
        {
            var pattern = string.IsNullOrEmpty(entry.Pattern) ? "**/*.json" : entry.Pattern;
            MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanLoader), $"Loading {entry.Name} from {entry.Path} directory");
            try
            {
                var options = new TriggerPlanDirectoryLoadOptions
                {
                    CueFactory = cueFactory,
                    ThrowOnFileParseError = failFastOnDirectoryLoad
                };
                var rules = directoryLoader.LoadDirectory(entry.Path, pattern, options);
                db.MergeFrom(rules, replaceExisting: true);
                MobaRuntimeLog.Info(MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Configuration, nameof(MobaTriggerPlanLoader), $"{entry.Name} merged with override enabled. total records={db.Records?.Count ?? 0}");
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(ex, MobaRuntimeLogModule.Bootstrap, MobaRuntimeLogPurpose.Exception, nameof(MobaTriggerPlanLoader), $"Failed to load required trigger plan directory. name={entry.Name}, path={entry.Path}, pattern={pattern}");
                throw;
            }
        }
    }
}
