using System;

namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaSkillLevelParameterValue
    {
        public MobaSkillLevelParameterValue(string name, float value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value;
        }

        public string Name { get; }
        public float Value { get; }
    }

    /// <summary>Immutable, declaration-ordered values frozen for one skill cast.</summary>
    public sealed class MobaSkillLevelParameterSnapshot
    {
        private readonly MobaSkillLevelParameterValue[] _values;

        internal MobaSkillLevelParameterSnapshot(MobaSkillLevelParameterValue[] values)
        {
            _values = values == null || values.Length == 0
                ? Array.Empty<MobaSkillLevelParameterValue>()
                : (MobaSkillLevelParameterValue[])values.Clone();
        }

        public static MobaSkillLevelParameterSnapshot Empty { get; } =
            new MobaSkillLevelParameterSnapshot(Array.Empty<MobaSkillLevelParameterValue>());

        public int Count => _values.Length;

        public bool TryGetValue(string name, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(name)) return false;
            for (var i = 0; i < _values.Length; i++)
            {
                if (!string.Equals(_values[i].Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                value = _values[i].Value;
                return true;
            }
            return false;
        }

        public MobaSkillLevelParameterValue[] ToArray() =>
            _values.Length == 0 ? Array.Empty<MobaSkillLevelParameterValue>() : (MobaSkillLevelParameterValue[])_values.Clone();
    }
}
