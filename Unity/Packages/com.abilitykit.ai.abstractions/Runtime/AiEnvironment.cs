using System;

namespace AbilityKit.AI.Abstractions
{
    /// <summary>
    /// 观测元素的逻辑值类型，不改变观测缓冲的浮点存储布局。
    /// </summary>
    public enum AiSpaceValueType
    {
        /// <summary>
        /// 单精度浮点值。
        /// </summary>
        Float32 = 0,
        /// <summary>
        /// 以浮点元素承载的整数值。
        /// </summary>
        Int32 = 1,
        /// <summary>
        /// 以浮点元素承载的布尔值，具体编码由空间定义方约定。
        /// </summary>
        Boolean = 2
    }

    /// <summary>
    /// 动作分量所属的空间类别。
    /// </summary>
    public enum AiActionValueKind
    {
        /// <summary>
        /// 使用浮点缓冲存储的连续动作。
        /// </summary>
        Continuous = 0,
        /// <summary>
        /// 使用整数缓冲存储的离散动作。
        /// </summary>
        Discrete = 1
    }

    /// <summary>
    /// 描述观测空间的标识、元素数量和逻辑类型，不包含具体特征定义。
    /// </summary>
    public readonly struct AiObservationSpec
    {
        /// <summary>
        /// 创建观测规格；空白标识取默认值，负长度按零处理。
        /// </summary>
        public AiObservationSpec(string id, int length, AiSpaceValueType valueType = AiSpaceValueType.Float32)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "observation" : id;
            Length = length < 0 ? 0 : length;
            ValueType = valueType;
        }

