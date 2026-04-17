namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 支持获取载荷数据的接口
    /// </summary>
    public interface IHasPayload
    {
        bool TryGetPayloadDouble(int fieldId, out double value);
        bool TryGetPayloadInt(int fieldId, out int value);
        bool TryGetPayloadString(int fieldId, out string value);
    }
}