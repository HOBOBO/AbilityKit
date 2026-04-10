using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.PlayPresentation, "表现", "行为/Presentation", 0)]
    public sealed class PlayPresentationActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.PlayPresentation;

        [LabelText("模板Id")]
        public int TemplateId;

        [LabelText("目标模式")]
        public PresentationTargetMode TargetMode = PresentationTargetMode.Target;

        [LabelText("Stop")]
        public bool Stop;

        [LabelText("查询模板Id(可选)")]
        public int QueryTemplateId;

        [LabelText("显式目标(可选)")]
        public object ExplicitTarget;

        [LabelText("RequestKey(可选)")]
        public string RequestKey;

        [LabelText("持续毫秒(覆盖,可选)")]
        public int DurationMs;

        [LabelText("Scale(覆盖,可选)")]
        public float Scale;

        [LabelText("Radius(覆盖,可选)")]
        public float Radius;

        [LabelText("Color(覆盖,可选)")]
        public string Color;

        [LabelText("PosKey(可选)")]
        public string PosKey;

        [LabelText("Pos(可选)")]
        public Vec3 Pos;

        protected override string GetTitleSuffix()
        {
            return TemplateId > 0 ? TemplateId.ToString() : null;
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new PlayPresentationActionConfig
            {
                TemplateId = TemplateId,
                TargetMode = TargetMode,
                Stop = Stop,
                QueryTemplateId = QueryTemplateId,
                ExplicitTarget = ExplicitTarget,
                RequestKey = RequestKey,
                DurationMs = DurationMs,
                Scale = Scale,
                Radius = Radius,
                Color = Color,
                PosKey = PosKey,
                Pos = Pos,
            };
        }
    }
}
