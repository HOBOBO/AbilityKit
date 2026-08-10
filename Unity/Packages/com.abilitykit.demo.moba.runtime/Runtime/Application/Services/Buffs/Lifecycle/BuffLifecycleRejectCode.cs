namespace AbilityKit.Demo.Moba.Services.Buffs.Lifecycle
{
    /// <summary>
    /// Buff 生命周期拒绝码：集中定义入口可观测的失败分类，避免散落字符串难以治理。
    /// </summary>
    internal enum BuffLifecycleRejectCode
    {
        None = 0,
        LifecycleRejected = 1,
        ApplyInvalidRequest = 100,
        ApplyConfigDatabaseMissing = 101,
        ApplyConfigNotFound = 102,
        ApplyTargetNotFound = 103,
        ApplyTagRequirementsBlocked = 104,
        ApplyRuntimeListUnavailable = 105,
        ApplyExistingRuntimeMissing = 106,
        ApplyContinuousActivationFailed = 107,
        ApplySkillRuntimeBindingFailed = 108,
        ApplyReplacementCommitFailed = 109,
        RemoveInvalidRequest = 200,
        RemoveTargetNotFound = 201,
        RemoveBuffsComponentMissing = 202,
        RemoveNoActiveRuntimes = 203,
        RemoveRuntimeNotFound = 204,
    }

    internal static class BuffLifecycleRejectCodes
    {
        public const string LifecycleRejected = "buff.lifecycle.rejected";
        public const string ApplyInvalidRequest = "buff.apply.invalidRequest";
        public const string ApplyConfigDatabaseMissing = "buff.apply.configDatabaseMissing";
        public const string ApplyConfigNotFound = "buff.apply.configNotFound";
        public const string ApplyTargetNotFound = "buff.apply.targetNotFound";
        public const string ApplyTagRequirementsBlocked = "buff.apply.tagRequirementsBlocked";
        public const string ApplyRuntimeListUnavailable = "buff.apply.runtimeListUnavailable";
        public const string ApplyExistingRuntimeMissing = "buff.apply.existingRuntimeMissing";
        public const string ApplyContinuousActivationFailed = "buff.apply.continuousActivationFailed";
        public const string ApplySkillRuntimeBindingFailed = "buff.apply.skillRuntimeBindingFailed";
        public const string ApplyReplacementCommitFailed = "buff.apply.replacementCommitFailed";
        public const string RemoveInvalidRequest = "buff.remove.invalidRequest";
        public const string RemoveTargetNotFound = "buff.remove.targetNotFound";
        public const string RemoveBuffsComponentMissing = "buff.remove.buffsComponentMissing";
        public const string RemoveNoActiveRuntimes = "buff.remove.noActiveRuntimes";
        public const string RemoveRuntimeNotFound = "buff.remove.runtimeNotFound";

        public static string ToCode(BuffLifecycleRejectCode code)
        {
            switch (code)
            {
                case BuffLifecycleRejectCode.ApplyInvalidRequest: return ApplyInvalidRequest;
                case BuffLifecycleRejectCode.ApplyConfigDatabaseMissing: return ApplyConfigDatabaseMissing;
                case BuffLifecycleRejectCode.ApplyConfigNotFound: return ApplyConfigNotFound;
                case BuffLifecycleRejectCode.ApplyTargetNotFound: return ApplyTargetNotFound;
                case BuffLifecycleRejectCode.ApplyTagRequirementsBlocked: return ApplyTagRequirementsBlocked;
                case BuffLifecycleRejectCode.ApplyRuntimeListUnavailable: return ApplyRuntimeListUnavailable;
                case BuffLifecycleRejectCode.ApplyExistingRuntimeMissing: return ApplyExistingRuntimeMissing;
                case BuffLifecycleRejectCode.ApplyContinuousActivationFailed: return ApplyContinuousActivationFailed;
                case BuffLifecycleRejectCode.ApplySkillRuntimeBindingFailed: return ApplySkillRuntimeBindingFailed;
                case BuffLifecycleRejectCode.ApplyReplacementCommitFailed: return ApplyReplacementCommitFailed;
                case BuffLifecycleRejectCode.RemoveInvalidRequest: return RemoveInvalidRequest;
                case BuffLifecycleRejectCode.RemoveTargetNotFound: return RemoveTargetNotFound;
                case BuffLifecycleRejectCode.RemoveBuffsComponentMissing: return RemoveBuffsComponentMissing;
                case BuffLifecycleRejectCode.RemoveNoActiveRuntimes: return RemoveNoActiveRuntimes;
                case BuffLifecycleRejectCode.RemoveRuntimeNotFound: return RemoveRuntimeNotFound;
                case BuffLifecycleRejectCode.None: return null;
                case BuffLifecycleRejectCode.LifecycleRejected:
                default:
                    return LifecycleRejected;
            }
        }
    }
}
