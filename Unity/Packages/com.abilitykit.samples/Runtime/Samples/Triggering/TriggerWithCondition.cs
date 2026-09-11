#nullable enable

using System.Collections.Generic;
using AbilityKit.Core.Eventing;
using AbilityKit.Samples.Abstractions;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Expression;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// 演示 Trigger Evaluate 阶段读取 Blackboard，把运行时状态作为触发条件。
    /// </summary>
    [Sample(402, "triggering", "condition", "blackboard", "package-api", "web", "deterministic")]
    public sealed class TriggerWithCondition : SampleBase
    {
        private const int CombatBoardId = 1;
        private const int ManaKey = 101;
        private const int SilencedKey = 102;
        private const int ComboKey = 103;

        public override string Title => "Trigger with Condition";
        public override string Description => "使用 DictionaryBlackboard 为 Trigger Evaluate 提供运行时条件";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            var castEvent = new EventKey<CastEvent>("sample.cast");
            var eventBus = new EventBus();
            var context = new ConditionSampleContext();
            var blackboard = new DictionaryBlackboard();
            var resolver = new DictionaryBlackboardResolver();
            resolver.Register(CombatBoardId, blackboard);

            var runner = new TriggerRunner<ConditionSampleContext>(
                eventBus,
                new FunctionRegistry(),
                new ActionRegistry(),
                new InlineContextSource(context),
                blackboards: resolver,
                numericDomains: new NumericVarDomainRegistry(),
                numericFunctions: new NumericRpnFunctionRegistry());

            using var registration = runner.Register(
                castEvent,
                new ManaAndStateTrigger(boardId: CombatBoardId, manaKey: ManaKey, silencedKey: SilencedKey, comboKey: ComboKey, manaCost: 30),
                phase: 0,
                priority: 10);

            Section("黑板条件初始化");
            UpdateBoard(blackboard, mana: 20, silenced: false, combo: 0);
            PublishCast(eventBus, castEvent, "fireball-low-mana");

            Divider();
            Section("状态变化后再次发布事件");
            UpdateBoard(blackboard, mana: 60, silenced: true, combo: 0);
            PublishCast(eventBus, castEvent, "fireball-silenced");

            UpdateBoard(blackboard, mana: 60, silenced: false, combo: 1);
            PublishCast(eventBus, castEvent, "fireball-ready");

            Divider();
            Section("触发结果");
            KeyValue("Evaluated", context.Evaluated.ToString());
            KeyValue("Executed", context.Executed.ToString());
            KeyValue("Mana", ReadInt(blackboard, ManaKey).ToString());
            KeyValue("Combo", ReadInt(blackboard, ComboKey).ToString());
            KeyValue("Blackboard.FinalMana", ReadInt(blackboard, ManaKey).ToString());
            KeyValue("Blackboard.FinalCombo", ReadInt(blackboard, ComboKey).ToString());
            KeyValue("Trace", string.Join(" | ", context.Trace));

            Divider();
            Section("这个示例实际接入的包能力");
            Bullet("DictionaryBlackboard：保存 mana、silenced、combo 这类可变运行时状态。");
            Bullet("DictionaryBlackboardResolver：把 ExecCtx.Blackboards 中的 boardId 解析成具体黑板。");
            Bullet("ITrigger.Evaluate：读取黑板并同时检查资源、沉默和事件参数。");
            Bullet("ITrigger.Execute：条件通过后扣除资源，并把连击计数写回黑板。");
        }

        private void UpdateBoard(DictionaryBlackboard blackboard, int mana, bool silenced, int combo)
        {
            blackboard.SetInt(ManaKey, mana);
            blackboard.SetBool(SilencedKey, silenced);
            blackboard.SetInt(ComboKey, combo);
            KeyValue("Blackboard", $"mana={mana}, silenced={silenced}, combo={combo}");
            KeyValue("Blackboard.Mana", mana.ToString());
            KeyValue("Blackboard.Silenced", silenced.ToString());
            KeyValue("Blackboard.Combo", combo.ToString());
        }

        private void PublishCast(EventBus eventBus, EventKey<CastEvent> key, string skillId)
        {
            var cast = new CastEvent(skillId, requiredCombo: 1);
            Log($"Publish {skillId}: requiredCombo={cast.RequiredCombo}");
            eventBus.Publish(key, in cast);
        }

        private static int ReadInt(DictionaryBlackboard blackboard, int key)
        {
            return blackboard.TryGetInt(key, out var value) ? value : 0;
        }

        private sealed class ManaAndStateTrigger : ITrigger<CastEvent, ConditionSampleContext>
        {
            private readonly int _boardId;
            private readonly int _manaKey;
            private readonly int _silencedKey;
            private readonly int _comboKey;
            private readonly int _manaCost;

            public ManaAndStateTrigger(int boardId, int manaKey, int silencedKey, int comboKey, int manaCost)
            {
                _boardId = boardId;
                _manaKey = manaKey;
                _silencedKey = silencedKey;
                _comboKey = comboKey;
                _manaCost = manaCost;
            }

            public bool Evaluate(in CastEvent args, in ExecCtx<ConditionSampleContext> ctx)
            {
                ctx.Context.Evaluated++;
                if (ctx.Blackboards == null || !ctx.Blackboards.TryResolve(_boardId, out var board))
                {
                    ctx.Context.Trace.Add($"eval({args.SkillId})=missing-board");
                    return false;
                }

                board.TryGetInt(_manaKey, out var mana);
                board.TryGetBool(_silencedKey, out var silenced);
                board.TryGetInt(_comboKey, out var combo);

                var passed = mana >= _manaCost && !silenced && combo >= args.RequiredCombo;
                ctx.Context.Trace.Add($"eval({args.SkillId})=mana:{mana},silenced:{silenced},combo:{combo},passed:{passed}");
                return passed;
            }

            public void Execute(in CastEvent args, in ExecCtx<ConditionSampleContext> ctx)
            {
                if (ctx.Blackboards == null || !ctx.Blackboards.TryResolve(_boardId, out var board))
                {
                    return;
                }

                board.TryGetInt(_manaKey, out var mana);
                board.TryGetInt(_comboKey, out var combo);
                board.SetInt(_manaKey, mana - _manaCost);
                board.SetInt(_comboKey, combo + 1);

                ctx.Context.Executed++;
                ctx.Context.Trace.Add($"execute({args.SkillId})=-{_manaCost} mana,+1 combo");
            }
        }

        private readonly struct CastEvent
        {
            public CastEvent(string skillId, int requiredCombo)
            {
                SkillId = skillId;
                RequiredCombo = requiredCombo;
            }

            public string SkillId { get; }
            public int RequiredCombo { get; }
        }

        private sealed class ConditionSampleContext
        {
            public int Evaluated { get; set; }
            public int Executed { get; set; }
            public List<string> Trace { get; } = new List<string>();
        }

        private sealed class InlineContextSource : ITriggerContextSource<ConditionSampleContext>
        {
            private readonly ConditionSampleContext _context;

            public InlineContextSource(ConditionSampleContext context)
            {
                _context = context;
            }

            public ConditionSampleContext GetContext()
            {
                return _context;
            }
        }
    }
}
