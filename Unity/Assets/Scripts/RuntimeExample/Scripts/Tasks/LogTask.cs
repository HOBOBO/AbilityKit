using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class LogTask : NTask
    {
        private string _info;

        public LogTask(string info)
        {
            _info = info;
        }

        protected override void OnStart()
        {
            Debug.Log(_info);
            Finish();
        }
    }
}