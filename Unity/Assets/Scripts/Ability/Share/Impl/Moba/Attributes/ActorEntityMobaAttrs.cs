using AbilityKit.Ability.Impl.Moba.Attributes;

public sealed partial class ActorEntity
{
    public MobaAttrs GetMobaAttrs()
    {
        return new MobaAttrs(this);
    }
}
