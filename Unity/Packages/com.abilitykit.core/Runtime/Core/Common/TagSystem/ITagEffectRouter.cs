namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public interface ITagEffectRouter
    {
        void Register(ITagChangeSubscriber subscriber);
        bool Unregister(ITagChangeSubscriber subscriber);
    }
}
