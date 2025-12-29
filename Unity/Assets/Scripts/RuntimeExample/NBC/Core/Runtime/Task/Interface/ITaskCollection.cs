using System.Collections.Generic;

namespace NBC
{
    public interface ITaskCollection : ITask
    {
        /// <summary>
        /// 当前运行的任务堆栈
        /// </summary>
        List<ITask> CurrentTask { get; }

        /// <summary>
        /// 添加一个任务
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        ITaskCollection AddTask(ITask task);

        /// <summary>
        /// 清理任务列表
        /// </summary>
        void Clear();
    }
}