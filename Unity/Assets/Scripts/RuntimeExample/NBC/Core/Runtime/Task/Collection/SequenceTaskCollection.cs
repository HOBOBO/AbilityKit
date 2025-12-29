namespace NBC
{
    public class SequenceTaskCollection : TaskCollection
    {
        private int _currentIndex;

        public override string Info
        {
            get
            {
                if (CurrentTask != null && CurrentTask.Count > 0)
                {
                    return CurrentTask[0].Info;
                }

                return TaskInfo;
            }
        }

        protected override TaskStatus RunTasksAndCheckIfDone()
        {
            var st = TaskStatus.Running;

            if (RawList.Count > 0)
            {
                if (CurRunTask.Count == 0 && _currentIndex < RawList.Count)
                {
                    CurRunTask.Add(RawList[_currentIndex]);
                }

                TaskStatus curSt;
                do
                {
                    var childTask = CurRunTask[0];
                    curSt = childTask.Process();
                    if (curSt >= TaskStatus.Success)
                    {
                        if (FailBreak && curSt == TaskStatus.Fail)
                        {
                            _errorMsg = childTask.ErrorMsg;
                            st = TaskStatus.Fail;
                            break;
                        }

                        FinishList.Add(childTask);
                        CurRunTask.RemoveAt(0);
                        _currentIndex++;
                        if (_currentIndex < RawList.Count)
                            CurRunTask.Add(RawList[_currentIndex]);
                    }
                } while (curSt >= TaskStatus.Success && CurRunTask.Count > 0);

                if (FinishList.Count == RawList.Count)
                    st = TaskStatus.Success;
            }
            else
            {
                st = TaskStatus.Success;
            }


            // if (RawList.Count > 0)
            // {
            //     if (FinishList.Count == RawList.Count)
            //     {
            //         st = Status.Success;
            //     }
            //     else
            //     {
            //         if (CurRunTask.Count == 0)
            //         {
            //             CurRunTask.Add(RawList[_currentIndex]);
            //         }
            //
            //         var childTask = CurRunTask[0];
            //         var curSt = childTask.Process();
            //         
            //        
            //         if (curSt >= Status.Success)
            //         {
            //             if (FailBreak && curSt == Status.Fail)
            //             {
            //                 _errorMsg = childTask.ErrorMsg;
            //                 st = Status.Fail;
            //             }
            //             
            //             FinishList.Add(childTask);
            //             CurRunTask.RemoveAt(0);
            //             _currentIndex++;
            //         }
            //     }
            // }
            // else
            // {
            //     st = Status.Success;
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