using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class PlayAnimationClipTask : SkillClipBase
    {
        private PlayAnimation PlayAnimation => ActionClip as PlayAnimation;

        protected override void Begin()
        {
            //这里执行粒子播放逻辑
            Debug.Log($"播放一个粒子：{PlayAnimation.resPath}");
        }

        protected override void End()
        {
            //执行粒子回收逻辑
            Debug.Log($"播放粒子结束：{PlayAnimation.resPath}");
        }
    }
}