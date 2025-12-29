using NBC.ActionEditor;
using NBC.ActionEditorExample;
using UnityEngine;

namespace ActionEditorExample
{
    /// <summary>
    /// 音频预览
    /// </summary>
    [CustomPreview(typeof(PlayAudio))]
    public class PlayAudioPreview : PreviewBase<PlayAudio>
    {
        private AudioSource source;

        public override void Update(float time, float previousTime)
        {
            if (source != null)
            {
                AudioSampler.Sample(source, clip.audioClip, time - clip.clipOffset, previousTime - clip.clipOffset,
                    clip.volume);
            }
        }

        public override void Enter()
        {
            Do();
        }

        public override void ReverseEnter()
        {
            Do();
        }

        public override void Exit()
        {
            Undo();
        }

        public override void Reverse()
        {
            Undo();
        }

        void Do()
        {
            if (source == null)
            {
                source = AudioSampler.GetSource();
            }

            if (source != null)
            {
                source.clip = clip.audioClip;
            }
        }

        void Undo()
        {
            if (source != null)
            {
                source.clip = null;
                AudioSampler.RetureSource(source);
            }
        }
    }
}