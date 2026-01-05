using AbilityKit.Ability.Share.Effect;

namespace AbilityKit.Ability.World.Services
{
    public sealed class DefaultEffectTriggeringSwitch : IEffectTriggeringSwitch
    {
        public bool Enabled { get; set; } = true;
    }
}
