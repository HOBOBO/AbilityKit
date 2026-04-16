using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Abstractions;

namespace AbilityKit.Triggering.Runtime.Context
{
    /// <summary>
    /// Action 执行上下文
    /// 职责：
    /// 1. 承载 Action 的输入参数（来自配置）
    /// 2. 提供运行时服务（黑板、变量、事件等）
    /// 3. 存储运行时状态（用于快照/回滚）
    /// 4. 跟踪执行进度
    /// </summary>
    [Serializable]
    public sealed class ActionContext
    {
        // ========== 元数据 ==========
        public int InstanceId { get; set; }
        public int TriggerId { get; set; }
        public int ActionId { get; set; }
        public string ActionName { get; set; }

        // ========== 参数（配置阶段注入，执行期间不变）==========
        public ParameterBag Parameters { get; } = new();
        public Dictionary<string, object> NamedArguments { get; } = new();

        // ========== 运行时状态（每帧变化，需要快照）==========
        public StateBag State { get; } = new();
        public int ExecutionCount { get; internal set; }
        public float ElapsedTimeMs { get; internal set; }
        public float LastExecuteTimeMs { get; internal set; }
        public bool IsInterrupted { get; set; }
        public bool IsCancelled { get; set; }

        // ========== 服务（外部注入，不序列化）==========
        [NonSerialized] private IServiceProvider _serviceProvider;

        public void SetServiceProvider(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;
        public T GetService<T>() where T : class => _serviceProvider?.GetService(typeof(T)) as T;
        public T GetRequiredService<T>() where T : class
            => GetService<T>() ?? throw new InvalidOperationException($"Service {typeof(T).Name} not registered");

        // ========== 常用服务（便捷属性，不序列化）==========
        [NonSerialized] private IBlackboardResolver _blackboard;
        [NonSerialized] private IPayloadAccessor _payloads;
        [NonSerialized] private IVariableRepository _variables;
        [NonSerialized] private ITimeService _time;
        [NonSerialized] private IEventBus _events;
        [NonSerialized] private IEntityFinder _entities;

        public IBlackboardResolver Blackboard
        {
            get => _blackboard ?? GetService<IBlackboardResolver>();
            set => _blackboard = value;
        }

        public IPayloadAccessor Payloads
        {
            get => _payloads ?? GetService<IPayloadAccessor>();
            set => _payloads = value;
        }

        public IVariableRepository Variables
        {
            get => _variables ?? GetService<IVariableRepository>();
            set => _variables = value;
        }

        public ITimeService Time
        {
            get => _time ?? GetService<ITimeService>();
            set => _time = value;
        }

        public IEventBus Events
        {
            get => _events ?? GetService<IEventBus>();
            set => _events = value;
        }

        public IEntityFinder Entities
        {
            get => _entities ?? GetService<IEntityFinder>();
            set => _entities = value;
        }

        // ========== 便捷访问器 ==========
        public T GetNumericParam<T>(string key) where T : unmanaged => Parameters.Get<T>(key);
        public void SetNumericParam<T>(string key, T value) where T : unmanaged => Parameters.Set(key, value);
        public T GetObjectParam<T>(string key) where T : class => Parameters.GetObject(key) is T t ? t : null;
        public void SetObjectParam<T>(string key, T value) where T : class => Parameters.SetObject(key, value);
        public string GetStringParam(string key) => Parameters.GetString(key);
        public void SetStringParam(string key, string value) => Parameters.SetString(key, value);
        public T GetNamedArgument<T>(string key) => NamedArguments.TryGetValue(key, out var value) ? (T)value : default;
        public void SetNamedArgument<T>(string key, T value) => NamedArguments[key] = value;
        public T GetState<T>(string key) where T : class => State.Get<T>(key);
        public void SetState<T>(string key, T value) where T : class => State.Set(key, value);
        public bool TryGetState<T>(string key, out T value) where T : class => State.TryGet(key, out value);
        public void RemoveState(string key) => State.Remove(key);
        public bool HasState(string key) => State.Has(key);

        // ========== 数值辅助方法 ==========
        public double GetNumeric(string key, double defaultValue = 0) => Parameters.HasNumeric(key) ? Parameters.GetDouble(key) : defaultValue;
        public void SetNumeric(string key, double value) => Parameters.SetDouble(key, value);
        public Abstractions.Vector3 GetVector3(string key)
        {
            var x = GetNumeric($"{key}.x");
            var y = GetNumeric($"{key}.y");
            var z = GetNumeric($"{key}.z");
            return new Abstractions.Vector3((float)x, (float)y, (float)z);
        }
        public void SetVector3(string key, Abstractions.Vector3 value)
        {
            SetNumeric($"{key}.x", value.X);
            SetNumeric($"{key}.y", value.Y);
            SetNumeric($"{key}.z", value.Z);
        }

        // ========== 快照支持 ==========
        public ActionSnapshot CaptureSnapshot()
        {
            return new ActionSnapshot
            {
                InstanceId = InstanceId,
                TriggerId = TriggerId,
                ActionId = ActionId,
                ActionName = ActionName,
                Parameters = Parameters.Clone(),
                State = State.Clone(),
                ExecutionCount = ExecutionCount,
                ElapsedTimeMs = ElapsedTimeMs,
                LastExecuteTimeMs = LastExecuteTimeMs,
                IsInterrupted = IsInterrupted,
                IsCancelled = IsCancelled
            };
        }

        public void RestoreSnapshot(in ActionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            InstanceId = snapshot.InstanceId;
            TriggerId = snapshot.TriggerId;
            ActionId = snapshot.ActionId;
            ActionName = snapshot.ActionName;
            Parameters.CopyFrom(snapshot.Parameters);
            State.CopyFrom(snapshot.State);
            ExecutionCount = snapshot.ExecutionCount;
            ElapsedTimeMs = snapshot.ElapsedTimeMs;
            LastExecuteTimeMs = snapshot.LastExecuteTimeMs;
            IsInterrupted = snapshot.IsInterrupted;
            IsCancelled = snapshot.IsCancelled;
        }

        // ========== 生命周期 ==========
        public void Reset()
        {
            State.Clear();
            ExecutionCount = 0;
            ElapsedTimeMs = 0;
            LastExecuteTimeMs = 0;
            IsInterrupted = false;
            IsCancelled = false;
        }

        public void MarkExecuted()
        {
            ExecutionCount++;
            LastExecuteTimeMs = Time?.DeltaTimeMs ?? 0;
        }

        public void UpdateTime(float deltaTimeMs)
        {
            ElapsedTimeMs += deltaTimeMs;
        }

        public override string ToString()
        {
            return $"ActionContext[Id={InstanceId}, Action={ActionName}, ExecCount={ExecutionCount}, Elapsed={ElapsedTimeMs:F2}ms]";
        }
    }
}
