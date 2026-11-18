using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Continuous
{
    /// <summary>
    /// 旧 Core 默认持续体管理器，保留至下一主版本移除窗口；新代码使用 AbilityKit.Continuous。
    /// 按默认相等比较登记，按注册顺序查询，不按配置 Id 去重；不推进时间或自动执行配置扩展策略。
    /// 非线程安全；登记期间应保持配置、OwnerId、哈希及相等性稳定。回调不具备通用重入事务保证。
    /// </summary>
    [Obsolete("Continuous lifecycle APIs no longer belong in Core. Use AbilityKit.Continuous; this compatibility API will be removed in the next major version.")]
    public class DefaultContinuousManager : IContinuousManager
    {
        private readonly Dictionary<long, List<IContinuous>> _ownerContinuous = new Dictionary<long, List<IContinuous>>();
        private readonly HashSet<IContinuous> _registered = new HashSet<IContinuous>();
        private readonly HashSet<IContinuous> _active = new HashSet<IContinuous>();
        private readonly List<IContinuous> _registrationOrder = new List<IContinuous>();
        private readonly List<IContinuousAdmissionPolicy> _admissionPolicies = new List<IContinuousAdmissionPolicy>();
        private readonly List<IContinuousLifecycleBinder> _lifecycleBinders = new List<IContinuousLifecycleBinder>();

        /// <summary>
        /// 注册绑定器全部成功后同步触发；事件异常传播，但不回滚已经完成的登记。
        /// </summary>
        public event Action<IContinuous>? OnRegistered;

        /// <summary>
        /// 由非激活进入激活后，在激活绑定器之后同步触发；异常不补偿已完成的激活。
        /// </summary>
        public event Action<IContinuous>? OnActivated;

        /// <summary>
        /// 注销索引及绑定器通知完成后同步触发；绑定器异常会阻止本事件。
        /// </summary>
        public event Action<IContinuous, ContinuousEndReason>? OnUnregistered;

        /// <summary>
        /// 获取最近保留的准入拒绝原因。
        /// Register 和 TryActivate 在入口清空；TryResume 仅在准入拒绝时写入，成功时可保留旧值。
        /// </summary>
        public string? LastRejectReason { get; private set; }

        /// <summary>
        /// 获取活动索引数量；直接调用持续体方法不会自动同步该索引。
        /// </summary>
        public int ActiveCount => _active.Count;

        /// <summary>
        /// 获取已登记对象数量，采用默认相等比较。
        /// </summary>
        public int TotalCount => _registered.Count;

        /// <summary>
        /// 按枚举顺序复制策略和绑定器到内部列表；构造时不去重，也不筛除列表中的空元素。
        /// </summary>
        public DefaultContinuousManager(
            IEnumerable<IContinuousAdmissionPolicy>? admissionPolicies = null,
            IEnumerable<IContinuousLifecycleBinder>? lifecycleBinders = null)
        {
            if (admissionPolicies != null)
                _admissionPolicies.AddRange(admissionPolicies);

            if (lifecycleBinders != null)
                _lifecycleBinders.AddRange(lifecycleBinders);
        }

        /// <summary>
        /// 追加非空且按默认相等比较尚不存在的准入策略。
        /// </summary>
        public void AddAdmissionPolicy(IContinuousAdmissionPolicy policy)
        {
            if (policy != null && !_admissionPolicies.Contains(policy))
                _admissionPolicies.Add(policy);
        }

        /// <summary>
        /// 按默认相等比较移除首个匹配策略；空参数或未找到时返回假。
        /// </summary>
        public bool RemoveAdmissionPolicy(IContinuousAdmissionPolicy policy)
        {
            return policy != null && _admissionPolicies.Remove(policy);
        }

        /// <summary>
        /// 追加非空且按默认相等比较尚不存在的生命周期绑定器。
        /// </summary>
        public void AddLifecycleBinder(IContinuousLifecycleBinder binder)
        {
            if (binder != null && !_lifecycleBinders.Contains(binder))
                _lifecycleBinders.Add(binder);
        }

        /// <summary>
        /// 按默认相等比较移除首个匹配绑定器；空参数或未找到时返回假。
        /// </summary>
        public bool RemoveLifecycleBinder(IContinuousLifecycleBinder binder)
        {
            return binder != null && _lifecycleBinders.Remove(binder);
        }

        /// <summary>
        /// 检查配置及准入后登记；重复登记直接成功，不重复准入或通知。
        /// 注册绑定器使用快照；失败时先撤销登记，再对已尝试绑定器倒序补偿，包括失败者。
        /// 补偿异常被隔离以保留原异常；之后的公共注册事件异常不触发回滚。
        /// </summary>
        public bool Register(IContinuous continuous)
        {
            LastRejectReason = null;
            if (continuous == null || continuous.Config == null)
            {
                LastRejectReason = "Continuous or config is null";
                return false;
            }

            if (_registered.Contains(continuous))
                return true;

            if (!CanRegister(continuous, out var reason))
            {
                LastRejectReason = reason;
                return false;
            }

            _registered.Add(continuous);
            _registrationOrder.Add(continuous);
            AddOwnerIndex(continuous);
            continuous.OnEnded += HandleContinuousEnded;

            var binders = new List<IContinuousLifecycleBinder>(_lifecycleBinders);
            var attemptedBinderCount = 0;
            try
            {
                for (int i = 0; i < binders.Count; i++)
                {
                    attemptedBinderCount = i + 1;
                    binders[i].OnRegistered(continuous, this);
                }
            }
            catch
            {
                RollbackRegistration(continuous);
                CompensateRegistrationBinders(continuous, binders, attemptedBinderCount);
                throw;
            }

            OnRegistered?.Invoke(continuous);
            return true;
        }

        /// <summary>
        /// 先移除登记、索引和结束事件订阅，再通知当前绑定器及公共事件；不调用 End 或 Abort。
        /// 空对象或未登记对象无动作；回调异常传播并中断后续通知，已移除索引不回滚。
        /// </summary>
        public void Unregister(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.CleanedUp)
        {
            if (continuous == null || !_registered.Remove(continuous))
                return;

            continuous.OnEnded -= HandleContinuousEnded;
            _active.Remove(continuous);
            _registrationOrder.Remove(continuous);
            RemoveOwnerIndex(continuous);

            NotifyUnregistered(continuous, reason);
            OnUnregistered?.Invoke(continuous, reason);
        }

        /// <summary>
        /// 自动登记后进行激活准入，并调用 Activate，包括已经激活的对象。
        /// 按调用后状态更新活动索引，仅在此前非激活且当前激活时通知绑定器及事件。
        /// 异常传播且不补偿，激活绑定器失败时仍保留已提交的登记与活动索引。
        /// </summary>
        public bool TryActivate(IContinuous continuous)
        {
            LastRejectReason = null;
            if (continuous == null)
            {
                LastRejectReason = "Continuous is null";
                return false;
            }

            if (!_registered.Contains(continuous) && !Register(continuous))
                return false;

            if (!CanActivate(continuous, out var reason))
            {
                LastRejectReason = reason;
                return false;
            }

            var wasActive = continuous.IsActive;
            continuous.Activate();
            if (continuous.IsActive)
                _active.Add(continuous);
            else
                _active.Remove(continuous);

            if (continuous.IsActive && !wasActive)
            {
                NotifyActivated(continuous);
                OnActivated?.Invoke(continuous);
            }

            return continuous.IsActive;
        }

        /// <summary>
        /// 按注册顺序返回当前所属实体索引列表的只读包装，后续同列表变更可见。
        /// 最后一项移除后旧包装不会跟随该实体新建的索引列表；未找到时返回空数组。
        /// </summary>
        public IReadOnlyList<IContinuous> GetOwnerContinuous(long ownerId)
        {
            if (!_ownerContinuous.TryGetValue(ownerId, out var list))
                return Array.Empty<IContinuous>();

            return list.AsReadOnly();
        }

        /// <summary>
        /// 按所属实体注册顺序返回副本，实时筛选 IsActive 且未终止的对象。
        /// 结果可能与基于活动索引的 ActiveCount 或 GetAllActiveContinuous 不同。
        /// </summary>
        public IReadOnlyList<IContinuous> GetOwnerActiveContinuous(long ownerId)
        {
            if (!_ownerContinuous.TryGetValue(ownerId, out var list) || list.Count == 0)
                return Array.Empty<IContinuous>();

            var result = new List<IContinuous>();
            for (int i = 0; i < list.Count; i++)
            {
                var continuous = list[i];
                if (continuous != null && continuous.IsActive && !continuous.IsTerminated)
                    result.Add(continuous);
            }

            return result;
        }

        /// <summary>
        /// 返回全部已登记持续体的副本，保持注册顺序。
        /// </summary>
        public IReadOnlyList<IContinuous> GetAllContinuous()
        {
            return new List<IContinuous>(_registrationOrder);
        }

        /// <summary>
        /// 按注册顺序返回活动索引成员的副本，不重新检查持续体的实时状态。
        /// </summary>
        public IReadOnlyList<IContinuous> GetAllActiveContinuous()
        {
            var result = new List<IContinuous>(_active.Count);
            for (int i = 0; i < _registrationOrder.Count; i++)
            {
                var continuous = _registrationOrder[i];
                if (_active.Contains(continuous))
                    result.Add(continuous);
            }

            return result;
        }

        /// <summary>
        /// 先复制所属实体列表，再依次尝试中断；回调新增项不进入本轮，异常中断且保留此前进度。
        /// </summary>
        public void InterruptAll(long ownerId, string reason)
        {
            var snapshot = SnapshotOwner(ownerId);
            for (int i = 0; i < snapshot.Count; i++)
                TryInterrupt(snapshot[i], reason);
        }

        /// <summary>
        /// 对非空且未终止对象调用 End 并返回调用后的终止标记，不要求对象已经登记。
        /// 已登记对象依赖其结束事件触发索引清理；异常直接传播。
        /// </summary>
        public bool TryEnd(IContinuous continuous, ContinuousEndReason reason = ContinuousEndReason.Completed)
        {
            if (continuous == null || continuous.IsTerminated)
                return false;

            continuous.End(reason);
            return continuous.IsTerminated;
        }

        /// <summary>
        /// 对非空、未终止且配置允许中断的对象调用 Abort，返回调用后的终止标记。
        /// 不要求对象已经登记，不自动补发结束事件，异常直接传播。
        /// </summary>
        public bool TryInterrupt(IContinuous continuous, string reason)
        {
            if (continuous == null || continuous.IsTerminated || !continuous.Config.CanBeInterrupted)
                return false;

            continuous.Abort(reason);
            return continuous.IsTerminated;
        }

        /// <summary>
        /// 先复制所属实体列表再逐项暂停；快照中的已注销项仍可被操作，异常中断本轮。
        /// </summary>
        public void PauseAll(long ownerId)
        {
            var snapshot = SnapshotOwner(ownerId);
            for (int i = 0; i < snapshot.Count; i++)
                TryPause(snapshot[i]);
        }

        /// <summary>
        /// 对激活、未暂停且未终止的对象调用 Pause，成功后移除活动索引并通知当前绑定器。
        /// 不要求已经登记；状态未变为暂停时返回假，回调异常不回滚暂停。
        /// </summary>
        public bool TryPause(IContinuous continuous)
        {
            if (continuous == null || !continuous.IsActive || continuous.IsPaused || continuous.IsTerminated)
                return false;

            continuous.Pause();
            if (!continuous.IsPaused)
                return false;

            _active.Remove(continuous);
            NotifyPaused(continuous);
            return true;
        }

        /// <summary>
        /// 先复制所属实体列表再逐项恢复；回调新增项不进入本轮，异常中断本轮。
        /// </summary>
        public void ResumeAll(long ownerId)
        {
            var snapshot = SnapshotOwner(ownerId);
            for (int i = 0; i < snapshot.Count; i++)
                TryResume(snapshot[i]);
        }

        /// <summary>
        /// 对暂停且未终止的对象执行激活准入，再调用 Resume；激活后加入活动索引并通知当前绑定器。
        /// 不自动登记，因而可产生未登记的活动索引成员；成功时不清空 LastRejectReason。
        /// </summary>
        public bool TryResume(IContinuous continuous)
        {
            if (continuous == null || !continuous.IsPaused || continuous.IsTerminated)
                return false;

            if (!CanActivate(continuous, out var reason))
            {
                LastRejectReason = reason;
                return false;
            }

            continuous.Resume();
            if (!continuous.IsActive)
                return false;

            _active.Add(continuous);
            NotifyResumed(continuous);
            return true;
        }

        /// <summary>
        /// 按注册顺序复制列表后逐项注销，不结束持续体。
        /// 回调中新登记的对象可以保留；异常中断清理，已完成的注销不回滚。
        /// </summary>
        public void Clear(ContinuousEndReason reason = ContinuousEndReason.CleanedUp)
        {
            var snapshot = new List<IContinuous>(_registrationOrder);
            for (int i = 0; i < snapshot.Count; i++)
                Unregister(snapshot[i], reason);
        }

        /// <summary>
        /// 按当前策略列表顺序检查登记准入，首个拒绝即返回；异常传播。
        /// </summary>
        protected virtual bool CanRegister(IContinuous continuous, out string? reason)
        {
            for (int i = 0; i < _admissionPolicies.Count; i++)
            {
                if (!_admissionPolicies[i].CanRegister(continuous, this, out reason))
                    return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// 先拒绝空对象或终止对象，再按当前策略列表顺序检查激活准入，首个拒绝即返回。
        /// </summary>
        protected virtual bool CanActivate(IContinuous continuous, out string? reason)
        {
            if (continuous == null || continuous.IsTerminated)
            {
                reason = "Continuous is null or terminated";
                return false;
            }

            for (int i = 0; i < _admissionPolicies.Count; i++)
            {
                if (!_admissionPolicies[i].CanActivate(continuous, this, out reason))
                    return false;
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// 保留的兼容扩展点：空对象返回清理，中止状态返回中断，其他状态返回完成。
        /// 默认结束事件链不调用此方法，而是直接透传事件原因。
        /// </summary>
        protected virtual ContinuousEndReason ResolveEndReason(IContinuous continuous)
        {
            if (continuous == null)
                return ContinuousEndReason.CleanedUp;

            return continuous.State == ContinuousState.Aborted
                ? ContinuousEndReason.Interrupted
                : ContinuousEndReason.Completed;
        }

        private void HandleContinuousEnded(IContinuous continuous, ContinuousEndReason reason)
        {
            if (continuous == null)
                return;

            _active.Remove(continuous);
            try
            {
                NotifyEnded(continuous, reason);
            }
            finally
            {
                Unregister(continuous, reason);
            }
        }

        private void RollbackRegistration(IContinuous continuous)
        {
            continuous.OnEnded -= HandleContinuousEnded;
            _active.Remove(continuous);
            _registrationOrder.Remove(continuous);
            RemoveOwnerIndex(continuous);
            _registered.Remove(continuous);
        }

        private void CompensateRegistrationBinders(
            IContinuous continuous,
            IReadOnlyList<IContinuousLifecycleBinder> binders,
            int attemptedBinderCount)
        {
            for (int i = attemptedBinderCount - 1; i >= 0; i--)
            {
                try
                {
                    binders[i].OnUnregistered(continuous, ContinuousEndReason.CleanedUp, this);
                }
                catch
                {
                    /*
                     * 补偿尽力执行；隔离补偿异常，保留最初的注册失败。
                     */
                }
            }
        }

        private void AddOwnerIndex(IContinuous continuous)
        {
            var ownerId = continuous.Config.OwnerId;
            if (!_ownerContinuous.TryGetValue(ownerId, out var list))
            {
                list = new List<IContinuous>();
                _ownerContinuous.Add(ownerId, list);
            }

            if (!list.Contains(continuous))
                list.Add(continuous);
        }

        private void RemoveOwnerIndex(IContinuous continuous)
        {
            var ownerId = continuous.Config.OwnerId;
            if (!_ownerContinuous.TryGetValue(ownerId, out var list))
                return;

            list.Remove(continuous);
            if (list.Count == 0)
                _ownerContinuous.Remove(ownerId);
        }

        private List<IContinuous> SnapshotOwner(long ownerId)
        {
            if (!_ownerContinuous.TryGetValue(ownerId, out var list))
                return new List<IContinuous>();

            return new List<IContinuous>(list);
        }

        private void NotifyActivated(IContinuous continuous)
        {
            for (int i = 0; i < _lifecycleBinders.Count; i++)
                _lifecycleBinders[i].OnActivated(continuous, this);
        }

        private void NotifyPaused(IContinuous continuous)
        {
            for (int i = 0; i < _lifecycleBinders.Count; i++)
                _lifecycleBinders[i].OnPaused(continuous, this);
        }

        private void NotifyResumed(IContinuous continuous)
        {
            for (int i = 0; i < _lifecycleBinders.Count; i++)
                _lifecycleBinders[i].OnResumed(continuous, this);
        }

        private void NotifyEnded(IContinuous continuous, ContinuousEndReason reason)
        {
            for (int i = 0; i < _lifecycleBinders.Count; i++)
                _lifecycleBinders[i].OnEnded(continuous, reason, this);
        }

        private void NotifyUnregistered(IContinuous continuous, ContinuousEndReason reason)
        {
            for (int i = 0; i < _lifecycleBinders.Count; i++)
                _lifecycleBinders[i].OnUnregistered(continuous, reason, this);
        }
    }
}
