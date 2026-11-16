using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Battle.Component;
using UnityEngine;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    internal sealed class BattleProjectileVfxSpawner
    {
        private readonly EC.IECWorld _world;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;
        private readonly BattleVfxPresentationService _presentation;
        private readonly Dictionary<int, EC.IEntityId> _phaseHandles = new Dictionary<int, EC.IEntityId>();
        private ActorStatePresentationBinding[] _phaseBindings;

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

        public bool TrySpawn(in BattleProjectileVfxSpawnSpec spec, bool retainLifecycle = false)
        {
            if (!CanSpawn) return false;
            if (!spec.IsValid) return false;
            retainLifecycle |= spec.PlayProjectileSpawn;
            if (retainLifecycle && spec.FollowTargetActorId > 0 && _phaseHandles.TryGetValue(spec.FollowTargetActorId, out var existing) &&
                _world.IsAlive(existing)) return true;

            var attachment = spec.FollowTargetActorId > 0
                ? new PresentationAttachment(PresentationAttachmentKind.Projectile, spec.FollowTargetActorId)
                : default;
            if (!_vfx.TryCreatePresentationRequest(
                spec.VfxId,
                "projectile:" + spec.FollowTargetActorId + ":" + spec.VfxId,
                new PresentationVector3(spec.Position.x, spec.Position.y, spec.Position.z),
                out var request,
                attachment,
                new PresentationRotation(spec.Rotation.x, spec.Rotation.y, spec.Rotation.z, spec.Rotation.w),
                scale: 1f)) return false;
            if (!_presentation.TryPlay(in request, spec.FollowTarget, out var handle)) return false;
            if (retainLifecycle && spec.FollowTargetActorId > 0) _phaseHandles[spec.FollowTargetActorId] = handle;
            if (spec.PlayProjectileSpawn) PlayProjectileSpawn(in handle);
            return true;
        }

        public bool HasPhaseBinding(in ProjectileEventData entry)
        {
            if (_phaseBindings == null)
            {
                var text = Resources.Load<TextAsset>("moba/actor_state_presentations");
                _phaseBindings = text != null ? ActorStatePresentationBinding.Load(text.text) : System.Array.Empty<ActorStatePresentationBinding>();
            }
            foreach (var binding in _phaseBindings)
                if (binding.ProfileId == entry.ProfileId && binding.StatePath == entry.StatePath) return true;
            return false;
        }

        public bool ApplyPhase(in ProjectileEventData entry)
        {
            if (!HasPhaseBinding(in entry) || !_phaseHandles.TryGetValue(entry.ProjectileActorId, out var handle) ||
                !_world.IsAlive(handle)) return false;
            var entity = _world.Wrap(handle);
            if (!entity.TryGetRef(out BattleViewGameObjectComponent view) || view.GameObject == null) return false;
            foreach (var binding in _phaseBindings)
            {
                if (binding.ProfileId != entry.ProfileId || binding.StatePath != entry.StatePath) continue;
                var normalized = Mathf.Clamp01(entry.LocalSeconds / binding.PlaybackDurationSeconds);
                var hash = Animator.StringToHash(binding.AnimatorState);
                var applied = false;
                foreach (var animator in view.GameObject.GetComponentsInChildren<Animator>(true))
                {
                    if (animator.runtimeAnimatorController == null || !animator.HasState(0, hash)) continue;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.speed = 0;
                    animator.Play(hash, 0, normalized);
                    animator.Update(0);
                    applied = true;
                }
                return applied;
            }
            return false;
        }

        public void ClearPhaseHandles()
        {
            foreach (var actorId in new List<int>(_phaseHandles.Keys)) StopFollowingActor(actorId);
            _phaseHandles.Clear();
        }

        private void PlayProjectileSpawn(in EC.IEntityId handle)
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
                    if (!IsProjectileSpawnTrigger(parameter.type, parameter.name)) continue;
                    animator.SetTrigger(parameter.nameHash);
                    break;
                }
            }
        }

        internal static bool IsProjectileSpawnTrigger(AnimatorControllerParameterType type, string name)
        {
            return type == AnimatorControllerParameterType.Trigger && name == "ProjectileSpawn";
        }

        public int StopFollowingActor(int projectileActorId)
        {
            _phaseHandles.Remove(projectileActorId);
            if (_vfx == null) return 0;
            if (!_vfxNode.IsValid) return 0;
            if (projectileActorId <= 0) return 0;

            return _vfx.DestroyVfxByFollowTargetActorId(_vfxNode, projectileActorId);
        }
    }
}
