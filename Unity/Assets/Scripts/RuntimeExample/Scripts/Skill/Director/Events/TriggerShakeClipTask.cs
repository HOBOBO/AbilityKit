using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class TriggerShakeClipTask : SkillClipBase
    {
        private TriggerShake TriggerShake => ActionClip as TriggerShake;

        protected override void Begin()
        {
            //调用自己项目震动逻辑
            Debug.Log("播放震动逻辑");
        }

        protected override void End()
        {
        }
    }
}