using NBC.ActionEditor;

namespace NBC.ActionEditorExample
{
    public static class SkillDirector
    {
        public static SkillClipBase GetClip(Clip clip)
        {
            SkillClipBase classInst = null;
            //这里用穷举方式来做。当然大家如果不用脚本剔除，也可以用反射来做
            if (clip is PlayAudio)
            {
                classInst = new PlayAudioClipTask();
            }
            else if (clip is PlayAnimation)
            {
                classInst = new PlayAnimationClipTask();
            }
            else if (clip is PlayParticle)
            {
                classInst = new PlayParticleClipTask();
            }
            else if (clip is MoveBy)
            {
                classInst = new MoveByClipTask();
            }
            else if (clip is MoveTo)
            {
                classInst = new MoveToClipTask();
            }
            else if (clip is RotateTo)
            {
                classInst = new RotateToClipTask();
            }
            else if (clip is ScaleTo)
            {
                classInst = new ScaleToClipTask();
            }
            else if (clip is VisibleTo)
            {
                classInst = new VisibleToClipTask();
            }
            else if (clip is TriggerLog)
            {
                classInst = new TriggerLogClipTask();
            }
            else if (clip is TriggerEvent)
            {
                classInst = new TriggerEventClipTask();
            }
            else if (clip is TriggerShake)
            {
                classInst = new TriggerShakeClipTask();
            }

            if (classInst != null)
            {
                classInst.Time = clip.StartTime;
                classInst.TotalTime = clip.Length;
                classInst.SetActionClip(clip);
            }

            return classInst;
        }
    }
}