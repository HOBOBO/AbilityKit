using System;

namespace AbilityKit.Ability.FrameSync.Rollback
{
    public enum RollbackOperationKind
    {
        Capture = 0,
        Store = 1,
        Restore = 2,
        Clear = 3
    }

    public enum RollbackOperationStatus
    {
        Succeeded = 0,
        SnapshotNotFound = 1,
        UnsupportedVersion = 2,
        ProviderMissing = 3,
        ProviderFailed = 4,
        Failed = 5,
        /// <summary>
        /// 请求早于已生效的最早可恢复帧，未进入 Provider 预检或导入。
        /// </summary>
        BeforeRestoreFloor = 6
    }

    public readonly struct RollbackOperationResult
    {
        public readonly RollbackOperationKind Kind;
        public readonly RollbackOperationStatus Status;
        public readonly FrameIndex Frame;
        public readonly int ProviderKey;
        public readonly int ProviderCount;
        public readonly int PayloadBytes;
        public readonly string Message;
        public readonly Exception Exception;

        public RollbackOperationResult(
            RollbackOperationKind kind,
            RollbackOperationStatus status,
            FrameIndex frame,
            int providerKey = 0,
            int providerCount = 0,
            int payloadBytes = 0,
            string message = null,
            Exception exception = null)
        {
            Kind = kind;
            Status = status;
            Frame = frame;
            ProviderKey = providerKey;
            ProviderCount = providerCount;
            PayloadBytes = payloadBytes;
            Message = message;
            Exception = exception;
        }

        public bool IsSuccess => Status == RollbackOperationStatus.Succeeded;

        public static RollbackOperationResult Success(
            RollbackOperationKind kind,
            FrameIndex frame,
            int providerCount = 0,
            int payloadBytes = 0)
        {
            return new RollbackOperationResult(kind, RollbackOperationStatus.Succeeded, frame, providerCount: providerCount, payloadBytes: payloadBytes);
        }

        public static RollbackOperationResult Failure(
            RollbackOperationKind kind,
            RollbackOperationStatus status,
            FrameIndex frame,
            string message,
            int providerKey = 0,
            Exception exception = null)
        {
            return new RollbackOperationResult(kind, status, frame, providerKey: providerKey, message: message, exception: exception);
        }

        public override string ToString()
        {
            return $"RollbackOperationResult(kind={Kind}, status={Status}, frame={Frame.Value}, providerKey={ProviderKey}, providerCount={ProviderCount}, payloadBytes={PayloadBytes}, message={Message})";
        }
    }
}
