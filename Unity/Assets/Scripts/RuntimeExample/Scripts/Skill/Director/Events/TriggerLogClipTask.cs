using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class TriggerLogClipTask : SkillClipBase
    {
        private TriggerLog TriggerLog => ActionClip as TriggerLog;

        protected override void Begin()
        {
            Debug.Log($"TriggerLog:{TriggerLog.log}");
        }

        protected override void End()
        {
        }
    }
}