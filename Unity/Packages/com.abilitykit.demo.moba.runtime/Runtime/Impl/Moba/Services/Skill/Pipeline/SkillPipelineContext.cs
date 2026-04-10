using System;
using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.Share.ECS; using AbilityKit.ECS; using AbilityKit.Ability.Share.ECS;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class SkillPipelineContext : IAbilityPipelineContext<object>
    {
        public object AbilityInstance { get; private set; }
        public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        public EAbilityPipelineState PipelineState { get; set; }
        public bool IsAborted { get; set; }
        public bool IsPaused { get; set; }
        public float StartTime { get; set; }
        public float ElapsedTime { get; private set; }

        public long SourceContextId { get; set; }
        public string FailReason { get; set; }

        public int SkillId { get; private set; }
        public int SkillSlot { get; private set; }
        public int CasterActorId { get; private set; }
        public int TargetActorId { get; private set; }
        public Vec3 AimPos { get; private set; }
        public Vec3 AimDir { get; private set; }

        public IWorldResolver WorldServices { get; private set; }
        public AbilityKit.Triggering.Eventing.IEventBus EventBus { get; private set; }
        public IUnitFacade CasterUnit { get; private set; }
        public IUnitFacade TargetUnit { get; private set; }

        public Dictionary<string, object> SharedData { get; } = new();

        /// <summary>
        /// 当前正在执行的处理项 DTO（用于 Handler 内部访问配置）
        /// </summary>
        internal SkillHandlerDTO CurrentHandlerDto { get; set; }

        /// <summary>
        /// 技能冷却时间（毫秒）
        /// </summary>
        public int SkillCooldownMs { get; set; }

        private readonly List<IDisposable> _disposables = new List<IDisposable>(4);
        private readonly List<Action> _cleanupActions = new List<Action>(4);

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

        public bool TryGetData<T>(string key, out T value)
        {
            if (SharedData.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            value = default;
            return false;
        }

        public bool RemoveData(string key)
        {
            return SharedData.Remove(key);
        }

        public void ClearData()
        {
            SharedData.Clear();
        }

        public void RegisterCleanup(IDisposable disposable)
        {
            if (disposable == null) return;
            _disposables.Add(disposable);
        }

        public void RegisterCleanup(Action action)
        {
            if (action == null) return;
            _cleanupActions.Add(action);
        }

        public void RunAndClearCleanups()
        {
            for (int i = _cleanupActions.Count - 1; i >= 0; i--)
            {
                try { _cleanupActions[i]?.Invoke(); }
                catch { }
            }
            _cleanupActions.Clear();

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                try { _disposables[i]?.Dispose(); }
                catch { }
            }
            _disposables.Clear();
        }

        public void Initialize(object abilityInstance, in SkillCastRequest request, SkillCastContext triggerContext = null)
        {
            AbilityInstance = abilityInstance;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0f;
            ElapsedTime = 0f;

            SharedData.Clear();
            FailReason = null;

            _disposables.Clear();
            _cleanupActions.Clear();

            SourceContextId = triggerContext?.SourceContextId ?? 0L;

            SkillId = request.SkillId;
            SkillSlot = request.SkillSlot;
            CasterActorId = request.CasterActorId;
            TargetActorId = request.TargetActorId;
            // 先设置属性值
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

            // 使用扩展方法填充共享数据（兼容旧代码）
            Vec3 aimPos = AimPos;
            Vec3 aimDir = AimDir;
            this.SetSkillInfo(SkillId, SkillSlot, triggerContext?.SkillLevel ?? 0);
            this.SetParticipants(CasterActorId, TargetActorId);
            this.SetAim(in aimPos, in aimDir);
            this.SetContextKind((int)EffectContextKind.Skill);
            this.SetSourceContextId(SourceContextId);
        }

        public void AdvanceTime(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            ElapsedTime += deltaTime;
        }

        public void Reset()
        {
            AbilityInstance = null;
            CurrentPhaseId = default;
            PipelineState = EAbilityPipelineState.Ready;
            IsAborted = false;
            IsPaused = false;
            StartTime = 0f;
            ElapsedTime = 0f;

            SharedData.Clear();

            _disposables.Clear();
            _cleanupActions.Clear();

            this.SetSourceContextId(0L);
            FailReason = null;

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

        public readonly IWorldResolver WorldServices;
        public readonly AbilityKit.Triggering.Eventing.IEventBus EventBus;
        public readonly IUnitFacade CasterUnit;
        public readonly IUnitFacade TargetUnit;

        public SkillCastRequest(
            int skillId,
            int skillSlot,
            int casterActorId,
            int targetActorId,
            in Vec3 aimPos,
            in Vec3 aimDir,
            IWorldResolver worldServices,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
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

    public sealed class MobaSkillCastInstanceSyncSettings : AbilityKit.Ability.World.Services.IService
    {
        public int RetainCompletedFrames { get; set; } = 30;
        public int DestroyConfirmGateFrames { get; set; } = 10;

        public void Dispose()
        {
        }
    }
}
