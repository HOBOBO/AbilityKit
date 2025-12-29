namespace NBC
{
    public class ParallelTaskCollection : TaskCollection
    {
        private int _currentIndex = 0;

        /// <summary>
        /// 最大并行数量 (默认为9)
        /// </summary>
        public int ParallelNum = 9;

        protected override TaskStatus RunTasksAndCheckIfDone()
        {
            var st = TaskStatus.Running;

            if (CurRunTask.Count < ParallelNum && _currentIndex < RawList.Count)
            {
                var num = ParallelNum - CurRunTask.Count;
                for (var index = 0; index < num; index++)
                {
                    if (_currentIndex < RawList.Count)
                    {
                        CurRunTask.Add(RawList[_currentIndex]);
                        _currentIndex += 1;
                    }
                }
            }

            for (var index = 0; index < CurrentTask.Count; index++)
            {
                var element = CurrentTask[index];
                var childSt = element.Process();

                if (FailBreak && childSt == TaskStatus.Fail)
                {
                    _errorMsg = element.ErrorMsg;
                    st = TaskStatus.Fail;
                    break;
                }

                if (childSt == TaskStatus.Success || childSt == TaskStatus.Fail)
                {
                    CurrentTask.RemoveAt(index);
                    index--;
                    FinishList.Add(element);
                }
            }

            if (FinishList.Count >= RawList.Count)
            {
                st = TaskStatus.Success;
            }

            // for (var index = 0; index < CurrentTask.Count; index++)
            // {
            //     var element = CurrentTask[index];
            //     var childSt = element.Process();
            //
            //     if (childSt >= Status.Success)
            //     {
            //         if (FailBreak && childSt == Status.Fail)
            //         {
            //             _errorMsg = element.ErrorMsg;
            //             st = Status.Fail;
            //         }
            //
            //         CurrentTask.RemoveAt(index);
            //         index--;
            //         FinishList.Add(element);
            //     }
            // }
            //
            // if (FinishList.Count >= RawList.Count)
            // {
            //     st = Status.Success;
            // }
            // else if (CurRunTask.Count < ParallelNum && _currentIndex < RawList.Count)
            // {
            //     var num = ParallelNum - CurRunTask.Count;
            //     for (var index = 0; index < num; index++)
            //     {
            //         if (_currentIndex < RawList.Count)
            //         {
            //             CurRunTask.Add(RawList[_currentIndex]);
            //             _currentIndex += 1;
            //         }
            //     }
            // }

            return st;
        }

        public override void Reset()
        {
            base.Reset();
            _currentIndex = 0;
        }

        public override void Stop()
        {
            base.Stop();
            _currentIndex = 0;
        }

        public override void Clear()
        {
            base.Clear();
            _currentIndex = 0;
        }
    }
}