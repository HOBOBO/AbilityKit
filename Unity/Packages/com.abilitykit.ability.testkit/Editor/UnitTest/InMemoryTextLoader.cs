using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Json;

namespace AbilityKit.Ability.UnitTest
{
    public sealed class InMemoryTextLoader : ITextLoader
    {
        private readonly IReadOnlyDictionary<string, string> _byId;

        public InMemoryTextLoader(IReadOnlyDictionary<string, string> byId)
        {
            _byId = byId;
        }

        public bool TryLoad(string id, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(id)) return false;
            if (_byId == null) return false;
            return _byId.TryGetValue(id, out text) && !string.IsNullOrEmpty(text);
        }
    }
}
