namespace AbilityKit.Ability.Triggering
{
    public interface IEventHandler
    {
        void Handle(in TriggerEvent evt);
    }
}
