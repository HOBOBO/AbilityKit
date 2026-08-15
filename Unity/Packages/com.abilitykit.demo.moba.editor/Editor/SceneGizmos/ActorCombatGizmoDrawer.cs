using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Diagnostics.DebugDraw;
using AbilityKit.Diagnostics.Editor.DebugDraw;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle;
using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// Scene 视图绘制：
    /// - Attack Range（攻击/技能范围圈）：取 CharacterMO.SkillIds 中 Range>0 的技能，围绕 Actor 绘制 wire sphere
    /// - Spawn Area（出生标记）：在 Actor 首次出现的 transform.Position 落下一个固定的 wire 小盒，
    ///   即使 Actor 移动也不会跟随。Session 切换时清空。
    /// </summary>
    [InitializeOnLoad]
    public static class ActorCombatGizmoDrawer
    {
        private static readonly HashSet<int> s_seenActors = new HashSet<int>();
        private static readonly Dictionary<int, Vec3> s_spawnPoints = new Dictionary<int, Vec3>();
        private static BattleLogicSession s_lastSession;

        static ActorCombatGizmoDrawer()
        {
            DebugDrawSceneViewDriver.Register(CombatContributor.Instance);
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode || change == PlayModeStateChange.ExitingEditMode)
            {
                ClearSpawnCache();
            }
        }

        public static void ClearSpawnCache()
        {
            s_seenActors.Clear();
            s_spawnPoints.Clear();
            s_lastSession = null;
        }

        private sealed class CombatContributor : IDebugDrawContributor
        {
            public static readonly CombatContributor Instance = new CombatContributor();

            private readonly List<int> _skillBuffer = new List<int>(8);

            public DebugDrawMask Mask => new DebugDrawMask(
                DebugDrawEditorSettings.Masks.Targeting.Value
                | MobaSceneGizmoSettings.AttackBit
                | MobaSceneGizmoSettings.SpawnBit);

            public void Draw(in DebugDrawContext ctx, IDebugDraw draw)
            {
                var session = BattleLogicSessionHost.Current;
                if (session == null)
                {
                    ClearSpawnCache();
                    return;
                }

                if (!session.TryGetWorld(out var world) || world == null) return;
                var services = world.Services;
                if (services == null) return;

                if (!ReferenceEquals(session, s_lastSession))
                {
                    ClearSpawnCache();
                    s_lastSession = session;
                }

                if (!services.TryResolve<MobaActorRegistry>(out var registry) || registry == null) return;
                if (!services.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return;

                var drawAttack = MobaSceneGizmoSettings.IsAttackEnabled();
                var drawSpawn = MobaSceneGizmoSettings.IsSpawnEnabled();
                if (!drawAttack && !drawSpawn) return;

                var maxActors = MobaSceneGizmoSettings.MaxActors;
                if (maxActors <= 0) maxActors = 64;

                var attackStyle = new DebugDrawStyle(MobaSceneGizmoSettings.AttackColor);
                var spawnStyle = new DebugDrawStyle(MobaSceneGizmoSettings.SpawnColor);

                var actorCount = 0;
                foreach (var kv in registry.Entries)
                {
                    if (actorCount >= maxActors) break;
                    var entity = kv.Value;
                    if (entity == null || !entity.isEnabled) continue;
                    if (!entity.hasTransform || !entity.hasActorId) continue;

                    var pos = entity.transform.Value.Position;

                    if (drawSpawn && !s_seenActors.Contains(kv.Key))
                    {
                        s_seenActors.Add(kv.Key);
                        s_spawnPoints[kv.Key] = new Vec3(pos.X, pos.Y, pos.Z);
                    }

                    if (drawAttack)
                    {
                        DrawAttackRanges(entity, configs, in pos, in attackStyle, draw);
                    }

                    actorCount++;
                }

                if (drawSpawn)
                {
                    DrawSpawnMarkers(draw, in spawnStyle);
                }
            }

            private void DrawAttackRanges(
                global::ActorEntity entity,
                MobaConfigDatabase configs,
                in Vec3 pos,
                in DebugDrawStyle style,
                IDebugDraw draw)
            {
                _skillBuffer.Clear();
                CollectSkillIdsForEntity(entity, _skillBuffer);

                var skillCount = _skillBuffer.Count;
                for (var i = 0; i < skillCount; i++)
                {
                    var skillId = _skillBuffer[i];
                    if (!configs.TryGetSkill(skillId, out var skill) || skill == null) continue;
                    var range = skill.Range;
                    if (range <= 0f) continue;

                    var center = new Vec3(pos.X, pos.Y, pos.Z);
                    draw.DrawWireSphere(in center, range, in style);
                }

                _skillBuffer.Clear();
            }

            private static void CollectSkillIdsForEntity(
                global::ActorEntity entity,
                List<int> buffer)
            {
                if (!entity.hasSkillLoadout) return;
                var loadout = entity.skillLoadout;
                var actives = loadout.ActiveSkills;
                if (actives == null) return;

                var count = actives.Length;
                for (var i = 0; i < count; i++)
                {
                    var runtime = actives[i];
                    if (runtime == null) continue;
                    if (runtime.SkillId > 0) buffer.Add(runtime.SkillId);
                }
            }

            private void DrawSpawnMarkers(IDebugDraw draw, in DebugDrawStyle style)
            {
                if (s_spawnPoints.Count == 0) return;

                var halfSize = 0.5f;
                var count = 0;
                var maxAreas = MobaSceneGizmoSettings.MaxAreas;
                if (maxAreas <= 0) maxAreas = 64;

                foreach (var kv in s_spawnPoints)
                {
                    if (count >= maxAreas) break;
                    var p = kv.Value;
                    var size = new Vec3(halfSize * 2f, halfSize * 2f, halfSize * 2f);
                    draw.DrawWireAabb(in p, in size, in style);

                    var tip = new Vec3(p.X, p.Y + halfSize, p.Z);
                    var up = new Vec3(p.X, p.Y + halfSize * 3f, p.Z);
                    draw.DrawLine(in tip, in up, in style);
                    count++;
                }
            }
        }
    }
}
