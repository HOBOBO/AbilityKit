using System;
using System.Collections.Generic;
using AbilityKit.Attributes.Formula;
using AbilityKit.Attributes.Constraint;

namespace AbilityKit.Attributes.Core
{
    /// <summary>
    /// 属性 ID 映射器。
    /// 业务层可以继承此类来实现自己的 ID 映射策略。
    /// 
    /// 设计说明：
    /// - 提供类型安全的枚举到 AttributeId 的映射
    /// - 避免每次查询都走 switch 语句
    /// - 支持延迟初始化
    /// 
    /// 使用示例：
    /// ```csharp
    /// public class MobaAttributeIdMapper : AttributeIdMapper
    /// {
    ///     private MobaAttributeIdMapper() : base("Moba") { }
    ///     
    ///     public static readonly MobaAttributeIdMapper Instance = new();
    ///     
    ///     public AttributeId HP => GetOrCreate(BattleAttributeType.HP, "Battle.HP");
    ///     public AttributeId MAX_HP => GetOrCreate(BattleAttributeType.MAX_HP, "Battle.MAX_HP");
    ///     
    ///     public AttributeId FromType(BattleAttributeType type) => type switch
    ///     {
    ///         BattleAttributeType.HP => HP,
    ///         BattleAttributeType.MAX_HP => MAX_HP,
    ///         _ => default
    ///     };
    /// }
    /// ```
    /// </summary>
    public abstract class AttributeIdMapper
    {
        private readonly string _prefix;
        private readonly Dictionary<int, AttributeId> _enumToId = new Dictionary<int, AttributeId>();
        private readonly Dictionary<string, AttributeId> _enumToIdByName = new Dictionary<string, AttributeId>();
        private readonly List<AttributeId> _registeredIds = new List<AttributeId>();
        private bool _frozen;

        /// <summary>
        /// 注册表引用
        /// </summary>
        protected IAttributeRegistry Registry { get; private set; }

        /// <summary>
        /// 是否已冻结
        /// </summary>
        public bool IsFrozen => _frozen;

        protected AttributeIdMapper(string prefix, IAttributeRegistry registry = null)
        {
            _prefix = prefix ?? string.Empty;
            Registry = registry ?? AttributeRegistry.DefaultRegistry;
        }

        /// <summary>
        /// 设置注册表
        /// </summary>
        public void SetRegistry(IAttributeRegistry registry)
        {
            if (_frozen) throw new InvalidOperationException("Cannot set registry after Freeze()");
            Registry = registry ?? AttributeRegistry.DefaultRegistry;
        }

        /// <summary>
        /// 冻结映射器，冻结后不能注册新属性
        /// </summary>
        public void Freeze()
        {
            Registry.Freeze();
            _frozen = true;
        }

        /// <summary>
        /// 获取或创建 AttributeId
        /// </summary>
        protected AttributeId GetOrCreate(Enum enumValue, string name)
        {
            var enumValueInt = Convert.ToInt32(enumValue);
            if (_enumToId.TryGetValue(enumValueInt, out var existingId))
            {
                return existingId;
            }

            var id = Registry.Request(name);
            _enumToId[enumValueInt] = id;
            _enumToIdByName[name] = id;
            _registeredIds.Add(id);
            return id;
        }

        /// <summary>
        /// 获取或创建带自定义定义的 AttributeId
        /// </summary>
        protected AttributeId GetOrCreate(Enum enumValue, string name, AttributeDef def)
        {
            var enumValueInt = Convert.ToInt32(enumValue);
            if (_enumToId.TryGetValue(enumValueInt, out var existingId))
            {
                return existingId;
            }

            def.Name = name;
            var id = Registry.Register(def);
            _enumToId[enumValueInt] = id;
            _enumToIdByName[name] = id;
            _registeredIds.Add(id);
            return id;
        }

        /// <summary>
        /// 批量预注册多个属性
        /// </summary>
        protected void RegisterMany(params AttributeDef[] defs)
        {
            foreach (var def in defs)
            {
                var id = Registry.Register(def);
                _enumToIdByName[def.Name] = id;
                _registeredIds.Add(id);
            }
        }

        /// <summary>
        /// 通过名称查找
        /// </summary>
        public AttributeId FindByName(string name)
        {
            return _enumToIdByName.TryGetValue(name, out var id) ? id : default;
        }

        /// <summary>
        /// 获取所有已注册的 ID
        /// </summary>
        public IReadOnlyList<AttributeId> GetAllRegistered()
        {
            return _registeredIds;
        }

        /// <summary>
        /// 获取已注册数量
        /// </summary>
        public int RegisteredCount => _registeredIds.Count;
    }
}
