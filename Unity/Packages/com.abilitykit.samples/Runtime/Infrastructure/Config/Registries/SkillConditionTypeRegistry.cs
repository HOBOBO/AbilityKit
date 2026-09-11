#nullable enable

using System;
using AbilityKit.Core.Markers;
using AbilityKit.Samples.Logic.Infrastructure.Config.Attributes;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 技能条件类型注册表
    /// 閫氳繃 SkillConditionTypeIdAttribute 鑷姩鍙戠幇鍜屾敞鍐屾妧鑳芥潯浠剁被鍨?(HasEnoughMana, TargetInRange 绛?
    /// </summary>
    public sealed class SkillConditionTypeRegistry : KeyedMarkerRegistry<string, SkillConditionTypeIdAttribute>
    {
        private static readonly Lazy<SkillConditionTypeRegistry> _instance = new(() => new SkillConditionTypeRegistry());
        public static SkillConditionTypeRegistry Instance => _instance.Value;

        private SkillConditionTypeRegistry()
        {
            ScanCurrentAssembly();
        }

        private void ScanCurrentAssembly()
        {
            var assembly = typeof(SkillConditionTypeRegistry).Assembly;
            MarkerScanner<SkillConditionTypeIdAttribute>.Scan(new[] { assembly }, this);
        }

        internal void RegisterByAttribute(SkillConditionTypeIdAttribute attr, Type implType)
        {
            if (attr == null || implType == null) return;
            Register(attr.ConditionName, implType);
        }

        /// <summary>
        /// 鏍规嵁鏉′欢鍚嶇О鍒涘缓鎶€鑳芥潯浠跺疄渚?
        /// </summary>
        public object CreateCondition(string conditionName)
        {
            return GetOrCreateInstance(conditionName);
        }
    }
}
