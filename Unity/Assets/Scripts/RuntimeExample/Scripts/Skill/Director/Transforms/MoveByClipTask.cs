using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class MoveByClipTask : SkillClipBase
    {
        protected override void Begin()
        {
            Debug.Log("播放 MoveByClipTask");
        }

        protected override void End()
        {
            
        }
    }
}