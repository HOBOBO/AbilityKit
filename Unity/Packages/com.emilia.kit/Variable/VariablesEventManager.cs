using System;
using System.Collections.Generic;

namespace Emilia.Variables
{
    public class VariablesEventManager
    {
        private struct EventInfo : IEquatable<EventInfo>
        {
            public string key;
            public Action action;

            public EventInfo(string key, Action action)
            {
                this.key = key;
                this.action = action;
            }

            public bool Equals(EventInfo other) => this.key == other.key && Equals(this.action, other.action);

            public override bool Equals(object obj) => obj is EventInfo other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(this.key, this.action);
        }

        private VariablesManager _variablesManager;

        private Dictionary<string, Action> events = new Dictionary<string, Action>();

        private HashSet<EventInfo> addEvents = new HashSet<EventInfo>();
        private HashSet<EventInfo> removeEvents = new HashSet<EventInfo>();

        private List<string> fireEvents = new List<string>();

        public VariablesManager variablesManager => this._variablesManager;

        public VariablesEventManager(VariablesManager variablesManager)
        {
            this._variablesManager = variablesManager;
        }

        /// <summary>
        /// 设置变量
        /// </summary>
        public void Set<T>(string key, T value)
        {
            bool isSet = this._variablesManager.SetValue(key, value);
            if (isSet == false) return;
            this.fireEvents.Add(key);
            OnSet(key, value);
        }

        protected virtual void OnSet<T>(string key, T value) { }

        /// <summary>
        /// 获取变量
        /// </summary>
        public T Get<T>(string key) => this._variablesManager.GetValue<T>(key);

        public Variable GetVariable(string key) => this._variablesManager.GetThisValue(key);

        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe(string key, Action action)
        {
            EventInfo eventInfo = new EventInfo(key, action);
            if (this.addEvents.Contains(eventInfo)) return;
            addEvents.Add(eventInfo);
            OnSubscribe(key, action);
        }
        
        protected virtual void OnSubscribe(string key, Action action) { }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        public void Unsubscribe(string key, Action action)
        {
            EventInfo eventInfo = new EventInfo(key, action);
            if (this.removeEvents.Contains(eventInfo)) return;
            removeEvents.Add(eventInfo);
            OnUnsubscribe(key, action);
        }
        
        protected virtual void OnUnsubscribe(string key, Action action) { }

        /// <summary>
        /// 更新
        /// </summary>
        public void Tick()
        {
            RemoveEvent();
            AddEvent();
            FireEvent();

            OnTick();
        }
        
        protected virtual void OnTick() { }

        void AddEvent()
        {
            foreach (EventInfo eventInfo in this.addEvents)
            {
                if (this.events.TryGetValue(eventInfo.key, out var current)) this.events[eventInfo.key] = current + eventInfo.action;
                else this.events[eventInfo.key] = eventInfo.action;
            }

            addEvents.Clear();
        }

        void RemoveEvent()
        {
            foreach (EventInfo eventInfo in this.removeEvents)
            {
                if (this.events.TryGetValue(eventInfo.key, out var current) == false) continue;
                Action next = current - eventInfo.action;

                if (next == null) this.events.Remove(eventInfo.key);
                else this.events[eventInfo.key] = next;
            }

            removeEvents.Clear();
        }

        void FireEvent()
        {
            if (this.fireEvents.Count == 0) return;

            int fireCount = this.fireEvents.Count;
            for (int i = 0; i < fireCount; i++)
            {
                string key = this.fireEvents[i];

                if (this.events.TryGetValue(key, out var current) == false) continue;
                current?.Invoke();
            }

            this.fireEvents.Clear();
        }
    }
}