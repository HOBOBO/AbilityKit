#nullable enable

using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Onboarding
{
    /// <summary>
    /// 展示 Web 观察器需要的结构化时间轴快照，而不是只依赖普通文本日志。
    /// </summary>
    [Sample(6, "onboarding", "web", "timeline", "snapshot", "deterministic")]
    public sealed class WebTimelineSnapshot : SampleBase
    {
        public override string Title => "Web Timeline Snapshot";
        public override string Description => "演示如何用稳定的结构化输出描述帧、实体、事件和范围，供 Web timeline 观察器消费";
        public override SampleCategory Category => SampleCategory.Onboarding;

        protected override void OnRun()
        {
            Section("Timeline 快照契约");
            Bullet("日志仍然适合解释教学步骤。快照则用于驱动 Canvas、Replay 或外部可视化宿主。");
            Bullet("第一版先用 KeyValue 输出稳定字段；后续可升级为专门的 snapshot sink。");

            Divider();
            Section("Frame snapshots");
            EmitFrame(0, 0.00f, "caster", 0.0f, 0.0f, "prepare", "skill.fireball.cast-start");
            EmitFrame(1, 0.10f, "projectile", 1.5f, 0.0f, "flying", "projectile.spawn");
            EmitFrame(2, 0.20f, "projectile", 3.0f, 0.0f, "flying", "projectile.move");
            EmitFrame(3, 0.30f, "target", 4.5f, 0.0f, "hit", "projectile.hit");
            EmitFrame(4, 0.40f, "target", 4.5f, 0.0f, "burning", "damage.apply");

            Divider();
            Section("Web 宿主可视化建议");
            KeyValue("Stage", "960x540 canvas");
            KeyValue("Timeline", "frame slider + play/pause + event card");
            KeyValue("EntityLayer", "caster/projectile/target positions");
            KeyValue("EventLayer", "cast-start/spawn/move/hit/damage badges");
            KeyValue("Next", "combat/projectile-hit-damage -> skill/fireball-complete");
        }

        private void EmitFrame(int frame, float time, string entity, float x, float y, string state, string evt)
        {
            KeyValue($"snapshot.frame[{frame}].time", time.ToString("F2"));
            KeyValue($"snapshot.frame[{frame}].entity", entity);
            KeyValue($"snapshot.frame[{frame}].position", $"x={x:F1}, y={y:F1}");
            KeyValue($"snapshot.frame[{frame}].state", state);
            KeyValue($"snapshot.frame[{frame}].event", evt);
        }
    }
}
