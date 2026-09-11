#nullable enable

using System;
using AbilityKit.Modifiers;

namespace AbilityKit.Samples.Logic.Samples.Modifiers
{
    /// <summary>
    /// 示例用 ModifierContext 实现 (共享)
    /// </summary>
    internal class SampleModifierContext : IModifierContext
    {
        public float Level => 1f;
        public float CurrentTime { get; set; }
        public float DeltaTime { get; set; }
        public float ElapsedTime { get; set; }
        public ModifierMetadata Metadata => ModifierMetadata.Empty;

        public float GetAttribute(ModifierKey key) => 0f;
        public T GetData<T>(string key) where T : class => null;
        public bool TryGetData<T>(string key, out T value) where T : class { value = null; return false; }
        public float GetFloat(string key) => 0f;
        public bool TryGetFloat(string key, out float value) { value = 0f; return false; }
        public int GetInt(string key) => 0;
        public bool TryGetInt(string key, out int value) { value = 0; return false; }
    }
}
