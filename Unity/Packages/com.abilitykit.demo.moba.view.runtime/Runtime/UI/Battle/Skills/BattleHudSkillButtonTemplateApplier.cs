using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Game.Battle.View.Lib.Skill;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillButtonTemplateApplier
    {
        public void Apply(SkillButtonView view, SkillButtonTemplateMO template, BattleHudSkillPresentationSpec spec)
        {
            if (view == null) return;

            // A reused slot must not inherit gestures or input gating from the previous skill.
            view.ResetPresentationState();
            var cfg = view.Config;
            cfg.LongPressSeconds = template != null && template.LongPressSeconds > 0f
                ? template.LongPressSeconds
                : cfg.LongPressSeconds > 0f ? cfg.LongPressSeconds : 0.35f;
            cfg.DragThreshold = template != null && template.DragThreshold > 0f
                ? template.DragThreshold
                : cfg.DragThreshold > 0f ? cfg.DragThreshold : 12f;
            cfg.EnableAim = spec.EnableAim;
            cfg.AimMode = spec.AimMode;
            cfg.AimMaxRadius = spec.UiAimRadiusPixels;
            cfg.UsePointMode = spec.UsePointMode;
            cfg.SelectRange = template != null ? template.SelectRange : 0f;
            cfg.FaceToAim = spec.FaceToAim;
            cfg.IndicatorShape = spec.IndicatorShape;
            cfg.IndicatorLengthPixels = spec.UiLengthPixels;
            cfg.IndicatorWidthPixels = spec.UiWidthPixels;

            view.Config = cfg;
            Log.Info($"[BattleHudSkillButtonTemplateApplier] applied skillId={spec.SkillId}, templateId={(template != null ? template.Id : 0)}, enableAim={cfg.EnableAim}, aimMode={cfg.AimMode}, usePointMode={cfg.UsePointMode}, dragThreshold={cfg.DragThreshold:F1}");
        }
    }
}
