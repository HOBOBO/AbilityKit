using NBC.ActionEditor;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AbilityKit.ActionEditorImpl
{
    [Name("普通粒子片段")]
    [Description("播放一个粒子特效")]
    [Color(0.0f, 1f, 1f)]
    [Attachable(typeof(EffectTrack))]
    public class PlayParticle : Clip, IActionTimelineRuntimeClip
    {
        public ActionTimelineRuntimeKind RuntimeKind => ActionTimelineRuntimeKind.Presentation;

        [MenuName("Resources Key")] public string resourceKey = "";

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

        public override bool IsValid => !string.IsNullOrEmpty(resourceKey) || audioClip != null;

        public override string Info => audioClip != null ? audioClip.name :
            !string.IsNullOrEmpty(resourceKey) ? resourceKey : base.Info;

        public EffectTrack Track => (EffectTrack)Parent;

        public void FillLogicArgs(System.Collections.Generic.Dictionary<string, string> args)
        {
            if (args == null) return;
            args["resourceKey"] = resourceKey ?? string.Empty;
        }
    }
}
