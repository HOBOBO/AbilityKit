using System;
using UnityEngine;
using AbilityKit.Ability.Triggering.Json;

namespace AbilityKit.Demo.Moba.Systems
{
    public sealed class UnityResourcesTextLoader : ITextLoader
    {
        public bool TryLoad(string id, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(id)) return false;

            try
            {
                var textAsset = Resources.Load<TextAsset>(id);
                if (textAsset != null)
                {
                    text = textAsset.text;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityResourcesTextLoader] Failed to load '{id}': {ex.Message}");
            }

            return false;
        }
    }
}
