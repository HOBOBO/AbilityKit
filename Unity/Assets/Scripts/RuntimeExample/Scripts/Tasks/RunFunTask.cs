using System;

namespace NBC.ActionEditorExample
{
    public class RunFunTask : NTask
    {
        private readonly Action _action;

        public RunFunTask(Action action)
        {
            _action = action;
        }

        protected override void OnStart()
        {
            _action?.Invoke();

            Finish();
        }
    }
}