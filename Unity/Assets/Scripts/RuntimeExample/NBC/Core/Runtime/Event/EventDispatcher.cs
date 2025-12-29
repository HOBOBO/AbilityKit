using System;
using System.Collections.Generic;

namespace NBC
{
    struct EventBin
    {
        public string type;
        public Action<EventArgs> listener;
        public object thisObject;
        public int priority;
        public EventDispatcher target;
        public bool dispatchOnce;
    }

    public class EventDispatcher : IEventDispatcher
    {
        private readonly Dictionary<string, List<EventBin>>
            _dicEventListener = new Dictionary<string, List<EventBin>>();

        // private Queue<EventBin> _curNeedDispatcherListeners;
        private readonly Stack<EventBin> _onceList = new Stack<EventBin>();

        public IEventDispatcher On(string type, Action<EventArgs> listener, object caller, int priority = 0,
            bool once = false)
        {
            List<EventBin> list;
            if (HasEventListener(type))
            {
                list = _dicEventListener[type];
            }
            else
            {
                list = new List<EventBin>();
                _dicEventListener[type] = list;
            }

            InsertEventBin(list, type, listener, caller, priority, once);
            return this;
        }

        public IEventDispatcher Once(string type, Action<EventArgs> listener, object caller, int priority = 0)
        {
            On(type, listener, caller, priority, true);
            return this;
        }

        public IEventDispatcher Off(string type, Action<EventArgs> listener, object caller)
        {
            RemoveListener(type, listener, caller);
            return this;
        }

        public IEventDispatcher OffAll(string type = "")
        {
            if (type != "" && HasEventListener(type))
            {
                _dicEventListener.Remove(type);
            }
            else
            {
                _dicEventListener.Clear();
            }

            return this;
        }

        public IEventDispatcher OffAllCaller(object caller)
        {
            List<EventBin> arr = new List<EventBin>();
            foreach (var v in _dicEventListener.Values)
            {
                foreach (var eventBin in v)
                {
                    if (eventBin.thisObject == caller)
                    {
                        arr.Add(eventBin);
                    }
                }
            }

            foreach (var e in arr)
            {
                RemoveListener(e.type, e.listener, e.thisObject);
            }

            return this;
        }

        public bool HasEventListener(string type)
        {
            return _dicEventListener.ContainsKey(type);
        }

        public void DispatchEvent(EventArgs ev)
        {
            ev.Sender = this;
            notifyListener(ev);
        }

        public bool DispatchEventWith(string type, object data = null)
        {
            if (HasEventListener(type))
            {
                EventArgs eventArgs = EventArgs.Create<EventArgs>(type);
                eventArgs.Data = data;
                DispatchEvent(eventArgs);
                EventArgs.Release(eventArgs);
            }

            return true;
        }


        private bool InsertEventBin(List<EventBin> list, string type, Action<EventArgs> listener, object thisObject,
            int priority = 0, bool dispatchOnce = false)

        {
            var insertIndex = -1;
            var length = list.Count;
            for (var i = 0; i < length; i++)
            {
                var bin = list[i];
                if (bin.listener == listener && bin.thisObject == thisObject && bin.target == this)
                {
                    return false;
                }

                if (insertIndex == -1 && bin.priority < priority)
                {
                    insertIndex = i;
                }
            }

            var eventBin = new EventBin
            {
                type = type,
                listener = listener,
                thisObject = thisObject,
                priority = priority,
                target = this,
                dispatchOnce = dispatchOnce
            };

            if (insertIndex != -1)
            {
                list.Insert(insertIndex, eventBin);
            }
            else
            {
                list.Add(eventBin);
            }

            return true;
        }

        private void RemoveListener(string type, Action<EventArgs> listener, object caller)
        {
            if (HasEventListener(type))
            {
                RemoveEventBin(_dicEventListener[type], listener, caller);
            }
        }

        private bool RemoveEventBin(List<EventBin> list, Action<EventArgs> listener, object caller)
        {
            var length = list.Count;
            for (var i = 0; i < length; i++)
            {
                var bin = list[i];
                if (bin.listener == listener && bin.thisObject == caller && bin.target == this)
                {
                    list.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private void notifyListener(EventArgs eventArgs)
        {
            if (!_dicEventListener.ContainsKey(eventArgs.Type)) return;
            var list = _dicEventListener[eventArgs.Type];
            var length = list.Count;
            if (length <= 0) return;
            var curNeedDispatcherListeners = new Queue<EventBin>();
            
            var curIndex = 0;
            while (curIndex < list.Count)
            {
                var eventBin = list[curIndex];
                
                curNeedDispatcherListeners.Enqueue(eventBin);
                curIndex++;
            }

            while (curNeedDispatcherListeners.Count > 0)
            {
                var eventBin = curNeedDispatcherListeners.Dequeue();
                eventBin.listener?.Invoke(eventArgs);
                if (eventBin.dispatchOnce)
                {
                    _onceList.Push(eventBin);
                }
                if(eventArgs.IsPropagationImmediateStopped) break;
            }


            while (_onceList.Count > 0)
            {
                var eventBin = _onceList.Pop();
                eventBin.target.Off(eventBin.type, eventBin.listener, eventBin.thisObject);
            }
        }
    }
}