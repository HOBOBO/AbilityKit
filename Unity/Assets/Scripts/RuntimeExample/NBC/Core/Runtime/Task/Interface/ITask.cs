using System;
using System.Collections;

namespace NBC
{
    public interface ITask : IProcess, IEnumerator
    {
        TaskStatus Status { get; }

        /// <summary>
        /// 当前任务的信息
        /// </summary>
        string Info { get; }

        /// <summary>
        /// 错误信息
        /// </summary>
        string ErrorMsg { get; }

        /// <summary>
        /// 当前任务的进度
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// 任务正在执行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 任务是否执行完成
        /// </summary>
        bool IsDone { get; }


        /// <summary>
        /// 停止任务
        /// </summary>
        void Stop();

        /// <summary>
        /// 任务开始回调
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="cover"></param>
        /// <returns></returns>
        ITask OnStarted(Action<ITask> callback, bool cover = false);

        /// <summary>
        /// 任务执行回调
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="cover"></param>
        /// <returns></returns>
        ITask OnUpdated(Action<ITask> callback, bool cover = false);

        /// <summary>
        /// 任务完成回调
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="cover"></param>
        /// <returns></returns>
        ITask OnCompleted(Action<ITask> callback, bool cover = false);
        
        /// <summary>
        /// 运行任务
        /// </summary>
        /// <param name="runner">任务运行器</param>
        void Run(IRunner runner);

        /// <summary>
        /// 任务参数
        /// </summary>
        /// <param name="argsName"></param>
        object this[string argsName] { get; set; }
    }
}