namespace AbilityKit.Demo.Moba.Services
{
    internal static class SkillFailureCodes
    {
        internal static class Input
        {
            public const string InvalidActor = "skill.input.invalidActor";
            public const string InvalidSlot = "skill.input.invalidSlot";
            public const string UnsupportedPhase = "skill.input.unsupportedPhase";
            public const string NoRunningForHold = "skill.input.noRunningForHold";
            public const string NoRunningForCancel = "skill.input.noRunningForCancel";
            public const string CastRejected = "skill.input.castRejected";
        }

        internal static class Cast
        {
            public const string PreparationFailed = "skill.cast.prepareFailed";
            public const string MissingSkill = "skill.cast.missingSkill";
            public const string InvalidCaster = "skill.cast.invalidCaster";
            public const string InvalidSkill = "skill.cast.invalidSkill";
            public const string CasterMissing = "skill.cast.casterMissing";
            public const string TargetMissing = "skill.cast.targetMissing";
            public const string ConfigurationInvalid = "skill.cast.configurationInvalid";
            public const string PipelineMissing = "skill.cast.pipelineMissing";
            public const string TraceRegistryMissing = "skill.cast.traceRegistryMissing";
            public const string TraceRootCreateFailed = "skill.cast.traceRootCreateFailed";
            public const string RuntimeServiceMissing = "skill.cast.runtimeServiceMissing";
            public const string RuntimeHandleInvalid = "skill.cast.runtimeHandleInvalid";
            public const string Failed = "skill.cast.failed";
        }

        internal static class Start
        {
            public const string AlreadyRunning = "skill.start.alreadyRunning";
            public const string ContextMissing = "skill.start.contextMissing";
            public const string CastConfigMissing = "skill.start.castConfigMissing";
            public const string CastPhasesMissing = "skill.start.castPhasesMissing";
            public const string FrameTimeMissing = "skill.start.frameTimeMissing";
            public const string FrameResolveFailed = "skill.start.frameResolveFailed";
            public const string Rejected = "skill.start.rejected";
        }

        internal static class Pipeline
        {
            public const string PreCastFailed = "skill.pipeline.preCastFailed";
            public const string CastFailed = "skill.pipeline.castFailed";
            public const string Failed = "skill.pipeline.failed";
        }
    }
}