        /// <summary>
        /// 由空间定义方约定的标识；经构造时默认值为 observation。
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 观测空间的元素数量。
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// 元素的逻辑类型；不执行类型转换或值校验。
        /// </summary>
        public AiSpaceValueType ValueType { get; }
    }

    /// <summary>
    /// 按规格持有固定长度且可复用的观测缓冲，不提供并发同步。
    /// </summary>
    public sealed class AiObservationBuffer
    {
        private readonly float[] _values;

        /// <summary>
        /// 创建与指定规格等长的零初始化浮点缓冲。
        /// </summary>
        public AiObservationBuffer(AiObservationSpec spec)
        {
            Spec = spec;
            _values = spec.Length == 0 ? Array.Empty<float>() : new float[spec.Length];
        }

        /// <summary>
        /// 用于创建缓冲的观测规格。
        /// </summary>
        public AiObservationSpec Spec { get; }

        /// <summary>
        /// 缓冲中的元素数量。
        /// </summary>
        public int Length => _values.Length;

        /// <summary>
        /// 返回可写底层数组，不复制数据；写入会直接修改缓冲。
        /// </summary>
        public float[] Values => _values;

        /// <summary>
        /// 原地将所有观测元素清零，不改变规格或替换数组。
        /// </summary>
        public void Clear()
        {
            Array.Clear(_values, 0, _values.Length);
        }

        /// <summary>
        /// 按下标读取或写入观测元素，越界时抛出异常。
        /// </summary>
        public float this[int index]
        {
            get => _values[index];
            set => _values[index] = value;
        }
    }

    /// <summary>
    /// 描述连续和离散动作的分量数量，不约束分量范围或离散选项。
    /// </summary>
    public readonly struct AiActionSpec
    {
        /// <summary>
        /// 创建动作规格；空白标识取默认值，负长度按零处理。
        /// </summary>
        public AiActionSpec(string id, int continuousLength, int discreteLength)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "action" : id;
            ContinuousLength = continuousLength < 0 ? 0 : continuousLength;
            DiscreteLength = discreteLength < 0 ? 0 : discreteLength;
        }

        /// <summary>
        /// 由空间定义方约定的标识；经构造时默认值为 action。
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 连续动作分量数量。
        /// </summary>
        public int ContinuousLength { get; }

        /// <summary>
        /// 离散动作分量数量。
        /// </summary>
        public int DiscreteLength { get; }
    }

    /// <summary>
    /// 按规格持有固定长度且可复用的连续、离散动作缓冲，不提供并发同步。
    /// </summary>
    public sealed class AiActionBuffer
    {
        private readonly float[] _continuous;
        private readonly int[] _discrete;

        /// <summary>
        /// 按规格创建零初始化的连续、离散动作数组。
        /// </summary>
        public AiActionBuffer(AiActionSpec spec)
        {
            Spec = spec;
            _continuous = spec.ContinuousLength == 0 ? Array.Empty<float>() : new float[spec.ContinuousLength];
            _discrete = spec.DiscreteLength == 0 ? Array.Empty<int>() : new int[spec.DiscreteLength];
        }

        /// <summary>
        /// 用于创建缓冲的动作规格。
        /// </summary>
        public AiActionSpec Spec { get; }

        /// <summary>
        /// 返回可写的连续动作底层数组，不复制数据。
        /// </summary>
        public float[] Continuous => _continuous;

        /// <summary>
        /// 返回可写的离散动作底层数组，不复制数据。
        /// </summary>
        public int[] Discrete => _discrete;

        /// <summary>
        /// 原地清零两组动作，不改变规格或替换数组。
        /// </summary>
        public void Clear()
        {
            Array.Clear(_continuous, 0, _continuous.Length);
            Array.Clear(_discrete, 0, _discrete.Length);
        }
    }

    /// <summary>
    /// 传给环境的单局运行参数；具体执行与步数限制由环境负责。
    /// </summary>
    public readonly struct AiEpisodeOptions
    {
        /// <summary>
        /// 创建运行参数；最大步数至少为一，步长未大于零时回退到三十分之一秒。
        /// </summary>
        public AiEpisodeOptions(int seed, int maxSteps, float fixedDeltaSeconds = 1f / 30f)
        {
            Seed = seed;
            MaxSteps = maxSteps < 1 ? 1 : maxSteps;
            FixedDeltaSeconds = fixedDeltaSeconds > 0f ? fixedDeltaSeconds : 1f / 30f;
        }

        /// <summary>
        /// 传给环境的随机种子；单凭种子不保证跨实现确定性。
        /// </summary>
        public int Seed { get; }

        /// <summary>
        /// 环境使用的单局最大步数。
        /// </summary>
        public int MaxSteps { get; }

        /// <summary>
        /// 期望的固定步长，单位为秒。
        /// </summary>
        public float FixedDeltaSeconds { get; }
    }

    /// <summary>
    /// 环境重置或推进后的结果；标量随值复制，观测缓冲引用不复制。
    /// </summary>
    public readonly struct AiStepResult
    {
        /// <summary>
        /// 保存非空观测缓冲及结果标量，负步号按零处理。
        /// </summary>
        /// <exception cref="ArgumentNullException">未提供观测缓冲。</exception>
        public AiStepResult(AiObservationBuffer observation, float reward, bool done, bool truncated, int stepIndex, uint stateHash = 0)
        {
            Observation = observation ?? throw new ArgumentNullException(nameof(observation));
            Reward = reward;
            Done = done;
            Truncated = truncated;
            StepIndex = stepIndex < 0 ? 0 : stepIndex;
            StateHash = stateHash;
        }

        /// <summary>
        /// 借用环境返回的观测，不是历史快照；跨环境调用保留数据前应复制。
        /// </summary>
        public AiObservationBuffer Observation { get; }

        /// <summary>
        /// 环境计算的本步奖励；量纲及计算规则由环境定义。
        /// </summary>
        public float Reward { get; }

        /// <summary>
        /// 环境任务是否已到达终止状态。
        /// </summary>
        public bool Done { get; }

        /// <summary>
        /// 是否因步数上限等限制截断，而非任务本身自然终止。
        /// </summary>
        public bool Truncated { get; }

        /// <summary>
        /// 环境报告的非负步号。
        /// </summary>
        public int StepIndex { get; }

        /// <summary>
        /// 环境可选提供的状态摘要，默认值为零；不定义跨实现的哈希格式。
        /// </summary>
        public uint StateHash { get; }
    }

    /// <summary>
    /// 将观测转换为动作的模型无关同步策略接口。
    /// </summary>
    public interface IAiPolicy
    {
        /// <summary>
        /// 策略写入的动作规格，应与消费者的动作空间一致。
        /// </summary>
        AiActionSpec ActionSpec { get; }

        /// <summary>
        /// 在调用期间只读借用观测，并写入调用方提供的动作缓冲。
        /// 实现不应在返回后持有任一缓冲；接口不自动清零或校验规格。
        /// </summary>
        void Decide(in AiObservationBuffer observation, AiActionBuffer action);
    }

    /// <summary>
    /// 可选的策略自有状态快照，用于有状态策略和支持回滚的运行时。
    /// 不包含环境状态，普通无状态推理无需实现。
    /// </summary>
    public interface IAiPolicyRuntimeSnapshot
    {
        /// <summary>
        /// 标识策略快照类型，供宿主判定负载是否可交给此实现。
        /// </summary>
        string SnapshotType { get; }

        /// <summary>
        /// 捕获供宿主保存的策略状态字节，格式由策略实现定义。
        /// </summary>
        byte[] CaptureSnapshot();

        /// <summary>
        /// 从策略支持的负载恢复自有状态，不推进环境。
        /// </summary>
        void RestoreSnapshot(byte[] payload);
    }

    /// <summary>
    /// 可选有状态策略的只读快照验证；仅接入回滚时要求，不影响普通推理。
    /// </summary>
    public interface IAiPolicySnapshotValidator : IAiPolicyRuntimeSnapshot
    {
        /// <summary>
        /// 验证完整快照和配置兼容性，不调用恢复或推理。
        /// </summary>
        void ValidateSnapshot(byte[] payload);
    }

    /// <summary>
    /// 显式声明策略没有会影响后续推理的自有跨帧状态。
    /// 包装委托或模型执行器时也必须保证其无隐藏状态。
    /// </summary>
    public interface IAiStatelessPolicy : IAiPolicy
    {
    }

    /// <summary>
    /// 暴露单局环境的重置和按步执行能力，不指定模型、训练器或玩法规则。
    /// 调用方先重置，再按序推进；结束或截断后重置再开新局。
    /// </summary>
    public interface IAiEnvironment
    {
        /// <summary>
        /// 环境返回的观测空间规格。
        /// </summary>
        AiObservationSpec ObservationSpec { get; }

        /// <summary>
        /// 环境消费的动作空间规格。
        /// </summary>
        AiActionSpec ActionSpec { get; }

        /// <summary>
        /// 按给定选项开始新局并返回初始观测；实现可复用上次返回的缓冲。
        /// </summary>
        AiStepResult Reset(in AiEpisodeOptions options);

        /// <summary>
        /// 同步读取调用方的动作并推进一步；动作所有权不转移，返回的观测可复用。
        /// </summary>
        AiStepResult Step(in AiActionBuffer action);
    }
}
