namespace AbilityKit.Core.Common.Record.Core
{
    public interface IRecordContainerSerializer
    {
        byte[] Serialize(RecordContainer container);

        RecordContainer Deserialize(byte[] data);
    }
}
