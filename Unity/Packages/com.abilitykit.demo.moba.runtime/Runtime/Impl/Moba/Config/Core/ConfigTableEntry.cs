using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// 配置表条目，定义单个配置表的元数据
    /// </summary>
    public sealed class ConfigTableEntry
    {
        /// <summary>
        /// 配置文件名（不含扩展名）
        /// </summary>
        public string FileWithoutExt { get; }

        /// <summary>
        /// DTO 类型
        /// </summary>
        public Type DtoType { get; }

        /// <summary>
        /// MO 类型
        /// </summary>
        public Type MoType { get; }

        /// <summary>
        /// 所属配置组名称
        /// </summary>
        public string GroupName { get; }

        public ConfigTableEntry(string fileWithoutExt, Type dtoType, Type moType, string groupName)
        {
            FileWithoutExt = fileWithoutExt ?? throw new ArgumentNullException(nameof(fileWithoutExt));
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
            MoType = moType ?? throw new ArgumentNullException(nameof(moType));
            GroupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
        }
    }
}
