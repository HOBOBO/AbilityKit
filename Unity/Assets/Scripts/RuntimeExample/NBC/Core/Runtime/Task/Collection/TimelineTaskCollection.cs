using UnityEngine;

namespace NBC
{
    public class TimelineTaskCollection : TaskCollection
    {
        private float _currentTime = 0;


        protected override TaskStatus RunTasksAndCheckIfDone()
        {
            this._currentTime += Time.deltaTime;
            var st = TaskStatus.Running;
            for (var index = 0; index < this.CurrentTask.Count; index++)
            {
                var element = this.CurrentTask[index];
                var childSt = element.Process();
                // if (FailBreak && childSt == Status.Fail)
                // {
                //     _errorMsg = element.ErrorMsg;
                //     st = Status.Fail;
                // }
                // else 
                if (childSt >= TaskStatus.Success)
                {
                    this.CurrentTask.RemoveAt(index);
                    index--;
                    this.FinishList.Add(element);
                }
            }


            if (this.RawList.Count > 0)
            {
                for (var index = 0; index < this.RawList.Count; index++)
                {
                    var raw = this.RawList[index];
                    if (raw == null) continue;
                    var t = raw["time"];
                    if (t != null)
                    {
                        float time = 0;
                        if (t is int i)
                        {
                            time = i;
                        }
                        else if (t is float f)
                        {
                            time = f;
                        }
                        else if (t is string s)
                        {
                            float.TryParse(s, out time);
                        }

                        if (time <= this._currentTime)
                        {
                            this.CurrentTask.Add(this.RawList[index]);
                            this.RawList.RemoveAt(index);
                            index--;
                        }
                    }
                }
            }
            else if (this.RawList.Count <= 0 && this.CurrentTask.Count <= 0)
            {
                st = TaskStatus.Success;
            }

            return st;
        }

        public override void Reset()
        {
            base.Reset();
            _currentTime = 0;
        }

        public override void Stop()
        {
            base.Stop();
            _currentTime = 0;
        }

        public override void Clear()
        {
            base.Clear();
            _currentTime = 0;
        }
    }
}