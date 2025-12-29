using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 并行阶段
    /// 同时执行所有子阶段，全部完成后阶段完成
    /// </summary>
    public class AbilityParallelPhase : AbilityCompositePhase
    {
        private List<int> _activePhases = new();
    
        public override void Execute(IAbilityPipelineContext context)
        {
            IsComplete = false;
            _activePhases.Clear();
            
            // 同时启动所有子阶段
            for (int i = 0; i < _subPhases.Count; i++)
            {
                if (_subPhases[i].ShouldExecute(context))
                {
                    _subPhases[i].Execute(context);
                    
                    // 只有未完成的阶段需要跟踪
                    if (!_subPhases[i].IsComplete)
                    {
                        _activePhases.Add(i);
                    }
                }
            }
            
            // 如果所有阶段都是瞬时完成的
            if (_activePhases.Count == 0)
            {
                OnAllSubPhasesComplete(context);
            }
        }

        public override void OnUpdate(IAbilityPipelineContext context, float deltaTime)
        {
            if (IsComplete)
                return;

            // 更新所有活跃的子阶段
            for (int i = _activePhases.Count - 1; i >= 0; i--)
            {
                int phaseIndex = _activePhases[i];
                var phase = _subPhases[phaseIndex];
                
                phase.OnUpdate(context, deltaTime);
                
                if (phase.IsComplete)
                {
                    _activePhases.RemoveAt(i);
                }
            }
        
            // 检查是否全部完成
            if (_activePhases.Count == 0)
            {
                OnAllSubPhasesComplete(context);
            }
        }

        public override void Reset()
        {
            base.Reset();
            _activePhases.Clear();
        }
    }
}