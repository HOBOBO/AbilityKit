using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.Host.Extensions.Moba.StartSources;
using AbilityKit.Demo.Moba.Services.LogicWorld;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaBattleMainFlowHealthValidator : IMobaRuntimeValidator
    {
        private const string Source = "battle.main_flow";
        private const string MetricReady = "moba.main_flow.ready";
        private const string MetricRuntimeReady = "moba.main_flow.runtime.ready";
        private const string MetricInputReady = "moba.main_flow.input.ready";
        private const string MetricExecuteReady = "moba.main_flow.execute.ready";
        private const string MetricOutputReady = "moba.main_flow.output.ready";

        public string Name => Source;

        public void Validate(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (report == null) return;

            context.TryResolve<IMobaBattleDiagnosticsService>(out var diagnostics);

            var runtimeReady = ValidateRuntimePort(in context, report);
            var startPlanReady = ValidateGameStartSpec(in context, report);
            var inputReady = ValidateInput(in context, report);
            var executeReady = ValidateExecution(in context, report);
            var outputReady = ValidateOutput(in context, report);
            var ready = runtimeReady && startPlanReady && inputReady && executeReady && outputReady;

            diagnostics?.Gauge(MetricRuntimeReady, runtimeReady ? 1 : 0);
            diagnostics?.Gauge(MetricInputReady, inputReady ? 1 : 0);
            diagnostics?.Gauge(MetricExecuteReady, executeReady ? 1 : 0);
            diagnostics?.Gauge(MetricOutputReady, outputReady ? 1 : 0);
            diagnostics?.Gauge(MetricReady, ready ? 1 : 0);
        }

        private static bool ValidateRuntimePort(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (!context.TryResolve<IMobaBattleRuntimePort>(out var runtime) || runtime == null)
            {
                report.Error(Source, "world.runtime.port", "IMobaBattleRuntimePort is required as the single formal entry for battle startup, input submission and snapshot output.", nameof(IMobaBattleRuntimePort), blocksStartup: true);
                return false;
            }

            var status = runtime.Status;
            var ready = true;
            ready &= RequireCapability(report, status, MobaBattleRuntimeCapability.GameStart, "world.runtime.game_start", "Battle runtime port must expose game start capability before the formal world flow can start.");
            ready &= RequireCapability(report, status, MobaBattleRuntimeCapability.Input, "input.runtime.port", "Battle runtime port must expose input capability for external command submission.");
            ready &= RequireCapability(report, status, MobaBattleRuntimeCapability.SnapshotOutput, "output.runtime.port", "Battle runtime port must expose snapshot output capability for view synchronization.");
            ready &= RequireCapability(report, status, MobaBattleRuntimeCapability.StateReadModel, "output.runtime.state_read", "Battle runtime port must expose state read model capability for formal diagnostics.");
            return ready;
        }

        private static bool ValidateGameStartSpec(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (!context.TryResolve<IMobaPendingGameStartSpecStore>(out var starts) || starts == null)
            {
                report.Error(Source, "world.start_spec.service", "IMobaPendingGameStartSpecStore is required to retain the validated battle start plan before the formal world flow starts.", nameof(IMobaPendingGameStartSpecStore), blocksStartup: true);
                return false;
            }

            var specValidation = starts.ValidatePendingSpec();
            if (!specValidation.Succeeded)
            {
                report.Error(Source, "world.start_spec.pending", specValidation.Message, nameof(IMobaPendingGameStartSpecStore), blocksStartup: true);
                return false;
            }

            var planValidation = starts.ValidatePendingPlan();
            if (planValidation.Succeeded) return true;

            report.Error(Source, "world.start_plan.pending", planValidation.Message, nameof(IMobaPendingGameStartSpecStore), blocksStartup: true);
            return false;
        }

        private static bool ValidateInput(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            var ready = true;
            ready &= Require<IMobaBattleInputPort>(in context, report, "input.port", "IMobaBattleInputPort is required to accept external player command batches.");
            ready &= Require<IMobaInputCoordinator>(in context, report, "input.coordinator", "IMobaInputCoordinator is required to validate frame ownership and dispatch player commands.");
            ready &= ValidateInputCommandContracts(in context, report);
            return ready;
        }

        private static bool ValidateExecution(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            var ready = true;
            // Preparation and policy resolution are private collaborators constructed by
            // SkillCastCoordinator; validating them as container services would require
            // duplicate instances that are not part of the actual cast execution path.
            ready &= Require<SkillCastCoordinator>(in context, report, "execute.skill.executor", "SkillCastCoordinator is required to execute command driven skill casts.");
            ready &= Require<MobaEffectExecutionService>(in context, report, "execute.effect.service", "MobaEffectExecutionService is required to execute configured skill effects and trigger plans.");
            ready &= Require<MobaEffectInvokerService>(in context, report, "execute.effect.invoker", "MobaEffectInvokerService is required as the formal bridge from skill phases and buff stages into effect execution.");
            return ready;
        }

        private static bool ValidateOutput(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            var ready = true;
            ready &= Require<IMobaBattleOutputPort>(in context, report, "output.port", "IMobaBattleOutputPort is required to expose battle snapshots.");
            ready &= Require<IWorldStateSnapshotProvider>(in context, report, "output.snapshot.provider", "IWorldStateSnapshotProvider is required for snapshot retrieval by frame.");
            ready &= Require<IMobaSnapshotHealthProvider>(in context, report, "output.snapshot.health", "IMobaSnapshotHealthProvider is required to verify snapshot emitter readiness.");

            if (context.TryResolve<IMobaSnapshotHealthProvider>(out var healthProvider) && healthProvider != null)
            {
                var health = healthProvider.GetHealth();
                var contract = MobaSnapshotOutputContract.CreateDefault();
                var validation = contract.Validate(in health);
                if (!validation.Succeeded)
                {
                    for (int i = 0; i < validation.Errors.Count; i++)
                    {
                        report.Error(Source, "output.snapshot.contract." + i, validation.Errors[i], nameof(MobaSnapshotOutputContract), blocksStartup: true);
                    }

                    ready = false;
                }
            }

            return ready;
        }

        private static bool ValidateInputCommandContracts(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report)
        {
            if (!context.TryResolve<MobaInputCommandContractRegistry>(out var contracts) || contracts == null)
            {
                report.Error(Source, "input.command_contract.registry", "MobaInputCommandContractRegistry is required to declare the formal external input command surface.", nameof(MobaInputCommandContractRegistry), blocksStartup: true);
                return false;
            }

            var result = contracts.Validate();
            if (result.Succeeded) return true;

            for (int i = 0; i < result.Errors.Count; i++)
            {
                report.Error(Source, "input.command_contract." + i, result.Errors[i], nameof(MobaInputCommandContractRegistry), blocksStartup: true);
            }

            return false;
        }

        private static bool Require<T>(in MobaRuntimeValidationContext context, MobaRuntimeValidationReport report, string path, string message) where T : class
        {
            if (context.TryResolve<T>(out var service) && service != null)
            {
                return true;
            }

            report.Error(Source, path, message, typeof(T).Name, blocksStartup: true);
            return false;
        }

        private static bool RequireCapability(MobaRuntimeValidationReport report, MobaBattleRuntimeStatus status, MobaBattleRuntimeCapability capability, string path, string message)
        {
            if (status.Has(capability)) return true;

            report.Error(Source, path, message + " " + status, capability.ToString(), blocksStartup: true);
            return false;
        }
    }
}

