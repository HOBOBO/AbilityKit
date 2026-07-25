using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Game.Battle;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor
{
    /// <summary>
    /// Displays live temporary objects and continuous effects with their immutable provenance data.
    /// </summary>
    internal sealed class BattleDebugActiveObjectsPanel : IBattleDebugPanel, IBattleDebugPanelLayout
    {
        private readonly List<MobaProjectileLinkDiagnostics> _projectiles = new List<MobaProjectileLinkDiagnostics>(32);
        private readonly List<MobaAreaRuntimeInfo> _areas = new List<MobaAreaRuntimeInfo>(32);
        private IReadOnlyList<MobaContinuousRuntimeView> _continuous = Array.Empty<MobaContinuousRuntimeView>();
        private Vector2 _scroll;
        private double _nextRefreshAt;

        public string Name => "活跃对象";
        public int Order => 395;
        public BattleDebugWorkspace Workspace => BattleDebugWorkspace.Diagnostics;
        public bool OwnsScrollView => true;

        public bool IsVisible(in BattleDebugContext ctx) => true;

        public void Draw(in BattleDebugContext ctx)
        {
            if (ctx.IsOffline)
            {
                EditorGUILayout.HelpBox(
                    "离线 Artifact 不包含可继续检查的活跃临时对象。请在实时会话暂停后查看投射物、AOE 和持续效果。",
                    MessageType.Info);
                return;
            }

            if (!TryResolveServices(
                    ctx.Facade,
                    out var links,
                    out var areas,
                    out var continuous))
            {
                EditorGUILayout.HelpBox(
                    "当前实时 World 未就绪，无法解析活跃对象运行时服务。",
                    MessageType.Info);
                return;
            }

            DrawToolbar(links, areas, continuous, ctx.RequestRepaint);
            RefreshIfNeeded(links, areas, continuous, force: false);
            DrawSummary();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawProjectiles(in ctx);
            DrawAreas(in ctx);
            DrawContinuous(in ctx);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar(
            MobaProjectileLinkService links,
            MobaAreaRuntimeService areas,
            IMobaContinuousRuntimeQueryService continuous,
            Action requestRepaint)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("实时活跃对象", EditorStyles.miniBoldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshIfNeeded(links, areas, continuous, force: true);
                requestRepaint?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void RefreshIfNeeded(
            MobaProjectileLinkService links,
            MobaAreaRuntimeService areas,
            IMobaContinuousRuntimeQueryService continuous,
            bool force)
        {
            var now = EditorApplication.timeSinceStartup;
            if (!force && now < _nextRefreshAt) return;

            _nextRefreshAt = now + 0.15d;
            _projectiles.Clear();
            links?.CopyDiagnosticsTo(_projectiles);
            _projectiles.Sort((left, right) => right.ProjectileId.Value.CompareTo(left.ProjectileId.Value));

            _areas.Clear();
            areas?.TryGetAreas(_areas);
            _areas.Sort((left, right) => right.AreaId.CompareTo(left.AreaId));

            _continuous = continuous?.GetAllContinuous() ?? Array.Empty<MobaContinuousRuntimeView>();
        }

        private void DrawSummary()
        {
            EditorGUILayout.LabelField(
                $"投射物={_projectiles.Count}  AOE={_areas.Count}  持续效果={_continuous.Count}",
                EditorStyles.miniLabel);

            if (_projectiles.Count == 0 && _areas.Count == 0 && _continuous.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前没有可检查的活跃临时对象或持续效果。可暂停在技能飞行、范围生效或持续状态期间后刷新。",
                    MessageType.Info);
            }
        }

        private void DrawProjectiles(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("投射物", EditorStyles.boldLabel);
            if (_projectiles.Count == 0)
            {
                EditorGUILayout.LabelField("（无活跃投射物）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < _projectiles.Count; i++)
            {
                var entry = _projectiles[i];
                var source = entry.Source;
                var skillRuntimeHandle = source.SkillRuntimeHandle;
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(
                    $"Projectile #{entry.ProjectileId.Value}  Actor #{entry.ActorId}  Config={source.ProjectileConfigId}",
                    EditorStyles.miniBoldLabel);
                DrawLineage(
                    source.SourceActorId,
                    source.InitialTargetActorId,
                    source.SourceContextId,
                    source.RootContextId,
                    source.OwnerContextId,
                    in skillRuntimeHandle,
                    in ctx);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawAreas(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("AOE", EditorStyles.boldLabel);
            if (_areas.Count == 0)
            {
                EditorGUILayout.LabelField("（无活跃 AOE）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < _areas.Count; i++)
            {
                var area = _areas[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(
                    $"Area #{area.AreaId}  Template={area.TemplateId}  Owner #{area.OwnerActorId}",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    $"Center={area.Center}  Radius={area.Radius:0.###}  Spawn={area.SpawnFrame}  Delay={area.DelayTriggerFrame}",
                    EditorStyles.miniLabel);
                var noSkillRuntime = default(MobaSkillCastRuntimeHandle);
                DrawLineage(
                    area.OwnerActorId,
                    0,
                    area.SourceContextId,
                    area.RootContextId,
                    area.OwnerContextId,
                    in noSkillRuntime,
                    in ctx);
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawContinuous(in BattleDebugContext ctx)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("持续效果", EditorStyles.boldLabel);
            if (_continuous == null || _continuous.Count == 0)
            {
                EditorGUILayout.LabelField("（无活跃持续效果）", EditorStyles.miniLabel);
                return;
            }

            for (var i = 0; i < _continuous.Count; i++)
            {
                var runtime = _continuous[i];
                if (runtime == null) continue;

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField(
                    $"{runtime.Kind}  Id={runtime.Id}  Config={runtime.ConfigId}",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    $"State={runtime.State}  Active={runtime.IsActive}  Paused={runtime.IsPaused}  Elapsed={runtime.ElapsedSeconds:0.###}  Remaining={runtime.RemainingSeconds:0.###}",
                    EditorStyles.miniLabel);
                var skillRuntimeHandle = runtime.SkillRuntimeHandle;
                DrawLineage(
                    runtime.SourceActorId,
                    runtime.TargetActorId,
                    runtime.SourceContextId,
                    runtime.RootContextId,
                    runtime.OwnerContextId,
                    in skillRuntimeHandle,
                    in ctx);
                EditorGUILayout.EndVertical();
            }
        }

        private static void DrawLineage(
            int sourceActorId,
            int targetActorId,
            long sourceContextId,
            long rootContextId,
            long ownerContextId,
            in MobaSkillCastRuntimeHandle skillRuntimeHandle,
            in BattleDebugContext ctx)
        {
            EditorGUILayout.LabelField(
                $"Trace: Source={sourceContextId}  Root={rootContextId}  Owner={ownerContextId}  SkillRuntime={FormatRuntime(in skillRuntimeHandle)}",
                EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            DrawActorButton("来源", sourceActorId, ctx.SelectActor);
            DrawActorButton("目标", targetActorId, ctx.SelectActor);
            var traceContextId = sourceContextId != 0L ? sourceContextId : rootContextId;
            var effectiveRootId = rootContextId != 0L ? rootContextId : traceContextId;
            EditorGUI.BeginDisabledGroup(effectiveRootId <= 0L || traceContextId <= 0L || ctx.OpenTrace == null);
            if (GUILayout.Button("打开 Trace", GUILayout.Width(90)))
            {
                ctx.OpenTrace?.Invoke(effectiveRootId, traceContextId);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private static string FormatRuntime(in MobaSkillCastRuntimeHandle handle)
        {
            return handle.IsValid ? $"#{handle.RuntimeId}:{handle.Generation}" : "-";
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

        private static bool TryResolveServices(
            IBattleDebugFacade facade,
            out MobaProjectileLinkService links,
            out MobaAreaRuntimeService areas,
            out IMobaContinuousRuntimeQueryService continuous)
        {
            links = null;
            areas = null;
            continuous = null;
            if (facade == null || !facade.TryGetSession(out var session) || session == null) return false;
            if (!session.TryGetWorld(out var world) || world?.Services == null) return false;

            world.Services.TryResolve(out links);
            world.Services.TryResolve(out areas);
            world.Services.TryResolve(out continuous);
            return links != null || areas != null || continuous != null;
        }
    }
}
