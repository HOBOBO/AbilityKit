namespace AbilityKit.Triggering.Payload
{
    public interface IPayloadDoubleAccessor<TArgs>
    {
        bool TryGet(in TArgs args, int fieldId, out double value);
    }
}
