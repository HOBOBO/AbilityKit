using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 扩展外部插入流程前后执行点
    /// </summary>
    public abstract partial class AbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private Dictionary<AbilityPipelinePhaseId, List<IAbilityPipelineExtensionPoint<TCtx>>> _extensionPoints =
            new Dictionary<AbilityPipelinePhaseId, List<IAbilityPipelineExtensionPoint<TCtx>>>(8);
    
        public void AddExtensionPoint(AbilityPipelinePhaseId phaseId, IAbilityPipelineExtensionPoint<TCtx> extension, int order = 0)
        {
            if (!_extensionPoints.TryGetValue(phaseId, out var list))
            {
                list = new List<IAbilityPipelineExtensionPoint<TCtx>>(4);
                _extensionPoints[phaseId] = list;
            }
            list.Add(extension);
        }
    
        protected void ExecuteExtensionPhaseStart(AbilityPipelinePhaseId phaseId, TCtx context, IAbilityPipelinePhase<TCtx> phase)
        {
            if (_extensionPoints.TryGetValue(phaseId, out var extensions))
            {
                for (int i = 0; i < extensions.Count; i++)
                {
                    extensions[i].OnPhaseStart(context, phase);
                }
            }
        }
    
        protected void ExecuteExtensionPhaseComplete(AbilityPipelinePhaseId phaseId, TCtx context, IAbilityPipelinePhase<TCtx> phase)
        {
            if (_extensionPoints.TryGetValue(phaseId, out var extensions))
            {
                for (int i = 0; i < extensions.Count; i++)
                {
                    extensions[i].OnPhaseComplete(context, phase);
                }
            }
        }
    }
}