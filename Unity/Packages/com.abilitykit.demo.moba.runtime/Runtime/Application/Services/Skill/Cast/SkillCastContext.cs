using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SkillCastContext : IMobaActorContextProvider, IMobaCombatContextSource, IMobaCombatExecutionContextProvider, IMobaTriggerLineageContextProvider, IMobaTriggerExecutionSnapshotProvider, IMobaOriginContextProvider, IMobaContextSourceProvider
    {
        public int SkillId;
        public int SkillSlot;
        public int SkillLevel;
        public int CastFlowId;
        public ResolvedSkillCastConfiguration ResolvedConfiguration;

        public int Sequence;

        public MobaSkillCastRuntimeHandle RuntimeHandle;
        public long RuntimeId;
        public long SourceContextId;

        public string FailReason;

        public int CasterActorId;
        public int TargetActorId;

        public Vec3 AimPos;
        public Vec3 AimDir;

        public IWorldResolver WorldServices;
        public AbilityKit.Triggering.Eventing.IEventBus EventBus;
        public IUnitFacade CasterUnit;
        public IUnitFacade TargetUnit;

        public SkillCastContext()
        {
        }

        public SkillCastContext(
            int skillId,
            int skillSlot,
            int skillLevel,
            int sequence,
            int casterActorId,
            int targetActorId,
            in Vec3 aimPos,
            in Vec3 aimDir,
            IWorldResolver worldServices,
            AbilityKit.Triggering.Eventing.IEventBus eventBus,
            IUnitFacade casterUnit,
            IUnitFacade targetUnit)
        {
            Initialize(
                skillId,
                skillSlot,
                skillLevel,
                sequence,
                casterActorId,
                targetActorId,
                in aimPos,
                in aimDir,
                worldServices,
                eventBus,
                casterUnit,
                targetUnit);
        }

        public void Initialize(in SkillCastRequest req, int skillLevel, int sequence = 0)
        {
            Initialize(
                req.SkillId,
                req.SkillSlot,
                skillLevel,
                sequence,
                req.CasterActorId,
                req.TargetActorId,
                in req.AimPos,
                in req.AimDir,
                req.WorldServices,
                req.EventBus,
                req.CasterUnit,
                req.TargetUnit);
        }

        public void Initialize(
            int skillId,
            int skillSlot,
            int skillLevel,
            int sequence,
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
            SkillLevel = skillLevel;
            CastFlowId = 0;
            ResolvedConfiguration = default;
            Sequence = sequence;
            RuntimeHandle = default;
            RuntimeId = 0L;
            SourceContextId = 0L;
            FailReason = null;
            CasterActorId = casterActorId;
            TargetActorId = targetActorId;
            AimPos = aimPos;
            AimDir = aimDir;
            WorldServices = worldServices;
            EventBus = eventBus;
            CasterUnit = casterUnit;
            TargetUnit = targetUnit;
        }

        public void Reset()
        {
            SkillId = 0;
            SkillSlot = 0;
            SkillLevel = 0;
            CastFlowId = 0;
            ResolvedConfiguration = default;
            Sequence = 0;
            RuntimeHandle = default;
            RuntimeId = 0L;
            SourceContextId = 0L;
            FailReason = null;
            CasterActorId = 0;
            TargetActorId = 0;
            AimPos = Vec3.Zero;
            AimDir = Vec3.Forward;
            WorldServices = null;
            EventBus = null;
            CasterUnit = null;
            TargetUnit = null;
        }

        public static SkillCastContext FromRequest(in SkillCastRequest req, int skillLevel)
        {
            return SkillCastContextBuilder.Create()
                .FromRequest(in req)
                .WithSkillLevel(skillLevel)
                .Build();
        }

        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = CasterActorId;
            return actorId > 0;
        }

        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = TargetActorId;
            return actorId > 0;
        }

        public bool TryGetCombatContextSource(out MobaCombatContextSource source)
        {
            source = MobaCombatContextBuilder.SkillCast(
                SkillId,
                CasterActorId,
                TargetActorId,
                SourceContextId,
                0,
                in RuntimeHandle);
            return source.HasExecutionSource;
        }

        public bool TryGetCombatExecutionContext(out MobaCombatExecutionContext context)
        {
            return MobaCombatContextBuilder.TryFromSource(this, out context);
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            lineageContext = default;
            if (!TryGetCombatContextSource(out var source)) return false;

            lineageContext = source.ToLineageContext();
            return true;
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetCombatContextSource(out var source)) return false;

            snapshot = source.ToExecutionSnapshot();
            return snapshot.IsValid;
        }

        public bool TryGetOrigin(out MobaGameplayOrigin origin)
        {
            origin = default;
            if (!TryGetCombatContextSource(out var source)) return false;

            origin = source.ToOrigin();
            return origin.IsValid;
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            source = default;
            if (!TryGetCombatContextSource(out var combatSource)) return false;

            source = combatSource.ToContextSourceView(
                MobaContextSourceResolveKind.DirectProvider,
                MobaContextSourceBoundary.LiveRuntime);
            return source.IsValid;
        }
    }

    public readonly struct MobaSkillCastFailure
    {
        public static readonly MobaSkillCastFailure None = new MobaSkillCastFailure(null, null, null, null);

        public MobaSkillCastFailure(string source, string stage, string code, string message)
        {
            Source = source;
            Stage = stage;
            Code = code;
            Message = message;
        }

        public string Source { get; }
        public string Stage { get; }
        public string Code { get; }
        public string Message { get; }
        public bool HasValue => !string.IsNullOrEmpty(Source) || !string.IsNullOrEmpty(Stage) || !string.IsNullOrEmpty(Code) || !string.IsNullOrEmpty(Message);

        public override string ToString()
        {
            var prefix = string.IsNullOrEmpty(Source) ? Stage : string.IsNullOrEmpty(Stage) ? Source : Source + "." + Stage;
            if (string.IsNullOrEmpty(Code)) return string.IsNullOrEmpty(Message) ? prefix ?? "unknown" : Message;
            if (string.IsNullOrEmpty(Message)) return string.IsNullOrEmpty(prefix) ? Code : prefix + ": " + Code;
            return string.IsNullOrEmpty(prefix) ? Code + ": " + Message : prefix + ": " + Code + ": " + Message;
        }
    }

    public readonly struct MobaSkillInputHandleResult
    {
        public MobaSkillInputHandleResult(bool success, string message, in MobaSkillCastFailure failure)
        {
            Success = success;
            Message = message;
            Failure = failure;
        }

        public bool Success { get; }
        public string Message { get; }
        public MobaSkillCastFailure Failure { get; }
        public string Code => Failure.Code;

        public static MobaSkillInputHandleResult Accepted(string message = null)
        {
            return new MobaSkillInputHandleResult(true, message, in MobaSkillCastFailure.None);
        }

        public static MobaSkillInputHandleResult Failed(string code, string message)
        {
            var failure = new MobaSkillCastFailure("Input", null, code, message);
            return new MobaSkillInputHandleResult(false, message, in failure);
        }

        public static MobaSkillInputHandleResult FromCast(in MobaSkillCastResult result, string successMessage = null)
        {
            if (result.Success)
            {
                return Accepted(successMessage ?? result.FailReason);
            }

            var failure = result.Failure.HasValue
                ? result.Failure
                : new MobaSkillCastFailure("Cast", null, "skill.input.castRejected", result.FailReason);
            return new MobaSkillInputHandleResult(false, result.FailReason, in failure);
        }
    }

    public readonly struct MobaSkillCastResult
    {
        public MobaSkillCastResult(bool success, string failReason, in MobaSkillCastRuntimeHandle runtimeHandle)
            : this(success, failReason, in runtimeHandle, MobaSkillCastFailure.None)
        {
        }

        public MobaSkillCastResult(bool success, string failReason, in MobaSkillCastRuntimeHandle runtimeHandle, in MobaSkillCastFailure failure)
        {
            Success = success;
            FailReason = failReason;
            RuntimeHandle = runtimeHandle;
            Failure = failure;
        }

        public bool Success { get; }
        public string FailReason { get; }
        public MobaSkillCastRuntimeHandle RuntimeHandle { get; }
        public MobaSkillCastFailure Failure { get; }
        public long RuntimeId => RuntimeHandle.RuntimeId;

        public static MobaSkillCastResult Failed(string failReason)
        {
            return new MobaSkillCastResult(false, failReason, default);
        }

        public static MobaSkillCastResult Failed(string failReason, in MobaSkillCastFailure failure)
        {
            return new MobaSkillCastResult(false, failReason, default, in failure);
        }

        public static MobaSkillCastResult From(bool success, string failReason, in MobaSkillCastRuntimeHandle runtimeHandle)
        {
            return new MobaSkillCastResult(success, failReason, in runtimeHandle);
        }

        public static MobaSkillCastResult From(bool success, string failReason, in MobaSkillCastRuntimeHandle runtimeHandle, in MobaSkillCastFailure failure)
        {
            return new MobaSkillCastResult(success, failReason, in runtimeHandle, in failure);
        }
    }
}
