using System;

namespace AbilityKit.Demo.Moba.Config.Core
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class MobaConfigTableAttribute : Attribute
    {
        public MobaConfigTableAttribute(
            string filePath,
            Type dtoType,
            Type moType,
            string groupName,
            int order)
        {
            FilePath = filePath;
            DtoType = dtoType;
            MoType = moType;
            GroupName = groupName;
            Order = order;
        }

        public string FilePath { get; }
        public Type DtoType { get; }
        public Type MoType { get; }
        public string GroupName { get; }
        public int Order { get; }
    }
}
