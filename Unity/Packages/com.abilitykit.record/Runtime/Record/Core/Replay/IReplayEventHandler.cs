namespace AbilityKit.Core.Common.Record.Core
{
    public interface IReplayEventHandler
    {
        void Handle(in RecordEvent e);
    }
}
