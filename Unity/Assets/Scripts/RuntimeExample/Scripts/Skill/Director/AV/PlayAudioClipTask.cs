namespace NBC.ActionEditorExample
{
    public class PlayAudioClipTask : SkillClipBase
    {
        private PlayAudio PlayAudio => ActionClip as PlayAudio;

        protected override void Begin()
        {
            //调用自己播放音频的接口
            Log.I($"开始播放一个声音,");
        }

        protected override void End()
        {
            Log.I("播放一个声音结束");
        }
    }
}