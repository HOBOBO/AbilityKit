using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 扩展外部插入流程前后执行点
    /// </summary>
    public abstract partial class AbilityPipeline
    {
        private Dictionary<AbilityPipelinePhaseId, List<IAbilityPipelineExtensionPoint>> _extensionPoints = new();
    
        public void AddExtensionPoint(AbilityPipelinePhaseId phaseId, IAbilityPipelineExtensionPoint extension,int order = 0)
        {
            if (!_extensionPoints.ContainsKey(phaseId))
            {
                _extensionPoints[phaseId] = new List<IAbilityPipelineExtensionPoint>();
            }
            _extensionPoints[phaseId].Add(extension);
        }
    
        protected void ExecuteExtensionPhaseStart(AbilityPipelinePhaseId phaseId, IAbilityPipelineContext context,IAbilityPipelinePhase phase)
        {
            if (_extensionPoints.TryGetValue(phaseId, out var extensions))
            {
                foreach (var extension in extensions)
                {
                    extension.OnPhaseStart(context,phase);
                }
            }
        }
    
        protected void ExecuteExtensionPhaseComplete(AbilityPipelinePhaseId phaseId, IAbilityPipelineContext context,IAbilityPipelinePhase phase)
        {
            if (_extensionPoints.TryGetValue(phaseId, out var extensions))
            {
                foreach (var extension in extensions)
                {
                    extension.OnPhaseComplete(context,phase);
                }
            }
        }
    }
}