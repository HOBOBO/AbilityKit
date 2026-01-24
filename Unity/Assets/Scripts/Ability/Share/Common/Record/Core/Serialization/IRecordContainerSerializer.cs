namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public interface IRecordContainerSerializer
    {
        byte[] Serialize(RecordContainer container);

        RecordContainer Deserialize(byte[] data);
    }
}
