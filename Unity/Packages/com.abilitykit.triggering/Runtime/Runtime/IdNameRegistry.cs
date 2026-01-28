using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime
{
    public sealed class IdNameRegistry : IIdNameRegistry
    {
        private readonly Dictionary<int, string> _functions = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _actions = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _boards = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _keys = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _fields = new Dictionary<int, string>();

        public void RegisterFunction(FunctionId id, string name) => Register(_functions, id.Value, name);
        public void RegisterAction(ActionId id, string name) => Register(_actions, id.Value, name);

        public void RegisterBoard(int boardId, string name) => Register(_boards, boardId, name);
        public void RegisterKey(int keyId, string name) => Register(_keys, keyId, name);
        public void RegisterField(int fieldId, string name) => Register(_fields, fieldId, name);

        public bool TryGetFunctionName(FunctionId id, out string name) => _functions.TryGetValue(id.Value, out name);
        public bool TryGetActionName(ActionId id, out string name) => _actions.TryGetValue(id.Value, out name);

        public bool TryGetBoardName(int boardId, out string name) => _boards.TryGetValue(boardId, out name);
        public bool TryGetKeyName(int keyId, out string name) => _keys.TryGetValue(keyId, out name);
        public bool TryGetFieldName(int fieldId, out string name) => _fields.TryGetValue(fieldId, out name);

        private static void Register(Dictionary<int, string> map, int id, string name)
        {
            if (id == 0) throw new ArgumentOutOfRangeException(nameof(id), "id must not be 0");
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name is null or empty", nameof(name));
            map[id] = name;
        }
    }
}
