using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 持续体准入策略。
    /// 用于在注册或激活前处理标签阻止、互斥、替换等横切规则。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuousAdmissionPolicy
    {
        /// <summary>
        /// 判断持续体是否允许注册到管理器。
        /// </summary>
        bool CanRegister(IContinuous continuous, IContinuousManager manager, out string? reason);

        /// <summary>
        /// 判断持续体是否允许进入激活态。
        /// </summary>
        bool CanActivate(IContinuous continuous, IContinuousManager manager, out string? reason);
    }

    /// <summary>
    /// 持续体生命周期绑定器。
    /// 用于在生命周期节点挂接 modifier、表现层状态、统计等横切能力。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuousLifecycleBinder
    {
        /// <summary>
        /// 持续体注册后调用。
        /// </summary>
        void OnRegistered(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 持续体激活后调用。
        /// </summary>
        void OnActivated(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 持续体暂停后调用。
        /// </summary>
        void OnPaused(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 持续体恢复后调用。
        /// </summary>
        void OnResumed(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 持续体结束时调用。
        /// </summary>
        void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager);

        /// <summary>
        /// 持续体注销后调用。
        /// </summary>
        void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager);
    }

    /// <summary>
    /// 空生命周期绑定器。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public sealed class NullContinuousLifecycleBinder : IContinuousLifecycleBinder
    {
        /// <summary>
        /// 空生命周期绑定器单例。
        /// </summary>
        public static readonly NullContinuousLifecycleBinder Instance = new NullContinuousLifecycleBinder();

        private NullContinuousLifecycleBinder()
        {
        }

        /// <inheritdoc />
        public void OnRegistered(IContinuous continuous, IContinuousManager manager) { }

        /// <inheritdoc />
        public void OnActivated(IContinuous continuous, IContinuousManager manager) { }

        /// <inheritdoc />
        public void OnPaused(IContinuous continuous, IContinuousManager manager) { }

        /// <inheritdoc />
        public void OnResumed(IContinuous continuous, IContinuousManager manager) { }

        /// <inheritdoc />
        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) { }

        /// <inheritdoc />
        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) { }
    }

    /// <summary>
    /// 默认准入策略。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public sealed class AllowAllContinuousAdmissionPolicy : IContinuousAdmissionPolicy
    {
        /// <summary>
        /// 默认准入策略单例。
        /// </summary>
        public static readonly AllowAllContinuousAdmissionPolicy Instance = new AllowAllContinuousAdmissionPolicy();

        private AllowAllContinuousAdmissionPolicy()
        {
        }

        /// <inheritdoc />
        public bool CanRegister(IContinuous continuous, IContinuousManager manager, out string? reason)
        {
            reason = null;
            return continuous != null && continuous.Config != null;
        }

        /// <inheritdoc />
        public bool CanActivate(IContinuous continuous, IContinuousManager manager, out string? reason)
        {
            reason = null;
            return continuous != null && !continuous.IsTerminated;
        }
    }

    /// <summary>
    /// 按标签阻止激活的准入策略。
    /// 当同一 owner 已存在匹配 blockedTags 的活跃持续体时，阻止目标持续体激活。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public sealed class BlockByOwnerActiveTagsPolicy : IContinuousAdmissionPolicy
    {
        private readonly ITagContainer _blockedTags;

        /// <summary>
        /// 创建按 owner 活跃标签阻止激活的策略。
        /// </summary>
        public BlockByOwnerActiveTagsPolicy(ITagContainer blockedTags)
        {
            _blockedTags = blockedTags;
        }

        /// <inheritdoc />
        public bool CanRegister(IContinuous continuous, IContinuousManager manager, out string? reason)
        {
            reason = null;
            return true;
        }

        /// <inheritdoc />
        public bool CanActivate(IContinuous continuous, IContinuousManager manager, out string? reason)
        {
            reason = null;
            if (continuous == null || manager == null || _blockedTags == null || _blockedTags.Count == 0)
                return true;

            var active = manager.GetOwnerActiveContinuous(continuous.Config.OwnerId);
            for (int i = 0; i < active.Count; i++)
            {
                if (ReferenceEquals(active[i], continuous))
                    continue;

                var tagConfig = active[i].Config as ITagConfig;
                if (tagConfig != null && tagConfig.Tags != null && tagConfig.Tags.HasAny(_blockedTags))
                {
                    reason = "Blocked by active continuous tags";
                    return false;
                }
            }

            return true;
        }
    }
}
