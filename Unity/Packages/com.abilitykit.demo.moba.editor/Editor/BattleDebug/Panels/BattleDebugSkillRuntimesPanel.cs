using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// Displays live skill cast runtime snapshots from the active logic world.
    /// </summary>
    internal sealed class BattleDebugSkillRuntimesPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        private readonly List<MobaSkillRuntimeDiagnostics> _items = new List<MobaSkillRuntimeDiagnostics>(32);
        private Vector2 _scroll;
        private MobaSkillCastRuntimeHandle _selectedRuntime;
        private double _nextRefreshAt;

        public string Name => "技能运行时";
        public int Order => 390;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            var service = ctx.SkillRuntimeService;
            if (service == null)
            {
                var message = ctx.IsOffline
                    ? "离线 Artifact 不包含活动技能运行时快照。可通过诊断事件查看已记录的技能生命周期。"
                    : "当前 World 未解析到 MobaSkillCastRuntimeService；技能运行时面板无法读取实时施放状态。";
                EditorGUILayout.HelpBox(message, MessageType.Info);
                return;
            }

            DrawToolbar(service, ctx.RequestRepaint);
            RefreshIfNeeded(service, force: false);
            DrawSummary(service);
            DrawRuntimeList(in ctx);
        }

        private void DrawToolbar(MobaSkillCastRuntimeService service, Action requestRepaint)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("实时技能运行时", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshIfNeeded(service, force: true);
                requestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshIfNeeded(MobaSkillCastRuntimeService service, bool force)
        {
            var now = EditorApplication.timeSinceStartup;
            if (!force && now < _nextRefreshAt) return;

            _nextRefreshAt = now + 0.15d;
            _items.Clear();
            service.CopyDiagnosticsTo(_items);
            _items.Sort((left, right) => right.Handle.RuntimeId.CompareTo(left.Handle.RuntimeId));

        }

        private void DrawSummary(MobaSkillCastRuntimeService service)
        {
            var scan = service.ScanDiagnostics();
            EditorGUILayout.LabelField(
                $"活动={scan.ActiveRuntimes}  等待子运行时={scan.WaitingChildrenRuntimes}  挂起子对象={scan.PendingChildren}",
                EditorStyles.miniLabel);

            if (_items.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前没有活动技能运行时。施放已快速完成的技能会出现在“诊断事件”面板的技能生命周期记录中。",
                    MessageType.Info);
            }
        }

        private void DrawRuntimeList(in BattleDebugContext ctx)
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _items.Count; i++)
            {
                DrawRuntime(in ctx, _items[i]);
            }

            if (_selectedRuntime.IsValid && !ContainsRuntime(in _selectedRuntime))
            {
                DrawInspection(ctx.SkillRuntimeService, in _selectedRuntime, ctx.OpenTrace);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRuntime(in BattleDebugContext ctx, in MobaSkillRuntimeDiagnostics runtime)
        {
            var handle = runtime.Handle;
            var selected = handle.Equals(_selectedRuntime);
            var oldColor = GUI.color;
            if (runtime.IsWaitingChildren)
            {
                GUI.color = new Color(1f, 0.9f, 0.55f);
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            var title = $"#{handle.RuntimeId}:{handle.Generation}  Skill {runtime.SkillId}  {runtime.Stage}";
            if (GUILayout.Button(title, selected ? EditorStyles.toolbarButton : EditorStyles.miniButton))
            {
                _selectedRuntime = selected ? default : handle;
                ctx.RequestRepaint?.Invoke();
            }
            GUILayout.Label(runtime.IsWaitingChildren ? "等待子对象" : "运行中", EditorStyles.miniLabel, GUILayout.Width(72));
            EditorGUILayout.EndHorizontal();
            GUI.color = oldColor;

            EditorGUILayout.LabelField(
                "技能",
                $"ID={runtime.SkillId}  Slot={runtime.SkillSlot}  Level={runtime.SkillLevel}  Sequence={runtime.Sequence}");
            EditorGUILayout.LabelField(
                "状态",
                $"Stage={runtime.Stage}  PipelineEnded={runtime.PipelineEnded}  Ending={runtime.IsEnding}  Reason={runtime.EndReason}");
            EditorGUILayout.LabelField(
                "资源",
                $"PendingChildren={runtime.PendingChildren}  Blackboard={runtime.BlackboardEntryCount}  RootTrace={handle.RootTraceContextId}");

            EditorGUILayout.BeginHorizontal();
            DrawActorButton("施法者", runtime.CasterActorId, ctx.SelectActor);
            DrawActorButton("目标", runtime.TargetActorId, ctx.SelectActor);
            EditorGUI.BeginDisabledGroup(handle.RootTraceContextId <= 0L || ctx.OpenTrace == null);
            if (GUILayout.Button("打开 Trace", GUILayout.Width(90)))
            {
                ctx.OpenTrace?.Invoke(handle.RootTraceContextId, handle.RootTraceContextId);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (selected)
            {
                DrawInspection(service: ctx.SkillRuntimeService, in handle, ctx.OpenTrace);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawActorButton(string label, int actorId, Action<long> selectActor)
        {
            EditorGUI.BeginDisabledGroup(actorId <= 0 || selectActor == null);
            if (GUILayout.Button($"{label} #{actorId}", GUILayout.Width(100)))
            {
                selectActor?.Invoke(actorId);
            }
            EditorGUI.EndDisabledGroup();
        }

        private static void DrawInspection(
            MobaSkillCastRuntimeService service,
            in MobaSkillCastRuntimeHandle handle,
            Action<long, long> openTrace)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("运行时详情", EditorStyles.boldLabel);
            if (service == null || !service.TryGetDetailDiagnostics(in handle, out var detail))
            {
                EditorGUILayout.HelpBox("该技能运行时已结束或被清理，无法读取实时详情。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("输入", $"AimPos={detail.AimPos}  AimDir={detail.AimDir}");
            DrawBlackboard(detail.BlackboardEntries);
            DrawChildren(detail.Runtime.Children, openTrace);
        }

        private static void DrawBlackboard(IReadOnlyList<MobaSkillRuntimeBlackboardEntryDiagnostics> entries)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Blackboard", EditorStyles.miniBoldLabel);
            if (entries == null || entries.Count == 0)
            {
                EditorGUILayout.LabelField("（无已写入条目）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var key = entry.Key;
                var entryValue = entry.Value;
                var value = entry.IsCollection
                    ? $"Count={entry.CollectionCount}"
                    : FormatValue(in entryValue);
                EditorGUILayout.LabelField(
                    $"{key.Name} [{key.ValueKind}] = {value}",
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    $"Scope={key.Scope}  Flags={key.Flags}  OwnerModule={key.OwnerModuleId}",
                    EditorStyles.miniLabel);
            }
        }

        private static string FormatValue(in MobaSkillRuntimeValue value)
        {
            switch (value.Kind)
            {
                case MobaSkillRuntimeValueKind.Int:
                case MobaSkillRuntimeValueKind.ActorId:
                    return value.IntValue.ToString();
                case MobaSkillRuntimeValueKind.Long:
                case MobaSkillRuntimeValueKind.ContextId:
                    return value.LongValue.ToString();
                case MobaSkillRuntimeValueKind.Float:
                    return value.FloatValue.ToString("0.###");
                case MobaSkillRuntimeValueKind.Bool:
                    return value.BoolValue.ToString();
                case MobaSkillRuntimeValueKind.String:
                    return value.StringValue ?? string.Empty;
                case MobaSkillRuntimeValueKind.Vec3:
                    return value.Vec3Value.ToString();
                default:
                    return "（未设置）";
            }
        }

        private static void DrawChildren(IReadOnlyList<MobaSkillRuntimeChildRef> children, Action<long, long> openTrace)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("挂起子对象", EditorStyles.miniBoldLabel);
            if (children == null || children.Count == 0)
            {
                EditorGUILayout.LabelField("（无）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(
                    $"{child.Kind}  ID={child.ChildId}  Config={child.ConfigId}  Trace={child.TraceContextId}",
                    EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(child.TraceContextId <= 0L || openTrace == null);
                if (GUILayout.Button("Trace", EditorStyles.miniButton, GUILayout.Width(48)))
                {
                    openTrace?.Invoke(child.TraceContextId, child.TraceContextId);
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }
        }

        private bool ContainsRuntime(in MobaSkillCastRuntimeHandle handle)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Handle.Equals(handle)) return true;
            }

            return false;
        }
    }
}
