#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Triggering.Eventing;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerAuthoringMobaMigrationResult
    {
        public TriggerAuthoringProjectAsset Project;
        public readonly List<TriggerAuthoringModuleAsset> Modules = new List<TriggerAuthoringModuleAsset>();
        public readonly List<string> SourcePaths = new List<string>();
        public int SourceFileCount;
        public int TriggerCount;
        public TriggerAuthoringProjectValidationResult Validation;

        public bool Success => Project != null && Validation != null && Validation.Success;
    }

    /// <summary>
    /// Converts the maintained MOBA readable trigger files into strict Trigger Authoring source documents,
    /// then imports those documents into a project that the workspace can edit directly.
    /// </summary>
    internal static class TriggerAuthoringMobaMigration
    {
        internal const string LegacySourceRoot =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/ability/triggers";
        internal const string OutputRoot = "Assets/AbilityKit/MobaTriggerAuthoring";
        internal const string ProjectAssetPath = OutputRoot + "/MobaTriggerAuthoringProject.asset";
        internal const string P0ShowcaseSourceAssetPath =
            OutputRoot + "/Showcases/moba-p0-complex-skill.trigger.json";
        internal const string P0ShowcaseModuleAssetPath =
            OutputRoot + "/Packages/ability_moba_tests_p0_showcase.Module.asset";

        private const string CatalogRoot = OutputRoot + "/Catalogs";
        private const string PackageRoot = OutputRoot + "/Packages";
        private const string SourceRoot = OutputRoot + "/Sources";
        private const string RuntimePreviewRoot = OutputRoot + "/RuntimePreview";

        private static readonly TriggerEventDescriptorCatalog MobaEvents =
            new TriggerEventDescriptorCatalog(TriggerAuthoringProjectDefaults.CreateMobaEvents());

        private static readonly PackageDefinition[] PackageDefinitions =
        {
            new PackageDefinition("skills", "ability", "moba.skills", "ability.moba.skills", "MOBA 技能触发器", TriggerModuleKind.Ability),
            new PackageDefinition("buffs", "buff", "moba.buffs", "buff.moba.buffs", "MOBA Buff 触发器", TriggerModuleKind.Buff),
            new PackageDefinition("passives", "passive", "moba.passives", "passive.moba.passives", "MOBA 被动触发器", TriggerModuleKind.Passive),
            new PackageDefinition("gameplay", "gameplay", "moba.rules", "gameplay.moba.rules", "MOBA 玩法规则", TriggerModuleKind.Custom)
        };

        [MenuItem("Tools/AbilityKit/Framework/Ability/触发器示例/迁移 MOBA 配置到工作台")]
        public static void GenerateFromMenu()
        {
            var result = Generate();
            if (!result.Success)
            {
                var message = result.Validation != null
                    ? result.Validation.BuildMessage()
                    : "未能创建 MOBA 触发器项目。";
                Debug.LogError("[TriggerAuthoringMobaMigration] " + message);
                EditorUtility.DisplayDialog("MOBA 触发器迁移失败", message, "确定");
                return;
            }

            Selection.activeObject = result.Project;
            EditorGUIUtility.PingObject(result.Project);
            EditorApplication.ExecuteMenuItem("Window/AbilityKit/触发器编辑工作台");
            EditorUtility.DisplayDialog(
                "MOBA 触发器迁移完成",
                $"已将 {result.SourceFileCount} 个旧 JSON 文件转换为 {result.Modules.Count} 个内容包，共 {result.TriggerCount} 个触发器。",
                "确定");
        }

        // Unity -executeMethod entry point used by CI and repository maintenance.
        public static void GenerateBatch()
        {
            var result = Generate();
            if (!result.Success)
                throw new InvalidOperationException(result.Validation?.BuildMessage() ?? "MOBA trigger migration failed.");
            Debug.Log(
                $"[TriggerAuthoringMobaMigration] Generated {result.Modules.Count} packages, " +
                $"{result.TriggerCount} triggers from {result.SourceFileCount} files.");
        }

        // Keeps the checked-in MOBA project catalog aligned without regenerating modules or sources.
        public static void SyncEventCatalogBatch()
        {
            EnsureFolder(CatalogRoot);
            var eventCatalog = GetOrCreateAsset<TriggerEventCatalogAsset>(
                CatalogRoot + "/MobaTriggerEvents.asset");
            eventCatalog.Events = TriggerAuthoringProjectDefaults.CreateMobaEvents();
            EditorUtility.SetDirty(eventCatalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TriggerAuthoringMobaMigration] Synchronized {eventCatalog.Events.Count} MOBA events.");
        }

        [MenuItem("Tools/AbilityKit/Framework/Ability/触发器示例/同步 MOBA P0 复杂技能展示")]
        public static void SyncP0ShowcaseBatch()
        {
            var project = AssetDatabase.LoadAssetAtPath<TriggerAuthoringProjectAsset>(ProjectAssetPath);
            if (project == null)
                throw new InvalidOperationException("MOBA Trigger Authoring project is missing: " + ProjectAssetPath);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var module = ImportP0Showcase(project);
            TriggerAuthoringProjectMembership.Assign(module, project);
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();

            var validation = TriggerAuthoringProjectValidator.Validate(project);
            if (!validation.Success) throw new InvalidDataException(validation.BuildMessage());
            Debug.Log("[TriggerAuthoringMobaMigration] Synchronized MOBA P0 showcase into the Trigger Authoring workspace.");
        }

        internal static TriggerAuthoringMobaMigrationResult Generate()
        {
            var result = new TriggerAuthoringMobaMigrationResult();
            var legacyRoot = ResolveProjectPath(LegacySourceRoot);
            if (!Directory.Exists(legacyRoot))
                throw new DirectoryNotFoundException("MOBA trigger source directory was not found: " + legacyRoot);

            EnsureFolder(CatalogRoot);
            EnsureFolder(PackageRoot);
            EnsureFolder(SourceRoot);
            EnsureFolder(RuntimePreviewRoot);

            var eventCatalog = GetOrCreateAsset<TriggerEventCatalogAsset>(
                CatalogRoot + "/MobaTriggerEvents.asset");
            eventCatalog.Events = TriggerAuthoringProjectDefaults.CreateMobaEvents();
            var blackboardCatalog = GetOrCreateAsset<TriggerGlobalBlackboardCatalogAsset>(
                CatalogRoot + "/MobaTriggerBlackboard.asset");
            blackboardCatalog.Keys = TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys();
            var templateCatalog = GetOrCreateAsset<TriggerAuthoringTemplateCatalogAsset>(
                CatalogRoot + "/MobaTriggerTemplates.asset");
            var project = GetOrCreateAsset<TriggerAuthoringProjectAsset>(ProjectAssetPath);
            project.SetCatalogs(eventCatalog, blackboardCatalog, templateCatalog);
            project.SetExtensionIds(new[] { "abilitykit.demo.moba" });
            project.SetRuntimeOutputRoot(RuntimePreviewRoot);

            var documents = new List<(PackageDefinition Definition, TriggerAuthoringSourceDocument Document, string SourcePath)>();
            for (var i = 0; i < PackageDefinitions.Length; i++)
            {
                var definition = PackageDefinitions[i];
                var directory = Path.Combine(legacyRoot, definition.LegacyDirectory);
                var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                var module = ConvertFiles(definition, legacyRoot, files);
                var document = new TriggerAuthoringSourceDocument
                {
                    Metadata = new TriggerAuthoringSourceMetadata
                    {
                        Author = "AbilityKit MOBA",
                        Description = $"Migrated from {LegacySourceRoot}/{definition.LegacyDirectory}. Edit in the Trigger Authoring workspace."
                    },
                    Module = module
                };
                var sourceAssetPath = SourceRoot + "/moba-" + definition.LegacyDirectory + ".trigger.json";
                var sourcePath = ResolveProjectPath(sourceAssetPath);
                TriggerAuthoringSourceCodec.WriteFileAtomic(sourcePath, document);
                documents.Add((definition, document, sourcePath));
                result.SourcePaths.Add(sourcePath);
                result.SourceFileCount += files.Length;
                result.TriggerCount += module.Triggers.Count;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var modules = new List<TriggerAuthoringModuleAsset>();
            for (var i = 0; i < documents.Count; i++)
            {
                var item = documents[i];
                var assetPath = PackageRoot + "/" + item.Definition.ModuleId.Replace('.', '_') + ".Module.asset";
                var asset = GetOrCreateAsset<TriggerAuthoringModuleAsset>(assetPath);
                TriggerAuthoringProjectMembership.Assign(asset, project);
                asset.Module = new TriggerAuthoringModuleData { ModuleId = item.Definition.ModuleId };
                var import = TriggerAuthoringSourceSync.Import(asset, item.SourcePath, force: true);
                if (!import.Success)
                    throw new InvalidDataException(item.Definition.ModuleId + ": " + import.Message);

                var metadata = new TriggerAuthoringPackageMetadata();
                metadata.SetIdentity(item.Definition.DomainId, item.Definition.ContentKey);
                metadata.SetOwner("moba-demo");
                metadata.SetTags(new[] { "moba", item.Definition.LegacyDirectory, "migrated" });
                asset.SetPackageMetadata(metadata);
                asset.name = item.Definition.DisplayName;
                EditorUtility.SetDirty(asset);
                modules.Add(asset);
            }

            if (File.Exists(ResolveProjectPath(P0ShowcaseSourceAssetPath)))
                modules.Add(ImportP0Showcase(project));

            project.SetModules(modules);
            EditorUtility.SetDirty(eventCatalog);
            EditorUtility.SetDirty(blackboardCatalog);
            EditorUtility.SetDirty(templateCatalog);
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            result.Project = project;
            result.Modules.AddRange(modules);
            result.Validation = TriggerAuthoringProjectValidator.Validate(project);
            return result;
        }

        private static TriggerAuthoringModuleAsset ImportP0Showcase(TriggerAuthoringProjectAsset project)
        {
            var sourcePath = ResolveProjectPath(P0ShowcaseSourceAssetPath);
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException("MOBA P0 showcase source is missing.", sourcePath);

            EnsureFolder(OutputRoot + "/Showcases");
            EnsureFolder(PackageRoot);
            var asset = GetOrCreateAsset<TriggerAuthoringModuleAsset>(P0ShowcaseModuleAssetPath);
            TriggerAuthoringProjectMembership.Assign(asset, project);
            asset.Module = new TriggerAuthoringModuleData { ModuleId = "ability.moba.tests.p0_showcase" };
            var import = TriggerAuthoringSourceSync.Import(asset, sourcePath, force: true);
            if (!import.Success) throw new InvalidDataException(import.Message);

            var metadata = new TriggerAuthoringPackageMetadata();
            metadata.SetIdentity("ability", "moba.tests.p0_showcase");
            metadata.SetOwner("moba-demo-tests");
            metadata.SetTags(new[] { "moba", "test", "p0", "showcase" });
            asset.SetPackageMetadata(metadata);
            asset.name = "MOBA P0 复杂技能展示";
            EditorUtility.SetDirty(asset);
            return asset;
        }

        internal static TriggerAuthoringModuleData ConvertFiles(
            PackageDefinition definition,
            string legacyRoot,
            IReadOnlyList<string> files)
        {
            var module = new TriggerAuthoringModuleData
            {
                ModuleId = definition.ModuleId,
                DisplayName = definition.DisplayName,
                Kind = definition.Kind
            };

            for (var i = 0; i < files.Count; i++)
            {
                var text = File.ReadAllText(files[i]);
                var root = JToken.Parse(text);
                var triggers = ReadTriggers(root);
                var relativePath = files[i].Substring(legacyRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                for (var triggerIndex = 0; triggerIndex < triggers.Count; triggerIndex++)
                    module.Triggers.Add(ConvertTrigger(triggers[triggerIndex], relativePath));
            }

            module.Triggers.Sort((left, right) => left.Id.CompareTo(right.Id));
            return module;
        }

        private static List<JObject> ReadTriggers(JToken root)
        {
            if (root is JArray array) return array.OfType<JObject>().ToList();
            if (!(root is JObject obj)) throw new InvalidDataException("Trigger source root must be an object or array.");
            if (obj["triggers"] is JArray triggers) return triggers.OfType<JObject>().ToList();
            return new List<JObject> { obj };
        }

        private static TriggerDefinitionData ConvertTrigger(JObject source, string relativePath)
        {
            var eventName = ReadString(source, "event");
            var executionMode = ReadString(source, "execution");
            var hasActions = (source["actions"] as JArray)?.OfType<JObject>().Any() == true;
            MobaEvents.TryResolve(eventName, out var eventDefinition);
            return new TriggerDefinitionData
            {
                Id = ReadInt(source, "id"),
                Name = ReadString(source, "name"),
                GroupPath = Path.GetFileNameWithoutExtension(relativePath),
                Tags = new List<string> { "moba", relativePath.Split('/')[0] },
                Enabled = ReadBool(source, true, "enabled") && hasActions,
                EntryMode = string.IsNullOrWhiteSpace(eventName) ? TriggerEntryMode.Callable : TriggerEntryMode.Event,
                Event = eventName,
                Phase = ReadString(source, "phase") ?? "immediate",
                Priority = ReadInt(source, "priority"),
                Scope = ReadString(source, "scope") ?? "owner",
                AllowExternal = ReadBool(source, false, "allowExternal"),
                Condition = ConvertConditionList(source["conditions"] as JArray, eventDefinition),
                Actions = ConvertActionList(source["actions"] as JArray),
                ExecutionControl = new TriggerExecutionControlData { Mode = executionMode },
                Note = "Migrated from " + LegacySourceRoot + "/" + relativePath
            };
        }

        private static TriggerNodeData ConvertConditionList(
            JArray source,
            TriggerEventDefinitionData eventDefinition)
        {
            var nodes = source == null
                ? new List<TriggerNodeData>()
                : source.OfType<JObject>().Select(item => ConvertCondition(item, eventDefinition)).ToList();
            if (nodes.Count == 0) return null;
            if (nodes.Count == 1) return nodes[0];
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = "all",
                Children = nodes
            };
        }

        private static TriggerNodeData ConvertCondition(
            JObject source,
            TriggerEventDefinitionData eventDefinition)
        {
            var type = ReadString(source, "type") ?? string.Empty;
            if (string.Equals(type, "not", StringComparison.OrdinalIgnoreCase))
            {
                var children = new List<TriggerNodeData>();
                if (source["item"] is JObject child) children.Add(ConvertCondition(child, eventDefinition));
                return new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Condition,
                    Type = "not",
                    Children = children
                };
            }

            var node = new TriggerNodeData { Kind = TriggerNodeKind.Condition, Type = type };
            if (IsComparison(type))
            {
                var comparisonType = ResolveComparisonType(source, eventDefinition);
                node.Arguments.Add(new TriggerArgumentData
                {
                    Name = "left",
                    Value = ConvertComparisonLeft(source, eventDefinition, comparisonType)
                });
                node.Arguments.Add(new TriggerArgumentData
                {
                    Name = "right",
                    Value = ConvertComparisonOperand(
                        source["right"] ?? source["value"],
                        eventDefinition,
                        comparisonType)
                });
                return node;
            }

            foreach (var property in source.Properties())
            {
                if (string.Equals(property.Name, "type", StringComparison.OrdinalIgnoreCase)) continue;
                node.Arguments.Add(new TriggerArgumentData
                {
                    Name = property.Name,
                    Value = ConvertValue(property.Value, FindParameter(TriggerNodeKind.Condition, type, property.Name), property.Name)
                });
            }
            return node;
        }

        private static TriggerValueRefData ConvertComparisonLeft(
            JObject source,
            TriggerEventDefinitionData eventDefinition,
            TriggerValueType comparisonType)
        {
            if (source["left"] != null)
                return ConvertComparisonOperand(source["left"], eventDefinition, comparisonType);
            var domain = ReadString(source, "left_var_domain", "var_domain");
            var key = ReadString(source, "left_var_key", "var_key");
            if (!string.IsNullOrWhiteSpace(domain) && !string.IsNullOrWhiteSpace(key))
                return Reference(TriggerValueSource.Context, comparisonType, domain + ":" + key);
            return Reference(
                TriggerValueSource.Payload,
                ResolvePayloadFieldType(eventDefinition, ReadString(source, "arg_name"), comparisonType),
                ReadString(source, "arg_name"));
        }

        private static TriggerValueRefData ConvertComparisonOperand(
            JToken token,
            TriggerEventDefinitionData eventDefinition,
            TriggerValueType comparisonType)
        {
            if (token != null && token.Type == JTokenType.String)
            {
                var text = token.Value<string>() ?? string.Empty;
                if (text.StartsWith("payload:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = text.Substring("payload:".Length);
                    return Reference(
                        TriggerValueSource.Payload,
                        ResolvePayloadFieldType(eventDefinition, path, comparisonType),
                        path);
                }
                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return comparisonType == TriggerValueType.Integer
                        ? ConstantInteger(Convert.ToInt64(number))
                        : ConstantNumber(number);
            }
            return ConvertValue(token, null, string.Empty, comparisonType);
        }

        private static TriggerValueType ResolveComparisonType(
            JObject source,
            TriggerEventDefinitionData eventDefinition)
        {
            var payloadPath = ReadPayloadPath(source["left"]);
            if (string.IsNullOrWhiteSpace(payloadPath)) payloadPath = ReadString(source, "arg_name");
            if (string.IsNullOrWhiteSpace(payloadPath)) payloadPath = ReadPayloadPath(source["right"]);
            return ResolvePayloadFieldType(eventDefinition, payloadPath, TriggerValueType.Number);
        }

        private static string ReadPayloadPath(JToken token)
        {
            if (token == null || token.Type != JTokenType.String) return null;
            var text = token.Value<string>() ?? string.Empty;
            return text.StartsWith("payload:", StringComparison.OrdinalIgnoreCase)
                ? text.Substring("payload:".Length)
                : null;
        }

        private static TriggerValueType ResolvePayloadFieldType(
            TriggerEventDefinitionData eventDefinition,
            string path,
            TriggerValueType fallback)
        {
            if (eventDefinition?.PayloadFields == null || string.IsNullOrWhiteSpace(path)) return fallback;
            for (var i = 0; i < eventDefinition.PayloadFields.Count; i++)
            {
                var field = eventDefinition.PayloadFields[i];
                if (field != null && string.Equals(field.Path, path, StringComparison.Ordinal)) return field.Type;
            }
            return fallback;
        }

        private static TriggerNodeData ConvertActionList(JArray source)
        {
            var nodes = source == null
                ? new List<TriggerNodeData>()
                : source.OfType<JObject>().Select(ConvertAction).ToList();
            if (nodes.Count == 0)
            {
                return new TriggerNodeData
                {
                    Enabled = false,
                    Kind = TriggerNodeKind.Action,
                    Type = "seq"
                };
            }
            if (nodes.Count == 1) return nodes[0];
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "seq",
                Children = nodes
            };
        }

        private static TriggerNodeData ConvertAction(JObject source)
        {
            var type = ReadString(source, "type") ?? string.Empty;
            var node = new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = type };
            foreach (var property in source.Properties())
            {
                if (string.Equals(property.Name, "type", StringComparison.OrdinalIgnoreCase)) continue;
                var name = NormalizeActionArgument(type, property.Name, source);
                if (string.IsNullOrEmpty(name)) continue;
                var parameter = FindParameter(TriggerNodeKind.Action, type, name);
                node.Arguments.Add(new TriggerArgumentData
                {
                    Name = name,
                    Value = ConvertActionValue(name, property.Value, parameter)
                });
            }
            return node;
        }

        private static string NormalizeActionArgument(string type, string name, JObject source)
        {
            if (string.Equals(name, "target_payload_field", StringComparison.OrdinalIgnoreCase))
                return "target_payload_field_id";
            if (string.Equals(type, "add_buff", StringComparison.OrdinalIgnoreCase))
            {
                // Buff lifetime is owned by the Buff config. Legacy trigger files carried this
                // field, but the runtime action has never treated it as an override.
                if (string.Equals(name, "duration_ms", StringComparison.OrdinalIgnoreCase)) return null;
                if (string.Equals(name, "buffIds", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "buff_id", StringComparison.OrdinalIgnoreCase)) return "buff_ids";
                if (string.Equals(name, "targetActorId", StringComparison.OrdinalIgnoreCase)) return "target_actor_id";
            }
            if (string.Equals(type, "give_damage", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(name, "value", StringComparison.OrdinalIgnoreCase) && source["damage_value"] == null) return "damage_value";
                if (string.Equals(name, "scale", StringComparison.OrdinalIgnoreCase) && source["source_attack_ratio"] == null) return "source_attack_ratio";
                if (string.Equals(name, "damageType", StringComparison.OrdinalIgnoreCase)) return "damage_type";
                if (string.Equals(name, "reasonKind", StringComparison.OrdinalIgnoreCase)) return "reason_kind";
                if (string.Equals(name, "reasonParam", StringComparison.OrdinalIgnoreCase)) return "reason_param";
                if (string.Equals(name, "queryTemplateId", StringComparison.OrdinalIgnoreCase)) return "query_template_id";
            }
            if (string.Equals(type, "emit", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(name, "emitterId", StringComparison.OrdinalIgnoreCase)) return "emitter_id";
            return name;
        }

        private static TriggerValueRefData ConvertActionValue(
            string name,
            JToken token,
            TriggerParameterDescriptor parameter)
        {
            if (string.Equals(name, "target_payload_field_id", StringComparison.OrdinalIgnoreCase) && token?.Type == JTokenType.String)
            {
                var field = token.Value<string>() ?? string.Empty;
                if (field.StartsWith("payload:", StringComparison.OrdinalIgnoreCase)) field = field.Substring("payload:".Length);
                return ConstantInteger(StableStringId.Get("payload:" + field));
            }
            if ((string.Equals(name, "damage_type", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(name, "formulaType", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(name, "directionSource", StringComparison.OrdinalIgnoreCase)) &&
                token?.Type == JTokenType.String && TryMapEnum(name, token.Value<string>(), out var enumValue))
                return ConstantInteger(enumValue);
            return ConvertValue(token, parameter, name);
        }

        private static TriggerValueRefData ConvertValue(
            JToken token,
            TriggerParameterDescriptor parameter,
            string argumentName,
            TriggerValueType fallbackType = TriggerValueType.None)
        {
            var expected = parameter != null && parameter.Type != TriggerValueType.None
                ? parameter.Type
                : fallbackType;
            if (token == null || token.Type == JTokenType.Null)
                return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = expected };
            if (token.Type == JTokenType.Array)
            {
                return new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.IntegerList,
                    IntegerListValue = token.Values<long>().ToList()
                };
            }
            if (token.Type == JTokenType.Boolean)
                return new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.Boolean,
                    BooleanValue = token.Value<bool>()
                };
            if (token.Type == JTokenType.Integer)
            {
                if (expected == TriggerValueType.Boolean)
                    return new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Boolean,
                        BooleanValue = token.Value<long>() != 0
                    };
                if (expected == TriggerValueType.IntegerList)
                    return new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.IntegerList,
                        IntegerListValue = new List<long> { token.Value<long>() }
                    };
                if (expected == TriggerValueType.Number) return ConstantNumber(token.Value<double>());
                return ConstantInteger(token.Value<long>());
            }
            if (token.Type == JTokenType.Float) return ConstantNumber(token.Value<double>());
            if (token.Type == JTokenType.String)
            {
                var text = token.Value<string>() ?? string.Empty;
                if (text.StartsWith("payload:", StringComparison.OrdinalIgnoreCase))
                    return Reference(TriggerValueSource.Payload, expected == TriggerValueType.None ? TriggerValueType.Number : expected, text.Substring("payload:".Length));
                if (text.StartsWith("@", StringComparison.Ordinal))
                    return Reference(TriggerValueSource.Payload, expected == TriggerValueType.None ? TriggerValueType.Number : expected, text.Substring(1));
                if (text.StartsWith("=", StringComparison.Ordinal))
                    return new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Expression,
                        Type = expected == TriggerValueType.None ? TriggerValueType.Number : expected,
                        Expression = text.Substring(1).Trim()
                    };
                if (expected == TriggerValueType.Integer && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                    return ConstantInteger(integer);
                if (expected == TriggerValueType.Number && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                    return ConstantNumber(number);
                return new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.String,
                    StringValue = text
                };
            }
            throw new InvalidDataException($"Unsupported value for argument '{argumentName}': {token.Type}.");
        }

        private static bool TryMapEnum(string argumentName, string value, out long result)
        {
            result = 0;
            if (string.Equals(argumentName, "damage_type", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(value, "physical", StringComparison.OrdinalIgnoreCase)) { result = 1; return true; }
                if (string.Equals(value, "magic", StringComparison.OrdinalIgnoreCase)) { result = 2; return true; }
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "true_damage", StringComparison.OrdinalIgnoreCase)) { result = 4; return true; }
            }
            if (string.Equals(argumentName, "formulaType", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(value, "fixed", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "standard", StringComparison.OrdinalIgnoreCase)) { result = 1; return true; }
            }
            if (string.Equals(argumentName, "directionSource", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(value, "cast_context", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(value, "target_query", StringComparison.OrdinalIgnoreCase)) { result = 1; return true; }
            }
            return false;
        }

        private static TriggerParameterDescriptor FindParameter(TriggerNodeKind kind, string type, string name)
        {
            var catalog = TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            if (!catalog.TryGet(kind, type, out var descriptor)) return null;
            for (var i = 0; i < descriptor.Parameters.Count; i++)
                if (string.Equals(descriptor.Parameters[i].Name, name, StringComparison.Ordinal))
                    return descriptor.Parameters[i];
            return null;
        }

        private static bool IsComparison(string type)
        {
            return string.Equals(type, "arg_eq", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "arg_neq", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "arg_gt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "arg_gte", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "arg_lt", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "arg_lte", StringComparison.OrdinalIgnoreCase);
        }

        private static TriggerValueRefData Reference(TriggerValueSource source, TriggerValueType type, string path)
        {
            return new TriggerValueRefData { Source = source, Type = type, Path = path ?? string.Empty };
        }

        private static TriggerValueRefData ConstantInteger(long value)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = TriggerValueType.Integer, IntegerValue = value };
        }

        private static TriggerValueRefData ConstantNumber(double value)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = TriggerValueType.Number, NumberValue = value };
        }

        private static string ReadString(JObject source, params string[] names)
        {
            for (var i = 0; i < names.Length; i++)
                if (source.TryGetValue(names[i], StringComparison.OrdinalIgnoreCase, out var token) && token.Type != JTokenType.Null)
                    return token.Value<string>();
            return null;
        }

        private static int ReadInt(JObject source, string name)
        {
            return source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token) && token.Type != JTokenType.Null
                ? token.Value<int>()
                : 0;
        }

        private static bool ReadBool(JObject source, bool fallback, string name)
        {
            return source.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token) && token.Type != JTokenType.Null
                ? token.Value<bool>()
                : fallback;
        }

        private static T GetOrCreateAsset<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static string ResolveProjectPath(string projectRelativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath ?? string.Empty));
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        internal readonly struct PackageDefinition
        {
            public PackageDefinition(
                string legacyDirectory,
                string domainId,
                string contentKey,
                string moduleId,
                string displayName,
                TriggerModuleKind kind)
            {
                LegacyDirectory = legacyDirectory;
                DomainId = domainId;
                ContentKey = contentKey;
                ModuleId = moduleId;
                DisplayName = displayName;
                Kind = kind;
            }

            public string LegacyDirectory { get; }
            public string DomainId { get; }
            public string ContentKey { get; }
            public string ModuleId { get; }
            public string DisplayName { get; }
            public TriggerModuleKind Kind { get; }
        }
    }
}
#endif
