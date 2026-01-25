namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public interface IReplayEventHandler
    {
        void Handle(in RecordEvent e);
    }
}
