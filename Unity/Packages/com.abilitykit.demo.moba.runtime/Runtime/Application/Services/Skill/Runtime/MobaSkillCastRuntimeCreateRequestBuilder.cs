using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaSkillCastRuntimeCreateRequestBuilder
    {
        private int _skillId;
        private int _baseSkillId;
        private int _skillSlot;
        private int _skillLevel;
        private int _championLevel;
        private int _sequence;
        private int _casterActorId;
        private int _targetActorId;
        private Vec3 _aimPos;
        private Vec3 _aimDir;
        private long _rootContextId;
        private long _diagnosticCommandId;
        private MobaSkillLevelParameterSnapshot _levelParameters;

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
            _baseSkillId = 0;
            _skillSlot = 0;
            _skillLevel = 0;
            _championLevel = 0;
            _sequence = 0;
            _casterActorId = 0;
            _targetActorId = 0;
            _aimPos = Vec3.Zero;
            _aimDir = Vec3.Forward;
            _rootContextId = 0L;
            _diagnosticCommandId = 0L;
            _levelParameters = MobaSkillLevelParameterSnapshot.Empty;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder FromCastContext(SkillCastContext context)
        {
            if (context == null) return this;

            _skillId = context.SkillId;
            _baseSkillId = context.BaseSkillId;
            _skillSlot = context.SkillSlot;
            _skillLevel = context.SkillLevel;
            _championLevel = context.ChampionLevel;
            _sequence = context.Sequence;
            _casterActorId = context.CasterActorId;
            _targetActorId = context.TargetActorId;
            _aimPos = context.AimPos;
            _aimDir = context.AimDir;
            _rootContextId = context.SourceContextId;
            _diagnosticCommandId = context.DiagnosticCommandId;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder FromRequest(in SkillCastRequest request)
        {
            _skillId = request.SkillId;
            _baseSkillId = 0;
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

        public MobaSkillCastRuntimeCreateRequestBuilder WithRootContext(long rootContextId)
        {
            _rootContextId = rootContextId;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder WithChampionLevel(int championLevel)
        {
            _championLevel = championLevel;
            return this;
        }

        public MobaSkillCastRuntimeCreateRequestBuilder WithLevelParameters(MobaSkillLevelParameterSnapshot parameters)
        {
            _levelParameters = parameters ?? MobaSkillLevelParameterSnapshot.Empty;
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
                _rootContextId,
                _diagnosticCommandId,
                _baseSkillId,
                _levelParameters,
                _championLevel);
        }
    }
}
