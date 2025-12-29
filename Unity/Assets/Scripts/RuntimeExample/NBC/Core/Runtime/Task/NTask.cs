using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NBC
{
    public abstract class NTask : ITask
    {
        protected float _progress = 0;
        protected string _errorMsg;
        protected string TaskInfo;

        protected readonly Dictionary<string, object> _argsDic = new Dictionary<string, object>();

        public virtual string Info
        {
            get => TaskInfo;
            set => TaskInfo = value;
        }

        public TaskStatus Status { get; protected set; } = TaskStatus.None;
        public virtual string ErrorMsg => _errorMsg;
        public virtual float Progress => _progress;

        public virtual bool IsRunning => Status == TaskStatus.Running;
        public virtual bool IsDone => Status == TaskStatus.Success || Status == TaskStatus.Fail;

        public object this[string argsName]
        {
            get
            {
                if (_argsDic.TryGetValue(argsName, out object args))
                {
                    return args;
                }

                return null;
            }
            set => _argsDic[argsName] = value;
        }


        public virtual void Stop()
        {
            Status = TaskStatus.None;
        }


        public virtual void Run(IRunner runner)
        {
            Reset();
            runner?.Run(this);
        }

        public TaskStatus Process()
        {
            if (Status == TaskStatus.None)
            {
                Start();
            }

            Status = OnProcess();
            CallUpdateListener();
            if (Status == TaskStatus.Success)
            {
                _progress = 1;
                CallCompleteListener(Status);
            }
            else if (Status == TaskStatus.Fail)
            {
                _progress = 1;
                CallCompleteListener(Status);
            }

            return Status;
        }

        protected virtual void OnStart()
        {
        }

        protected virtual TaskStatus OnProcess()
        {
            return this.Status;
        }

        protected void Finish()
        {
            _progress = 1;
            Status = TaskStatus.Success;
        }

        protected void Fail(string message)
        {
            _progress = 1;
            Status = TaskStatus.Fail;
            _errorMsg = message;
        }

        private void Start()
        {
            Reset();
            Status = TaskStatus.Running;
            _progress = 0;
            OnStart();
            CallStartListener();
        }

        #region 事件

        protected event Action<ITask> OnStartListener;
        protected event Action<ITask> OnCompleteListener;
        protected event Action<ITask> OnUpdateListener;

        public ITask OnStarted(Action<ITask> callback, bool cover = false)
        {
            if (cover)
            {
                OnStartListener = callback;
            }
            else
            {
                OnStartListener += callback;
            }

            return this;
        }

        public ITask OnUpdated(Action<ITask> callback, bool cover = false)
        {
            if (cover)
            {
                OnUpdateListener = callback;
            }
            else
            {
                OnUpdateListener += callback;
            }

            return this;
        }

        public ITask OnCompleted(Action<ITask> callback, bool cover = false)
        {
            if (cover)
            {
                OnCompleteListener = callback;
            }
            else
            {
                OnCompleteListener += callback;
            }

            return this;
        }

        protected void CallStartListener()
        {
            OnStartListener?.Invoke(this);
        }

        protected void CallCompleteListener(TaskStatus taskStatus)
        {
            OnCompleteListener?.Invoke(this);

            _taskCompletionSource?.TrySetResult(null);
        }

        protected void CallUpdateListener()
        {
            OnUpdateListener?.Invoke(this);
        }

        #endregion

        #region 异步编程相关

        private TaskCompletionSource<object> _taskCompletionSource;

        /// <summary>
        /// 异步操作任务
        /// </summary>
        public Task Task
        {
            get
            {
                if (_taskCompletionSource == null)
                {
                    _taskCompletionSource = new TaskCompletionSource<object>();
                    if (IsDone)
                        _taskCompletionSource.SetResult(null);
                }

                return _taskCompletionSource.Task;
            }
        }

        #endregion

        #region IEnumerator

        bool IEnumerator.MoveNext()
        {
            return !IsDone;
        }

        public virtual void Reset()
        {
            Status = TaskStatus.None;
        }

        object IEnumerator.Current => null;

        #endregion
    }
}