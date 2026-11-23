using System;

namespace AbilityKit.Ability.StateSync
{
    /// <summary>
    /// 描述一次局部预测槽位的值变化，不是权威状态提交记录。
    /// 结构体按值传递，但旧值和新值不深拷贝；订阅方不应改写其中的可变对象。
    /// </summary>
    public struct StateChangeEvent
    {
        /// <summary>
        /// 记录发布方提供的实体标识，不含世界或实体代际。
        /// </summary>
        public int EntityId;

        /// <summary>
        /// 记录发布方收集槽位变化所用的帧，不是输入确认或权威接收游标。
        /// </summary>
        public int Frame;

        /// <summary>
        /// 记录发生变化的槽位名称；默认实体模块不据此跨实体排序。
        /// </summary>
        public string SlotName;

        /// <summary>
        /// 原样保存变化前的值；新增槽位时为空，空值本身不区分缺少槽位与现有空值。
        /// </summary>
        public object OldValue;

        /// <summary>
        /// 原样保存变化后的值；删除槽位时为空，不代表销毁实体。
        /// </summary>
        public object NewValue;

        /// <summary>
        /// 标记变化是否由本地预测产生；假值也可来自历史回滚，不代表服务器输入确认。
        /// </summary>
        public bool IsPredicted;

        /// <summary>
        /// 按对象相等语义比较新旧值，两个空值视为相等；不比较槽位是否存在或引用对象的历史内容。
        /// </summary>
        public bool HasChanged => !Equals(OldValue, NewValue);

        /// <summary>
        /// 按参数初始化通知，把新旧值原样保存在对象字段中，不复制或校验。
        /// </summary>
        public StateChangeEvent(int entityId, int frame, string slotName, object oldValue, object newValue, bool isPredicted)
        {
            EntityId = entityId;
            Frame = frame;
            SlotName = slotName;
            OldValue = oldValue;
            NewValue = newValue;
            IsPredicted = isPredicted;
        }
    }

    /// <summary>
    /// 描述发布方报告的局部回滚，不证明恢复了业务世界或重演了输入。
    /// 默认实体模块在校正应用成功后、快照通知之前同步发布此值。
    /// </summary>
    public struct RollbackEvent
    {
        /// <summary>
        /// 受本次局部恢复影响的实体标识。
        /// </summary>
        public int EntityId;

        /// <summary>
        /// 发布方报告的起始帧；默认实体模块给出快照批开始前的模块当前帧，而非该实体的独立游标。
        /// </summary>
        public int FromFrame;

        /// <summary>
        /// 发布方报告的目标帧；默认实体模块使用本批传入的校正帧。
        /// </summary>
        public int ToFrame;

        /// <summary>
        /// 发布方填写的诊断原因；默认实体模块设置为服务器校正。
        /// </summary>
        public RollbackReason Reason;

        /// <summary>
        /// 发布方提供的冲突级别；默认实体模块不计算严重度，保留默认级别。
        /// </summary>
        public ConflictLevel Level;

        /// <summary>
        /// 按参数创建回滚通知，不验证帧顺序，也不执行任何恢复。
        /// </summary>
        public RollbackEvent(int entityId, int fromFrame, int toFrame, RollbackReason reason, ConflictLevel level)
        {
            EntityId = entityId;
            FromFrame = fromFrame;
            ToFrame = toFrame;
            Reason = reason;
            Level = level;
        }
    }

    /// <summary>
    /// 回滚通知的诊断原因，不决定能否回滚或改写游标。
    /// </summary>
    public enum RollbackReason
    {
        /// <summary>
        /// 应用服务器校正时发生的局部回滚；也是枚举默认值。
        /// </summary>
        ServerCorrection,

        /// <summary>
        /// 为不同步诊断保留的原因值，不会自动启动恢复。
        /// </summary>
        DesyncDetected,

        /// <summary>
        /// 为调用方标记手动回滚保留的原因值。
        /// </summary>
        Manual
    }

    /// <summary>
    /// 保留的快照应用通知值类型。
    /// 默认实体模块的快照通知仍传递实体标识和帧两个参数，不发布此结构体。
    /// </summary>
    public struct SnapshotAppliedEvent
    {
        /// <summary>
        /// 发布方填写的快照相关实体标识。
        /// </summary>
        public int EntityId;

        /// <summary>
        /// 发布方填写的应用帧；不是已提交的权威接收位置。
        /// </summary>
        public int Frame;

        /// <summary>
        /// 由创建方标记应用前是否发生局部回滚；该字段不会触发回滚。
        /// </summary>
        public bool WasRollback;

        /// <summary>
        /// 按参数保存应用通知，不校验帧或应用状态。
        /// </summary>
        public SnapshotAppliedEvent(int entityId, int frame, bool wasRollback)
        {
            EntityId = entityId;
            Frame = frame;
            WasRollback = wasRollback;
        }
    }
}
