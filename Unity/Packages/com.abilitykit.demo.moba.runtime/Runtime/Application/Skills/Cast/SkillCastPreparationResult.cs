using AbilityKit.Ability;

namespace AbilityKit.Demo.Moba.Services
{
    internal readonly struct SkillCastPreparationResult
    {
        private SkillCastPreparationResult(
            bool success,
            string failReason,
            in MobaSkillCastFailure failure,
            in SkillCastRequest request,
            SkillCastContext context,
            MobaSkillCastRuntimeService runtimes,
            MobaSkillPipelineDefinition definition)
        {
            Success = success;
            FailReason = failReason;
            Failure = failure;
            Request = request;
            Context = context;
            Runtimes = runtimes;
            Definition = definition;
        }

        public bool Success { get; }
        public string FailReason { get; }
        public MobaSkillCastFailure Failure { get; }
        public SkillCastRequest Request { get; }
        public SkillCastContext Context { get; }
        public MobaSkillCastRuntimeService Runtimes { get; }
        internal MobaSkillPipelineDefinition Definition { get; }

        public static SkillCastPreparationResult Failed(string failReason)
        {
            return Failed("skill.cast.prepareFailed", failReason);
        }

        public static SkillCastPreparationResult Failed(string code, string failReason)
        {
            var failure = new MobaSkillCastFailure("Preparation", null, code, failReason);
            return new SkillCastPreparationResult(false, failReason, in failure, default, null, null, null);
        }

        public static SkillCastPreparationResult Ready(
            in SkillCastRequest request,
            SkillCastContext context,
            MobaSkillCastRuntimeService runtimes,
            MobaSkillPipelineDefinition definition)
        {
            return new SkillCastPreparationResult(true, null, in MobaSkillCastFailure.None, in request, context, runtimes, definition);
        }
    }
}
