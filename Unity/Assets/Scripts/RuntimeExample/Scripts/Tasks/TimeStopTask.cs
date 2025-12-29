using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class TimeStopTask : NTask
    {
        public float ProcessTime = 0;
        public readonly float EndTime = 0;

        public TimeStopTask(float time)
        {
            EndTime = time;
            ProcessTime = 0;
        }

        public override void Reset()
        {
            ProcessTime = 0;
        }

        public override void Stop()
        {
            ProcessTime = EndTime;
        }

        protected override TaskStatus OnProcess()
        {
            this.ProcessTime += Time.deltaTime;
            return this.ProcessTime >= this.EndTime ? TaskStatus.Success : TaskStatus.Running;
        }
    }
}