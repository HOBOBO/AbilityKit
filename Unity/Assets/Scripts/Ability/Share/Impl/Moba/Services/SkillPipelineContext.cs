using System;
using System.Collections.Generic;
using AbilityKit.Ability;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class SkillPipelineContext : IAbilityPipelineContext
    {
        public object AbilityInstance { get; private set; }
        public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        public EAbilityPipelineState PipelineState { get; set; }
        public bool IsAborted { get; set; }
        public bool IsPaused { get; set; }
        public float StartTime { get; set; }
        public float ElapsedTime { get; private set; }

        public Dictionary<string, object> SharedData { get; } = new Dictionary<string, object>();

        public int SkillId { get; private set; }
        public int SkillSlot { get; private set; }
        public int CasterActorId { get; private set; }
        public int TargetActorId { get; private set; }
        public Vec3 AimPos { get; private set; }
        public Vec3 AimDir { get; private set; }

        public IWorldServices WorldServices { get; private set; }
        public IEventBus EventBus { get; private set; }
        public IUnitFacade CasterUnit { get; private set; }
        public IUnitFacade TargetUnit { get; private set; }

        public void Initialize(object abilityInstance, in SkillCastRequest request)
        {
            AbilityInstance = abilityInstance;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0f;
            ElapsedTime = 0f;

            SkillId = request.SkillId;
            SkillSlot = request.SkillSlot;
            CasterActorId = request.CasterActorId;
            TargetActorId = request.TargetActorId;
            AimPos = request.AimPos;
            AimDir = request.AimDir;

            WorldServices = request.WorldServices;
            EventBus = request.EventBus;
            CasterUnit = request.CasterUnit;
            TargetUnit = request.TargetUnit;

            SharedData.Clear();

            SharedData[MobaSkillPipelineSharedKeys.SkillId] = SkillId;
            SharedData[MobaSkillPipelineSharedKeys.SkillSlot] = SkillSlot;
            SharedData[MobaSkillPipelineSharedKeys.CasterActorId] = CasterActorId;
            SharedData[MobaSkillPipelineSharedKeys.TargetActorId] = TargetActorId;
            SharedData[MobaSkillPipelineSharedKeys.AimPos] = AimPos;
            SharedData[MobaSkillPipelineSharedKeys.AimDir] = AimDir;
        }

        public void AdvanceTime(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            ElapsedTime += deltaTime;
        }

        public T GetData<T>(string key, T defaultValue = default)
        {
            if (SharedData.TryGetValue(key, out var value) && value is T typedValue)
                return typedValue;
            return defaultValue;
        }

        public void SetData<T>(string key, T value)
        {
            SharedData[key] = value;
        }

        public bool RemoveData(string key)
        {
            return SharedData.Remove(key);
        }

        public void ClearData()
        {
            SharedData.Clear();
        }

        public void Reset()
        {
            AbilityInstance = null;
            CurrentPhaseId = null;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0f;
            ElapsedTime = 0f;
            SharedData.Clear();

            SkillId = 0;
            SkillSlot = 0;
            CasterActorId = 0;
            TargetActorId = 0;
            AimPos = Vec3.Zero;
            AimDir = Vec3.Forward;

            WorldServices = null;
            EventBus = null;
            CasterUnit = null;
            TargetUnit = null;
        }
    }

    public readonly struct SkillCastRequest
    {
        public readonly int SkillId;
        public readonly int SkillSlot;
        public readonly int CasterActorId;
        public readonly int TargetActorId;
        public readonly Vec3 AimPos;
        public readonly Vec3 AimDir;

        public readonly IWorldServices WorldServices;
        public readonly IEventBus EventBus;
        public readonly IUnitFacade CasterUnit;
        public readonly IUnitFacade TargetUnit;

        public SkillCastRequest(
            int skillId,
            int skillSlot,
            int casterActorId,
            int targetActorId,
            in Vec3 aimPos,
            in Vec3 aimDir,
            IWorldServices worldServices,
            IEventBus eventBus,
            IUnitFacade casterUnit,
            IUnitFacade targetUnit)
        {
            SkillId = skillId;
            SkillSlot = skillSlot;
            CasterActorId = casterActorId;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            WorldServices = worldServices;
            EventBus = eventBus;
            CasterUnit = casterUnit;
            TargetUnit = targetUnit;
        }
    }
}
