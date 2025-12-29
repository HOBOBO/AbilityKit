using NBC.ActionEditor;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace NBC.ActionEditorExample
{
    [Name("普通粒子片段")]
    [Description("播放一个粒子特效")]
    [Color(0.0f, 1f, 1f)]
    [Attachable(typeof(EffectTrack))]
    public class PlayParticle : Clip
    {
        [MenuName("特效对象")] [SelectObjectPath(typeof(GameObject))]
        public string resPath = "";

        [MenuName("是否变形")] public bool scale;

        private GameObject _effectObject;

        private GameObject audioClip
        {
            get
            {
                if (_effectObject == null)
                {
#if UNITY_EDITOR
                    _effectObject = AssetDatabase.LoadAssetAtPath<GameObject>(resPath);
#endif
                }

                return _effectObject;
            }
        }


        public override float Length
        {
            get => length;
            set => length = value;
        }

        public override bool IsValid => audioClip != null;

        public override string Info => IsValid ? audioClip.name : base.Info;

        public AudioTrack Track => (AudioTrack)Parent;
    }
}