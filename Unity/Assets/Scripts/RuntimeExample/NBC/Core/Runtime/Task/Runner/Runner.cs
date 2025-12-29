using System.Collections.Generic;

namespace NBC
{
    public class Runner : IRunner
    {
        public bool IsPaused
        {
            get => FlushingOperation.Paused;
            set => FlushingOperation.Paused = value;
        }

        public bool IsKilled => FlushingOperation.Kill;
        public int RunningTaskNum => Coroutines.Count;
        public int NeedRunTaskNum => ReadyTask.Count;

        /// <summary>
        /// 当前运行的任务
        /// </summary>
        protected readonly List<ITask> Coroutines = new List<ITask>();

        /// <summary>
        /// 准备要运行的任务
        /// </summary>
        protected readonly Queue<ITask> ReadyTask = new Queue<ITask>();

        /// <summary>
        /// 当前操作的信息
        /// </summary>
        protected readonly FlushingOperation FlushingOperation = new FlushingOperation();

        public virtual void Run(ITask task)
        {
            ReadyTask.Enqueue(task);
        }

        public virtual void Process()
        {
            var count = ReadyTask.Count;
            for (var i = 0; i < count; i++)
            {
                var task = ReadyTask.Dequeue();
                Coroutines.Add(task);
            }

            if (Coroutines.Count < 1) return;

            var index = 0;
            bool mustExit;
            do
            {
                var childTask = Coroutines[index];
                var st = childTask.Process();
                if (st >= TaskStatus.Success)
                {
                    Coroutines.Remove(childTask);
                }
                else
                {
                    index++;
                }

                mustExit = Coroutines.Count == 0 || index >= Coroutines.Count;
            } while (!mustExit);
        }

        public virtual void StopTask(ITask task)
        {
            var index = Coroutines.IndexOf(task);
            if (index != -1)
            {
                var t = Coroutines[index];
                t.Stop();
                Coroutines.RemoveAt(index);
            }
        }

        public virtual void StopAllTask()
        {
            ReadyTask.Clear();
            for (int i = 0; i < Coroutines.Count; i++)
            {
                Coroutines[i].Stop();
            }

            Coroutines.Clear();
        }

        public virtual void ShutDown()
        {
            IsPaused = false;
            FlushingOperation.Kill = true;
            StopAllTask();
        }
    }
}