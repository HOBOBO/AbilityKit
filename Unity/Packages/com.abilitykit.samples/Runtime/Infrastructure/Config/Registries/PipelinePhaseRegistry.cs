#nullable enable

using System;
using AbilityKit.Core.Markers;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 管线阶段类型注册表
    /// 通过 PipelinePhaseTypeIdAttribute 自动发现和注册阶段类型
    /// </summary>
    public sealed class PipelinePhaseRegistry : KeyedMarkerRegistry<string, PipelinePhaseTypeIdAttribute>
    {
        private static readonly Lazy<PipelinePhaseRegistry> _instance = new(() => new PipelinePhaseRegistry());
        public static PipelinePhaseRegistry Instance => _instance.Value;

        private PipelinePhaseRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(PipelinePhaseRegistry).Assembly;
            MarkerScanner<PipelinePhaseTypeIdAttribute>.Scan(new[] { assembly }, this);
        }

        /// <summary>
        /// 通过 Attribute 注册
        /// </summary>
        internal void RegisterByAttribute(PipelinePhaseTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            Register(attr.TypeName, implType);
        }

        /// <summary>
        /// 根据名称创建阶段实例
        /// </summary>
        public object CreatePhase(string phaseName)
        {
            return GetOrCreateInstance(phaseName);
        }

        /// <summary>
        /// 尝试根据名称创建阶段实例
        /// </summary>
        public bool TryCreatePhase(string phaseName, out object phase)
        {
            if (TryGet(phaseName, out var type))
            {
                phase = Activator.CreateInstance(type);
                return true;
            }
            phase = null;
            return false;
        }
    }
}
