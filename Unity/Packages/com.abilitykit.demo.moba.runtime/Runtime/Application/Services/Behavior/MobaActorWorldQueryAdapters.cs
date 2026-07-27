using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Moba.Behavior;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    /// <summary>
    /// MobaWorldQuery.IEntityManager 的 Entitas 实现：
    /// 位置/朝向读写走 ActorEntity.transform（决策层不直接驱动 Motion，
    /// SetPosition/SetForward 仅供特殊决策使用，常规移动应通过 MoveInput）。
    /// </summary>
    internal sealed class MobaBrainEntityManager : MobaWorldQuery.IEntityManager
    {
        private readonly MobaActorRegistry _registry;

        public MobaBrainEntityManager(MobaActorRegistry registry)
        {
            _registry = registry;
        }

        public bool Exists(long entityId)
        {
            return entityId > 0
                && _registry.TryGet((int)entityId, out var e)
                && e != null
                && e.isEnabled;
        }

        public global::AbilityKit.Core.Mathematics.Vec3 GetPosition(long entityId)
        {
            return TryGetTransform(entityId, out var t) ? t.Position : global::AbilityKit.Core.Mathematics.Vec3.Zero;
        }

        public void SetPosition(long entityId, global::AbilityKit.Core.Mathematics.Vec3 position)
        {
            if (!TryGetEntity(entityId, out var e) || !e.hasTransform) return;
            var t = e.transform.Value;
            e.ReplaceTransform(new global::AbilityKit.Core.Mathematics.Transform3(position, t.Rotation, t.Scale));
        }

        public global::AbilityKit.Core.Mathematics.Vec3 GetForward(long entityId)
        {
            return TryGetTransform(entityId, out var t) ? t.Forward : global::AbilityKit.Core.Mathematics.Vec3.Forward;
        }

        public void SetForward(long entityId, global::AbilityKit.Core.Mathematics.Vec3 forward)
        {
            if (!TryGetEntity(entityId, out var e) || !e.hasTransform) return;
            var t = e.transform.Value;
            var rotation = global::AbilityKit.Core.Mathematics.Quat.LookRotation(forward.Normalized, global::AbilityKit.Core.Mathematics.Vec3.Up);
            e.ReplaceTransform(new global::AbilityKit.Core.Mathematics.Transform3(t.Position, rotation, t.Scale));
        }

        private bool TryGetEntity(long entityId, out global::ActorEntity entity)
        {
            entity = null;
            if (entityId <= 0) return false;
            if (!_registry.TryGet((int)entityId, out var e) || e == null || !e.isEnabled) return false;
            entity = e;
            return true;
        }

        private bool TryGetTransform(long entityId, out global::AbilityKit.Core.Mathematics.Transform3 transform)
        {
            transform = default;
            if (!TryGetEntity(entityId, out var e) || !e.hasTransform) return false;
            transform = e.transform.Value;
            return true;
        }
    }

    /// <summary>
    /// MobaWorldQuery.IBuffManager 的 Entitas 实现：
    /// HasBuff 按数值 buffId 扫描 Active 列表。
    /// HasTag 当前返回 false——BuffRuntime 的 TagRequirements 是结构化约束而非简单标签列表，
    /// 控制类标签（Stunned/Rooted）接入后在此实现（P2）。
    /// </summary>
    internal sealed class MobaBrainBuffManager : MobaWorldQuery.IBuffManager
    {
        private readonly MobaActorRegistry _registry;

        public MobaBrainBuffManager(MobaActorRegistry registry)
        {
            _registry = registry;
        }

        public bool HasBuff(long entityId, string buffId)
        {
            if (!int.TryParse(buffId, out var id) || id <= 0) return false;
            if (!TryGetActiveBuffs(entityId, out var active)) return false;

            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].BuffId == id) return true;
            }

            return false;
        }

        public bool HasTag(long entityId, string tag)
        {
            return false;
        }

        private bool TryGetActiveBuffs(long entityId, out System.Collections.Generic.List<global::AbilityKit.Demo.Moba.Components.BuffRuntime> active)
        {
            active = null;
            if (entityId <= 0) return false;
            if (!_registry.TryGet((int)entityId, out var e) || e == null || !e.hasBuffs) return false;
            active = e.buffs.Active;
            return active != null;
        }
    }

    /// <summary>
    /// MobaWorldQuery.IAttributeSystem 的 Entitas 实现：
    /// 属性走 AttributeGroup；IsAlive 看 HP；GetTeam 读 team 组件。
    /// </summary>
    internal sealed class MobaBrainAttributeSystem : MobaWorldQuery.IAttributeSystem
    {
        private readonly MobaActorRegistry _registry;

        public MobaBrainAttributeSystem(MobaActorRegistry registry)
        {
            _registry = registry;
        }

        public float GetAttribute(long entityId, string attributeId)
        {
            if (!TryGetGroup(entityId, out var group)) return 0f;

            // MobaWorldQuery.GetMoveSpeed 使用字符串 "MoveSpeed"
            if (string.Equals(attributeId, "MoveSpeed", System.StringComparison.Ordinal))
            {
                return group.GetValue(MobaAttributeIds.MOVE_SPEED);
            }

            if (string.Equals(attributeId, "HP", System.StringComparison.Ordinal))
            {
                return group.GetValue(MobaAttributeIds.HP);
            }

            return 0f;
        }

        public bool IsAlive(long entityId)
        {
            if (!TryGetGroup(entityId, out var group)) return false;
            return group.GetValue(MobaAttributeIds.HP) > 0f;
        }

        public int GetTeam(long entityId)
        {
            if (entityId <= 0) return 0;
            if (!_registry.TryGet((int)entityId, out var e) || e == null || !e.hasTeam) return 0;
            return (int)e.team.Value;
        }

        private bool TryGetGroup(long entityId, out global::AbilityKit.Attributes.Core.AttributeGroup group)
        {
            group = null;
            if (entityId <= 0) return false;
            if (!_registry.TryGet((int)entityId, out var e) || e == null || !e.hasAttributeGroup) return false;
            group = e.attributeGroup.Group;
            return group != null;
        }
    }
}
