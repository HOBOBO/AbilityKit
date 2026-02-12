#if ABK_LEGACY_MOBA_HOST_EXTENSIONS
namespace AbilityKit.Ability.Impl.Moba.Util
{
    public sealed class FixedStepTickRunner : AbilityKit.Ability.Host.Extensions.Time.FixedStepTickRunner
    {
        public FixedStepTickRunner(int tickRate)
            : base(tickRate)
        {
        }
    }
}
#endif
