using UnityEngine;

namespace AbilityKit.Ability.Triggering.Json
{
    public sealed class UnityResourcesTextLoader : ITextLoader
    {
        public bool TryLoad(string id, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(id)) return false;

            var ta = Resources.Load<TextAsset>(id);
            if (ta == null) return false;

            text = ta.text;
            return !string.IsNullOrEmpty(text);
        }
    }
}
