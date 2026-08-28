#nullable enable

using System;
using AbilityKit.Ability.Host.Extensions.Client.StateSync;
using AbilityKit.Demo.Shooter.View.Hosting;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.View.PlayMode
{
    internal sealed class ShooterRemoteInputSubmitStrategy
    {
        private readonly RemoteClientInputSubmitQueue<ShooterClientInputSubmitResult, ShooterClientGatewayInputSubmitResult> _queue;

        private ShooterRemoteInputSubmitStrategy(RemoteClientInputSubmitQueue<ShooterClientInputSubmitResult, ShooterClientGatewayInputSubmitResult> queue)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        }

        public ShooterClientGatewayInputSubmitResult LastResult => _queue.LastResult;
        public Exception? LastError => _queue.LastError;
        public bool HasPending => _queue.HasPending;
        public bool HasQueued => _queue.HasQueued;
        public long SubmittedCount => _queue.SubmittedCount;
        public long QueuedCount => _queue.QueuedCount;
        public long ReplacedCount => _queue.ReplacedCount;
        public long CompletedCount => _queue.CompletedCount;
        public long FailedCount => _queue.FailedCount;
        public long ResyncRequestedCount => _queue.ResyncRequestedCount;

        public static ShooterRemoteInputSubmitStrategy Create(ShooterClientBattleHandle battle, TimeSpan timeout)
        {
            if (battle == null) throw new ArgumentNullException(nameof(battle));

            return new ShooterRemoteInputSubmitStrategy(
                new RemoteClientInputSubmitQueue<ShooterClientInputSubmitResult, ShooterClientGatewayInputSubmitResult>(
                    (local, requestTimeout) => battle.SubmitAcceptedInputToGatewayAsync(local, requestTimeout),
                    timeout,
                    result => result.Remote.ShouldResync,
                    MergeQueuedInput,
                    // 30Hz 提交 × RTT/抖动窗口：4 个在途槽在 ~130ms RTT 下即饱和，
                    // 饱和后的"替换合并"会丢弃排队输入的移动分量——本地已预测、服务端
                    // 永远收不到，制造停止后回拉的漂移。32 槽给出 >1 秒的抖动余量，
                    // 使替换在正常运行中不再发生。
                    maxInFlight: 32));
        }

        internal static ShooterClientInputSubmitResult MergeQueuedInput(
            ShooterClientInputSubmitResult queued,
            ShooterClientInputSubmitResult latest)
        {
            if (!queued.Packet.Command.Fire || latest.Packet.Command.Fire)
            {
                return latest;
            }

            var command = latest.Packet.Command;
            command.Fire = true;
            command.AttackSlot = queued.Packet.Command.AttackSlot;
            var payload = ShooterInputCodec.Serialize(new[] { command });
            var packet = new ShooterInputPacket(latest.Packet.OpCode, payload, in command);
            return new ShooterClientInputSubmitResult(
                latest.AcceptedInputs,
                latest.RequestedFrame,
                in packet,
                latest.SubmissionId);
        }

        public void SubmitOrQueue(in ShooterClientInputSubmitResult local)
        {
            _queue.SubmitOrQueue(local);
        }

        public void CompleteIfFinished()
        {
            _queue.CompleteIfFinished();
        }

        public void Reset()
        {
            _queue.Reset();
        }

        public ShooterRemoteLatencyCompensationDiagnostics CreateLatencyDiagnostics()
        {
            return ShooterRemoteLatencyCompensationDiagnostics.FromGatewayInput(
                _queue.LastResult,
                _queue.HasPending,
                _queue.HasQueued,
                _queue.SubmittedCount,
                _queue.QueuedCount,
                _queue.ReplacedCount,
                _queue.CompletedCount,
                _queue.FailedCount,
                _queue.ResyncRequestedCount);
        }
    }
}
