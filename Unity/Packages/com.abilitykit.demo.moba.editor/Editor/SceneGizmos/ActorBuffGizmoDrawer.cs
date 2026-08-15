using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Diagnostics.DebugDraw;
using AbilityKit.Diagnostics.Editor.DebugDraw;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle;
using UnityEditor;

namespace AbilityKit.Game.Editor.Gizmos
{
    /// <summary>
    /// Scene 视图绘制当前帧每个 Actor 身上挂载的 Buff 半径：
    /// - 数据源：MobaActorRegistry 遍历 + ActorEntity.BuffsComponent.Active
    /// - 配置源：MobaConfigDatabase → BuffMO.PresentationTemplateId → PresentationTemplateMO.Radius
    /// - 通过 IDebugDrawContributor 注册到 DebugDrawSceneViewDriver，绘制一次wire sphere
    /// </summary>
    [InitializeOnLoad]
    public static class ActorBuffGizmoDrawer
    {
        static ActorBuffGizmoDrawer()
        {
            DebugDrawSceneViewDriver.Register(BuffContributor.Instance);
        }

        private sealed class BuffContributor : IDebugDrawContributor
        {
            public static readonly BuffContributor Instance = new BuffContributor();

            private readonly List<ActorEntityEntry> _actorBuffer = new List<ActorEntityEntry>(64);

            public DebugDrawMask Mask => new DebugDrawMask(
                DebugDrawEditorSettings.Masks.Targeting.Value | MobaSceneGizmoSettings.BuffBit);

            public void Draw(in DebugDrawContext ctx, IDebugDraw draw)
            {
                if (!MobaSceneGizmoSettings.IsBuffEnabled()) return;

                var session = BattleLogicSessionHost.Current;
                if (session == null) return;
                if (!session.TryGetWorld(out var world) || world == null) return;

                var services = world.Services;
                if (services == null) return;
                if (!services.TryResolve<MobaActorRegistry>(out var registry) || registry == null) return;
                if (!services.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return;

                _actorBuffer.Clear();
                CollectActors(registry, _actorBuffer);

                var style = new DebugDrawStyle(MobaSceneGizmoSettings.BuffColor);
                var maxActors = MobaSceneGizmoSettings.MaxActors;
                if (maxActors <= 0) maxActors = 64;
                var maxBuffs = MobaSceneGizmoSettings.MaxBuffPerActor;
                if (maxBuffs <= 0) maxBuffs = 8;

                var actorCount = _actorBuffer.Count;
                if (actorCount > maxActors) actorCount = maxActors;

                for (var i = 0; i < actorCount; i++)
                {
                    var entry = _actorBuffer[i];
                    var entity = entry.Entity;
                    if (entity == null) continue;

                    if (!entity.hasTransform || !entity.hasBuffs || !entity.hasActorId) continue;

                    var active = entity.buffs.Active;
                    if (active == null || active.Count == 0) continue;

                    var pos = entity.transform.Value.Position;
                    var buffCount = active.Count;
                    if (buffCount > maxBuffs) buffCount = maxBuffs;

                    for (var b = 0; b < buffCount; b++)
                    {
                        var runtime = active[b];
                        if (runtime == null) continue;
                        if (runtime.Remaining <= 0f) continue;

                        if (!configs.TryGetBuff(runtime.BuffId, out var buffMo) || buffMo == null) continue;
                        if (buffMo.PresentationTemplateId <= 0) continue;
                        if (!configs.GetTable<PresentationTemplateMO>().TryGet(buffMo.PresentationTemplateId, out var template) || template == null) continue;

                        var radius = template.Radius;
                        if (radius <= 0f) continue;

                        var center = new Vec3(pos.X + template.OffsetX, pos.Y + template.OffsetY, pos.Z + template.OffsetZ);
                        draw.DrawWireSphere(in center, radius, in style);
                    }
                }

                _actorBuffer.Clear();
            }

            private static void CollectActors(MobaActorRegistry registry, List<ActorEntityEntry> buffer)
            {
                if (registry == null) return;
                foreach (var kv in registry.Entries)
                {
                    var entity = kv.Value;
                    if (entity == null || !entity.isEnabled) continue;
                    buffer.Add(new ActorEntityEntry(entity));
                }
            }

            private readonly struct ActorEntityEntry
            {
                public readonly global::ActorEntity Entity;

                public ActorEntityEntry(global::ActorEntity entity)
                {
                    Entity = entity;
                }
            }
        }
    }
}
