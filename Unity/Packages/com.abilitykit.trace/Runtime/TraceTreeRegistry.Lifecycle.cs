using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
    public sealed partial class TraceTreeRegistry
    {
        /// <summary>
        /// 结束指定节点
        /// </summary>
        /// <param name="contextId">节点 ID</param>
        /// <param name="reason">结束原因（使用业务层定义的枚举值）</param>
        /// <returns>是否成功结束</returns>
        public bool End(long contextId, int reason = 0)
        {
            if (!_contexts.TryGetValue(contextId, out var record))
                return false;

            if (record.IsEnded)
                return false;

            // 更新节点记录
            _contexts[contextId] = new TraceContextRecord(
                record.ContextId,
                record.RootId,
                record.ParentId,
                record.Kind,
                record.ConfigId,
                record.SourceActorId,
                record.TargetActorId,
                record.OriginSourceId,
                record.OriginSourceDisplay,
                record.OriginTargetId,
                record.OriginTargetDisplay,
                record.CreatedFrame,
                Frame,
                reason);

            // 更新根节点的活跃计数
            if (_roots.TryGetValue(record.RootId, out var rootRecord))
            {
                _roots[record.RootId] = rootRecord.WithActiveCount(rootRecord.ActiveCount - 1)
                    .WithLastTouchedFrame(Frame);
            }

            return true;
        }

        /// <summary>
        /// 结束根节点及其所有子节点
        /// </summary>
        /// <param name="rootId">根节点 ID</param>
        /// <param name="reason">结束原因</param>
        /// <returns>被结束的节点数量</returns>
        public int EndRoot(long rootId, int reason = 0)
        {
            if (!_roots.ContainsKey(rootId))
                return 0;

            var count = 0;

            // 递归结束所有子节点
            if (_childrenByParent.TryGetValue(rootId, out var children))
            {
                foreach (var childId in children)
                {
                    count += EndRoot(childId, reason);
                }
            }

            // 结束根节点
            if (End(rootId, reason))
                count++;

            return count;
        }

        /// <summary>
        /// 保留根节点（增加外部引用计数）
        /// 防止根节点在还有外部引用时被清理
        /// </summary>
        public void RetainRoot(long rootId)
        {
            if (_roots.TryGetValue(rootId, out var record))
            {
                _roots[rootId] = record.WithExternalRefCount(record.ExternalRefCount + 1);
            }
        }

        /// <summary>
        /// 释放根节点（减少外部引用计数）
        /// </summary>
        public void ReleaseRoot(long rootId)
        {
            if (_roots.TryGetValue(rootId, out var record))
            {
                var newCount = Math.Max(0, record.ExternalRefCount - 1);
                _roots[rootId] = record.WithExternalRefCount(newCount);
            }
        }

        /// <summary>
        /// 清理已结束的根节点
        /// 只有当活跃计数和外部引用计数都为 0 时才会被清理
        /// </summary>
        /// <param name="currentFrame">当前帧号</param>
        /// <param name="keepEndedFrames">已结束节点保留的帧数</param>
        /// <returns>被清理的根节点数量</returns>
        public int Purge(int currentFrame, int keepEndedFrames = 0)
        {
            var purgedCount = 0;
            var toRemove = new List<long>();

            foreach (var kvp in _roots)
            {
                var rootId = kvp.Key;
                var record = kvp.Value;

                // 只有活跃计数和外部引用计数都为 0 时才能清理
                if (record.ActiveCount > 0 || record.ExternalRefCount > 0)
                    continue;

                // 检查是否需要保留
                if (keepEndedFrames > 0)
                {
                    // 获取根节点记录，检查结束帧
                    if (_contexts.TryGetValue(rootId, out var rootRecord) && !rootRecord.IsEnded)
                        continue;

                    var age = currentFrame - rootRecord.EndedFrame;
                    if (age < keepEndedFrames)
                        continue;
                }

                toRemove.Add(rootId);
            }

            foreach (var rootId in toRemove)
            {
                PurgeRoot(rootId);
                purgedCount++;
            }

            return purgedCount;
        }

        /// <summary>
        /// 清理指定根节点及其所有子节点
        /// </summary>
        public void PurgeRoot(long rootId)
        {
            // 递归清理所有子节点
            if (_childrenByParent.TryGetValue(rootId, out var children))
            {
                foreach (var childId in children)
                {
                    PurgeRoot(childId);
                }
            }

            // 清理数据
            _contexts.Remove(rootId);
            _roots.Remove(rootId);
            _childrenByParent.Remove(rootId);
            _metadataStore.Clear(rootId);
        }

        /// <summary>
        /// 清理所有数据
        /// </summary>
        public void Clear()
        {
            _contexts.Clear();
            _roots.Clear();
            _childrenByParent.Clear();
            _nextId = 1;
            // 注意：不清理 metadataStore，因为它是外部传入的
        }
    }
}
