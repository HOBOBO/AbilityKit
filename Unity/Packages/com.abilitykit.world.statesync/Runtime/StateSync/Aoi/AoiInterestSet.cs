using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Aoi
{
    /// <summary>
    /// AOI 样本的类别和标识组合键；不包含实体代际或世界身份。
    /// </summary>
    public readonly struct AoiEntityKey : IEquatable<AoiEntityKey>, IComparable<AoiEntityKey>
    {
        /// <summary>
        /// 直接保存类别和标识；是否可用于评估由有效性属性判断。
        /// </summary>
        public AoiEntityKey(int kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        /// <summary>
        /// 调用方定义的类别，与标识共同区分样本；评估不解释类别值。
        /// </summary>
        public int Kind { get; }

        /// <summary>
        /// 类别内的实体标识；正值才参与评估。
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 返回标识是否大于零，不验证类别或实体是否存在。
        /// </summary>
        public bool IsValid => Id > 0;

        /// <summary>
        /// 类别和标识均相同才相等，不检查有效性。
        /// </summary>
        public bool Equals(AoiEntityKey other)
        {
            return Kind == other.Kind && Id == other.Id;
        }

        /// <summary>
        /// 仅当对象为同类型且类别和标识相同才相等。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is AoiEntityKey other && Equals(other);
        }

        /// <summary>
        /// 组合类别和标识以支持哈希查找，不作为唯一实体标识。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Kind * 397) ^ Id;
            }
        }

        /// <summary>
        /// 按类别升序比较，类别相同再按标识升序比较。
        /// </summary>
        public int CompareTo(AoiEntityKey other)
        {
            var byKind = Kind.CompareTo(other.Kind);
            return byKind != 0 ? byKind : Id.CompareTo(other.Id);
        }

        /// <summary>
        /// 返回由冒号分隔的类别和标识，供诊断使用。
        /// </summary>
        public override string ToString()
        {
            return Kind + ":" + Id;
        }
    }

    /// <summary>
    /// 描述单个观察者的一次二维半径评估；坐标和半径须使用相同单位。
    /// 不定义世界恢复范围，评估器不读取实体数量上限。
    /// </summary>
    public readonly struct AoiInterestScope
    {
        /// <summary>
        /// 保存圆心并将可见半径限制为非负；边界半径默认为 0，非正时沿用可见半径，否则至少等于可见半径。
        /// 数量参数原样保存，默认 0；调用方应提供有限坐标和半径。
        /// </summary>
        public AoiInterestScope(float centerX, float centerY, float visibleRadius, float boundaryRadius = 0f, int maxEntities = 0)
        {
            CenterX = centerX;
            CenterY = centerY;
            VisibleRadius = Math.Max(0f, visibleRadius);
            BoundaryRadius = Math.Max(VisibleRadius, boundaryRadius <= 0f ? visibleRadius : boundaryRadius);
            MaxEntities = maxEntities;
        }

        /// <summary>
        /// 返回二维评估中心的第一坐标。
        /// </summary>
        public float CenterX { get; }

        /// <summary>
        /// 返回二维评估中心的第二坐标。
        /// </summary>
        public float CenterY { get; }

        /// <summary>
        /// 新实体的进入半径，正值时包含圆周；零值关闭距离过滤。
        /// </summary>
        public float VisibleRadius { get; }

        /// <summary>
        /// 曾可见实体的保留半径，正半径评估时包含圆周。
        /// </summary>
        public float BoundaryRadius { get; }

        /// <summary>
        /// 兼容配置值，默认 0；当前评估器不据此排序或截断。
        /// </summary>
        public int MaxEntities { get; }

        /// <summary>
        /// 仅当可见半径大于零时启用距离评估；单独设置边界半径不能启用过滤。
        /// </summary>
        public bool HasRadius => VisibleRadius > 0f;
    }

    /// <summary>
    /// 供观察者评估的二维值样本；不携带可用于还原业务实体的完整状态。
    /// </summary>
    public readonly struct AoiEntitySample
    {
        /// <summary>
        /// 保存坐标、键和可选元数据；优先级、层、所有者及标志默认均为 0。
        /// </summary>
        public AoiEntitySample(AoiEntityKey key, float x, float y, int priority = 0, int layer = 0, int ownerId = 0, byte flags = 0)
        {
            Key = key;
            X = x;
            Y = y;
            Priority = priority;
            Layer = layer;
            OwnerId = ownerId;
            Flags = flags;
        }

        /// <summary>
        /// 样本的组合键；评估跳过无效键，调用方应在一批样本内去重。
        /// </summary>
        public AoiEntityKey Key { get; }

        /// <summary>
        /// 第一坐标，应与评估圆心使用同一坐标系和单位。
        /// </summary>
        public float X { get; }

        /// <summary>
        /// 第二坐标，应与评估圆心使用同一坐标系和单位。
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// 调用方保留的优先级；当前评估器不读取或据此排序。
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 随评估结果透传的层元数据，不参与距离过滤。
        /// </summary>
        public int Layer { get; }

        /// <summary>
        /// 随评估结果透传的所有者标识，不参与权限判断。
        /// </summary>
        public int OwnerId { get; }

        /// <summary>
        /// 随评估结果透传的业务标志，评估器不解释各位含义。
        /// </summary>
        public byte Flags { get; }
    }

    /// <summary>
    /// 描述观察者可见变化，与实体生成、销毁和恢复资格无关。
    /// </summary>
    public enum AoiInterestTransition
    {
        /// <summary>
        /// 无变化的默认枚举值，正常评估不发出该项。
        /// </summary>
        None = 0,
        /// <summary>
        /// 样本进入当前可见集合；不表示生成实体。
        /// </summary>
        Enter = 1,
        /// <summary>
        /// 处理该样本时键已可见，且样本仍满足保留条件。
        /// </summary>
        Stay = 2,
        /// <summary>
        /// 原有键未在本轮保留，已移出此观察者的可见集合；不表示实体被销毁。
        /// </summary>
        Leave = 3
    }

    /// <summary>
    /// 记录一个样本的观察者可见变化，或由历史合成的离开项。
    /// </summary>
    public readonly struct AoiInterestChange
    {
        /// <summary>
        /// 创建不附带来源索引的变化，层、所有者及标志为 0，索引为 -1。
        /// </summary>
        public AoiInterestChange(AoiEntityKey key, AoiInterestTransition transition, float distanceSquared)
            : this(key, transition, distanceSquared, 0, 0, 0, -1)
        {
        }

        /// <summary>
        /// 创建带元数据但不附带来源索引的变化，索引为 -1。
        /// </summary>
        public AoiInterestChange(AoiEntityKey key, AoiInterestTransition transition, float distanceSquared, int layer, int ownerId, byte flags)
            : this(key, transition, distanceSquared, layer, ownerId, flags, -1)
        {
        }

        /// <summary>
        /// 按参数保存变化及来源索引，不校验索引或距离。
        /// </summary>
        public AoiInterestChange(
            AoiEntityKey key,
            AoiInterestTransition transition,
            float distanceSquared,
            int layer,
            int ownerId,
            byte flags,
            int sourceIndex)
        {
            Key = key;
            Transition = transition;
            DistanceSquared = distanceSquared;
            Layer = layer;
            OwnerId = ownerId;
            Flags = flags;
            SourceIndex = sourceIndex;
        }

        /// <summary>
        /// 返回变化所对应的组合键。
        /// </summary>
        public AoiEntityKey Key { get; }

        /// <summary>
        /// 返回进入、保留、离开或默认无变化标记。
        /// </summary>
        public AoiInterestTransition Transition { get; }

        /// <summary>
        /// 返回样本到圆心的距离平方；关闭距离过滤或合成离开项时为 0。
        /// </summary>
        public float DistanceSquared { get; }

        /// <summary>
        /// 返回样本层；合成离开项使用最后保留样本的层。
        /// </summary>
        public int Layer { get; }

        /// <summary>
        /// 返回样本所有者；合成离开项使用最后保留样本的所有者。
        /// </summary>
        public int OwnerId { get; }

        /// <summary>
        /// 返回样本业务标志；合成离开项使用最后保留样本的标志。
        /// </summary>
        public byte Flags { get; }

        /// <summary>
        /// 评估产生的进入、保留项在本次样本列表中的索引；合成离开项为 -1。
        /// </summary>
        public int SourceIndex { get; }

        /// <summary>
        /// 只有进入或保留项返回真。
        /// </summary>
        public bool IsVisible => Transition == AoiInterestTransition.Enter || Transition == AoiInterestTransition.Stay;
    }

    /// <summary>
    /// 一次 AOI 评估的变化及可见计数；保留期限取决于创建入口。
    /// </summary>
    public sealed class AoiInterestEvaluation
    {
        private static readonly IReadOnlyList<AoiInterestChange> EmptyChanges = Array.Empty<AoiInterestChange>();

        /// <summary>
        /// 直接保留变化列表引用，不复制；空引用视作空列表，不验证计数。
        /// </summary>
        public AoiInterestEvaluation(IReadOnlyList<AoiInterestChange> changes, int visibleCount)
        {
            Changes = changes ?? EmptyChanges;
            VisibleCount = visibleCount;
        }

        /// <summary>
        /// 只读接口不代表底层不可变；临时评估会复用并覆盖列表。
        /// </summary>
        public IReadOnlyList<AoiInterestChange> Changes { get; private set; }

        /// <summary>
        /// 可见键的计数，不是变化条目数；临时评估会更新同一个结果对象。
        /// </summary>
        public int VisibleCount { get; private set; }

        internal void Reset(IReadOnlyList<AoiInterestChange> changes, int visibleCount)
        {
            Changes = changes ?? EmptyChanges;
            VisibleCount = visibleCount;
        }

    }

    /// <summary>
    /// 维护单个观察者的可见键和滞回历史，不负责世界状态恢复、实体寿命或同步游标。
    /// 实例应按观察者隔离并串行访问；求值期间不得修改输入列表或重入，失败不保证保留调用前状态。
    /// </summary>
    public sealed class AoiInterestSet
    {
        private readonly Dictionary<AoiEntityKey, VisibleEntry> _visible = new Dictionary<AoiEntityKey, VisibleEntry>();
        private readonly List<AoiInterestChange> _transientChanges = new List<AoiInterestChange>();
        private readonly List<AoiEntityKey> _transientLeaves = new List<AoiEntityKey>();
        private readonly AoiInterestEvaluation _transientEvaluation = new AoiInterestEvaluation(Array.Empty<AoiInterestChange>(), 0);
        private int _evaluationGeneration;

        /// <summary>
        /// 返回当前可见的唯一键数量。
        /// </summary>
        public int VisibleCount => _visible.Count;

        /// <summary>
        /// 查询有效键是否在当前可见集合内，不查询业务世界是否持有实体。
        /// </summary>
        public bool IsVisible(AoiEntityKey key)
        {
            return key.IsValid && _visible.ContainsKey(key);
        }

        /// <summary>
        /// 清空可见集合和评估标记，不合成离开变化；切换观察者或世界前可显式调用。
        /// </summary>
        public void Clear()
        {
            _visible.Clear();
            _evaluationGeneration = 0;
        }

        /// <summary>
        /// 基于本轮完整候选样本更新可见集合，返回后续评估不会覆盖的结果；不得修改结果列表。
        /// 进入和保留按输入顺序，离开随后按键升序；空列表或空引用留下零个可见键。
        /// 强制基线时预先丢弃历史，不为旧键发离开项。重复键不去重，可能在同轮产生多条变化。
        /// </summary>
        public AoiInterestEvaluation Evaluate(IReadOnlyList<AoiEntitySample> samples, AoiInterestScope scope, bool forceFullBaseline = false)
        {
            return EvaluateCore(samples, scope, forceFullBaseline, useTransientBuffers: false);
        }

        /// <summary>
        /// 与常规评估共享可见历史和排序，但复用列表与结果对象。
        /// 必须在下一次临时求值前消费完毕；需要保留时使用常规评估或主动复制。初次和容量增长时可能分配。
        /// </summary>
        public AoiInterestEvaluation EvaluateTransient(IReadOnlyList<AoiEntitySample> samples, AoiInterestScope scope, bool forceFullBaseline = false)
        {
            return EvaluateCore(samples, scope, forceFullBaseline, useTransientBuffers: true);
        }

        private AoiInterestEvaluation EvaluateCore(
            IReadOnlyList<AoiEntitySample> samples,
            AoiInterestScope scope,
            bool forceFullBaseline,
            bool useTransientBuffers)
        {
            if (forceFullBaseline)
            {
                _visible.Clear();
            }

            var generation = NextEvaluationGeneration();

            if (samples == null || samples.Count == 0)
            {
                return RemoveUnseenEntities(generation, useTransientBuffers);
            }

            var changes = useTransientBuffers
                ? PrepareTransientChanges(samples.Count)
                : new List<AoiInterestChange>(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                var sample = samples[i];
                if (!sample.Key.IsValid)
                {
                    continue;
                }

                var distanceSquared = ComputeDistanceSquared(sample.X, sample.Y, scope);
                var wasVisible = _visible.ContainsKey(sample.Key);
                var shouldBeVisible = ShouldBeVisible(wasVisible, distanceSquared, scope);
                if (!shouldBeVisible)
                {
                    continue;
                }

                _visible[sample.Key] = new VisibleEntry(sample, generation);
                if (wasVisible)
                {
                    changes.Add(new AoiInterestChange(sample.Key, AoiInterestTransition.Stay, distanceSquared, sample.Layer, sample.OwnerId, sample.Flags, i));
                    continue;
                }

                changes.Add(new AoiInterestChange(sample.Key, AoiInterestTransition.Enter, distanceSquared, sample.Layer, sample.OwnerId, sample.Flags, i));
            }

            AppendUnseenLeaves(changes, generation, useTransientBuffers);
            return CreateEvaluation(changes, _visible.Count, useTransientBuffers);
        }

        private static bool ShouldBeVisible(bool wasVisible, float distanceSquared, AoiInterestScope scope)
        {
            if (!scope.HasRadius)
            {
                return true;
            }

            var radius = wasVisible ? scope.BoundaryRadius : scope.VisibleRadius;
            return distanceSquared <= radius * radius;
        }

        private static float ComputeDistanceSquared(float x, float y, AoiInterestScope scope)
        {
            if (!scope.HasRadius)
            {
                return 0f;
            }

            var dx = x - scope.CenterX;
            var dy = y - scope.CenterY;
            return dx * dx + dy * dy;
        }

        private AoiInterestEvaluation RemoveUnseenEntities(int generation, bool useTransientBuffers)
        {
            if (_visible.Count == 0)
            {
                var empty = useTransientBuffers
                    ? PrepareTransientChanges(0)
                    : (IReadOnlyList<AoiInterestChange>)Array.Empty<AoiInterestChange>();
                return CreateEvaluation(empty, 0, useTransientBuffers);
            }

            var changes = useTransientBuffers
                ? PrepareTransientChanges(_visible.Count)
                : new List<AoiInterestChange>(_visible.Count);
            AppendUnseenLeaves(changes, generation, useTransientBuffers);
            return CreateEvaluation(changes, _visible.Count, useTransientBuffers);
        }

        private void AppendUnseenLeaves(List<AoiInterestChange> changes, int generation, bool useTransientBuffers)
        {
            if (_visible.Count == 0)
            {
                return;
            }

            var leaves = useTransientBuffers ? _transientLeaves : new List<AoiEntityKey>();
            leaves.Clear();
            foreach (var pair in _visible)
            {
                if (pair.Value.SeenGeneration != generation)
                {
                    leaves.Add(pair.Key);
                }
            }

            leaves.Sort();

            for (int i = 0; i < leaves.Count; i++)
            {
                var key = leaves[i];
                if (_visible.TryGetValue(key, out var entry))
                {
                    _visible.Remove(key);
                    var sample = entry.Sample;
                    changes.Add(new AoiInterestChange(key, AoiInterestTransition.Leave, 0f, sample.Layer, sample.OwnerId, sample.Flags));
                    continue;
                }

                changes.Add(new AoiInterestChange(key, AoiInterestTransition.Leave, 0f));
            }
        }

        private int NextEvaluationGeneration()
        {
            if (_evaluationGeneration == int.MaxValue)
            {
                _evaluationGeneration = 0;
                if (_visible.Count > 0)
                {
                    var keys = new List<AoiEntityKey>(_visible.Keys);
                    for (var i = 0; i < keys.Count; i++)
                    {
                        var key = keys[i];
                        var entry = _visible[key];
                        _visible[key] = new VisibleEntry(entry.Sample, 0);
                    }
                }
            }

            return ++_evaluationGeneration;
        }

        /// <summary>
        /// 保存最后接受的样本及其求值标记，供合成离开和查找本轮未保留的键。
        /// </summary>
        private readonly struct VisibleEntry
        {
            public VisibleEntry(AoiEntitySample sample, int seenGeneration)
            {
                Sample = sample;
                SeenGeneration = seenGeneration;
            }

            public AoiEntitySample Sample { get; }

            public int SeenGeneration { get; }
        }

        private List<AoiInterestChange> PrepareTransientChanges(int capacity)
        {
            _transientChanges.Clear();
            if (_transientChanges.Capacity < capacity)
            {
                _transientChanges.Capacity = capacity;
            }

            return _transientChanges;
        }

        private AoiInterestEvaluation CreateEvaluation(
            IReadOnlyList<AoiInterestChange> changes,
            int visibleCount,
            bool useTransientBuffers)
        {
            if (!useTransientBuffers)
            {
                return new AoiInterestEvaluation(changes, visibleCount);
            }

            _transientEvaluation.Reset(changes, visibleCount);
            return _transientEvaluation;
        }
    }
}
