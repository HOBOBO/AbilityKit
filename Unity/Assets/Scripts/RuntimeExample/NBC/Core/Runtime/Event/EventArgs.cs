using System;
using System.Collections.Generic;

namespace NBC
{
    /// <summary>
    /// 事件对象 用于派发事件传参数使用
    /// </summary>
    public class EventArgs
    {
        public EventArgs()
        {
        }

        public EventArgs(string type)
        {
            Type = type;
        }

        public EventArgs(string type, object data)
        {
            Type = type;
            Data = data;
        }

        // private static readonly Queue<EventArgs> _poolQueue = new Queue<EventArgs>();

        /// <summary>
        /// 派发事件的对象
        /// </summary>
        public IEventDispatcher Sender;

        /// <summary>
        /// 派发事件夹带的普通参数
        /// </summary>
        public object Data;

        /// <summary>
        /// 事件类型
        /// </summary>
        public string Type;

        /// <summary>
        /// 是否停止事件派发
        /// </summary>
        public bool IsPropagationImmediateStopped;

        /// <summary>
        /// 流转传递参数（用于高优先级往低优先级传递）
        /// </summary>
        public object TransmitData;

        /// <summary>
        /// 停止一个事件的派发
        /// </summary>
        public void StopImmediatePropagation()
        {
            IsPropagationImmediateStopped = true;
        }


        /// <summary>
        /// 设置流转数据
        /// </summary>
        /// <param name="data">需要流转的数据</param>
        public void SetTransmitData(object data)
        {
            TransmitData = data;
        }

        /// <summary>
        /// 清理对象
        /// </summary>
        protected void Clean()
        {
            Data = null;
            IsPropagationImmediateStopped = false;
        }

        public static T Create<T>(string type) where T : EventArgs, new()
        {
            EventArgs eventArgs;
            // if (_poolQueue.Count > 0)
            // {
            //     eventArgs = _poolQueue.Dequeue();
            // }
            // else
            // {
            //     var t = typeof(T);
            //     eventArgs = Activator.CreateInstance(t) as EventArgs;
            //     _poolQueue.Enqueue(eventArgs);
            // }
            var t = typeof(T);
            eventArgs = Activator.CreateInstance(t) as EventArgs;

            if (eventArgs != null)
            {
                eventArgs.Type = type;
            }

            return eventArgs as T;
        }

        /// <summary>
        /// 派发一个特定事件
        /// </summary>
        /// <param name="target">派发一个事件</param>
        /// <param name="type">事件类型</param>
        /// <param name="data">事件附带参数</param>
        public static void DispatchEvent(IEventDispatcher target, string type, object data)
        {
            var ev = Create<EventArgs>(type);
            ev.Data = data;
            target.DispatchEvent(ev);
            Release(ev);
        }

        /// <summary>
        /// 释放事件对象
        /// </summary>
        /// <param name="ev"></param>
        public static void Release(EventArgs ev)
        {
            ev.Clean();
            // _poolQueue.Enqueue(ev);
        }
    }
}