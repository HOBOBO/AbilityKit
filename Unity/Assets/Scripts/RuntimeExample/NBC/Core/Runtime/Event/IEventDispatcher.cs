using System;

namespace NBC
{
    public interface IEventDispatcher
    {
        /// <summary>
        /// 注册一个消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="listener">监听函数</param>
        /// <param name="caller">监听对象</param>
        /// <param name="priority">优先级</param>
        /// <param name="once">是否只执行一次监听</param>
        /// <returns></returns>
        IEventDispatcher On(string type, Action<EventArgs> listener, object caller, int priority = 0,
            bool once = false);

        /// <summary>
        /// 注册一个监听一次的消息
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="listener">监听函数</param>
        /// <param name="caller">监听对象</param>
        /// <param name="priority">优先级</param>
        /// <returns></returns>
        IEventDispatcher Once(string type, Action<EventArgs> listener, object caller, int priority = 0);

        /// <summary>
        /// 取消监听
        /// </summary>
        /// <param name="type"></param>
        /// <param name="listener"></param>
        /// <param name="caller"></param>
        /// <returns></returns>
        IEventDispatcher Off(string type, Action<EventArgs> listener, object caller);

        /// <summary>
        /// 取消这个消息的所有监听
        /// </summary>
        /// <param name="type"></param>
        IEventDispatcher OffAll(string type = "");

        /// <summary>
        /// 取消接受对象上的所有消息
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        IEventDispatcher OffAllCaller(object caller);

        /// <summary>
        /// 是否存在监听
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        bool HasEventListener(string type);

        /// <summary>
        /// 派发消息
        /// </summary>
        /// <param name="ev"></param>
        void DispatchEvent(EventArgs ev);

        /// <summary>
        /// 根据消息类型派发消息
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        bool DispatchEventWith(string type, object data = null);
    }
}