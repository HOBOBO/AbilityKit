using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Triggering.Variables.Numeric;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Console.Bootstrap
{
    public sealed class ConsoleConfigModule : IWorldModule
    {
        private readonly string _resourcesDir;
        private readonly string _lubanResourcesDir;
        private readonly string _triggerPlansDir;

        public ConsoleConfigModule(string resourcesDir = "luban/moba", string lubanResourcesDir = "luban/moba", string triggerPlansDir = "luban/ability/triggers")
        {
            _resourcesDir = resourcesDir;
            _lubanResourcesDir = lubanResourcesDir;
            _triggerPlansDir = triggerPlansDir;
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.Register<ITextAssetLoader>(WorldLifetime.Singleton, _ => new ConsoleTextAssetLoader());
            builder.Register<IMobaConfigTableRegistry>(WorldLifetime.Singleton, _ => MobaConfigRegistry.Instance);
            builder.Register<IMobaConfigDtoDeserializer>(WorldLifetime.Singleton, _ => JsonNetMobaConfigDtoDeserializer.Instance);
            builder.Register<IMobaConfigDtoBytesDeserializer>(WorldLifetime.Singleton, _ => new LubanMobaConfigDtoBytesDeserializer());

            builder.Register<DefaultMobaConfigLoader>(WorldLifetime.Singleton, container =>
            {
                var textAssetLoader = container.Resolve<ITextAssetLoader>();
                var registry = container.Resolve<IMobaConfigTableRegistry>();
                return new DefaultMobaConfigLoader(registry, textAssetLoader);
            });

            builder.Register<LubanConfigGroup>(WorldLifetime.Singleton, container =>
            {
                var textAssetLoader = container.Resolve<ITextAssetLoader>();
                return LubanConfigGroup.Create(textAssetLoader, _lubanResourcesDir);
            });

            builder.Register<ILubanConfigLoader>(WorldLifetime.Singleton, container =>
            {
                var textAssetLoader = container.Resolve<ITextAssetLoader>();
                return new ConsoleLubanConfigLoader(textAssetLoader, _lubanResourcesDir);
            });

            builder.Register<AbilityKit.Demo.Moba.Config.Core.MobaConfigDatabase>(WorldLifetime.Singleton, container =>
            {
                var registry = container.Resolve<IMobaConfigTableRegistry>();
                var deserializer = container.Resolve<IMobaConfigDtoDeserializer>();
                var textAssetLoader = container.Resolve<ITextAssetLoader>();

                var db = new AbilityKit.Demo.Moba.Config.Core.MobaConfigDatabase(registry, deserializer, null, textAssetLoader);
                try
                {
                    db.LoadFromGroups(MobaLubanConfigGroups.Create(
                        textAssetLoader,
                        MobaRuntimeConfigTableRegistry.Tables.Select(table => table.FilePath).ToArray()));
                }
                catch (Exception ex)
                {
                    Platform.Log.Error($"[ConsoleConfigModule] Failed to load MobaConfigDatabase from resources '{_resourcesDir}': {ex.Message}");
                    throw;
                }

                return db;
            });

            builder.Register<TriggerPlanJsonDatabase>(WorldLifetime.Scoped, container =>
            {
                var textAssetLoader = container.Resolve<ITextAssetLoader>();
                var db = new TriggerPlanJsonDatabase
                {
                    CueFactory = new MobaPresentationCueFactory(container.Resolve<AbilityKit.Demo.Moba.Services.MobaPresentationCueSnapshotService>())
                };

                try
                {
                    var adapter = new TextAssetLoaderAdapter(textAssetLoader);
                    var directoryLoader = new TriggerPlanDirectoryLoader(adapter);
                    var directories = new[] { _triggerPlansDir, "luban/ability/rules" }
                        .Where(d => !string.IsNullOrEmpty(d))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var options = new TriggerPlanDirectoryLoadOptions { CueFactory = db.CueFactory };
                    var loadedDb = directoryLoader.LoadDirectories(directories, "**/*.json", options);
                    if (loadedDb?.Records != null)
                    {
                        db.MergeFrom(loadedDb, replaceExisting: true);
                    }

                    Platform.Log.System($"[ConsoleConfigModule] Loaded {db.Records?.Count ?? 0} trigger plans from configured directories");
                }
                catch (Exception ex)
                {
                    Platform.Log.Error($"[ConsoleConfigModule] Failed to load TriggerPlanJsonDatabase: {ex.Message}");
                }

                return db;
            });
        }

        private static int ParsePhase(string phase)
        {
            return phase?.ToLowerInvariant() switch
            {
                "immediate" => 0,
                "delayed" => 1,
                "precondition" => 2,
                "postcondition" => 3,
                _ => 0
            };
        }

        private static ActionCallPlan[] BuildActionCallPlans(JArray actionsArray)
        {
            if (actionsArray == null || actionsArray.Count == 0)
                return Array.Empty<ActionCallPlan>();

            var result = new ActionCallPlan[actionsArray.Count];
            for (int i = 0; i < actionsArray.Count; i++)
            {
                var action = actionsArray[i];
                var actionId = action["actionId"]?.Value<int>() ?? 0;
                var id = new ActionId(actionId);

                var argsObj = action["args"] as JObject;
                if (argsObj != null && argsObj.Count > 0)
                {
                    var namedArgs = new Dictionary<string, ActionArgValue>(argsObj.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in argsObj.Properties())
                    {
                        namedArgs[prop.Name] = new ActionArgValue(BuildValueRef(prop.Value), prop.Name);
                    }
                    result[i] = ActionCallPlan.WithArgs(id, namedArgs);
                }
                else
                {
                    result[i] = new ActionCallPlan(id);
                }
            }
            return result;
        }

        private static NumericValueRef BuildValueRef(JToken token)
        {
            if (token == null) return default;

            var kind = token["kind"]?.Value<string>();
            switch (kind)
            {
                case "Const":
                    var constValue = token["constValue"]?.Value<double>() ?? 0;
                    return NumericValueRef.Const(constValue);
                case "PayloadField":
                    var fieldId = token["fieldId"]?.Value<int>() ?? 0;
                    return NumericValueRef.PayloadField(fieldId);
                default:
                    return default;
            }
        }

    }

    internal sealed class TextAssetLoaderAdapter : TriggerPlanJsonDatabase.ITextLoader, IFileSystemTextLoader
    {
        private readonly ITextAssetLoader _inner;
        private readonly string _basePath;

        public TextAssetLoaderAdapter(ITextAssetLoader inner)
        {
            _inner = inner;
            _basePath = GetBasePath(inner);
        }

        public bool TryLoad(string id, out string text)
        {
            var normalizedId = id;
            if (normalizedId.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                normalizedId = normalizedId.Substring(0, normalizedId.Length - 5);
            }
            return _inner.TryLoadText(normalizedId, out text);
        }

        public IEnumerable<string> GetFiles(string directory, string pattern)
        {
            if (string.IsNullOrEmpty(_basePath))
                return Enumerable.Empty<string>();

            var normalizedBasePath = _basePath.Replace('/', Path.DirectorySeparatorChar);
            var normalizedDir = directory.Replace('/', Path.DirectorySeparatorChar);
            var fullDir = Path.Combine(normalizedBasePath, normalizedDir);

            if (!Directory.Exists(fullDir)) return Enumerable.Empty<string>();

            var searchPattern = pattern.Replace("**/", "").Replace("**", "*");

            try
            {
                return Directory.GetFiles(fullDir, searchPattern, SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .Select(f => GetRelativePath(f))
                    .ToList();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private string GetRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(_basePath)) return fullPath;
            var normalizedBasePath = _basePath.Replace('/', Path.DirectorySeparatorChar);
            var relative = fullPath.Replace(normalizedBasePath + Path.DirectorySeparatorChar, "");
            return relative.Replace('\\', '/');
        }

        private static string GetBasePath(ITextAssetLoader loader)
        {
            if (loader is ConsoleTextAssetLoader consoleLoader)
            {
                var field = typeof(ConsoleTextAssetLoader).GetField("_basePath",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(consoleLoader) as string ?? string.Empty;
            }
            return string.Empty;
        }
    }
}
