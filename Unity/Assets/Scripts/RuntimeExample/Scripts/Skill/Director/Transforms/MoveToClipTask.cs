using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class MoveToClipTask : SkillClipBase
    {
        protected override void Begin()
        {
            Debug.Log("播放 MoveToClipTask");
        }

        protected override void End()
        {
        }
    }
}