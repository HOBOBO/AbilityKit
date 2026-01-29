using AbilityKit.Ability;
using AbilityKit.Ability.Share.Impl.Pipeline;
using AbilityKit.Ability.Share.Impl.Pipeline.Skill;
using AbilityKit.Ability.Share.Impl.Pipeline.Timeline;
using AbilityKit.ActionSchema;
using UnityEngine;

namespace AbilityKit.Game.Test
{
    public sealed class SkillPipelineDemoRunner : MonoBehaviour
    {
        [SerializeField] private TextAsset logicJson;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private float timeScale = 1f;

        private DemoPipeline _pipeline;
        private IAbilityPipelineRun<IAbilityPipelineContext> _run;
        private SkillPipelineConfig _config;

        private void Start()
        {
            if (!playOnStart) return;
            StartPipeline();
        }

        [ContextMenu("Start Pipeline")]
        public void StartPipeline()
        {
            if (logicJson == null)
            {
                UnityEngine.Debug.LogError("[SkillPipelineDemoRunner] logicJson is null");
                return;
            }

            var asset = ActionTimelineJson.LoadFromJson(logicJson.text);
            if (asset == null)
            {
                UnityEngine.Debug.LogError("[SkillPipelineDemoRunner] Failed to parse logic json");
                return;
            }

            var builder = new SkillPipelineBuilder(1, "DemoSkill")
                .Condition("Condition", _ => true)
                .Cost("Cost", _ => true)
                .Check("Check", _ => true)
                .Timeline("Timeline", asset)
                .RecoverWait("Recover", 0.1f);

            _config = builder.Build();

            _pipeline = new DemoPipeline();
            foreach (var phase in builder.CreatePhases())
            {
                _pipeline.AddPhase(phase);
            }

            var ctx = new DemoContext();
            ctx.Initialize(this);
            _run = _pipeline.Start(_config, ctx);
            UnityEngine.Debug.Log($"[SkillPipelineDemoRunner] Start state={_run.State}");
        }

        private void Update()
        {
            if (_pipeline == null) return;
            if (_run == null) return;
            if (_run.State != EAbilityPipelineState.Executing) return;

            var dt = Time.deltaTime * Mathf.Max(0f, timeScale);
            _run.Tick(dt);

            var ctx = _run.Context;
            if (ctx == null) return;

            var buffer = ctx.GetData<AbilityTimelineEventBuffer>(AbilityPipelineSharedKeys.TimelineEventBuffer);
            if (buffer == null) return;

            // Consume logs
            if (buffer.TriggerLogs.Count > 0)
            {
                foreach (var e in buffer.TriggerLogs)
                {
                    UnityEngine.Debug.Log($"[SkillPipelineDemoRunner] Timeline TriggerLog t={e.Time:F3} msg={e.Message}");
                }

                buffer.TriggerLogs.Clear();
            }

            if (_run.State == EAbilityPipelineState.Completed)
            {
                UnityEngine.Debug.Log("[SkillPipelineDemoRunner] Pipeline completed");
            }
        }

        private sealed class DemoPipeline : AbilityPipeline<IAbilityPipelineContext>
        {
            protected override void ReleaseContext(IAbilityPipelineContext context)
            {
                // no-op
            }
        }

        private sealed class DemoContext : AAbilityPipelineContext
        {
        }
    }
}
