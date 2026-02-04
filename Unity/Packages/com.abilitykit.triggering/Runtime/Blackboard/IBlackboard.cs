namespace AbilityKit.Triggering.Blackboard
{
    public interface IBlackboard
    {
        bool TryGetInt(int keyId, out int value);
        void SetInt(int keyId, int value);

        bool TryGetBool(int keyId, out bool value);
        void SetBool(int keyId, bool value);

        bool TryGetFloat(int keyId, out float value);
        void SetFloat(int keyId, float value);

        bool TryGetDouble(int keyId, out double value);
        void SetDouble(int keyId, double value);

        bool TryGetString(int keyId, out string value);
        void SetString(int keyId, string value);
    }
}
