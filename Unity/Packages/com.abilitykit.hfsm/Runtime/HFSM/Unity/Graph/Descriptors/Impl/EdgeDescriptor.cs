// ============================================================================
// Edge and Parameter Descriptor Implementations - 边和参数描述器实现
// 将现有的 TransitionEdge 和 Parameter 适配到描述器接口
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{
    /// <summary>
    /// 边（转换）描述器实现
    /// </summary>
    public class EdgeDescriptor : IEdgeDescriptor
    {
        private readonly TransitionEdge _edge;
        private List<IConditionDescriptor> _conditionDescriptors;

        public EdgeDescriptor(TransitionEdge edge)
        {
            _edge = edge ?? throw new ArgumentNullException(nameof(edge));
        }

        public string Id => _edge.Id;
        public string SourceNodeId => _edge.SourceNodeId;
        public string TargetNodeId => _edge.TargetNodeId;
        public int Priority => _edge.Priority;
        public bool IsExitTransition => _edge.IsExitTransition;
        public bool ForceInstantly => _edge.ForceInstantly;
        public bool UseAndLogic => _edge.UseAndLogic;

        public bool HasConditions
        {
            get
            {
                EnsureConditionsLoaded();
                return _conditionDescriptors.Count > 0;
            }
        }

        public IReadOnlyList<IConditionDescriptor> GetConditions()
        {
            EnsureConditionsLoaded();
            return _conditionDescriptors;
        }

        public string GetConditionSummary() => _edge.GetConditionSummary();

        private void EnsureConditionsLoaded()
        {
            if (_conditionDescriptors == null)
            {
                _conditionDescriptors = new List<IConditionDescriptor>();
                if (_edge.Conditions != null)
                {
                    foreach (var condition in _edge.Conditions)
                    {
                        _conditionDescriptors.Add(ConditionDescriptorFactory.Create(condition));
                    }
                }
            }
        }
    }
}
