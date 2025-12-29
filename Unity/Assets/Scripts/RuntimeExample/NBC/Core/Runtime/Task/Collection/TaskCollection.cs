using System.Collections.Generic;

namespace NBC
{
    public abstract class TaskCollection : NTask, ITaskCollection
    {
        protected readonly List<ITask> RawList;
        protected readonly List<ITask> FinishList;
        protected readonly List<ITask> CurRunTask;

        
        public TaskCollection(string taskInfo = "")
        {
            RawList = new List<ITask>();
            FinishList = new List<ITask>();
            CurRunTask = new List<ITask>();
            TaskInfo = taskInfo;
            Status = TaskStatus.None;
        }

        public List<ITask> CurrentTask => CurRunTask;

        public virtual int Count => RawList.Count + FinishList.Count;

        /// <summary>
        /// 任务失败中断任务链
        /// </summary>
        public virtual bool FailBreak
        {
            get;
            set;
        }
        
        public override float Progress
        {
            get
            {
                if (Status == TaskStatus.Success) return 1;
                if (Status == TaskStatus.None) return 0;
                var finishCount = FinishList.Count;
                for (var index = 0; index < CurRunTask.Count; index++)
                {
                    var element = CurRunTask[index];
                    finishCount += (int)element.Progress;
                }

                return finishCount * 1f / RawList.Count;
            }
        }

        protected override TaskStatus OnProcess()
        {
            return this.RunTasksAndCheckIfDone();
        }

        public ITaskCollection AddTask(ITask task)
        {
            RawList.Add(task);
            return this;
        }


        public override void Reset()
        {
            FinishList.Clear();
            CurrentTask.Clear();
            Status = TaskStatus.None;
            for (int i = 0,len = RawList.Count; i < len; i++)
            {
                RawList[i].Reset();
            }
        }

        public override void Stop()
        {
            FinishList.Clear();
            Status = TaskStatus.None;
            for (var i = 0; i < CurRunTask.Count; i++)
            {
                var task = CurrentTask[i];
                task.Stop();
            }
        }


        public virtual void Clear()
        {
            FinishList.Clear();
            RawList.Clear();
            Status = TaskStatus.None;
        }

        protected abstract TaskStatus RunTasksAndCheckIfDone();
    }
}