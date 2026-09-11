#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 瑙掕壊绫诲瀷鏍囪瘑灞炴€?
    /// 鐢ㄤ簬鏍囪瀹炵幇浜?ICharacter 鎺ュ彛鐨勮鑹插疄鐜扮被 (Hero, Boss, Tower 绛?
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class CharacterTypeIdAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string CharacterId { get; }

        public CharacterTypeIdAttribute(string characterId)
        {
            CharacterId = characterId;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is CharacterTypeRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
