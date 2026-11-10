namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SkillCastContextBuilder
    {
        private SkillCastRequest _request;
        private int _skillLevel;
        private int _championLevel;
        private int _sequence;
        private long _sourceContextId;
        private MobaSkillCastRuntimeHandle _runtimeHandle;
        private long _runtimeId;

        private SkillCastContextBuilder()
        {
            Reset();
        }

        public static SkillCastContextBuilder Create()
        {
            return new SkillCastContextBuilder();
        }

        public SkillCastContextBuilder Reset()
        {
            _request = default;
            _skillLevel = 0;
            _championLevel = 0;
            _sequence = 0;
            _sourceContextId = 0L;
            _runtimeHandle = default;
            _runtimeId = 0L;
            return this;
        }

        public SkillCastContextBuilder FromRequest(in SkillCastRequest request)
        {
            _request = request;
            return this;
        }

        public SkillCastContextBuilder WithSkillLevel(int skillLevel)
        {
            _skillLevel = skillLevel;
            return this;
        }

        public SkillCastContextBuilder WithChampionLevel(int championLevel)
        {
            _championLevel = championLevel;
            return this;
        }

        public SkillCastContextBuilder WithSequence(int sequence)
        {
            _sequence = sequence;
            return this;
        }

        public SkillCastContextBuilder WithSourceContext(long sourceContextId)
        {
            _sourceContextId = sourceContextId;
            return this;
        }

        public SkillCastContextBuilder WithRuntime(in MobaSkillCastRuntimeHandle runtimeHandle)
        {
            _runtimeHandle = runtimeHandle;
            _runtimeId = runtimeHandle.RuntimeId;
            return this;
        }

        public SkillCastContextBuilder WithRuntimeId(long runtimeId)
        {
            _runtimeId = runtimeId;
            return this;
        }

        public SkillCastContext Build()
        {
            var context = new SkillCastContext();
            context.Initialize(in _request, _skillLevel, _sequence);
            context.ChampionLevel = _championLevel;
            context.SourceContextId = _sourceContextId;
            context.RuntimeHandle = _runtimeHandle;
            context.RuntimeId = _runtimeId != 0L ? _runtimeId : _runtimeHandle.RuntimeId;
            return context;
        }
    }
}
