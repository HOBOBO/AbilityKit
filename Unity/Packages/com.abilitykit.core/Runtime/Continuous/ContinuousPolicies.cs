using System;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 旧 Core 持续体准入策略，保留至下一主版本移除窗口；新代码使用 AbilityKit.Continuous。
    /// 默认管理器按列表顺序调用并在首个拒绝处停止；异常传播，不自动执行互斥或替换。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuousAdmissionPolicy
    {
        /// <summary>
        /// 判断是否允许登记，拒绝时可通过 reason 返回原因。
        /// </summary>
        bool CanRegister(IContinuous continuous, IContinuousManager manager, out string? reason);

        /// <summary>
        /// 判断是否允许激活或恢复，拒绝时可通过 reason 返回原因。
        /// </summary>
        bool CanActivate(IContinuous continuous, IContinuousManager manager, out string? reason);
    }

    /// <summary>
    /// 旧 Core 生命周期回调契约，保留至下一主版本移除窗口；新代码使用 AbilityKit.Continuous。
    /// 回调同步执行；注册失败有倒序补偿，其余回调的异常和部分进度按管理器对应节点处理。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public interface IContinuousLifecycleBinder
    {
        /// <summary>
        /// 登记索引及订阅建立后调用；注册回调失败时，也会对已经尝试的当前绑定器执行注销补偿。
        /// </summary>
        void OnRegistered(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 激活状态及活动索引更新后调用；异常传播，旧 Core 不补偿激活。
        /// </summary>
        void OnActivated(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 暂停状态及活动索引更新后调用；不要求对象已经登记。
        /// </summary>
        void OnPaused(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 恢复状态及活动索引更新后调用；不要求对象已经登记。
        /// </summary>
        void OnResumed(IContinuous continuous, IContinuousManager manager);

        /// <summary>
        /// 收到结束事件并移除活动索引后调用；随后在 finally 中尝试注销。
        /// </summary>
        void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager);

        /// <summary>
        /// 正常注销移除索引和订阅后调用，也用于注册失败的倒序补偿；不代表对象已结束。
        /// </summary>
        void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager);
    }

    /// <summary>
    /// 不执行任何生命周期动作的正式空对象，供可选回调装配使用；新代码使用 AbilityKit.Continuous。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public sealed class NullContinuousLifecycleBinder : IContinuousLifecycleBinder
    {
        /// <summary>
        /// 获取无状态空绑定器单例。
        /// </summary>
        public static readonly NullContinuousLifecycleBinder Instance = new NullContinuousLifecycleBinder();

        private NullContinuousLifecycleBinder()
        {
        }

        /// <summary>
        /// 接受注册通知，不执行动作。
        /// </summary>
        public void OnRegistered(IContinuous continuous, IContinuousManager manager) { }

        /// <summary>
        /// 接受激活通知，不执行动作。
        /// </summary>
        public void OnActivated(IContinuous continuous, IContinuousManager manager) { }

        /// <summary>
        /// 接受暂停通知，不执行动作。
        /// </summary>
        public void OnPaused(IContinuous continuous, IContinuousManager manager) { }

        /// <summary>
        /// 接受恢复通知，不执行动作。
        /// </summary>
        public void OnResumed(IContinuous continuous, IContinuousManager manager) { }

        /// <summary>
        /// 接受结束通知，不执行动作。
        /// </summary>
        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) { }

        /// <summary>
        /// 接受注销通知，不执行动作。
        /// </summary>
        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) { }
    }

    /// <summary>
    /// 只做基本有效性检查的旧 Core 准入策略；新代码使用 AbilityKit.Continuous。
    /// 名称不表示无条件成功，拒绝时也不填写原因。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public sealed class AllowAllContinuousAdmissionPolicy : IContinuousAdmissionPolicy
    {
        /// <summary>
        /// 获取无状态默认准入策略单例。
        /// </summary>
        public static readonly AllowAllContinuousAdmissionPolicy Instance = new AllowAllContinuousAdmissionPolicy();

        private AllowAllContinuousAdmissionPolicy()
        {
        }

        /// <summary>
        /// 仅在持续体及配置均非空时允许登记；reason 始终为空。
        /// </summary>
        public bool CanRegister(IContinuous continuous, IContinuousManager manager, out string? reason)
        {
            reason = null;
            return continuous != null && continuous.Config != null;
        }

        /// <summary>
        /// 仅在持续体非空且未终止时允许激活；不检查配置，reason 始终为空。
        /// </summary>
        public bool CanActivate(IContinuous continuous, IContinuousManager manager, out string? reason)
        {
            reason = null;
            return continuous != null && !continuous.IsTerminated;
        }
    }

    /// <summary>
    /// 按同一所属实体的其他活跃持续体标签阻止激活的旧 Core 策略；新代码使用 AbilityKit.Continuous。
    /// 只拦截激活和恢复，不执行暂停、互斥或替换，也不读取目标的 PauseByTags 或 BlockByTags。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public sealed class BlockByOwnerActiveTagsPolicy : IContinuousAdmissionPolicy
    {
        private readonly ITagContainer _blockedTags;

        /// <summary>
        /// 保存待阻止标签容器的原始引用，不复制；后续容器变更会影响判断。
        /// </summary>
        public BlockByOwnerActiveTagsPolicy(ITagContainer blockedTags)
        {
            _blockedTags = blockedTags;
        }

        /// <summary>
        /// 允许登记，不做标签检查，reason 始终为空。
        /// </summary>
        public bool CanRegister(IContinuous continuous, IContinuousManager manager, out string? reason)
        {
            reason = null;
            return true;
        }

        /// <summary>
        /// 查询同一所属实体的实时活跃对象，按引用跳过目标自身；存在匹配标签时拒绝。
        /// 持续体、管理器或标签容器为空，以及标签数量为零时直接允许。
        /// </summary>
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
