using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class VisibleToClipTask : SkillClipBase
    {
        protected override void Begin()
        {
            Debug.Log("播放 VisibleToClipTask");
        }

        protected override void End()
        {
        }
    }
}