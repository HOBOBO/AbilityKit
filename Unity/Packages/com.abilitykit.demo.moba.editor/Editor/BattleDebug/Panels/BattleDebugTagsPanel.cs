using System;
using System.IO;
using System.Linq;
using AbilityKit.Demo.Moba.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    internal sealed class BattleDebugTagsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        private MobaTagDebugSnapshot _snapshot;
        private MobaTagDebugSnapshot _baseline;
        private object _session;
        private long _actorId;
        private bool _offline;
        private bool _includeRules;
        private bool _showSources = true;
        private bool _showGates = true;
        private bool _showRules;
        private bool _showComparison = true;
        private string _error;
        private Vector2 _scroll;

        public string Name => "标签";
        public int Order => 100;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Actor;
        public bool OwnsScrollView => true;
        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            object session = null;
            if (!ctx.IsOffline && ctx.Facade != null &&
                ctx.Facade.TryGetSession(out var liveSession))
                session = liveSession;
            if (_actorId != ctx.SelectedId.ActorId || !ReferenceEquals(_session, session) ||
                _offline != ctx.IsOffline)
            {
                _snapshot = null;
                _baseline = null;
                _error = null;
                _actorId = ctx.SelectedId.ActorId;
                _session = session;
                _offline = ctx.IsOffline;
            }

            if (!ctx.HasSelection)
            {
                EditorGUILayout.HelpBox("请先选择一个 Actor。", MessageType.Info);
                return;
            }

            DrawToolbar(in ctx);
            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Warning);
            if (_snapshot == null)
            {
                EditorGUILayout.HelpBox(ctx.IsOffline
                    ? "离线诊断 Artifact 不包含持续对象标签快照。可导入此前保存的标签快照。"
                    : "点击抓取，读取当前 Actor 的有效标签、持续来源与门禁。页面重绘不会重新读取战斗世界。",
                    MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSnapshot(_snapshot);
            if (_baseline != null)
            {
                _showComparison = EditorGUILayout.Foldout(_showComparison, "A/B 对比", true);
                if (_showComparison) DrawComparison(_baseline, _snapshot);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar(in BattleDebugContext ctx)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Actor #{ctx.SelectedId.ActorId}", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            _includeRules = GUILayout.Toggle(_includeRules, "包含规则树", EditorStyles.toolbarButton);
            EditorGUI.BeginDisabledGroup(ctx.IsOffline || _session == null || ctx.SelectedId.ActorId > int.MaxValue);
            if (GUILayout.Button("抓取", EditorStyles.toolbarButton, GUILayout.Width(52)))
                Capture(in ctx);
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(_snapshot == null);
            if (GUILayout.Button("设为 A", EditorStyles.toolbarButton, GUILayout.Width(55)))
                _baseline = _snapshot;
            if (GUILayout.Button("导出", EditorStyles.toolbarButton, GUILayout.Width(48)))
                Export();
            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("导入", EditorStyles.toolbarButton, GUILayout.Width(48)))
                Import();
            EditorGUILayout.EndHorizontal();
        }

        private void Capture(in BattleDebugContext ctx)
        {
            try
            {
                if (ctx.Facade == null || !ctx.Facade.TryGetSession(out var session) || session == null ||
                    !session.TryGetWorld(out var world) || world?.Services == null ||
                    !world.Services.TryResolve<MobaTagDebugSnapshotService>(out var service) || service == null)
                    throw new InvalidOperationException("当前战斗世界没有标签快照服务。");
                _snapshot = service.Capture((int)ctx.SelectedId.ActorId, session.WorldId.ToString(), _includeRules);
                _error = null;
                ctx.RequestRepaint?.Invoke();
            }
            catch (Exception exception)
            {
                _error = exception.Message;
            }
        }

        private void Export()
        {
            var path = EditorUtility.SaveFilePanel("导出标签快照", string.Empty,
                $"moba-tags-{_snapshot.ActorId}-frame-{_snapshot.Frame}", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(_snapshot, Formatting.Indented));
                _error = null;
            }
            catch (Exception exception)
            {
                _error = exception.Message;
            }
        }

        private void Import()
        {
            var path = EditorUtility.OpenFilePanel("导入标签快照", string.Empty, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                var json = JObject.Parse(File.ReadAllText(path));
                if (json.Value<int?>("SchemaVersion") != 1 || json.Value<int?>("ActorId") <= 0 ||
                    json["Tags"] is not JArray || json["Sources"] is not JArray ||
                    json["Gates"] is not JArray || json["ResultFlags"] is not JArray ||
                    json["Rules"] is not JArray)
                    throw new InvalidDataException("标签快照格式或版本无效。");
                var snapshot = json.ToObject<MobaTagDebugSnapshot>();
                if (snapshot == null || snapshot.CapturedUtcTicks < DateTime.MinValue.Ticks ||
                    snapshot.CapturedUtcTicks > DateTime.MaxValue.Ticks ||
                    snapshot.Tags == null || snapshot.Sources == null || snapshot.Gates == null ||
                    snapshot.ResultFlags == null || snapshot.Rules == null)
                    throw new InvalidDataException("标签快照格式或版本无效。");
                _snapshot = snapshot;
                _error = null;
            }
            catch (Exception exception)
            {
                _error = exception.Message;
            }
        }

        private void DrawSnapshot(MobaTagDebugSnapshot snapshot)
        {
            EditorGUILayout.LabelField($"快照 B  Actor #{snapshot.ActorId}  世界={snapshot.WorldId}  帧={snapshot.Frame}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"抓取时间={new DateTime(snapshot.CapturedUtcTicks, DateTimeKind.Utc):yyyy-MM-dd HH:mm:ss} UTC  标签配置版本={snapshot.BundleVersion}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"有效标签 {snapshot.Tags.Length}  持续来源 {snapshot.Sources.Length}  结果位 0x{snapshot.ResultMask:X16}",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("有效标签", EditorStyles.boldLabel);
            if (snapshot.Tags.Length == 0)
                EditorGUILayout.LabelField("（无）", EditorStyles.miniLabel);
            foreach (var tag in snapshot.Tags)
            {
                if (tag == null) continue;
                var sources = tag.SourceIndices == null ? string.Empty : string.Join(", ", tag.SourceIndices.Select(index => $"#{index}"));
                EditorGUILayout.LabelField($"{tag.Name}  [{tag.Id}]  {tag.DisplayName}  父级={tag.ParentId}  来源={sources}",
                    EditorStyles.wordWrappedMiniLabel);
            }

            _showSources = EditorGUILayout.Foldout(_showSources, $"持续来源与生命周期 ({snapshot.Sources.Length})", true);
            if (_showSources)
            {
                foreach (var source in snapshot.Sources)
                    DrawSource(source, snapshot.ResultMask);
            }

            _showGates = EditorGUILayout.Foldout(_showGates, $"结果位与行为门禁 ({snapshot.Gates.Length})", true);
            if (_showGates)
            {
                EditorGUILayout.LabelField("命中结果位：" +
                    (snapshot.ResultFlags.Length == 0 ? "（无）" : string.Join(", ", snapshot.ResultFlags)),
                    EditorStyles.wordWrappedMiniLabel);
                foreach (var gate in snapshot.Gates)
                {
                    if (gate == null) continue;
                    EditorGUILayout.LabelField($"{gate.Name} [{gate.Key}/{gate.Id}]  {(gate.Blocked ? "阻断" : "通过")}" +
                        (gate.Blocked ? "  命中=" + string.Join(", ", gate.BlockingFlags ?? Array.Empty<string>()) : string.Empty),
                        EditorStyles.wordWrappedMiniLabel);
                }
            }

            _showRules = EditorGUILayout.Foldout(_showRules, $"规则映射 ({snapshot.Rules.Length})", true);
            if (_showRules)
            {
                if (!snapshot.HasRuleDetails)
                    EditorGUILayout.HelpBox("本次未抓取规则树。勾选“包含规则树”后再次抓取。", MessageType.Info);
                foreach (var rule in snapshot.Rules)
                {
                    if (rule == null) continue;
                    EditorGUILayout.LabelField($"{rule.ResultFlag}  ←  {rule.GroupName} [{rule.GroupId}]  {(rule.Matches ? "命中" : "未命中")}",
                        EditorStyles.miniBoldLabel);
                    foreach (var node in rule.Nodes ?? Array.Empty<MobaTagDebugRuleNode>())
                    {
                        if (node == null) continue;
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(Math.Min(node.Depth, 16) * 16);
                        EditorGUILayout.LabelField($"{node.Operator} {(node.TagId > 0 ? node.TagName : node.RefGroupId > 0 ? "组 " + node.RefGroupId : string.Empty)}" +
                            $"  {(node.Matches ? "通过" : "失败")}  来源={string.Join(", ", (node.SourceIndices ?? Array.Empty<int>()).Select(index => "#" + index))}",
                            EditorStyles.wordWrappedMiniLabel);
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private static void DrawSource(MobaTagDebugSource source, ulong resultMask)
        {
            if (source == null) return;
            EditorGUILayout.LabelField($"#{source.Index} {source.Kind}  配置={source.ConfigId}  实例={source.InstanceId}",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"状态={source.State}  {(source.IsEffective ? "标签生效" : "标签未生效")}  层数={source.Stack}" +
                $"  已持续={source.ElapsedSeconds:0.###}s  剩余={source.RemainingSeconds:0.###}s",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"来源 Actor={source.SourceActorId}  目标 Actor={source.TargetActorId}" +
                $"  来源上下文={source.SourceContextId}  根上下文={source.RootContextId}" +
                $"  授予标签={string.Join(", ", source.GrantedTagNames ?? Array.Empty<string>())}",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField($"当前判定={source.CurrentAction} {source.CurrentReason}  最近判定={source.LastAction} {source.LastReason}" +
                $"  执行门禁={((source.ExecutionBlockedMask & resultMask) != 0 ? "阻断" : "通过")}" +
                $"  配置掩码=0x{source.ExecutionBlockedMask:X16}", EditorStyles.wordWrappedMiniLabel);
            foreach (var check in source.Requirements ?? Array.Empty<MobaTagDebugRequirement>())
            {
                if (check == null) continue;
                EditorGUILayout.LabelField($"  {check.Category}: {(check.Satisfied ? "通过" : "失败")}" +
                    $"  必需={string.Join(", ", check.RequiredTags ?? Array.Empty<string>())}" +
                    $"  阻断={string.Join(", ", check.BlockedTags ?? Array.Empty<string>())}",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawComparison(MobaTagDebugSnapshot before, MobaTagDebugSnapshot after)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField($"A/B 对比  A: 帧 {before.Frame}  B: 帧 {after.Frame}", EditorStyles.boldLabel);
            if (before.ActorId != after.ActorId || !string.Equals(before.WorldId, after.WorldId, StringComparison.Ordinal))
                EditorGUILayout.HelpBox("两个快照来自不同 Actor 或世界；请核对比较对象。", MessageType.Warning);
            if (!string.Equals(before.BundleVersion, after.BundleVersion, StringComparison.Ordinal))
                EditorGUILayout.HelpBox("两个快照使用不同标签配置版本。", MessageType.Warning);
            DrawDifference("新增标签", after.Tags.Where(tag => tag != null).Select(tag => tag.Name)
                .Except(before.Tags.Where(tag => tag != null).Select(tag => tag.Name)));
            DrawDifference("移除标签", before.Tags.Where(tag => tag != null).Select(tag => tag.Name)
                .Except(after.Tags.Where(tag => tag != null).Select(tag => tag.Name)));
            DrawDifference("新增结果位", after.ResultFlags.Except(before.ResultFlags));
            DrawDifference("移除结果位", before.ResultFlags.Except(after.ResultFlags));
            DrawDifference("门禁变化", after.Gates.Where(gate => gate != null &&
                before.Gates.Any(old => old != null && old.Id == gate.Id && old.Blocked != gate.Blocked))
                .Select(gate => gate.Key + (gate.Blocked ? " → 阻断" : " → 通过")));
            var beforeSources = before.Sources.Where(source => source != null && source.IsEffective)
                .GroupBy(SourceKey).ToDictionary(group => group.Key, group => group.Count());
            var afterSources = after.Sources.Where(source => source != null && source.IsEffective)
                .GroupBy(SourceKey).ToDictionary(group => group.Key, group => group.Count());
            DrawDifference("来源变化", beforeSources.Keys.Union(afterSources.Keys)
                .Where(key => Count(beforeSources, key) != Count(afterSources, key))
                .Select(key => $"{key}: {Count(beforeSources, key)} → {Count(afterSources, key)}"));
        }

        private static string SourceKey(MobaTagDebugSource source) =>
            $"{source.Kind}/{source.ConfigId}/{source.InstanceId}";

        private static int Count(System.Collections.Generic.IDictionary<string, int> counts, string key) =>
            counts.TryGetValue(key, out var value) ? value : 0;

        private static void DrawDifference(string label, System.Collections.Generic.IEnumerable<string> values)
        {
            var entries = values.ToArray();
            EditorGUILayout.LabelField(label + "：" + (entries.Length == 0 ? "（无）" : string.Join(", ", entries)),
                EditorStyles.wordWrappedMiniLabel);
        }
    }
}
