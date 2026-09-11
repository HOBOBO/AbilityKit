#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.GameplayTags;
using AbilityKit.Pipeline;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Onboarding
{
    /// <summary>
    /// 新手导览：把概念串成一个真实使用框架包的技能切片。
    /// </summary>
    [Sample(3, "onboarding", "skill-slice", "package-api")]
    public sealed class FromConceptToSkillSlice : SampleBase
    {
        public override string Title => "From Concept To Skill Slice";
        public override string Description => "用 GameplayTags + Pipeline 串起标签、检查、执行、持续效果和事件";
        public override SampleCategory Category => SampleCategory.Onboarding;

        protected override void OnRun()
        {
            var manaCost = InputInt("manaCost", 30);
            var damage = InputInt("damage", 45);
            var fireballTagName = Input("fireballTag", "Ability.Fire.Fireball");
            var fireballTag = GameplayTagManager.Instance.RequestTag(fireballTagName);
            var burningTag = GameplayTagManager.Instance.RequestTag("State.Burning");

            var caster = new CombatActor("Hero", mana: 80, hp: 100);
            var target = new CombatActor("TrainingDummy", mana: 0, hp: 150);
            var context = new SkillCastContext(caster, target, manaCost, damage, fireballTag);

            Section("技能切片：Fireball");
            KeyValue("Caster", caster.ToString());
            KeyValue("Target", target.ToString());
            KeyValue("AbilityTag", fireballTag.TagName);
            KeyValue("Input.ManaCost", manaCost.ToString());
            KeyValue("Input.Damage", damage.ToString());
            KeyValue("Input.AbilityTag", fireballTag.TagName);

            Divider();
            Section("构建 Pipeline");
            var pipeline = new InstantAbilityPipeline<SkillCastContext>();
            pipeline.AddPhase(new SkillPhase("PreCheck", ctx =>
            {
                Log("[PreCheck] 目标存活且法力足够");
                if (ctx.Target.Hp <= 0 || ctx.Caster.Mana < ctx.ManaCost)
                    ctx.IsAborted = true;
            }));
            pipeline.AddPhase(new SkillPhase("Consume", ctx =>
            {
                Log($"[Consume] 消耗 {ctx.ManaCost} 法力");
                ctx.Caster.Mana -= ctx.ManaCost;
            }));
            pipeline.AddPhase(new SkillPhase("Execute", ctx =>
            {
                Log($"[Execute] 造成 {ctx.Damage} 伤害");
                ctx.Target.Hp -= ctx.Damage;
                ctx.Events.Add($"DamageApplied:{ctx.Damage}");
            }));
            pipeline.AddPhase(new SkillPhase("Ongoing", ctx =>
            {
                Log($"[Ongoing] 添加标签 {burningTag.TagName}");
                ctx.Target.Tags.Add(burningTag);
                ctx.Events.Add("BurningStarted:3s");
            }));

            Divider();
            Section("执行 Pipeline");
            var result = pipeline.RunToCompletion(new SkillPipelineConfig(), context);
            KeyValue("PipelineState", result.State.ToString());

            Divider();
            Section("执行结果");
            KeyValue("Caster", caster.ToString());
            KeyValue("Target", target.ToString());
            KeyValue("Target.Has(State.Burning)", target.Tags.HasTag(burningTag).ToString());
            KeyValue("Events", string.Join(", ", context.Events));

            Divider();
            Section("这个切片对应到 AbilityKit 模块");
            Bullet("GameplayTags：Ability.Fire.Fireball、State.Burning 等玩法词汇。");
            Bullet("Pipeline：PreCheck / Consume / Execute / Ongoing 阶段。");
            Bullet("后续可以继续接 Modifiers 表达数值变化，接 Triggering 响应命中/死亡事件。");
            Bullet("UI 宿主可以通过 SampleCatalog 展示这个示例，通过 SampleExecutionService 点击运行。");
        }

        private sealed class SkillCastContext : IAbilityPipelineContext
        {
            public SkillCastContext(CombatActor caster, CombatActor target, int manaCost, int damage, GameplayTag abilityTag)
            {
                Caster = caster;
                Target = target;
                ManaCost = manaCost;
                Damage = damage;
                AbilityTag = abilityTag;
            }

            public object AbilityInstance { get; set; } = string.Empty;
            public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
            public EAbilityPipelineState PipelineState { get; set; } = EAbilityPipelineState.Ready;
            public bool IsAborted { get; set; }
            public bool IsPaused { get; set; }
            public float StartTime { get; set; }
            public float ElapsedTime { get; set; }
            public Dictionary<string, object> SharedData { get; } = new();

            public CombatActor Caster { get; }
            public CombatActor Target { get; }
            public int ManaCost { get; }
            public int Damage { get; }
            public GameplayTag AbilityTag { get; }
            public List<string> Events { get; } = new();

            public T GetData<T>(string key, T defaultValue = default!)
            {
                return SharedData.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
            }

            public void SetData<T>(string key, T value)
            {
                SharedData[key] = value!;
            }

            public bool TryGetData<T>(string key, out T value)
            {
                if (SharedData.TryGetValue(key, out var data) && data is T typed)
                {
                    value = typed;
                    return true;
                }

                value = default!;
                return false;
            }

            public bool RemoveData(string key) => SharedData.Remove(key);
            public void ClearData() => SharedData.Clear();

            public void Reset()
            {
                CurrentPhaseId = default;
                PipelineState = EAbilityPipelineState.Ready;
                IsAborted = false;
                IsPaused = false;
                StartTime = 0;
                ElapsedTime = 0;
                SharedData.Clear();
                Events.Clear();
            }
        }

        private sealed class SkillPipelineConfig : IAbilityPipelineConfig
        {
            public int ConfigId => 3;
            public string ConfigName => "Onboarding.Fireball";
            public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
            public bool AllowInterrupt => true;
            public bool AllowPause => false;
        }

        private sealed class SkillPhase : AbilityInstantPhaseBase<SkillCastContext>
        {
            private readonly Action<SkillCastContext> _execute;

            public SkillPhase(string name, Action<SkillCastContext> execute) : base(name)
            {
                _execute = execute;
            }

            protected override void OnInstantExecute(SkillCastContext context)
            {
                _execute(context);
            }
        }

        private sealed class CombatActor
        {
            public CombatActor(string name, int mana, int hp)
            {
                Name = name;
                Mana = mana;
                Hp = hp;
            }

            public string Name { get; }
            public int Mana { get; set; }
            public int Hp { get; set; }
            public GameplayTagContainer Tags { get; } = new();

            public override string ToString()
            {
                return $"{Name}(HP={Hp}, Mana={Mana})";
            }
        }
    }
}
