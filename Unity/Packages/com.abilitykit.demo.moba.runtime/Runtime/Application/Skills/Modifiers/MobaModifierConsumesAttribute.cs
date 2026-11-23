using System;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaModifierSampleTiming
    {
        CreateSnapshot,
        EventSample,
        DerivedView,
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public sealed class MobaModifierConsumesAttribute : Attribute
    {
        public MobaModifierConsumesAttribute(int parameterId, MobaParameterStage stage,
            MobaModifierSampleTiming timing)
        {
            ParameterId = parameterId;
            Stage = stage;
            Timing = timing;
        }

        public int ParameterId { get; }
        public MobaParameterStage Stage { get; }
        public MobaModifierSampleTiming Timing { get; }
    }
}
