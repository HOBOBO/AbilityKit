using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Battle.Component;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileVfxSpawner
    {
        private readonly EC.IECWorld _world;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;
        private readonly BattleVfxPresentationService _presentation;

        public BattleProjectileVfxSpawner(EC.IECWorld world, BattleVfxManager vfx, in EC.IEntity vfxNode)
        {
            _world = world;
            _vfx = vfx;
            _vfxNode = vfxNode;
            _presentation = new BattleVfxPresentationService(world, vfx, in vfxNode);
        }

        public bool CanSpawn
        {
            get
            {
                if (_world == null) return false;
                if (_vfx == null) return false;
                if (!_vfxNode.IsValid) return false;
                return true;
            }
        }

        public bool TrySpawn(in BattleProjectileVfxSpawnSpec spec)
        {
            if (!CanSpawn) return false;
            if (!spec.IsValid) return false;

            var request = new PresentationRequest(
                "projectile:" + spec.FollowTargetActorId + ":" + spec.VfxId,
                spec.VfxId,
                new PresentationVector3(spec.Position.x, spec.Position.y, spec.Position.z),
                new PresentationRotation(spec.Rotation.x, spec.Rotation.y, spec.Rotation.z, spec.Rotation.w),
                spec.FollowTargetActorId > 0
                    ? new PresentationAttachment(PresentationAttachmentKind.Projectile, spec.FollowTargetActorId)
                    : default);
            if (!_presentation.TryPlay(in request, spec.FollowTarget, out var handle)) return false;
            if (spec.PlayProjectileSpawn) PlayProjectileSpawn(spec.VfxId, in handle);
            return true;
        }

        private void PlayProjectileSpawn(int vfxId, in EC.IEntityId handle)
        {
            if (!_world.IsAlive(handle)) return;
            var entity = _world.Wrap(handle);
            if (!entity.TryGetRef(out BattleViewGameObjectComponent view) || view.GameObject == null) return;

            var animators = view.GameObject.GetComponentsInChildren<Animator>(true);
            for (var animatorIndex = 0; animatorIndex < animators.Length; animatorIndex++)
            {
                var animator = animators[animatorIndex];
                if (animator.runtimeAnimatorController == null) continue;
                var parameters = animator.parameters;
                for (var parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    var parameter = parameters[parameterIndex];
                    // W's character cast uses Spell_W, while its moving projectile prefab
                    // owns a separate presentation controller with W_Shot/W_Beam triggers.
                    // Trigger the projectile controller here so the authored shot particles
                    // are enabled when the authoritative spawn snapshot is consumed.
                    if (parameter.type != AnimatorControllerParameterType.Trigger ||
                        (parameter.name != "ProjectileSpawn" && parameter.name != "Q_Shot" &&
                         parameter.name != "W_Shot" && parameter.name != "W_Beam" &&
                         parameter.name != "Spell_E")) continue;
                    animator.SetTrigger(parameter.nameHash);
                    break;
                }
            }

            if (vfxId == 90013001)
            {
                var missile = FindChild(view.GameObject.transform, "Grenade_Missile");
                if (missile != null) missile.gameObject.SetActive(true);
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public int StopFollowingActor(int projectileActorId)
        {
            if (_vfx == null) return 0;
            if (!_vfxNode.IsValid) return 0;
            if (projectileActorId <= 0) return 0;

            return _vfx.DestroyVfxByFollowTargetActorId(_vfxNode, projectileActorId);
        }
    }
}
