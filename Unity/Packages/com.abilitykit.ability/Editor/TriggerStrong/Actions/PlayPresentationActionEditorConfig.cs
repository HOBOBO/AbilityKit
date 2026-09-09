using System;
using AbilityKit.Ability.Config;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.PlayPresentation, "表现", "行为/表现", 0)]
    public sealed class PlayPresentationActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.PlayPresentation;

        [LabelText("模板 ID")]
        public int TemplateId;

        [LabelText("目标模式")]
        public PresentationTargetMode TargetMode = PresentationTargetMode.Target;

        [LabelText("停止")]
        public bool Stop;

        [LabelText("查询模板 ID（可选）")]
        public int QueryTemplateId;

        [LabelText("显式目标（可选）")]
        public object ExplicitTarget;

        [LabelText("请求键（可选）")]
        public string RequestKey;

        [LabelText("持续毫秒（覆盖，可选）")]
        public int DurationMs;

        [LabelText("缩放（覆盖，可选）")]
        public float Scale;

        [LabelText("半径（覆盖，可选）")]
        public float Radius;

        [LabelText("颜色（覆盖，可选）")]
        public string Color;

        [LabelText("位置键（可选）")]
        public string PosKey;

        [LabelText("位置（可选）")]
        public Vec3 Pos;

        protected override string GetTitleSuffix()
        {
            return TemplateId > 0 ? TemplateId.ToString() : null;
        }

        public override ActionConfigBase ToRuntimeConfig()
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
