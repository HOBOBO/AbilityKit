#nullable enable

using System;

namespace AbilityKit.Samples.Logic.Infrastructure.Config.Attributes
{
    /// <summary>
    /// 瑙掕壊鏍囩鏍囪瘑灞炴€?
    /// 鐢ㄤ簬鏍囪瑙掕壊鎵€灞炵殑鏍囩绫诲埆 (Hero, Boss, Tank, Healer 绛?
    /// 一个角色可以有多个标签
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class CharacterTagAttribute : AbilityKit.Core.Markers.MarkerAttribute
    {
        public string Tag { get; }

        public CharacterTagAttribute(string tag)
        {
            Tag = tag;
        }

        public override void OnScanned(Type implType, AbilityKit.Core.Markers.IMarkerRegistry registry)
        {
            if (registry is CharacterTagRegistry keyedRegistry)
            {
                keyedRegistry.RegisterByAttribute(this, implType);
            }
        }
    }
}
