using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Client
{
    /// <summary>
    /// 实体预测状态实现
    /// 封装单个实体的预测逻辑
    /// </summary>
    public sealed class EntityPredictionState : IEntityPredictionState
    {
        private readonly int _entityId;
        private readonly bool _isLocalPlayer;
        private readonly IPredictableEntity _entity;
        private readonly List<IClientPredictionHandler> _handlers = new List<IClientPredictionHandler>();
        private readonly Dictionary<int, AbilityKit.Ability.StateSync.Prediction.StateSlots> _snapshots = new Dictionary<int, AbilityKit.Ability.StateSync.Prediction.StateSlots>();
        private readonly List<StateChangeEvent> _pendingChanges = new List<StateChangeEvent>();
        private readonly Dictionary<string, object> _previousValues = new Dictionary<string, object>();

        private AbilityKit.Ability.StateSync.Prediction.StateSlots _currentSlots;
        private int _currentFrame;
        private int _confirmedFrame;
        private bool _isPredicted;

        public int EntityId => _entityId;
        public bool IsLocalPlayer => _isLocalPlayer;
        public AbilityKit.Ability.StateSync.Prediction.StateSlots CurrentSlots => _currentSlots;
        public bool IsPredicted => _isPredicted;
        public int CurrentFrame => _currentFrame;

        public event Action<string, object, object> OnSlotChanged;
        public event Action<int, int> OnRollback;

        public EntityPredictionState(int entityId, bool isLocalPlayer)
        {
            _entityId = entityId;
            _isLocalPlayer = isLocalPlayer;
            _currentSlots = new AbilityKit.Ability.StateSync.Prediction.StateSlots();
            _confirmedFrame = -1;
            _currentFrame = 0;
        }

        public EntityPredictionState(IPredictableEntity entity)
            : this(
                entity != null ? entity.EntityId : throw new ArgumentNullException(nameof(entity)),
                entity.IsLocalPlayer)
        {
            _entity = entity;
            var initialSlots = entity.GetStateSlots();
            if (initialSlots != null)
            {
                _currentSlots.OverwriteFrom(initialSlots);
                CaptureCurrentValues();
            }
        }

        public void RegisterHandler(IClientPredictionHandler handler)
        {
            if (handler != null)
            {
                _handlers.Add(handler);
            }
        }

        public void Predict(IInputCommand input, int frame)
        {
            _currentFrame = frame;

            foreach (var handler in _handlers)
            {
                if (handler.Strategy == PredictionStrategy.None)
                    continue;

                // 记录变化前的值
                var keys = new List<string>(_currentSlots.Keys);
                foreach (var key in keys)
                {
                    if (!_previousValues.ContainsKey(key))
                    {
                        _previousValues[key] = GetSlotValue(key);
                    }
                }

                // 执行预测
                handler.PredictLocal(input, _currentSlots, frame);
            }

            // 收集状态变化
            CollectStateChanges(frame, isPredicted: true);

            _isPredicted = true;
        }

        public bool ApplyServerState(int serverFrame, ServerEntitySnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.EntityId != _entityId)
                throw new ArgumentException("Snapshot entity does not match this prediction state.", nameof(snapshot));

            var wasRollback = serverFrame < _currentFrame;
            if (wasRollback)
            {
                RollbackTo(serverFrame);
            }

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

        public void RollbackTo(int frame)
        {
            if (frame >= _currentFrame)
                return;

            // 获取目标帧的快照
            if (_snapshots.TryGetValue(frame, out var snapshot))
            {
                // 记录回滚前的状态
                var oldFrame = _currentFrame;

                // 恢复快照
                _currentSlots.OverwriteFrom(snapshot);

                // 收集回滚产生的变化
                CollectStateChanges(frame, isRollback: true);

                _currentFrame = frame;
                _isPredicted = false;

                OnRollback?.Invoke(oldFrame, frame);
            }
        }

        public void CaptureSnapshot(int frame)
        {
            // 清理旧快照
            PruneOldSnapshots(frame);

            // 保存当前状态的克隆
            _snapshots[frame] = _currentSlots.Clone();
        }

        public AbilityKit.Ability.StateSync.Prediction.StateSlots GetSnapshot(int frame)
        {
            if (_snapshots.TryGetValue(frame, out var snapshot))
            {
                return snapshot.Clone();
            }
            return null;
        }

        public void AdvanceFrame()
        {
            _currentFrame++;
            _confirmedFrame = _currentFrame;
        }

        public IReadOnlyList<StateChangeEvent> GetPendingStateChanges()
        {
            return _pendingChanges;
        }

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
                    // 新增槽位
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

        private void PruneOldSnapshots(int currentFrame)
        {
            // 保留最近 30 帧的快照
            var framesToRemove = new List<int>();
            foreach (var frame in _snapshots.Keys)
            {
                if (frame < currentFrame - 30)
                {
                    framesToRemove.Add(frame);
                }
            }

            foreach (var frame in framesToRemove)
            {
                _snapshots.Remove(frame);
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
