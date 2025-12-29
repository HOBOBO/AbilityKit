using NBC.ActionEditor;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace NBC.ActionEditorExample
{
    [Name("声音片段")]
    [Description("播放音频的一个行为")]
    [Color(1f, 0.63f, 0f)]
    [Attachable(typeof(AudioTrack))]
    public class PlayAudio : Clip, ISubClipContainable
    {
        [SerializeField] [HideInInspector] private float blendIn = 0.25f;
        [SerializeField] [HideInInspector] private float blendOut = 0.25f;

        [MenuName("播放音频")] [SelectObjectPath(typeof(AudioClip))]
        public string resPath = "";

        private AudioClip _audioClip;

        public AudioClip audioClip
        {
            get
            {
                if (string.IsNullOrEmpty(resPath))
                {
                    _audioClip = null;
                    return null;
                }

                if (_audioClip == null)
                {
#if UNITY_EDITOR
                    _audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(resPath);
#endif
                }

                return _audioClip;
            }
        }

        [Range(0f, 1f)] [MenuName("音量")] public float volume = 1;
        [MenuName("偏移量")] public float clipOffset;


        public override float Length
        {
            get => length;
            set => length = value;
        }

        public override float BlendIn
        {
            get => blendIn;
            set => blendIn = value;
        }

        public override float BlendOut
        {
            get => blendOut;
            set => blendOut = value;
        }


        float ISubClipContainable.SubClipOffset
        {
            get => clipOffset;
            set => clipOffset = value;
        }

        float ISubClipContainable.SubClipLength => audioClip != null ? audioClip.length : 0;

        float ISubClipContainable.SubClipSpeed => 1;

        public override bool IsValid => audioClip != null;

        public override string Info => IsValid ? audioClip.name : base.Info;

        public AudioTrack Track => (AudioTrack)Parent;

        public override bool CanCrossBlend => true;

        // #if UNITY_EDITOR
//         protected override void OnClipGUI(Rect rect)
//         {
//             DrawTools.DrawLoopedAudioTexture(rect, audioClip, Length, clipOffset);
//         }
// #endif
    }
}