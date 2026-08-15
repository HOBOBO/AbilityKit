namespace AbilityKit.Demo.Moba.Services
{
    internal static class SkillResultFactory
    {
        internal static MobaSkillCastResult MissingSkillInSlot()
        {
            return MobaSkillCastResult.Failed(
                "Skill not found in slot.",
                new MobaSkillCastFailure("Cast", "Resolve", SkillFailureCodes.Cast.MissingSkill, "Skill not found in slot."));
        }

        internal static MobaSkillCastResult FailedPreparation(in MobaSkillCastFailure failure, string failReason)
        {
            return MobaSkillCastResult.Failed(failReason, in failure);
        }

        internal static MobaSkillCastResult FailedCast(string failReason, in MobaSkillCastFailure failure)
        {
            return MobaSkillCastResult.From(false, failReason, default, in failure);
        }

        internal static MobaSkillInputHandleResult CastRejected(in MobaSkillCastResult result, string successMessage = null)
        {
            if (result.Success)
            {
                return MobaSkillInputHandleResult.Accepted(successMessage ?? result.FailReason);
            }

            var failure = result.Failure.HasValue
                ? result.Failure
                : new MobaSkillCastFailure("Cast", null, SkillFailureCodes.Input.CastRejected, result.FailReason);
            return new MobaSkillInputHandleResult(false, result.FailReason, in failure);
        }

        internal static MobaSkillInputHandleResult InputFailed(string code, string message)
        {
            return MobaSkillInputHandleResult.Failed(code, message);
        }

        internal static MobaSkillInputHandleResult InputAccepted(string message = null)
        {
            return MobaSkillInputHandleResult.Accepted(message);
        }

        internal static MobaSkillCastFailure PipelineStartFailure(in SkillPipelineRunner.SkillPipelineStartResult result)
        {
            if (result.StartReject.HasValue)
            {
                return new MobaSkillCastFailure(
                    "StartReject",
                    null,
                    result.StartReject.Code,
                    result.StartReject.Message ?? result.FailReason);
            }

            if (result.PipelineFailure.HasValue)
            {
                return new MobaSkillCastFailure(
                    "Pipeline",
                    result.PipelineFailure.Stage,
                    result.PipelineFailure.Code,
                    result.PipelineFailure.Message ?? result.FailReason);
            }

            return UnknownCastFailure(result.FailReason);
        }

        internal static MobaSkillCastFailure UnknownCastFailure(string failReason)
        {
            return string.IsNullOrEmpty(failReason)
                ? MobaSkillCastFailure.None
                : new MobaSkillCastFailure("Unknown", null, SkillFailureCodes.Cast.Failed, failReason);
        }
    }
}
