using System;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 游戏标签，对标 Unreal Engine 的 FGameplayTag。
    /// 使用 int ID 实现高效存储和比较。
    /// </summary>
    [Serializable]
    public readonly struct GameplayTag : IEquatable<GameplayTag>, IComparable<GameplayTag>
    {
        /// <summary>
        /// 空标签/无效标签，对标 UE 的 NAME_None
        /// </summary>
        public static readonly GameplayTag None = default;

        /// <summary>
        /// 标签 ID（内部使用）
        /// </summary>
        internal readonly int Id;

        /// <summary>
        /// Net Index 用于网络同步
        /// </summary>
        internal readonly ushort NetIndex;

        /// <summary>
        /// 标签 ID
        /// </summary>
        public int Value => Id;

        /// <summary>
        /// 是否有效（非空标签）
        /// </summary>
        public bool IsValid => Id != 0;

        /// <summary>
        /// 标签名称
        /// </summary>
        public string TagName => GameplayTagManager.Instance.GetName(this);

        /// <summary>
        /// 获取标签的简略名称（不含父级路径）
        /// </summary>
        public string SimpleName
        {
            get
            {
                var name = TagName;
                var lastDot = name.LastIndexOf('.');
                return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
            }
        }

        /// <summary>
        /// 内部构造函数
        /// </summary>
        internal GameplayTag(int id, ushort netIndex = 0)
        {
            Id = id;
            NetIndex = netIndex;
        }

        /// <summary>
        /// 从 ID 创建标签
        /// </summary>
        public static GameplayTag FromId(int id)
        {
            return id == 0 ? None : new GameplayTag(id);
        }

        /// <summary>
        /// 从 NetIndex 创建标签
        /// </summary>
        public static GameplayTag FromNetIndex(ushort netIndex)
        {
            if (netIndex == 0) return None;
            return new GameplayTag(0, netIndex);
        }

        /// <summary>
        /// 检查是否匹配指定标签（支持父子层级匹配）
        /// </summary>
        public bool Matches(GameplayTag other)
        {
            return GameplayTagManager.Instance.Matches(this, other);
        }

        /// <summary>
        /// 精确匹配（仅比较 ID）
        /// </summary>
        public bool MatchesExact(GameplayTag other)
        {
            return Id == other.Id;
        }

        /// <summary>
        /// 检查是否是指定父标签的子标签
        /// </summary>
        public bool IsChildOf(GameplayTag parent)
        {
            return GameplayTagManager.Instance.IsChildOf(this, parent);
        }

        /// <summary>
        /// 检查是否是指定子标签的父标签
        /// </summary>
        public bool IsParentOf(GameplayTag child)
        {
            return GameplayTagManager.Instance.IsChildOf(child, this);
        }

        /// <summary>
        /// 获取直接父标签
        /// </summary>
        public GameplayTag GetParent()
        {
            return GameplayTagManager.Instance.GetParent(this);
        }

        /// <summary>
        /// 获取根标签
        /// </summary>
        public GameplayTag GetRootTag()
        {
            return GameplayTagManager.Instance.GetRootTag(this);
        }

        /// <summary>
        /// 获取标签层级深度
        /// </summary>
        public int GetDepth()
        {
            var name = TagName;
            if (string.IsNullOrEmpty(name)) return 0;
            int count = 0;
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] == '.') count++;
            }
            return count + 1;
        }

        public bool Equals(GameplayTag other) => Id == other.Id;

        public override bool Equals(object obj) => obj is GameplayTag other && Equals(other);

        public override int GetHashCode() => Id;

        public int CompareTo(GameplayTag other) => Id.CompareTo(other.Id);

        public static bool operator ==(GameplayTag a, GameplayTag b) => a.Id == b.Id;

        public static bool operator !=(GameplayTag a, GameplayTag b) => a.Id != b.Id;

        public override string ToString() => TagName ?? string.Empty;

        /// <summary>
        /// 隐式转换为字符串
        /// </summary>
        public static implicit operator string(GameplayTag tag) => tag.TagName;
    }
}
