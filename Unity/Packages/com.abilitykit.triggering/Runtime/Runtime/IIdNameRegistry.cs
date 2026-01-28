using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime
{
    public interface IIdNameRegistry
    {
        void RegisterFunction(FunctionId id, string name);
        void RegisterAction(ActionId id, string name);

        void RegisterBoard(int boardId, string name);
        void RegisterKey(int keyId, string name);
        void RegisterField(int fieldId, string name);

        bool TryGetFunctionName(FunctionId id, out string name);
        bool TryGetActionName(ActionId id, out string name);

        bool TryGetBoardName(int boardId, out string name);
        bool TryGetKeyName(int keyId, out string name);
        bool TryGetFieldName(int fieldId, out string name);
    }
}
