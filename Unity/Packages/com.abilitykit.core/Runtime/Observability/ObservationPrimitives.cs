using System;

namespace AbilityKit.Core.Observability
{
    /// <summary>
    /// 以运行标识和代次区分一次对象存续的值键，不拥有或查询目标对象。
    /// 构造只保存数据，不分配标识、不推进代次，也不验证目标是否仍存在。
    /// </summary>
    public readonly struct RuntimeObjectKey : IEquatable<RuntimeObjectKey>
    {
        /// <summary>
        /// 原样保存运行标识和代次，不拒绝无效组合。
        /// 调用方需要有效键时应另行检查 IsValid。
        /// </summary>
        /// <param name="runtimeId">领域分配的运行标识，零表示无效，负值可有效。</param>
        /// <param name="generation">领域维护的复用代次，默认为零，负值使键无效。</param>
        public RuntimeObjectKey(long runtimeId, int generation = 0)
        {
            RuntimeId = runtimeId;
            Generation = generation;
        }

        /// <summary>
        /// 获取原始运行标识，不包含分配器、所有者或跨世界唯一性信息。
        /// </summary>
        public long RuntimeId { get; }

        /// <summary>
        /// 获取原始复用代次；本类型不自动递增、检测溢出或解析目标存续状态。
        /// </summary>
        public int Generation { get; }

        /// <summary>
        /// 仅判断运行标识非零且代次非负，默认结构体无效。
        /// 结果不证明目标存在、标识唯一或对象尚未被回收。
        /// </summary>
        public bool IsValid => RuntimeId != 0L && Generation >= 0;

        /// <summary>
        /// 逐字段比较运行标识和代次，不先检查任一值的有效性。
        /// </summary>
        /// <param name="other">待比较的同类型值。</param>
        /// <returns>全部身份字段是否相同，无效值也可相等。</returns>
        public bool Equals(RuntimeObjectKey other)
        {
            return RuntimeId == other.RuntimeId && Generation == other.Generation;
        }

        /// <summary>
        /// 只与同类型装箱值按运行标识和代次比较；空值和其他类型均不相等。
        /// </summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>是否为字段相同的同类型值。</returns>
        public override bool Equals(object? obj)
        {
            return obj is RuntimeObjectKey other && Equals(other);
        }

        /// <summary>
        /// 组合运行标识和代次生成哈希，不校验有效性，允许不同值发生碰撞。
        /// 仅用于集合查找，不作为持久化标识或跨运行时协议。
        /// </summary>
        /// <returns>与本类型相等比较一致的哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeId.GetHashCode() * 397) ^ Generation;
            }
        }

        /// <summary>
        /// 判断全部身份字段是否相同，不检查值是否有效。
        /// </summary>
        /// <param name="left">左侧值。</param>
        /// <param name="right">右侧值。</param>
        /// <returns>字段比较结果。</returns>
        public static bool operator ==(RuntimeObjectKey left, RuntimeObjectKey right) => left.Equals(right);

        /// <summary>
        /// 判断至少一个身份字段是否不同，不检查值是否有效。
        /// </summary>
        /// <param name="left">左侧值。</param>
        /// <param name="right">右侧值。</param>
        /// <returns>字段比较结果。</returns>
        public static bool operator !=(RuntimeObjectKey left, RuntimeObjectKey right) => !left.Equals(right);
    }

    /// <summary>
    /// 按领域自有种类和标识关联定义的值引用，不加载定义或拥有定义生命周期。
    /// 构造不校验取值，也不保证所指配置存在、唯一或不可变。
    /// </summary>
    public readonly struct ObservationDefinitionRef : IEquatable<ObservationDefinitionRef>
    {
        /// <summary>
        /// 原样保存定义种类和标识，不解析资源或校验是否已登记。
        /// </summary>
        /// <param name="kind">领域定义的种类，零为未指定，负值可有效。</param>
        /// <param name="id">领域定义的标识，零为未指定，负值可有效。</param>
        public ObservationDefinitionRef(int kind, long id)
        {
            Kind = kind;
            Id = id;
        }

        /// <summary>
        /// 获取领域定义的原始种类值，本类型不维护种类枚举或注册表。
        /// </summary>
        public int Kind { get; }

        /// <summary>
        /// 获取领域定义的原始标识，不保证跨种类或跨所有者唯一。
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// 仅判断种类和标识均非零，默认结构体无效。
        /// 不要求正数，也不验证定义可解析。
        /// </summary>
        public bool IsValid => Kind != 0 && Id != 0L;

        /// <summary>
        /// 逐字段比较种类和定义标识，不先检查任一值的有效性。
        /// </summary>
        /// <param name="other">待比较的同类型值。</param>
        /// <returns>全部身份字段是否相同，无效值也可相等。</returns>
        public bool Equals(ObservationDefinitionRef other)
        {
            return Kind == other.Kind && Id == other.Id;
        }

        /// <summary>
        /// 只与同类型装箱值按种类和定义标识比较；空值和其他类型均不相等。
        /// </summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>是否为字段相同的同类型值。</returns>
        public override bool Equals(object? obj)
        {
            return obj is ObservationDefinitionRef other && Equals(other);
        }

        /// <summary>
        /// 组合种类和定义标识生成哈希，不校验有效性，允许不同值发生碰撞。
        /// 仅用于集合查找，不作为持久化标识或跨运行时协议。
        /// </summary>
        /// <returns>与本类型相等比较一致的哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Kind * 397) ^ Id.GetHashCode();
            }
        }

        /// <summary>
        /// 判断全部身份字段是否相同，不检查值是否有效。
        /// </summary>
        /// <param name="left">左侧值。</param>
        /// <param name="right">右侧值。</param>
        /// <returns>字段比较结果。</returns>
        public static bool operator ==(ObservationDefinitionRef left, ObservationDefinitionRef right) => left.Equals(right);

        /// <summary>
        /// 判断至少一个身份字段是否不同，不检查值是否有效。
        /// </summary>
        /// <param name="left">左侧值。</param>
        /// <param name="right">右侧值。</param>
        /// <returns>字段比较结果。</returns>
        public static bool operator !=(ObservationDefinitionRef left, ObservationDefinitionRef right) => !left.Equals(right);
    }

    /// <summary>
    /// 保存可选根、当前及父关联标识的值引用，不创建上下文或维护关联图。
    /// 所有标识的分配、解析和循环检测均由所属领域负责。
    /// </summary>
    public readonly struct ObservationTraceRef : IEquatable<ObservationTraceRef>
    {
        /// <summary>
        /// 原样保存三个关联标识，不检查父子关系、循环或目标存在性。
        /// </summary>
        /// <param name="rootId">根关联标识，零表示未指定。</param>
        /// <param name="contextId">当前关联标识，零表示未指定。</param>
        /// <param name="parentId">可选父关联标识，默认为零。</param>
        public ObservationTraceRef(long rootId, long contextId, long parentId = 0L)
        {
            RootId = rootId;
            ContextId = contextId;
            ParentId = parentId;
        }

        /// <summary>
        /// 获取原始根关联标识，允许零或负值。
        /// </summary>
        public long RootId { get; }

        /// <summary>
        /// 获取原始当前关联标识，允许零或负值。
        /// </summary>
        public long ContextId { get; }

        /// <summary>
        /// 获取原始父关联标识；它参与相等比较，但不参与有效性判定。
        /// </summary>
        public long ParentId { get; }

        /// <summary>
        /// 仅判断根或当前关联标识至少一个非零，允许负值。
        /// 默认结构体及只有父标识的值均无效，不校验关联关系。
        /// </summary>
        public bool IsValid => RootId != 0L || ContextId != 0L;

        /// <summary>
        /// 逐字段比较根、当前和父关联标识，不先检查任一值的有效性。
        /// </summary>
        /// <param name="other">待比较的同类型值。</param>
        /// <returns>全部身份字段是否相同，无效值也可相等。</returns>
        public bool Equals(ObservationTraceRef other)
        {
            return RootId == other.RootId && ContextId == other.ContextId && ParentId == other.ParentId;
        }

        /// <summary>
        /// 只与同类型装箱值按根、当前和父关联标识比较；空值和其他类型均不相等。
        /// </summary>
        /// <param name="obj">待比较对象。</param>
        /// <returns>是否为字段相同的同类型值。</returns>
        public override bool Equals(object? obj)
        {
            return obj is ObservationTraceRef other && Equals(other);
        }

        /// <summary>
        /// 组合根、当前和父关联标识生成哈希，不校验有效性，允许不同值发生碰撞。
        /// 仅用于集合查找，不作为持久化标识或跨运行时协议。
        /// </summary>
        /// <returns>与本类型相等比较一致的哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RootId.GetHashCode();
                hashCode = (hashCode * 397) ^ ContextId.GetHashCode();
                hashCode = (hashCode * 397) ^ ParentId.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// 判断全部身份字段是否相同，不检查值是否有效。
        /// </summary>
        /// <param name="left">左侧值。</param>
        /// <param name="right">右侧值。</param>
        /// <returns>字段比较结果。</returns>
        public static bool operator ==(ObservationTraceRef left, ObservationTraceRef right) => left.Equals(right);

        /// <summary>
        /// 判断至少一个身份字段是否不同，不检查值是否有效。
        /// </summary>
        /// <param name="left">左侧值。</param>
        /// <param name="right">右侧值。</param>
        /// <returns>字段比较结果。</returns>
        public static bool operator !=(ObservationTraceRef left, ObservationTraceRef right) => !left.Equals(right);
    }

    /// <summary>
    /// 同步尝试接收结构体观测事件的最小接口，不规定事件内容或采集策略。
    /// 不提供排队、调度、持久化、异常隔离或所有权转移。
    /// </summary>
    /// <typeparam name="TEvent">领域自有的结构体事件；其内部仍可含可变引用。</typeparam>
    /// <remarks>
    /// 具体实现须明确线程安全、重入、保留副本和失败语义。
    /// 同步调用不代表后续处理完成；需要保留事件的实现自行复制并管理引用成员。
    /// </remarks>
    public interface IObservationSink<TEvent> where TEvent : struct
    {
        /// <summary>
        /// 获取接收意愿的当前提示，不保证之后的 TryWrite 会接受事件。
        /// 本接口不将读取与写入组成原子操作，也不自动执行启用检查。
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 同步尝试写入，返回本次是否被接收；不代表已持久化或后续处理完成。
        /// 接口不隔离实现抛出的异常，也不保证失败时没有副作用。
        /// </summary>
        /// <param name="value">本次借用的只读参数；不转移所有权，也不保证引用成员深度不可变。</param>
        /// <returns>实现是否接收了本次事件，启用提示为真时也可返回假。</returns>
        bool TryWrite(in TEvent value);
    }

    /// <summary>
    /// 每种封闭事件类型复用的禁用接收器，恒拒绝写入且不保留事件。
    /// 这是正式空对象实现，无可变实例状态，可并发复用，不执行事件方法。
    /// </summary>
    /// <typeparam name="TEvent">结构体事件类型，不要求具备特定身份或内容。</typeparam>
    public sealed class NullObservationSink<TEvent> : IObservationSink<TEvent> where TEvent : struct
    {
        /// <summary>
        /// 获取该封闭事件类型的共享实例；不共享不同事件类型的实例。
        /// </summary>
        public static NullObservationSink<TEvent> Instance { get; } = new NullObservationSink<TEvent>();

        private NullObservationSink()
        {
        }

        /// <summary>
        /// 始终报告禁用，不读取外部配置或事件状态。
        /// </summary>
        public bool IsEnabled => false;

        /// <summary>
        /// 始终返回未接收，不复制、存储或检查事件内容，也不修改其引用成员。
        /// </summary>
        /// <param name="value">被忽略的结构体事件。</param>
        /// <returns>始终为假。</returns>
        public bool TryWrite(in TEvent value) => false;
    }
}
