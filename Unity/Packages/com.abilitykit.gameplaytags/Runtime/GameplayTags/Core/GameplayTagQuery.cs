using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签查询操作类型，对标 UE 的 EGameplayTagQueryMatchType。
    /// </summary>
    public enum GameplayTagQueryMatchType
    {
        /// <summary>
        /// 无操作
        /// </summary>
        None = 0,

        /// <summary>
        /// 任意匹配（OR）
        /// </summary>
        Any = 1,

        /// <summary>
        /// 所有匹配（AND）
        /// </summary>
        All = 2,

        /// <summary>
        /// 精确匹配
        /// </summary>
        ExactlyMatch = 3
    }

    /// <summary>
    /// 标签查询操作符，对标 UE 的 EGameplayTagQueryOperator。
    /// </summary>
    public enum GameplayTagQueryOperator
    {
        /// <summary>
        /// 无操作
        /// </summary>
        None = 0,

        /// <summary>
        /// 标签匹配
        /// </summary>
        IncludeTags = 1,

        /// <summary>
        /// 标签排除
        /// </summary>
        ExcludeTags = 2,

        /// <summary>
        /// 或操作
        /// </summary>
        Or = 3,

        /// <summary>
        /// 与操作
        /// </summary>
        And = 4,

        /// <summary>
        /// 否操作
        /// </summary>
        Not = 5
    }

    /// <summary>
    /// 游戏标签查询节点，对标 UE 的 FGameplayTagQueryNode。
    /// 用于构建复杂的标签查询表达式树。
    /// </summary>
    [Serializable]
    public class GameplayTagQueryNode
    {
        /// <summary>
        /// 操作类型
        /// </summary>
        public GameplayTagQueryOperator Operator = GameplayTagQueryOperator.None;

        /// <summary>
        /// 标签列表
        /// </summary>
        public List<GameplayTag> Tags = new List<GameplayTag>();

        /// <summary>
        /// 子节点
        /// </summary>
        public List<GameplayTagQueryNode> Children = new List<GameplayTagQueryNode>();

        /// <summary>
        /// 创建标签匹配节点
        /// </summary>
        public static GameplayTagQueryNode CreateTagQuery(
            GameplayTagQueryOperator op,
            params GameplayTag[] tags)
        {
            return new GameplayTagQueryNode
            {
                Operator = op,
                Tags = new List<GameplayTag>(tags)
            };
        }

        /// <summary>
        /// 创建组合节点
        /// </summary>
        public static GameplayTagQueryNode CreateCompound(
            GameplayTagQueryOperator op,
            params GameplayTagQueryNode[] children)
        {
            var node = new GameplayTagQueryNode
            {
                Operator = op,
                Children = new List<GameplayTagQueryNode>(children)
            };
            return node;
        }
    }

    /// <summary>
    /// 游戏标签查询，对标 Unreal Engine 的 FGameplayTagQuery。
    /// 支持复杂的标签查询表达式，用于条件检查。
    /// </summary>
    [Serializable]
    public struct GameplayTagQuery : IEquatable<GameplayTagQuery>
    {
        /// <summary>
        /// 空查询
        /// </summary>
        public static readonly GameplayTagQuery None = default;

        /// <summary>
        /// 根节点
        /// </summary>
        private readonly GameplayTagQueryNode _root;

        /// <summary>
        /// 查询描述（用于调试）
        /// </summary>
        private readonly string _description;

        internal GameplayTagQuery(GameplayTagQueryNode root, string description)
        {
            _root = root;
            _description = description;
        }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => _root != null;

        /// <summary>
        /// 检查查询是否满足标签容器
        /// </summary>
        public bool Matches(GameplayTagContainer container)
        {
            if (!IsValid) return true;
            if (container == null || container.IsEmpty) return false;
            return EvaluateNode(_root, container);
        }

        /// <summary>
        /// 检查查询是否满足单个标签
        /// </summary>
        public bool Matches(GameplayTag tag)
        {
            if (!IsValid) return true;
            if (!tag.IsValid) return false;
            return Matches(new GameplayTagContainer(tag));
        }

        /// <summary>
        /// 检查查询是否满足标签栈
        /// </summary>
        public bool Matches(GameplayTagStackContainer stacks)
        {
            if (!IsValid) return true;
            if (stacks == null || stacks.Count == 0) return false;
            return Matches(stacks.ToContainer());
        }

        private bool EvaluateNode(GameplayTagQueryNode node, GameplayTagContainer container)
        {
            if (node == null) return true;

            switch (node.Operator)
            {
                case GameplayTagQueryOperator.IncludeTags:
                    return container.HasAny(new GameplayTagContainer(node.Tags));

                case GameplayTagQueryOperator.ExcludeTags:
                    return !container.HasAny(new GameplayTagContainer(node.Tags));

                case GameplayTagQueryOperator.And:
                    foreach (var child in node.Children)
                    {
                        if (!EvaluateNode(child, container)) return false;
                    }
                    return true;

                case GameplayTagQueryOperator.Or:
                    foreach (var child in node.Children)
                    {
                        if (EvaluateNode(child, container)) return true;
                    }
                    return false;

                case GameplayTagQueryOperator.Not:
                    if (node.Children.Count > 0)
                    {
                        return !EvaluateNode(node.Children[0], container);
                    }
                    return true;

                default:
                    return true;
            }
        }

        public bool Equals(GameplayTagQuery other)
        {
            return _root == other._root && _description == other._description;
        }

        public override bool Equals(object obj) => obj is GameplayTagQuery other && Equals(other);

        public override int GetHashCode() => (_root?.GetHashCode() ?? 0) ^ (_description?.GetHashCode() ?? 0);

        public static bool operator ==(GameplayTagQuery a, GameplayTagQuery b) => a.Equals(b);

        public static bool operator !=(GameplayTagQuery a, GameplayTagQuery b) => !a.Equals(b);

        public override string ToString() => _description ?? "EmptyQuery";
    }

    /// <summary>
    /// 标签查询构建器，对标 UE 的 FGameplayTagQueryEvaluator。
    /// 提供流式 API 构建复杂查询表达式。
    /// </summary>
    public class GameplayTagQueryBuilder
    {
        private GameplayTagQueryNode _currentNode;
        private string _description = "";

        /// <summary>
        /// 开始构建 AND 查询
        /// </summary>
        public GameplayTagQueryBuilder And(params GameplayTag[] tags)
        {
            if (tags.Length > 0)
            {
                var node = GameplayTagQueryNode.CreateTagQuery(
                    GameplayTagQueryOperator.IncludeTags, tags);
                AddToCurrent(node);
            }
            return this;
        }

        /// <summary>
        /// 添加必须包含的标签
        /// </summary>
        public GameplayTagQueryBuilder RequireTags(params GameplayTag[] tags)
        {
            if (tags.Length > 0)
            {
                var node = GameplayTagQueryNode.CreateTagQuery(
                    GameplayTagQueryOperator.IncludeTags, tags);
                AddToCurrent(node);
            }
            return this;
        }

        /// <summary>
        /// 添加必须排除的标签
        /// </summary>
        public GameplayTagQueryBuilder ExcludeTags(params GameplayTag[] tags)
        {
            if (tags.Length > 0)
            {
                var node = GameplayTagQueryNode.CreateTagQuery(
                    GameplayTagQueryOperator.ExcludeTags, tags);
                AddToCurrent(node);
            }
            return this;
        }

        /// <summary>
        /// 添加 OR 组合
        /// </summary>
        public GameplayTagQueryBuilder Or(params GameplayTag[] tags)
        {
            if (tags.Length > 0)
            {
                var node = GameplayTagQueryNode.CreateTagQuery(
                    GameplayTagQueryOperator.IncludeTags, tags);

                if (_currentNode == null)
                {
                    _currentNode = GameplayTagQueryNode.CreateCompound(
                        GameplayTagQueryOperator.Or, node);
                }
                else if (_currentNode.Operator == GameplayTagQueryOperator.Or)
                {
                    _currentNode.Children.Add(node);
                }
                else
                {
                    var wrapper = GameplayTagQueryNode.CreateCompound(
                        GameplayTagQueryOperator.Or, _currentNode, node);
                    _currentNode = wrapper;
                }
            }
            return this;
        }

        /// <summary>
        /// 添加 AND 组合
        /// </summary>
        public GameplayTagQueryBuilder AndAlso(params GameplayTag[] tags)
        {
            if (tags.Length > 0)
            {
                var node = GameplayTagQueryNode.CreateTagQuery(
                    GameplayTagQueryOperator.IncludeTags, tags);
                AddToCurrent(node);
            }
            return this;
        }

        /// <summary>
        /// 添加 NOT 组合
        /// </summary>
        public GameplayTagQueryBuilder Not(params GameplayTag[] tags)
        {
            if (tags.Length > 0)
            {
                var child = GameplayTagQueryNode.CreateTagQuery(
                    GameplayTagQueryOperator.IncludeTags, tags);
                var node = GameplayTagQueryNode.CreateCompound(
                    GameplayTagQueryOperator.Not, child);
                AddToCurrent(node);
            }
            return this;
        }

        private void AddToCurrent(GameplayTagQueryNode node)
        {
            if (_currentNode == null)
            {
                _currentNode = node;
            }
            else if (_currentNode.Operator == GameplayTagQueryOperator.And ||
                     _currentNode.Operator == GameplayTagQueryOperator.Or)
            {
                _currentNode.Children.Add(node);
            }
            else
            {
                var wrapper = GameplayTagQueryNode.CreateCompound(
                    GameplayTagQueryOperator.And, _currentNode, node);
                _currentNode = wrapper;
            }
        }

        /// <summary>
        /// 构建查询
        /// </summary>
        public GameplayTagQuery Build()
        {
            if (_currentNode == null)
            {
                return GameplayTagQuery.None;
            }

            return new GameplayTagQuery(_currentNode, _description);
        }
    }
}
