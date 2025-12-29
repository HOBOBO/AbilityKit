using UnityEngine;

namespace NBC.ActionEditorExample
{
    public class PlayParticleClipTask : SkillClipBase
    {
        protected override void Begin()
        {
            //播放粒子
            Debug.Log("开始播放粒子===");
        }

        protected override void End()
        {
            //回收粒子
            Debug.Log("粒子播放完毕===");
        }
    }
}