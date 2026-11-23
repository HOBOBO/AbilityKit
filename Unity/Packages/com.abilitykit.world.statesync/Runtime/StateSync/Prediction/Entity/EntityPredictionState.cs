using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Client
{
    /// <summary>
    /// 维护单实体可变槽位、处理器和可选历史，局部回滚不恢复业务对象也不重演输入。
    /// 调用及事件在调用线程同步执行，由宿主串行驱动，回调异常不回退已完成的写入。
    /// </summary>
    public sealed class EntityPredictionState : IEntityPredictionState
    {
        private readonly int _entityId;
        private readonly bool _isLocalPlayer;
        private readonly IPredictableEntity _entity;
        private readonly List<IClientPredictionHandler> _handlers = new List<IClientPredictionHandler>();
        private readonly ISnapshotStore _snapshotStore;
        private readonly bool _enableRollback;
        private readonly List<StateChangeEvent> _pendingChanges = new List<StateChangeEvent>();
        private readonly Dictionary<string, object> _previousValues = new Dictionary<string, object>();

        private AbilityKit.Ability.StateSync.Prediction.StateSlots _currentSlots;
        private int _currentFrame;
        private int _confirmedFrame;
        private bool _isPredicted;

        /// <summary>
        /// 构造时固定的实体标识，用于校验校正快照。
        /// </summary>
        public int EntityId => _entityId;
        /// <summary>
        /// 构造时记录的本地控制标记；模块用它筛选输入预测对象。
        /// </summary>
        public bool IsLocalPlayer => _isLocalPlayer;
        /// <summary>
        /// 返回实体预测的实时可变槽位，不是副本；不得并发修改。
        /// </summary>
        public AbilityKit.Ability.StateSync.Prediction.StateSlots CurrentSlots => _currentSlots;
        /// <summary>
        /// 是否已完成本地预测且之后未成功回滚或应用权威状态；不表示输入确认状态。
        /// </summary>
        public bool IsPredicted => _isPredicted;
        /// <summary>
        /// 返回实体自身的槽位游标；记录快照不推进它，校正可将它设回早期帧。
        /// </summary>
        public int CurrentFrame => _currentFrame;
        /// <summary>
        /// 是否具备快照存储，不保证指定帧存在。
        /// </summary>
        public bool SnapshotHistoryEnabled => _snapshotStore != null;
        /// <summary>
        /// 是否同时允许回滚并提供历史存储。
        /// </summary>
        public bool RollbackEnabled => _enableRollback && _snapshotStore != null;
        /// <summary>
        /// 返回历史存储的容量控制引用；未配置或不支持时返回空。
        /// </summary>
        public IBufferCapacityControl SnapshotCapacityControl =>
            _snapshotStore as IBufferCapacityControl;

        /// <summary>
        /// 收集变化时同步传递槽名、旧值和新值；新增无旧值，删除无新值。
        /// 槽位顺序无排序保证，事件值不是独立深拷贝。
        /// </summary>
        public event Action<string, object, object> OnSlotChanged;
        /// <summary>
        /// 早期历史成功覆盖预测槽位并更新实体游标后，同步传递源帧和目标帧。
        /// </summary>
        public event Action<int, int> OnRollback;

        /// <summary>
        /// 创建独立槽位状态，默认保存 30 帧并允许回滚；无绑定实体，不能应用权威快照。
        /// </summary>
        public EntityPredictionState(int entityId, bool isLocalPlayer)
            : this(
                entityId,
                isLocalPlayer,
                entity: null,
                snapshotStore: new DictionarySnapshotStore(30),
                enableRollback: true)
        {
        }

        /// <summary>
        /// 创建不绑定业务实体的槽位状态；空存储禁用历史与回滚，非空时使用所给存储且不负责释放它。
        /// </summary>
        public EntityPredictionState(
            int entityId,
            bool isLocalPlayer,
            ISnapshotStore snapshotStore,
            bool enableRollback = true)
            : this(
                entityId,
                isLocalPlayer,
                entity: null,
                snapshotStore: snapshotStore,
                enableRollback: enableRollback)
        {
        }

        private EntityPredictionState(
            int entityId,
            bool isLocalPlayer,
            IPredictableEntity entity,
            ISnapshotStore snapshotStore,
            bool enableRollback)
        {
            _entityId = entityId;
            _isLocalPlayer = isLocalPlayer;
            _entity = entity;
            _snapshotStore = snapshotStore;
            _enableRollback = enableRollback;
            _currentSlots = new AbilityKit.Ability.StateSync.Prediction.StateSlots();
            _confirmedFrame = -1;
            _currentFrame = 0;
        }

        /// <summary>
        /// 绑定非空实体并复制初始槽位，默认保存 30 帧并允许回滚；处理器需另外登记。
        /// </summary>
        public EntityPredictionState(IPredictableEntity entity)
            : this(entity, new DictionarySnapshotStore(30), enableRollback: true)
        {
        }

        /// <summary>
        /// 绑定非空实体并复制其非空初始槽位，空存储禁用历史与回滚。
        /// 存储由调用方协调且不会被释放，实体回调和槽位复制失败向调用方传播。
        /// </summary>
        public EntityPredictionState(
            IPredictableEntity entity,
            ISnapshotStore snapshotStore,
            bool enableRollback = true)
            : this(
                entity != null ? entity.EntityId : throw new ArgumentNullException(nameof(entity)),
                entity.IsLocalPlayer,
                entity,
                snapshotStore,
                enableRollback)
        {
            var initialSlots = entity.GetStateSlots();
            if (initialSlots != null)
            {
                _currentSlots.OverwriteFrom(initialSlots);
                CaptureCurrentValues();
            }
        }

        /// <summary>
        /// 将非空处理器追加到执行队列，不去重；空处理器忽略。
        /// </summary>
        public void RegisterHandler(IClientPredictionHandler handler)
        {
            if (handler != null)
            {
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// 设置实体帧并依登记顺序运行非禁用处理器，再收集变化和标记预测。
        /// 直接调用不按本地控制标记过滤，也不写回业务实体或记录快照。
        /// </summary>
        public void Predict(IInputCommand input, int frame)
        {
            _currentFrame = frame;

            foreach (var handler in _handlers)
            {
                if (handler.Strategy == PredictionStrategy.None)
                    continue;

                /*
                 * 首次看到的槽位先建立比较基准，后续处理器仍共享同一组槽位。
                 */
                var keys = new List<string>(_currentSlots.Keys);
                foreach (var key in keys)
                {
                    if (!_previousValues.ContainsKey(key))
                    {
                        _previousValues[key] = GetSlotValue(key);
                    }
                }

                handler.PredictLocal(input, _currentSlots, frame);
            }

            CollectStateChanges(frame, isPredicted: true);

            _isPredicted = true;
        }

        /// <summary>
        /// 先检查快照非空且实体标识一致，尝试恢复校正帧的局部历史，再由绑定实体应用权威状态。
        /// 读取非空业务槽位、覆盖当前槽位并按登记顺序合并处理器结果；缺实体或槽位会抛异常，已有写入不撤销。
        /// 返回是否实际恢复过早期历史，不表示预测误差验证结果；不重演输入。
        /// </summary>
        public bool ApplyServerState(int serverFrame, ServerEntitySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.EntityId != _entityId)
                throw new ArgumentException("Snapshot entity does not match this prediction state.", nameof(snapshot));

            var wasRollback = TryRollbackTo(serverFrame);

            if (_entity == null)
                throw new InvalidOperationException("A prediction state created without an entity cannot apply authoritative snapshots.");

            _entity.ApplyServerState(snapshot);
            var serverSlots = _entity.GetStateSlots();
            if (serverSlots == null)
                throw new InvalidOperationException("The predictable entity returned no state slots after applying a server snapshot.");

            _currentSlots.OverwriteFrom(serverSlots);
            for (int i = 0; i < _handlers.Count; i++)
            {
                _handlers[i].ApplyServerState(serverSlots, _currentSlots);
            }

            CollectStateChanges(serverFrame);

            _confirmedFrame = serverFrame;
            _currentFrame = serverFrame;
            _isPredicted = false;

            return wasRollback;
        }

        /// <summary>
        /// 仅在回滚启用、帧早于当前且历史存在时恢复预测槽位；否则无操作。
        /// 不调用业务实体恢复接口，也不重演后继输入。
        /// </summary>
        public void RollbackTo(int frame)
        {
            TryRollbackTo(frame);
        }

        private bool TryRollbackTo(int frame)
        {
            if (!RollbackEnabled || frame >= _currentFrame)
                return false;

            var snapshot = _snapshotStore.Get(new Frame(frame));
            if (snapshot == null)
                return false;

            var oldFrame = _currentFrame;
            _currentSlots.OverwriteFrom(snapshot);
            CollectStateChanges(frame, isRollback: true);
            _currentFrame = frame;
            _isPredicted = false;
            OnRollback?.Invoke(oldFrame, frame);
            return true;
        }

        /// <summary>
        /// 把当前槽位交给存储并以指定帧索引，不改变实体游标；未配置历史时无操作。
        /// </summary>
        public void CaptureSnapshot(int frame)
        {
            if (_snapshotStore == null) return;
            _snapshotStore.Record(new Frame(frame), _currentSlots);
        }

        /// <summary>
        /// 按精确帧读取历史；没有存储或帧不存在时返回空，副本语义取决于存储实现。
        /// </summary>
        public AbilityKit.Ability.StateSync.Prediction.StateSlots GetSnapshot(int frame)
        {
            return _snapshotStore?.Get(new Frame(frame));
        }

        /// <summary>
        /// 将实体当前帧加一并同步内部确认游标，不运行处理器、不捕获快照。
        /// </summary>
        public void AdvanceFrame()
        {
            _currentFrame++;
            _confirmedFrame = _currentFrame;
        }

        /// <summary>
        /// 返回内部待发布列表的只读接口视图，不是副本；清理和后续收集会改变其内容。
        /// </summary>
        public IReadOnlyList<StateChangeEvent> GetPendingStateChanges()
        {
            return _pendingChanges;
        }

        /// <summary>
        /// 清空待发布变化，不撤销槽位写入或重置历史。
        /// </summary>
        public void ClearPendingStateChanges()
        {
            _pendingChanges.Clear();
        }

        private void CollectStateChanges(int frame, bool isPredicted = false, bool isRollback = false)
        {
            var currentKeys = _currentSlots.Keys;
            var currentKeySet = new HashSet<string>(currentKeys);
            foreach (var slotName in currentKeys)
            {
                var newValue = GetSlotValue(slotName);

                if (_previousValues.TryGetValue(slotName, out var oldValue))
                {
                    if (!ValuesEqual(oldValue, newValue))
                    {
                        var evt = new StateChangeEvent
                        {
                            EntityId = _entityId,
                            Frame = frame,
                            SlotName = slotName,
                            OldValue = oldValue,
                            NewValue = newValue,
                            IsPredicted = isPredicted
                        };
                        _pendingChanges.Add(evt);
                        OnSlotChanged?.Invoke(slotName, oldValue, newValue);
                    }
                }
                else
                {
                    var evt = new StateChangeEvent
                    {
                        EntityId = _entityId,
                        Frame = frame,
                        SlotName = slotName,
                        OldValue = null,
                        NewValue = newValue,
                        IsPredicted = isPredicted
                    };
                    _pendingChanges.Add(evt);
                    OnSlotChanged?.Invoke(slotName, null, newValue);
                }

                _previousValues[slotName] = newValue;
            }

            var previousKeys = new List<string>(_previousValues.Keys);
            foreach (var slotName in previousKeys)
            {
                if (currentKeySet.Contains(slotName)) continue;

                var oldValue = _previousValues[slotName];
                var evt = new StateChangeEvent
                {
                    EntityId = _entityId,
                    Frame = frame,
                    SlotName = slotName,
                    OldValue = oldValue,
                    NewValue = null,
                    IsPredicted = isPredicted
                };
                _pendingChanges.Add(evt);
                OnSlotChanged?.Invoke(slotName, oldValue, null);
                _previousValues.Remove(slotName);
            }
        }

        private object GetSlotValue(string slotName)
        {
            return _currentSlots.TryGetValue(slotName, out var value) ? value : null;
        }

        private void CaptureCurrentValues()
        {
            _previousValues.Clear();
            foreach (var slotName in _currentSlots.Keys)
            {
                _previousValues[slotName] = GetSlotValue(slotName);
            }
        }

        private static bool ValuesEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }
    }
}
