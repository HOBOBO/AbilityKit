using System;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class PlayPresentationActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.PlayPresentation;

        public int TemplateId;
        public PresentationTargetMode TargetMode = PresentationTargetMode.Target;

        public bool Stop;

        public int QueryTemplateId;
        public object ExplicitTarget;

        public string RequestKey;

        public int DurationMs;
        public float Scale;
        public float Radius;
        public string Color;

        public string PosKey;
        public Vec3 Pos;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["templateId"] = TemplateId;
            dict["targetMode"] = (int)TargetMode;

            if (Stop) dict["stop"] = true;

            if (QueryTemplateId > 0) dict["queryTemplateId"] = QueryTemplateId;
            if (ExplicitTarget != null) dict["target"] = ExplicitTarget;

            if (!string.IsNullOrEmpty(RequestKey)) dict["requestKey"] = RequestKey;
            if (DurationMs > 0) dict["durationMs"] = DurationMs;
            if (Scale != 0f) dict["scale"] = Scale;
            if (Radius != 0f) dict["radius"] = Radius;
            if (!string.IsNullOrEmpty(Color)) dict["color"] = Color;

            if (!string.IsNullOrEmpty(PosKey)) dict["posKey"] = PosKey;
            if (Pos.X != 0f || Pos.Y != 0f || Pos.Z != 0f) dict["pos"] = Pos;

            return new ActionDef(Type, dict);
        }
    }
}
