using System;
using NBC.ActionEditor;
using UnityEngine;

namespace NBC.ActionEditorExample
{
    [Name("缩放")]
    [Description("缩放剪辑的行为")]
    [Color(70f / 255f, 1, 140f / 255f)]
    [Attachable(typeof(ActionTrack))]
    public class ScaleTo: Clip
    {

        [MenuName("缩放曲线")] public EaseType interpolation = EaseType.QuadInOut;
        [MenuName("缩放目标")] public Vector2 targetScale = Vector2.one;

        public override float Length
        {
            get => length;
            set => length = value;
        }

        public override string Info => $"缩放:\n{targetScale}";

        public override bool IsValid => true;

        private ActionTrack Track => (ActionTrack)Parent;
    }
}