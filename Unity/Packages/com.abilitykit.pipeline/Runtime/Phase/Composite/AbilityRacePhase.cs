using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 竞争阶段：同时启动全部子阶段，任一分支完成后结束并中断其余活动分支。
    /// </summary>
    public sealed class AbilityRacePhase<TCtx> : AbilityCompositePhase<TCtx>, IInterruptiblePhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly List<int> _activePhases = new List<int>(8);
        private int _debugExecutingPhaseIndex = -1;

        public AbilityRacePhase(AbilityPipelinePhaseId phaseId) : base(phaseId)
        {
        }

        internal bool DebugIsChildActive(int index)
        {
            return index == _debugExecutingPhaseIndex || _activePhases.Contains(index);
        }

        public override void Execute(TCtx context)
        {
            IsComplete = false;
            _activePhases.Clear();

            for (var i = 0; i < _subPhases.Count; i++)
            {
                var phase = _subPhases[i];
                if (!phase.ShouldExecute(context)) continue;

                try
                {
                    _debugExecutingPhaseIndex = i;
                    phase.Execute(context);
                }
                finally
                {
                    _debugExecutingPhaseIndex = -1;
                }

                if (phase.IsComplete)
                {
                    CompleteRace(context, i);
                    return;
                }

                _activePhases.Add(i);
            }

            if (_activePhases.Count == 0) OnAllSubPhasesComplete(context);
        }

        public override void OnUpdate(TCtx context, float deltaTime)
        {
            if (IsComplete) return;

            for (var i = 0; i < _activePhases.Count; i++)
            {
                var phaseIndex = _activePhases[i];
                var phase = _subPhases[phaseIndex];
                phase.OnUpdate(context, deltaTime);
                if (!phase.IsComplete) continue;

                CompleteRace(context, phaseIndex);
                return;
            }
        }

        public void OnInterrupt(TCtx context)
        {
            if (IsComplete) return;
            InterruptActiveChildren(context, winnerIndex: -1);
            IsComplete = true;
        }

        public override IAbilityPipelinePhase<TCtx> CreateRunPhase()
        {
            var phase = new AbilityRacePhase<TCtx>(PhaseId);
            CopySubPhasesTo(phase);
            return phase;
        }

        public override void Reset()
        {
            base.Reset();
            _activePhases.Clear();
            _debugExecutingPhaseIndex = -1;
        }

        private void CompleteRace(TCtx context, int winnerIndex)
        {
            InterruptActiveChildren(context, winnerIndex);
            _activePhases.Clear();
            IsComplete = true;
        }

        private void InterruptActiveChildren(TCtx context, int winnerIndex)
        {
            for (var i = 0; i < _activePhases.Count; i++)
            {
                var index = _activePhases[i];
                if (index == winnerIndex) continue;
                InterruptPhase(_subPhases[index], context);
            }
        }

        private static void InterruptPhase(IAbilityPipelinePhase<TCtx> phase, TCtx context)
        {
            if (phase == null || phase.IsComplete) return;
            if (phase is IInterruptiblePhase<TCtx> interruptible)
            {
                interruptible.OnInterrupt(context);
                return;
            }

            var children = phase.SubPhases;
            if (children == null) return;
            for (var i = 0; i < children.Count; i++) InterruptPhase(children[i], context);
        }
    }
}
