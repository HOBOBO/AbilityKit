using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;

public sealed partial class ActorEntity
{
    public MobaAttrs GetMobaAttrs()
    {
        return new MobaAttrs(this);
    }

    public float GetMobaResource(ResourceType type)
    {
        return new MobaAttrs(this).GetResource(type);
    }
}
