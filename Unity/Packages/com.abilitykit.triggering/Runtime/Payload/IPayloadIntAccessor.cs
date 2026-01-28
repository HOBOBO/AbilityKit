namespace AbilityKit.Triggering.Payload
{
    public interface IPayloadIntAccessor<TArgs>
    {
        bool TryGet(in TArgs args, int fieldId, out int value);
    }
}
