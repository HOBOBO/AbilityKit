using System;

namespace AbilityKit.Ability.StateSync.Hash
{
    /// <summary>
    /// 哈希比较报告的诊断分类，不诊断具体玩法根因。
    /// </summary>
    public enum DesyncType
    {
        /// <summary>
        /// 没有报告不一致。
        /// </summary>
        None = 0,
        /// <summary>
        /// 检测到哈希不一致，但没有确定原因。
        /// </summary>
        Unknown = 99
    }

    /// <summary>
    /// 一次比较的诊断结果，不授予状态导入或恢复资格。
    /// </summary>
    public readonly struct ValidationResult
    {
        /// <summary>
        /// 表示比较通过或缺样本时跳过，不证明双方均有有效状态。
        /// </summary>
        public bool IsValid { get; }
        /// <summary>
        /// 调用方传入的报告帧，不验证它与参与比较的快照帧相同。
        /// </summary>
        public int Frame { get; }
        /// <summary>
        /// 失败时可保留客户端哈希；通过工厂生成的成功结果不保留该值。
        /// </summary>
        public StateHash ClientHash { get; }
        /// <summary>
        /// 失败时可保留服务端哈希；通过工厂生成的成功结果不保留该值。
        /// </summary>
        public StateHash ServerHash { get; }
        /// <summary>
        /// 返回比较填入的诊断类别。
        /// </summary>
        public DesyncType DesyncKind { get; }
        /// <summary>
        /// 构造时取当前 UTC Unix 毫秒，用于诊断，不是模拟时间。
        /// </summary>
        public long Timestamp { get; }

        /// <summary>
        /// 保存调用方给定的诊断数据并记录当前时间，不校验字段间一致性。
        /// </summary>
        public ValidationResult(
            bool isValid,
            int frame,
            StateHash clientHash,
            StateHash serverHash,
            DesyncType desyncKind)
        {
            IsValid = isValid;
            Frame = frame;
            ClientHash = clientHash;
            ServerHash = serverHash;
            DesyncKind = desyncKind;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// 创建通过结果，两个哈希均置为无效哨兵，类别为无不一致。
        /// </summary>
        public static ValidationResult Valid(int frame) => new ValidationResult(true, frame, StateHash.Invalid, StateHash.Invalid, DesyncType.None);
        /// <summary>
        /// 按指定帧、双方哈希和类别创建失败报告。
        /// </summary>
        public static ValidationResult Invalid(int frame, StateHash client, StateHash server, DesyncType kind) =>
            new ValidationResult(false, frame, client, server, kind);

        /// <summary>
        /// 生成诊断文本；通过时含帧，失败时还含类别与双方哈希。
        /// </summary>
        public override string ToString() => IsValid
            ? $"Valid(frame={Frame})"
            : $"Desync(frame={Frame}, kind={DesyncKind}, client={ClientHash}, server={ServerHash})";
    }

    /// <summary>
    /// 比较快照框架元数据或预先计算的哈希，仅作诊断，不验证基线、权威性或业务世界完整性。
    /// </summary>
    public sealed class StateHashValidator
    {
        /// <summary>
        /// 仅快照重载检测到不匹配时同步调用；成功、缺样本或直接比较哈希均不调用。
        /// 订阅方异常向调用方传播；配置和调用由宿主协调。
        /// </summary>
        public Action<ValidationResult> OnValidationResult;

        /// <summary>
        /// 任一快照为空时跳过检查并返回通过；否则按各自的框架元数据哈希比较，不校验参数帧与快照帧是否相等。
        /// 不匹配时同步回调后返回失败，回调抛异常时不会返回结果。
        /// </summary>
        public ValidationResult Validate(int frame, Snapshot.WorldStateSnapshot clientState, Snapshot.WorldStateSnapshot serverState)
        {
            if (clientState == null || serverState == null)
                return ValidationResult.Valid(frame);

            var clientHash = clientState.ComputeHash();
            var serverHash = serverState.ComputeHash();

            if (clientHash == serverHash)
            {
                return ValidationResult.Valid(frame);
            }

            var result = ValidationResult.Invalid(frame, clientHash, serverHash, DesyncType.Unknown);

            OnValidationResult?.Invoke(result);

            return result;
        }

        /// <summary>
        /// 直接按数值比较两个哈希；两个无效哨兵也判相等。
        /// 失败仅返回报告，不触发回调；需要拒绝未知状态时应由调用方先校验哈希有效性。
        /// </summary>
        public ValidationResult Validate(int frame, StateHash clientHash, StateHash serverHash)
        {
            if (clientHash == serverHash)
            {
                return ValidationResult.Valid(frame);
            }

            return ValidationResult.Invalid(frame, clientHash, serverHash, DesyncType.Unknown);
        }
    }
}
