using System.Collections.Generic;

namespace NBC
{
    public class RunnerProcess : IProcess
    {
        /// <summary>
        /// 当前运行的任务
        /// </summary>
        protected readonly List<ITask> Coroutines;

        /// <summary>
        /// 准备要运行的任务
        /// </summary>
        protected readonly Queue<ITask> ReadyTask;

        /// <summary>
        /// 当前操作的信息
        /// </summary>
        protected readonly FlushingOperation FlushingOperation;

        /// <summary>
        /// 进程名称
        /// </summary>
        protected string Name;

        public RunnerProcess(string name, List<ITask> coroutines, Queue<ITask> readyTask, FlushingOperation op)
        {
            this.Name = name;
            Coroutines = coroutines;
            ReadyTask = readyTask;
            FlushingOperation = op;
        }

        public TaskStatus Process()
        {
            var flag = false;
            if (this.FlushingOperation.Kill) flag = true;
            else
            {
                for (var index = 0; index < this.ReadyTask.Count; index++)
                {
                    // var task = ReadyTask[0];
                    var task = ReadyTask.Dequeue();
                    this.Coroutines.Add(task);
                    // ReadyTask.RemoveAt(0);
                }

                if (this.Coroutines.Count == 0 || this.FlushingOperation.Paused)
                {
                    flag = false;
                }
                else
                {
                    var index = 0;
                    var mustExit = false;
                    do
                    {
                        var childTask = this.Coroutines[index];
                        var st = childTask.Process();
                        if (st >= TaskStatus.Success)
                        {
                            this.Coroutines.RemoveAt(index); //.splice(index, 1);
                        }
                        else
                        {
                            index++;
                        }

                        mustExit = this.Coroutines.Count == 0 || index >= this.Coroutines.Count;
                    } while (!mustExit);
                }
            }

            return flag ? TaskStatus.Success : TaskStatus.Running;
        }
    }
}