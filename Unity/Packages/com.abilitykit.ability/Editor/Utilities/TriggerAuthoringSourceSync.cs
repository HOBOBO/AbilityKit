#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.Editor.Platform.Synchronization;
using AbilityKit.Editor.Platform.UI;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal enum TriggerAuthoringSyncState
    {
        Untracked = 0,
        InSync = 1,
        AssetChanged = 2,
        JsonChanged = 3,
        Conflict = 4,
        SourceMissing = 5,
        InvalidSource = 6
    }

    internal sealed class TriggerAuthoringSyncInspection
    {
        public TriggerAuthoringSyncState State;
        public string SourcePath;
        public bool SourceExists;
        public string AssetHash;
        public string SourceHash;
        public string Error;
        public EditorSourceSyncInspection PlatformInspection;
    }

    internal sealed class TriggerAuthoringSyncResult
    {
        public bool Success;
        public TriggerAuthoringSyncState State;
        public string Message;
        public string ContentHash;
        public bool CanForce;

        public static TriggerAuthoringSyncResult Succeeded(TriggerAuthoringSyncState state, string hash)
        {
            return new TriggerAuthoringSyncResult
            {
                Success = true,
                State = state,
                ContentHash = hash,
                Message = string.Empty
            };
        }

        public static TriggerAuthoringSyncResult Failed(
            TriggerAuthoringSyncState state,
            string message,
            bool canForce = false)
        {
            return new TriggerAuthoringSyncResult
            {
                Success = false,
                State = state,
                Message = message ?? string.Empty,
                ContentHash = string.Empty,
                CanForce = canForce
            };
        }
    }

    internal enum TriggerAuthoringSourcePreviewKind
    {
        Module = 0,
        Template = 1
    }

    internal enum TriggerAuthoringSourceChangeKind
    {
        Added = 0,
        Removed = 1,
        Modified = 2,
        Renamed = 3
    }

    internal sealed class TriggerAuthoringSourceImportChange
    {
        public TriggerAuthoringSourceChangeKind Kind;
        public string Area;
        public string Path;
        public string Summary;
        public string Before;
        public string After;

        public string BuildLine()
        {
            var builder = new StringBuilder();
            builder.Append(ChangeKindLabel(Kind)).Append(" ");
            if (!string.IsNullOrWhiteSpace(Area)) builder.Append(Area).Append(" ");
            if (!string.IsNullOrWhiteSpace(Path)) builder.Append(Path).Append(": ");
            builder.Append(Summary ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(Before) || !string.IsNullOrWhiteSpace(After))
                builder.Append("（").Append(Display(Before)).Append(" -> ").Append(Display(After)).Append("）");
            return builder.ToString();
        }

        private static string Display(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<空>" : value;
        }

        internal static string ChangeKindLabel(TriggerAuthoringSourceChangeKind kind)
        {
            switch (kind)
            {
                case TriggerAuthoringSourceChangeKind.Added: return "新增";
                case TriggerAuthoringSourceChangeKind.Removed: return "删除";
                case TriggerAuthoringSourceChangeKind.Modified: return "修改";
                case TriggerAuthoringSourceChangeKind.Renamed: return "重命名";
                default: return kind.ToString();
            }
        }
    }

    internal sealed class TriggerAuthoringSourceImportPreview
    {
        public bool Success;
        public bool CanImport;
        public bool RequiresForce;
        public TriggerAuthoringSyncState State;
        public TriggerAuthoringSourcePreviewKind Kind;
        public string SourcePath;
        public string Message;
        public string AssetIdentity;
        public string SourceIdentity;
        public string AssetDisplayName;
        public string SourceDisplayName;
        public int AssetTriggerCount;
        public int SourceTriggerCount;
        public int AssetBlackboardCount;
        public int SourceBlackboardCount;
        public int AssetConditionGroupCount;
        public int SourceConditionGroupCount;
        public int AssetActionGroupCount;
        public int SourceActionGroupCount;
        public int AssetTemplateParameterCount;
        public int SourceTemplateParameterCount;
        public List<TriggerAuthoringDiagnostic> Diagnostics = new List<TriggerAuthoringDiagnostic>();
        public List<TriggerAuthoringSourceImportChange> Changes = new List<TriggerAuthoringSourceImportChange>();

        public static TriggerAuthoringSourceImportPreview Failed(
            TriggerAuthoringSourcePreviewKind kind,
            TriggerAuthoringSyncState state,
            string sourcePath,
            string message)
        {
            return new TriggerAuthoringSourceImportPreview
            {
                Kind = kind,
                State = state,
                SourcePath = sourcePath ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        public string BuildDialogMessage()
        {
            var builder = new StringBuilder();
            builder.AppendLine(Kind == TriggerAuthoringSourcePreviewKind.Module
                ? "是否导入触发器模块 Source JSON？"
                : "是否导入触发器模板 Source JSON？");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(SourcePath))
                builder.AppendLine("源文件：" + SourcePath);
            builder.AppendLine("同步状态：" + SyncStateLabel(State));
            if (RequiresForce)
                builder.AppendLine("本次导入会覆盖本地资产改动。");
            if (!string.IsNullOrWhiteSpace(Message))
                builder.AppendLine(Message);
            builder.AppendLine();
            builder.AppendLine("标识：" + Display(AssetIdentity) + " -> " + Display(SourceIdentity));
            builder.AppendLine("显示名称：" + Display(AssetDisplayName) + " -> " + Display(SourceDisplayName));

            if (Kind == TriggerAuthoringSourcePreviewKind.Module)
            {
                builder.AppendLine("触发器：" + AssetTriggerCount + " -> " + SourceTriggerCount);
                builder.AppendLine("黑板变量：" + AssetBlackboardCount + " -> " + SourceBlackboardCount);
                builder.AppendLine("条件分组：" + AssetConditionGroupCount + " -> " + SourceConditionGroupCount);
                builder.AppendLine("行为分组：" + AssetActionGroupCount + " -> " + SourceActionGroupCount);
            }
            else
            {
                builder.AppendLine("参数：" + AssetTemplateParameterCount + " -> " + SourceTemplateParameterCount);
            }

            if (Changes != null && Changes.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("变更：");
                for (var i = 0; i < Changes.Count; i++)
                {
                    var change = Changes[i];
                    if (change == null) continue;
                    builder.AppendLine(change.BuildLine());
                    if (i >= 11 && Changes.Count > 12)
                    {
                        builder.AppendLine("... 还有 " + (Changes.Count - i - 1) + " 项");
                        break;
                    }
                }
            }

            if (Diagnostics != null && Diagnostics.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("诊断：");
                for (var i = 0; i < Diagnostics.Count; i++)
                {
                    var diagnostic = Diagnostics[i];
                    if (diagnostic == null) continue;
                    builder.AppendLine(
                        DiagnosticSeverityLabel(diagnostic.Severity) + " " +
                        diagnostic.Code + " " +
                        diagnostic.Path + ": " +
                        diagnostic.Message);
                    if (i >= 7 && Diagnostics.Count > 8)
                    {
                        builder.AppendLine("... 还有 " + (Diagnostics.Count - i - 1) + " 项");
                        break;
                    }
                }
            }

            return builder.ToString();
        }

        private static string Display(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<空>" : value;
        }

        internal static string SyncStateLabel(TriggerAuthoringSyncState state)
        {
            switch (state)
            {
                case TriggerAuthoringSyncState.Untracked: return "未跟踪";
                case TriggerAuthoringSyncState.InSync: return "已同步";
                case TriggerAuthoringSyncState.AssetChanged: return "资产已修改";
                case TriggerAuthoringSyncState.JsonChanged: return "源文件已修改";
                case TriggerAuthoringSyncState.Conflict: return "存在冲突";
                case TriggerAuthoringSyncState.SourceMissing: return "源文件缺失";
                case TriggerAuthoringSyncState.InvalidSource: return "源文件无效";
                default: return "未知";
            }
        }

        private static string DiagnosticSeverityLabel(TriggerAuthoringDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case TriggerAuthoringDiagnosticSeverity.Error: return "错误";
                case TriggerAuthoringDiagnosticSeverity.Warning: return "警告";
                default: return "信息";
            }
        }
    }

    internal static class TriggerAuthoringSourceImportDiff
    {
        public static List<TriggerAuthoringSourceImportChange> Compare(
            TriggerAuthoringModuleData local,
            TriggerAuthoringModuleData source)
        {
            var changes = new List<TriggerAuthoringSourceImportChange>();
            local = local ?? new TriggerAuthoringModuleData();
            source = source ?? new TriggerAuthoringModuleData();

            AddValueChange(changes, "模块", "module.displayName", "显示名称已更改", local.DisplayName, source.DisplayName);
            AddValueChange(changes, "模块", "module.kind", "模块类型已更改", local.Kind.ToString(), source.Kind.ToString());
            AddListChanges(
                changes,
                "触发器",
                "module.triggers",
                local.Triggers,
                source.Triggers,
                item => item.Id.ToString(),
                item => string.IsNullOrWhiteSpace(item.Name) ? item.Event : item.Name,
                item => item.Name,
                item => "触发器已更改");
            AddListChanges(
                changes,
                "黑板变量",
                "module.blackboard",
                local.Blackboard,
                source.Blackboard,
                item => item.Key,
                item => item.Key,
                item => item.Description,
                item => "黑板变量已更改");
            AddListChanges(
                changes,
                "条件分组",
                "module.conditionGroups",
                local.ConditionGroups,
                source.ConditionGroups,
                item => item.Id,
                item => string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName,
                item => item.DisplayName,
                item => "条件分组已更改");
            AddListChanges(
                changes,
                "行为分组",
                "module.actionGroups",
                local.ActionGroups,
                source.ActionGroups,
                item => item.Id,
                item => string.IsNullOrWhiteSpace(item.DisplayName) ? item.Id : item.DisplayName,
                item => item.DisplayName,
                item => "行为分组已更改");
            return changes;
        }

        public static List<TriggerAuthoringSourceImportChange> Compare(
            TriggerAuthoringTemplateData local,
            TriggerAuthoringTemplateData source)
        {
            var changes = new List<TriggerAuthoringSourceImportChange>();
            local = local ?? new TriggerAuthoringTemplateData();
            source = source ?? new TriggerAuthoringTemplateData();
            TriggerAuthoringTemplateDefinition.Normalize(local);
            TriggerAuthoringTemplateDefinition.Normalize(source);

            AddValueChange(changes, "模板", "template.displayName", "显示名称已更改", local.DisplayName, source.DisplayName);
            AddValueChange(changes, "模板", "template.templateVersion", "版本已更改", local.TemplateVersion, source.TemplateVersion);
            AddValueChange(changes, "触发器原型", "template.definition.event", "事件已更改", local.Definition.Event, source.Definition.Event);
            AddListChanges(
                changes,
                "参数",
                "template.parameters",
                local.Parameters,
                source.Parameters,
                item => item.Name,
                item => item.Name,
                item => item.Description,
                item => "模板参数已更改");
            if (!ContentEquals(local.Definition, source.Definition))
                changes.Add(new TriggerAuthoringSourceImportChange
                {
                    Kind = TriggerAuthoringSourceChangeKind.Modified,
                    Area = "触发器原型",
                    Path = "template.definition",
                    Summary = "完整触发器配置已更改",
                    Before = local.Definition.Name,
                    After = source.Definition.Name
                });
            return changes;
        }

        private static void AddValueChange(
            ICollection<TriggerAuthoringSourceImportChange> changes,
            string area,
            string path,
            string summary,
            string before,
            string after)
        {
            if (string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal)) return;
            changes.Add(new TriggerAuthoringSourceImportChange
            {
                Kind = TriggerAuthoringSourceChangeKind.Modified,
                Area = area,
                Path = path,
                Summary = summary,
                Before = before,
                After = after
            });
        }

        private static void AddNodeChange(
            ICollection<TriggerAuthoringSourceImportChange> changes,
            string area,
            string path,
            string summary,
            TriggerNodeData before,
            TriggerNodeData after)
        {
            if (ContentEquals(before, after)) return;
            changes.Add(new TriggerAuthoringSourceImportChange
            {
                Kind = TriggerAuthoringSourceChangeKind.Modified,
                Area = area,
                Path = path,
                Summary = summary,
                Before = DescribeNode(before),
                After = DescribeNode(after)
            });
        }

        private static void AddListChanges<T>(
            ICollection<TriggerAuthoringSourceImportChange> changes,
            string area,
            string pathPrefix,
            IList<T> local,
            IList<T> source,
            Func<T, string> keySelector,
            Func<T, string> labelSelector,
            Func<T, string> renameSelector,
            Func<T, string> modifiedSummary)
            where T : class
        {
            var localByKey = IndexByKey(local, keySelector);
            var sourceByKey = IndexByKey(source, keySelector);

            foreach (var pair in sourceByKey)
            {
                if (!localByKey.TryGetValue(pair.Key, out var localItem))
                {
                    changes.Add(new TriggerAuthoringSourceImportChange
                    {
                        Kind = TriggerAuthoringSourceChangeKind.Added,
                        Area = area,
                        Path = pathPrefix + "[" + pair.Key + "]",
                        Summary = "新增“" + Display(labelSelector(pair.Value)) + "”",
                        After = labelSelector(pair.Value)
                    });
                    continue;
                }

                if (renameSelector != null)
                {
                    var beforeName = renameSelector(localItem);
                    var afterName = renameSelector(pair.Value);
                    if (!string.Equals(beforeName ?? string.Empty, afterName ?? string.Empty, StringComparison.Ordinal))
                    {
                        changes.Add(new TriggerAuthoringSourceImportChange
                        {
                            Kind = TriggerAuthoringSourceChangeKind.Renamed,
                            Area = area,
                            Path = pathPrefix + "[" + pair.Key + "]",
                            Summary = "重命名为“" + Display(labelSelector(pair.Value)) + "”",
                            Before = beforeName,
                            After = afterName
                        });
                    }
                }

                if (ContentEquals(localItem, pair.Value)) continue;
                changes.Add(new TriggerAuthoringSourceImportChange
                {
                    Kind = TriggerAuthoringSourceChangeKind.Modified,
                    Area = area,
                    Path = pathPrefix + "[" + pair.Key + "]",
                    Summary = modifiedSummary(pair.Value),
                    Before = labelSelector(localItem),
                    After = labelSelector(pair.Value)
                });
            }

            foreach (var pair in localByKey)
            {
                if (sourceByKey.ContainsKey(pair.Key)) continue;
                changes.Add(new TriggerAuthoringSourceImportChange
                {
                    Kind = TriggerAuthoringSourceChangeKind.Removed,
                    Area = area,
                    Path = pathPrefix + "[" + pair.Key + "]",
                    Summary = "删除“" + Display(labelSelector(pair.Value)) + "”",
                    Before = labelSelector(pair.Value)
                });
            }
        }

        private static Dictionary<string, T> IndexByKey<T>(
            IList<T> items,
            Func<T, string> keySelector)
            where T : class
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            if (items == null) return result;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                var key = keySelector(item);
                if (string.IsNullOrWhiteSpace(key)) key = "#" + i;
                if (!result.ContainsKey(key)) result.Add(key, item);
            }
            return result;
        }

        private static bool ContentEquals(object before, object after)
        {
            if (before == null || after == null) return before == after;
            return string.Equals(
                TriggerSourceCanonical.ComputeContentHash(before),
                TriggerSourceCanonical.ComputeContentHash(after),
                StringComparison.Ordinal);
        }

        private static string DescribeNode(TriggerNodeData node)
        {
            if (node == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(node.GroupReference)) return "group:" + node.GroupReference;
            if (!string.IsNullOrWhiteSpace(node.Type)) return node.Kind + ":" + node.Type;
            return node.Kind.ToString();
        }

        private static string Display(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<空>" : value;
        }
    }

    internal static class TriggerAuthoringSourceImportPreviewDialog
    {
        public static bool Confirm(TriggerAuthoringSourceImportPreview preview)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            return TriggerAuthoringSourceImportPreviewWindow.Show(preview);
        }
    }

    internal sealed class TriggerAuthoringSourceImportPreviewWindow : EditorWindow
    {
        private const float DefaultWidth = 680f;
        private const float DefaultHeight = 560f;

        private TriggerAuthoringSourceImportPreview _preview;
        private EditorDiagnosticCollection _diagnostics;
        private EditorSearchState _diagnosticSearch;
        private Vector2 _summaryScroll;
        private Vector2 _changeScroll;
        private Vector2 _diagnosticScroll;
        private bool _accepted;

        public static bool Show(TriggerAuthoringSourceImportPreview preview)
        {
            var window = CreateInstance<TriggerAuthoringSourceImportPreviewWindow>();
            window.Initialize(preview);
            window.ShowModalUtility();
            return window._accepted;
        }

        private void Initialize(TriggerAuthoringSourceImportPreview preview)
        {
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _diagnostics = TriggerAuthoringDiagnosticAdapter.Adapt(_preview.Diagnostics);
            _diagnosticSearch = new EditorSearchState();
            titleContent = new GUIContent(_preview.Kind == TriggerAuthoringSourcePreviewKind.Module
                ? "触发器源文件导入预览"
                : "触发器模板导入预览");
            minSize = new Vector2(DefaultWidth, DefaultHeight);
            maxSize = new Vector2(1200f, 900f);
            position = new Rect(
                (Screen.currentResolution.width - DefaultWidth) * 0.5f,
                (Screen.currentResolution.height - DefaultHeight) * 0.5f,
                DefaultWidth,
                DefaultHeight);
        }

        private void OnGUI()
        {
            if (_preview == null)
            {
                Close();
                return;
            }

            DrawHeader();
            DrawSummary();
            DrawChanges();
            DrawDiagnostics();
            GUILayout.FlexibleSpace();
            DrawActions();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(
                _preview.Kind == TriggerAuthoringSourcePreviewKind.Module
                    ? "导入触发器模块 Source JSON"
                    : "导入触发器模板 Source JSON",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(TriggerAuthoringEditorIntegration.T("sync-state"), TriggerAuthoringSourceImportPreview.SyncStateLabel(_preview.State), EditorStyles.miniBoldLabel);
            if (!string.IsNullOrWhiteSpace(_preview.SourcePath))
            {
                EditorGUILayout.SelectableLabel(_preview.SourcePath, EditorStyles.miniLabel, GUILayout.Height(18f));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("open-source-json"), EditorStyles.miniButtonLeft))
                    EditorUtility.OpenWithDefaultApp(_preview.SourcePath);
                if (GUILayout.Button(TriggerAuthoringEditorIntegration.T("reveal-source"), EditorStyles.miniButtonRight))
                    EditorUtility.RevealInFinder(_preview.SourcePath);
                EditorGUILayout.EndHorizontal();
            }
            if (_preview.RequiresForce)
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("overwrite-warning"), MessageType.Warning);
            if (!_preview.CanImport)
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(_preview.Message)
                    ? "无法导入 Source JSON。"
                    : _preview.Message, MessageType.Error);
            else if (!string.IsNullOrWhiteSpace(_preview.Message))
                EditorGUILayout.HelpBox(_preview.Message, MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField(TriggerAuthoringEditorIntegration.T("summary"), EditorStyles.boldLabel);
            _summaryScroll = EditorGUILayout.BeginScrollView(_summaryScroll, EditorStyles.helpBox, GUILayout.Height(110f));
            DrawValueRow("标识", _preview.AssetIdentity, _preview.SourceIdentity);
            DrawValueRow("显示名称", _preview.AssetDisplayName, _preview.SourceDisplayName);
            if (_preview.Kind == TriggerAuthoringSourcePreviewKind.Module)
            {
                DrawCountRow("触发器", _preview.AssetTriggerCount, _preview.SourceTriggerCount);
                DrawCountRow("黑板变量", _preview.AssetBlackboardCount, _preview.SourceBlackboardCount);
                DrawCountRow("条件分组", _preview.AssetConditionGroupCount, _preview.SourceConditionGroupCount);
                DrawCountRow("行为分组", _preview.AssetActionGroupCount, _preview.SourceActionGroupCount);
            }
            else
            {
                DrawCountRow("参数", _preview.AssetTemplateParameterCount, _preview.SourceTemplateParameterCount);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawChanges()
        {
            EditorGUILayout.LabelField(
                "变更（" + (_preview.Changes != null ? _preview.Changes.Count : 0) + "）",
                EditorStyles.boldLabel);
            _changeScroll = EditorGUILayout.BeginScrollView(_changeScroll, EditorStyles.helpBox, GUILayout.MinHeight(120f));
            if (_preview.Changes == null || _preview.Changes.Count == 0)
            {
                EditorGUILayout.HelpBox(TriggerAuthoringEditorIntegration.T("no-structural-changes"), MessageType.Info);
            }
            else
            {
                for (var i = 0; i < _preview.Changes.Count; i++)
                {
                    var change = _preview.Changes[i];
                    if (change == null) continue;
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(TriggerAuthoringSourceImportChange.ChangeKindLabel(change.Kind) + "  " + change.Area, EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(change.Summary ?? string.Empty, EditorStyles.wordWrappedLabel);
                    if (!string.IsNullOrWhiteSpace(change.Path))
                        EditorGUILayout.SelectableLabel(change.Path, EditorStyles.miniLabel, GUILayout.Height(18f));
                    if (!string.IsNullOrWhiteSpace(change.Before) || !string.IsNullOrWhiteSpace(change.After))
                        EditorGUILayout.LabelField(Display(change.Before) + " -> " + Display(change.After), EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawDiagnostics()
        {
            if (_diagnostics == null || _diagnostics.Items.Count == 0) return;
            EditorGUILayout.LabelField(
                "诊断（错误 " + _diagnostics.ErrorCount + "，警告 " + _diagnostics.WarningCount + "）",
                EditorStyles.boldLabel);
            EditorImGuiControls.DrawDiagnostics(
                _diagnostics,
                TriggerAuthoringEditorIntegration.Localization,
                _diagnosticSearch,
                EditorDiagnosticSeverity.Info,
                ref _diagnosticScroll);
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            if (_preview.CanImport)
            {
                var label = _preview.RequiresForce ? "强制导入" : "导入";
                if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(120f)))
                {
                    _accepted = true;
                    Close();
                }
            }
            if (GUILayout.Button(_preview.CanImport ? "取消" : "关闭", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                _accepted = false;
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawValueRow(string label, string before, string after)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120f));
            EditorGUILayout.LabelField(Display(before), GUILayout.MinWidth(120f));
            EditorGUILayout.LabelField("->", GUILayout.Width(20f));
            EditorGUILayout.LabelField(Display(after), GUILayout.MinWidth(120f));
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawCountRow(string label, int before, int after)
        {
            DrawValueRow(label, before.ToString(), after.ToString());
        }

        private static string Display(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<空>" : value;
        }
    }

    internal static class TriggerAuthoringSourceCodec
    {
        public static TriggerAuthoringSourceDocument CreateDocument(TriggerAuthoringModuleAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return new TriggerAuthoringSourceDocument
            {
                Schema = TriggerAuthoringSchema.Id,
                Version = TriggerAuthoringSchema.Version,
                Metadata = asset.Metadata ?? new TriggerAuthoringSourceMetadata(),
                Module = asset.Module ?? new TriggerAuthoringModuleData()
            };
        }

        public static string Serialize(TriggerAuthoringSourceDocument document)
        {
            return TriggerSourceCodecs.ModuleDefault.Serialize(document);
        }

        public static TriggerAuthoringSourceDocument Deserialize(string json)
        {
            return TriggerSourceCodecs.ModuleDefault.Deserialize(json);
        }

        public static TriggerAuthoringSourceDocument ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("必须提供 Source 路径。", nameof(path));
            return ResolveCodec(path).Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }

        public static string ComputeContentHash(TriggerAuthoringSourceDocument document)
        {
            TriggerSourceDocumentRules.ValidateModuleHeader(document);
            return TriggerSourceCanonical.ComputeContentHash(document);
        }

        public static void WriteFileAtomic(string path, TriggerAuthoringSourceDocument document)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("必须提供 Source 路径。", nameof(path));
            TriggerSourceCanonical.WriteTextAtomic(path, ResolveCodec(path).Serialize(document));
        }

        private static ITriggerSourceCodec<TriggerAuthoringSourceDocument> ResolveCodec(string path)
        {
            if (!TriggerSourceCodecs.TryResolveModule(path, out var codec))
                throw new InvalidDataException(
                    "没有为扩展名“" +
                    (Path.GetExtension(path) ?? string.Empty) +
                    "”注册触发器 Source 编解码器。支持的格式：" + TriggerSourceCodecs.DescribeModuleExtensions() + "。");
            return codec;
        }
    }

    internal static class TriggerAuthoringSourceSync
    {
        public static TriggerAuthoringSourceImportPreview PreviewImport(
            TriggerAuthoringModuleAsset asset,
            string sourcePath = null)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return TriggerAuthoringSourceImportPreview.Failed(
                    TriggerAuthoringSourcePreviewKind.Module,
                    TriggerAuthoringSyncState.SourceMissing,
                    sourcePath,
                    "Source JSON 文件不存在。");

            TriggerAuthoringSourceDocument document;
            try
            {
                document = TriggerAuthoringSourceCodec.ReadFile(sourcePath);
            }
            catch (Exception ex)
            {
                return TriggerAuthoringSourceImportPreview.Failed(
                    TriggerAuthoringSourcePreviewKind.Module,
                    TriggerAuthoringSyncState.InvalidSource,
                    sourcePath,
                    ex.Message);
            }

            var preview = CreateModulePreview(asset, document.Module, sourcePath);
            var currentModuleId = asset.Module != null ? asset.Module.ModuleId : null;
            var incomingModuleId = document.Module.ModuleId;
            if (!string.IsNullOrWhiteSpace(currentModuleId) &&
                !string.Equals(currentModuleId, incomingModuleId, StringComparison.Ordinal))
            {
                preview.Message = $"模块标识不匹配。资产='{currentModuleId}'，Source='{incomingModuleId ?? string.Empty}'。";
                return preview;
            }

            preview.Diagnostics = TriggerAuthoringValidator.Validate(
                document.Module,
                TriggerAuthoringValidationContext.Create(asset));
            if (TriggerAuthoringValidator.HasErrors(preview.Diagnostics))
            {
                preview.State = TriggerAuthoringSyncState.InvalidSource;
                preview.Message = BuildValidationMessage(preview.Diagnostics);
                return preview;
            }

            var inspection = Inspect(asset, sourcePath);
            var assessment = EditorSourceSyncOperationPolicy.Assess(
                inspection.PlatformInspection,
                EditorSourceSyncDirection.Import,
                HasAuthoredContent(asset.Module));
            preview.State = inspection.State;
            preview.RequiresForce = assessment.RequiresForce;
            preview.CanImport = assessment.CanExecute;
            preview.Success = assessment.CanExecute;
            if (!assessment.CanExecute)
                preview.Message = inspection.Error ?? "当前同步状态下无法导入 Source JSON。";
            return preview;
        }

        public static TriggerAuthoringSyncInspection Inspect(TriggerAuthoringModuleAsset asset, string sourcePath = null)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            var assetHash = TriggerAuthoringSourceCodec.ComputeContentHash(TriggerAuthoringSourceCodec.CreateDocument(asset));
            var inspection = new TriggerAuthoringSyncInspection
            {
                SourcePath = sourcePath,
                AssetHash = assetHash,
                State = TriggerAuthoringSyncState.Untracked
            };

            if (string.IsNullOrWhiteSpace(sourcePath)) return inspection;
            inspection.SourceExists = File.Exists(sourcePath);
            var sourceIsValid = true;
            if (inspection.SourceExists)
            {
                try
                {
                    var source = TriggerAuthoringSourceCodec.ReadFile(sourcePath);
                    inspection.SourceHash = TriggerAuthoringSourceCodec.ComputeContentHash(source);
                }
                catch (Exception ex)
                {
                    sourceIsValid = false;
                    inspection.Error = ex.Message;
                }
            }

            var platformInspection = EditorSourceSyncClassifier.Inspect(
                new EditorSourceSyncSnapshot(
                    assetHash,
                    inspection.SourceHash ?? string.Empty,
                    asset.LastSynchronizedHash ?? string.Empty,
                    isTracked: inspection.SourceExists || !string.IsNullOrEmpty(asset.LastSynchronizedHash),
                    sourceExists: inspection.SourceExists,
                    sourceIsValid: sourceIsValid,
                    sourcePath: sourcePath,
                    error: inspection.Error));
            inspection.PlatformInspection = platformInspection;
            inspection.State = MapState(platformInspection.State);
            return inspection;
        }

        public static TriggerAuthoringSyncResult Export(
            TriggerAuthoringModuleAsset asset,
            string sourcePath = null,
            bool force = false)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.Untracked, "必须提供 Source JSON 路径。");

            var diagnostics = TriggerAuthoringValidator.Validate(
                asset.Module,
                TriggerAuthoringValidationContext.Create(asset));
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.AssetChanged, BuildValidationMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            var assessment = EditorSourceSyncOperationPolicy.Assess(
                inspection.PlatformInspection,
                EditorSourceSyncDirection.Export);
            if (!force && assessment.RequiresForce)
                return TriggerAuthoringSyncResult.Failed(
                    inspection.State,
                    inspection.Error ?? "Source JSON 包含将被覆盖的改动。",
                    true);

            var document = TriggerAuthoringSourceCodec.CreateDocument(asset);
            TriggerAuthoringSourceCodec.WriteFileAtomic(sourcePath, document);
            var hash = TriggerAuthoringSourceCodec.ComputeContentHash(document);
            asset.MarkSynchronized(NormalizePath(sourcePath), hash);
            EditorUtility.SetDirty(asset);
            return TriggerAuthoringSyncResult.Succeeded(TriggerAuthoringSyncState.InSync, hash);
        }

        public static TriggerAuthoringSyncResult Import(
            TriggerAuthoringModuleAsset asset,
            string sourcePath = null,
            bool force = false)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.SourceMissing, "Source JSON 文件不存在。");

            TriggerAuthoringSourceDocument document;
            try
            {
                document = TriggerAuthoringSourceCodec.ReadFile(sourcePath);
            }
            catch (Exception ex)
            {
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.InvalidSource, ex.Message);
            }

            var currentModuleId = asset.Module != null ? asset.Module.ModuleId : null;
            var incomingModuleId = document.Module.ModuleId;
            if (!string.IsNullOrWhiteSpace(currentModuleId) &&
                !string.Equals(currentModuleId, incomingModuleId, StringComparison.Ordinal))
            {
                return TriggerAuthoringSyncResult.Failed(
                    TriggerAuthoringSyncState.Conflict,
                    $"模块标识不匹配。资产='{currentModuleId}'，Source='{incomingModuleId ?? string.Empty}'。");
            }

            var diagnostics = TriggerAuthoringValidator.Validate(
                document.Module,
                TriggerAuthoringValidationContext.Create(asset));
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.InvalidSource, BuildValidationMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            var assessment = EditorSourceSyncOperationPolicy.Assess(
                inspection.PlatformInspection,
                EditorSourceSyncDirection.Import,
                HasAuthoredContent(asset.Module));
            if (!force && assessment.RequiresForce)
                return TriggerAuthoringSyncResult.Failed(
                    inspection.State,
                    "资产包含将被覆盖的改动。",
                    true);

            Undo.RecordObject(asset, "导入触发器 Source JSON");
            asset.Metadata = document.Metadata ?? new TriggerAuthoringSourceMetadata();
            asset.Module = document.Module;
            var hash = TriggerAuthoringSourceCodec.ComputeContentHash(document);
            asset.MarkSynchronized(NormalizePath(sourcePath), hash);
            EditorUtility.SetDirty(asset);
            return TriggerAuthoringSyncResult.Succeeded(TriggerAuthoringSyncState.InSync, hash);
        }

        private static TriggerAuthoringSyncState MapState(EditorSourceSyncState state)
        {
            return state switch
            {
                EditorSourceSyncState.Untracked => TriggerAuthoringSyncState.Untracked,
                EditorSourceSyncState.InSync => TriggerAuthoringSyncState.InSync,
                EditorSourceSyncState.LocalChanged => TriggerAuthoringSyncState.AssetChanged,
                EditorSourceSyncState.SourceChanged => TriggerAuthoringSyncState.JsonChanged,
                EditorSourceSyncState.Conflict => TriggerAuthoringSyncState.Conflict,
                EditorSourceSyncState.SourceMissing => TriggerAuthoringSyncState.SourceMissing,
                EditorSourceSyncState.InvalidSource => TriggerAuthoringSyncState.InvalidSource,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "未知的 Source 同步状态。")
            };
        }

        private static bool HasAuthoredContent(TriggerAuthoringModuleData module)
        {
            return module != null &&
                   (!string.IsNullOrWhiteSpace(module.ModuleId) ||
                    (module.Blackboard != null && module.Blackboard.Count > 0) ||
                    (module.ConditionGroups != null && module.ConditionGroups.Count > 0) ||
                    (module.ActionGroups != null && module.ActionGroups.Count > 0) ||
                    (module.Triggers != null && module.Triggers.Count > 0));
        }

        private static TriggerAuthoringSourceImportPreview CreateModulePreview(
            TriggerAuthoringModuleAsset asset,
            TriggerAuthoringModuleData source,
            string sourcePath)
        {
            var local = asset.Module ?? new TriggerAuthoringModuleData();
            return new TriggerAuthoringSourceImportPreview
            {
                Kind = TriggerAuthoringSourcePreviewKind.Module,
                SourcePath = sourcePath ?? string.Empty,
                State = TriggerAuthoringSyncState.Conflict,
                AssetIdentity = local.ModuleId,
                SourceIdentity = source != null ? source.ModuleId : string.Empty,
                AssetDisplayName = local.DisplayName,
                SourceDisplayName = source != null ? source.DisplayName : string.Empty,
                AssetTriggerCount = Count(local.Triggers),
                SourceTriggerCount = Count(source != null ? source.Triggers : null),
                AssetBlackboardCount = Count(local.Blackboard),
                SourceBlackboardCount = Count(source != null ? source.Blackboard : null),
                AssetConditionGroupCount = Count(local.ConditionGroups),
                SourceConditionGroupCount = Count(source != null ? source.ConditionGroups : null),
                AssetActionGroupCount = Count(local.ActionGroups),
                SourceActionGroupCount = Count(source != null ? source.ActionGroups : null),
                Changes = TriggerAuthoringSourceImportDiff.Compare(local, source)
            };
        }

        private static int Count<T>(ICollection<T> values)
        {
            return values != null ? values.Count : 0;
        }

        private static string BuildValidationMessage(IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            var builder = new StringBuilder("触发器编辑数据校验失败：");
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Severity != TriggerAuthoringDiagnosticSeverity.Error) continue;
                builder.AppendLine();
                builder.Append(diagnostic.Code).Append(" ").Append(diagnostic.Path).Append(": ").Append(diagnostic.Message);
            }
            return builder.ToString();
        }

        private static string ResolveSourcePath(TriggerAuthoringModuleAsset asset, string sourcePath)
        {
            if (!string.IsNullOrWhiteSpace(sourcePath)) return Path.GetFullPath(sourcePath);
            if (string.IsNullOrWhiteSpace(asset.SourceJsonPath)) return string.Empty;
            if (Path.IsPathRooted(asset.SourceJsonPath)) return Path.GetFullPath(asset.SourceJsonPath);
            return Path.GetFullPath(Path.Combine(GetProjectRoot(), asset.SourceJsonPath));
        }

        private static string NormalizePath(string sourcePath)
        {
            var fullPath = Path.GetFullPath(sourcePath);
            var projectRoot = AppendDirectorySeparator(Path.GetFullPath(GetProjectRoot()));
            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Replace('\\', '/');

            var rootUri = new Uri(projectRoot, UriKind.Absolute);
            var fileUri = new Uri(fullPath, UriKind.Absolute);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('\\', '/');
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) return path;
            return path + Path.DirectorySeparatorChar;
        }
    }
}
#endif
