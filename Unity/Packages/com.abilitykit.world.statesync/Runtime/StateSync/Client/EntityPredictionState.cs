using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Core.Buffers;

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
        private readonly ISnapshotStore _snapshotStore;
        private readonly bool _enableRollback;
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
        public bool SnapshotHistoryEnabled => _snapshotStore != null;
        public bool RollbackEnabled => _enableRollback && _snapshotStore != null;
        public IBufferCapacityControl SnapshotCapacityControl =>
            _snapshotStore as IBufferCapacityControl;

        public event Action<string, object, object> OnSlotChanged;
        public event Action<int, int> OnRollback;

        public EntityPredictionState(int entityId, bool isLocalPlayer)
            : this(
                entityId,
                isLocalPlayer,
                entity: null,
                snapshotStore: new DictionarySnapshotStore(30),
                enableRollback: true)
        {
        }

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

        public EntityPredictionState(IPredictableEntity entity)
            : this(entity, new DictionarySnapshotStore(30), enableRollback: true)
        {
        }

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

        public void CaptureSnapshot(int frame)
        {
            if (_snapshotStore == null) return;
            _snapshotStore.Record(new Frame(frame), _currentSlots);
        }

        public AbilityKit.Ability.StateSync.Prediction.StateSlots GetSnapshot(int frame)
        {
            return _snapshotStore?.Get(new Frame(frame));
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

        private static bool ValuesEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }
    }
}
