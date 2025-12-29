namespace NBC
{
    public interface IRunner
    {
        /// <summary>
        /// 是否暂停
        /// </summary>
        bool IsPaused { get; set; }

        /// <summary>
        /// 是否已经终止了
        /// </summary>
        bool IsKilled { get; }

        /// <summary>
        /// 当前运行的任务数量
        /// </summary>
        int RunningTaskNum { get; }

        /// <summary>
        /// 准备执行的任务数量
        /// </summary>
        int NeedRunTaskNum { get; }

        /// <summary>
        /// 执行一个任务
        /// </summary>
        /// <param name="task">任务对象</param>
        void Run(ITask task);

        void Process();
        
        /// <summary>
        /// 停止任务
        /// </summary>
        /// <param name="task">任务对象</param>
        void StopTask(ITask task);

        /// <summary>
        /// 停止所有任务
        /// </summary>
        void StopAllTask();

        /// <summary>
        /// 终止任务
        /// </summary>
        void ShutDown();
    }
}