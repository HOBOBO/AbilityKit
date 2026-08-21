#nullable enable

using System;
using AbilityKit.Demo.Shooter.Runtime;

namespace AbilityKit.Demo.Shooter.View
{
    internal sealed class ShooterClientSnapshotApplyCoordinator
    {
        private readonly IShooterBattleRuntimePort _runtime;
        private readonly ShooterGatewaySnapshotDecoder _gatewaySnapshotDecoder;
        private readonly ShooterFrameworkSnapshotPipeline _frameworkSnapshotPipeline;

        public ShooterClientSnapshotApplyCoordinator(
            IShooterBattleRuntimePort runtime,
            ShooterPresentationFacade presentation,
            ShooterGatewaySnapshotDecoder? decoder)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));

            _gatewaySnapshotDecoder = decoder ?? new ShooterGatewaySnapshotDecoder();
            _frameworkSnapshotPipeline = new ShooterFrameworkSnapshotPipeline(_runtime, presentation);
        }

        public ShooterFrameworkSnapshotPipelineDiagnostics Diagnostics => _frameworkSnapshotPipeline.Diagnostics;

        public ShooterClientSnapshotApplyOutcome ApplyGatewayPush(uint opCode, ArraySegment<byte> payload)
        {
            if (!_gatewaySnapshotDecoder.IsSnapshotPush(opCode))
            {
                return ShooterClientSnapshotApplyOutcome.Ignored;
            }

            var snapshot = _gatewaySnapshotDecoder.Decode(payload);
            var applyResult = _frameworkSnapshotPipeline.ApplyGatewaySnapshot(in snapshot);
            if (applyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot)
            {
                return new ShooterClientSnapshotApplyOutcome(
                    true,
                    applyResult,
                    0,
                    0u,
                    0u,
                    0u);
            }

            return new ShooterClientSnapshotApplyOutcome(
                true,
                applyResult,
                _frameworkSnapshotPipeline.LastAppliedFrame,
                _frameworkSnapshotPipeline.LastAppliedStateHash,
                _runtime.ComputeStateHash(),
                _frameworkSnapshotPipeline.LastAppliedSnapshotFlags);
        }

        public ShooterClientSnapshotApplyOutcome ApplyGatewaySnapshot(in ShooterGatewaySnapshot snapshot)
        {
            var applyResult = _frameworkSnapshotPipeline.ApplyGatewaySnapshot(in snapshot);
            if (applyResult != ShooterSnapshotApplyResult.AppliedPackedSnapshot)
            {
                return new ShooterClientSnapshotApplyOutcome(
                    true,
                    applyResult,
                    0,
                    0u,
                    0u,
                    0u);
            }

            return new ShooterClientSnapshotApplyOutcome(
                true,
                applyResult,
                _frameworkSnapshotPipeline.LastAppliedFrame,
                _frameworkSnapshotPipeline.LastAppliedStateHash,
                _runtime.ComputeStateHash(),
                _frameworkSnapshotPipeline.LastAppliedSnapshotFlags);
        }
    }

    internal readonly struct ShooterClientSnapshotApplyOutcome
    {
        public static readonly ShooterClientSnapshotApplyOutcome Ignored = new ShooterClientSnapshotApplyOutcome(
            false,
            ShooterSnapshotApplyResult.Ignored,
            0,
            0u,
            0u,
            0u);

        public ShooterClientSnapshotApplyOutcome(
            bool isSnapshotPush,
            ShooterSnapshotApplyResult applyResult,
            int authoritativeFrame,
            uint authoritativeStateHash,
            uint importedStateHash,
            uint snapshotFlags)
        {
            IsSnapshotPush = isSnapshotPush;
            ApplyResult = applyResult;
            AuthoritativeFrame = authoritativeFrame;
            AuthoritativeStateHash = authoritativeStateHash;
            ImportedStateHash = importedStateHash;
            SnapshotFlags = snapshotFlags;
        }

        public bool IsSnapshotPush { get; }

        public ShooterSnapshotApplyResult ApplyResult { get; }

        public int AuthoritativeFrame { get; }

        public uint AuthoritativeStateHash { get; }

        public uint ImportedStateHash { get; }

        public uint SnapshotFlags { get; }
    }
}
