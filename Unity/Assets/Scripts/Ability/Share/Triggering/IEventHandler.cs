namespace AbilityKit.Triggering
{
    public interface IEventHandler
    {
        void Handle(in TriggerEvent evt);
    }
}
