using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
    public sealed partial class TraceTreeRegistry
    {
        /// <summary>
        /// 创建根节点
        /// </summary>
        /// <param name="kind">节点种类（使用业务层定义的枚举值）</param>
        /// <param name="sourceActorId">源角色 ID</param>
        /// <param name="targetActorId">目标角色 ID</param>
        /// <param name="originSource">源原始对象（用于溯源，由 ITraceContextSource 提取标识）</param>
        /// <param name="originTarget">目标原始对象（用于溯源，由 ITraceContextSource 提取标识）</param>
        /// <param name="configId">配置 ID（业务层自定义）</param>
        /// <returns>新创建的根节点 ID</returns>
        public long CreateRoot(
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            var contextId = NewId();
            var (originId, originDisplay) = ExtractOrigin(originSource);
            var (targetId, targetDisplay) = ExtractOrigin(originTarget);

            var record = new TraceContextRecord(
                contextId: contextId,
                rootId: contextId,
                parentId: 0,
                kind: kind,
                configId: configId,
                sourceActorId: sourceActorId,
                targetActorId: targetActorId,
                originSourceId: originId,
                originSourceDisplay: originDisplay,
                originTargetId: targetId,
                originTargetDisplay: targetDisplay,
                createdFrame: Frame,
                endedFrame: 0,
                endReason: 0);

            _contexts[contextId] = record;
            _roots[contextId] = new TraceRootRecord(
                activeCount: 1,
                externalRefCount: 0,
                lastTouchedFrame: Frame);
            _childrenByParent[contextId] = new List<long>();

            return contextId;
        }

        /// <summary>
        /// 创建子节点
        /// </summary>
        /// <param name="parentContextId">父节点 ID</param>
        /// <param name="kind">节点种类</param>
        /// <param name="sourceActorId">源角色 ID</param>
        /// <param name="targetActorId">目标角色 ID</param>
        /// <param name="originSource">源原始对象（为空时继承父节点）</param>
        /// <param name="originTarget">目标原始对象（为空时继承父节点）</param>
        /// <param name="configId">配置 ID</param>
        /// <returns>新创建的子节点 ID</returns>
        public long CreateChild(
            long parentContextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            if (!_contexts.TryGetValue(parentContextId, out var parentRecord))
                throw new ArgumentException($"Parent context {parentContextId} not found", nameof(parentContextId));

            var contextId = NewId();

            // 如果未提供 origin，则继承父节点的 origin
            var (originId, originDisplay) = originSource != null
                ? ExtractOrigin(originSource)
                : (parentRecord.OriginSourceId, parentRecord.OriginSourceDisplay);

            var (targetId, targetDisplay) = originTarget != null
                ? ExtractOrigin(originTarget)
                : (parentRecord.OriginTargetId, parentRecord.OriginTargetDisplay);

            var record = new TraceContextRecord(
                contextId: contextId,
                rootId: parentRecord.RootId,
                parentId: parentContextId,
                kind: kind,
                configId: configId,
                sourceActorId: sourceActorId != 0 ? sourceActorId : parentRecord.SourceActorId,
                targetActorId: targetActorId != 0 ? targetActorId : parentRecord.TargetActorId,
                originSourceId: originId,
                originSourceDisplay: originDisplay,
                originTargetId: targetId,
                originTargetDisplay: targetDisplay,
                createdFrame: Frame,
                endedFrame: 0,
                endReason: 0);

            _contexts[contextId] = record;

            // 添加父子关系
            if (!_childrenByParent.TryGetValue(parentContextId, out var children))
            {
                children = new List<long>();
                _childrenByParent[parentContextId] = children;
            }
            children.Add(contextId);

            // 更新根节点的活跃计数
            var rootId = parentRecord.RootId;
            if (_roots.TryGetValue(rootId, out var rootRecord))
            {
                _roots[rootId] = rootRecord.WithActiveCount(rootRecord.ActiveCount + 1)
                    .WithLastTouchedFrame(Frame);
            }

            return contextId;
        }

        /// <summary>
        /// 开始根节点作用域
        /// 相当于 CreateRoot + RetainRoot
        /// </summary>
        public long BeginRoot(
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            var rootId = CreateRoot(kind, sourceActorId, targetActorId, originSource, originTarget, configId);
            RetainRoot(rootId);
            return rootId;
        }

        /// <summary>
        /// 开始子节点作用域
        /// 相当于 CreateChild + RetainRoot
        /// </summary>
        public long BeginChild(
            long parentContextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            var childId = CreateChild(parentContextId, kind, sourceActorId, targetActorId, originSource, originTarget, configId);
            var parentRecord = _contexts[parentContextId];
            RetainRoot(parentRecord.RootId);
            return childId;
        }

        /// <summary>
        /// 确保根节点存在
        /// 如果根节点不存在则创建，否则返回现有根节点 ID
        /// </summary>
        public long EnsureRoot(
            long contextId,
            int kind,
            long sourceActorId = 0,
            long targetActorId = 0,
            object originSource = null,
            object originTarget = null,
            int configId = 0)
        {
            if (contextId != 0 && _contexts.TryGetValue(contextId, out var record))
            {
                // 已存在，刷新活跃时间
                if (_roots.TryGetValue(record.RootId, out var rootRecord))
                {
                    _roots[record.RootId] = rootRecord.WithLastTouchedFrame(Frame);
                }
                return record.RootId;
            }

            return CreateRoot(kind, sourceActorId, targetActorId, originSource, originTarget, configId);
        }

        private (long id, string display) ExtractOrigin(object origin)
        {
            if (origin == null)
                return (0, null);

            if (_contextSource.TryExtractTraceId(origin, out var id, out var display))
                return (id, display);

            // 无法提取时，返回对象的 HashCode 和 ToString
            return (origin.GetHashCode(), origin.ToString());
        }
    }
}
