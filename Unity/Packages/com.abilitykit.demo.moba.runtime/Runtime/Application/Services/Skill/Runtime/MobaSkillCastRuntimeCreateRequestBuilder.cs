using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaSkillCastRuntimeCreateRequestBuilder
    {
        private int _skillId;
        private int _skillSlot;
        private int _skillLevel;
        private int _sequence;
        private int _casterActorId;
        private int _targetActorId;
        private Vec3 _aimPos;
        private Vec3 _aimDir;
        private long _rootTraceContextId;
        private long _diagnosticCommandId;

        private MobaSkillCastRuntimeCreateRequestBuilder()
        {
            Reset();
        }

        public static MobaSkillCastRuntimeCreateRequestBuilder Create()
        {
            return new MobaSkillCastRuntimeCreateRequestBuilder();
        }

        public MobaSkillCastRuntimeCreateRequestBuilder Reset()
        {
            _skillId = 0;
            _skillSlot = 0;
            _skillLevel = 0;
            _sequence = 0;
            _casterActorId = 0;
            _targetActorId = 0;
            _aimPos = Vec3.Zero;
            _aimDir = Vec3.Forward;
            _rootTraceContextId = 0L;
            _diagnosticCommandId = 0L;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder FromCastContext(SkillCastContext context)
        {
            if (context == null) return this;

            _skillId = context.SkillId;
            _skillSlot = context.SkillSlot;
            _skillLevel = context.SkillLevel;
            _sequence = context.Sequence;
            _casterActorId = context.CasterActorId;
            _targetActorId = context.TargetActorId;
            _aimPos = context.AimPos;
            _aimDir = context.AimDir;
            _rootTraceContextId = context.SourceContextId;
            _diagnosticCommandId = context.DiagnosticCommandId;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder FromRequest(in SkillCastRequest request)
        {
            _skillId = request.SkillId;
            _skillSlot = request.SkillSlot;
            _casterActorId = request.CasterActorId;
            _targetActorId = request.TargetActorId;
            _aimPos = request.AimPos;
            _aimDir = request.AimDir;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder WithSkillLevel(int skillLevel)
        {
            _skillLevel = skillLevel;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder WithSequence(int sequence)
        {
            _sequence = sequence;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder WithRootTraceContext(long rootTraceContextId)
        {
            _rootTraceContextId = rootTraceContextId;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequest Build()
        {
            return new MobaSkillCastRuntimeCreateRequest(
                _skillId,
                _skillSlot,
                _skillLevel,
                _sequence,
                _casterActorId,
                _targetActorId,
                in _aimPos,
                in _aimDir,
                _rootTraceContextId,
                _diagnosticCommandId);
        }
    }
}
