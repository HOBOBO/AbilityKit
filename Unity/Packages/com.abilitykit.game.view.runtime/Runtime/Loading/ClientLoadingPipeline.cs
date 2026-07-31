#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.View.Loading
{
    public readonly struct ClientLoadingProgress
    {
        public ClientLoadingProgress(string stageId, int overallProgress, float stageProgress)
        {
            StageId = stageId ?? string.Empty;
            OverallProgress = Math.Max(0, Math.Min(100, overallProgress));
            StageProgress = Math.Max(0f, Math.Min(1f, stageProgress));
        }

        public string StageId { get; }
        public int OverallProgress { get; }
        public float StageProgress { get; }
    }

    public sealed class ClientLoadingStepDefinition
    {
        public ClientLoadingStepDefinition(
            string id,
            string typeId,
            int weight,
            int parallelGroup = 0,
            bool required = true,
            IReadOnlyDictionary<string, string>? parameters = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Loading step id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Loading step type is required.", nameof(typeId));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));

            Id = id;
            TypeId = typeId;
            Weight = weight;
            ParallelGroup = Math.Max(0, parallelGroup);
            Required = required;
            Parameters = parameters ?? EmptyParameters;
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
            new Dictionary<string, string>(0);

        public string Id { get; }
        public string TypeId { get; }
        public int Weight { get; }
        public int ParallelGroup { get; }
        public bool Required { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }
    }

    public sealed class ClientLoadingPipelineDefinition
    {
        public ClientLoadingPipelineDefinition(IReadOnlyList<ClientLoadingStepDefinition> steps)
        {
            Steps = steps ?? throw new ArgumentNullException(nameof(steps));
            if (steps.Count == 0) throw new ArgumentException("A loading pipeline requires at least one step.", nameof(steps));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i] ?? throw new ArgumentException("Loading pipeline contains a null step.", nameof(steps));
                if (!ids.Add(step.Id)) throw new ArgumentException($"Duplicate loading step id '{step.Id}'.", nameof(steps));
            }
        }

        public IReadOnlyList<ClientLoadingStepDefinition> Steps { get; }
    }

    public interface IClientLoadingStep
    {
        Task ExecuteAsync(IProgress<float> progress, CancellationToken cancellationToken);
    }

    public interface IClientLoadingStepResolver
    {
        IClientLoadingStep Resolve(ClientLoadingStepDefinition definition);
    }

    public sealed class ClientLoadingStepRegistry : IClientLoadingStepResolver
    {
        private readonly Dictionary<string, Func<ClientLoadingStepDefinition, IClientLoadingStep>> _factories =
            new Dictionary<string, Func<ClientLoadingStepDefinition, IClientLoadingStep>>(StringComparer.Ordinal);

        public ClientLoadingStepRegistry Register(
            string typeId,
            Func<ClientLoadingStepDefinition, IClientLoadingStep> factory)
        {
            if (string.IsNullOrWhiteSpace(typeId)) throw new ArgumentException("Loading step type is required.", nameof(typeId));
            _factories[typeId] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public IClientLoadingStep Resolve(ClientLoadingStepDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!_factories.TryGetValue(definition.TypeId, out var factory))
            {
                throw new InvalidOperationException($"No client loading step factory is registered for '{definition.TypeId}'.");
            }

            return factory(definition)
                ?? throw new InvalidOperationException($"Loading step factory '{definition.TypeId}' returned null.");
        }
    }

    public sealed class DelegateClientLoadingStep : IClientLoadingStep
    {
        private readonly Func<IProgress<float>, CancellationToken, Task> _execute;

        public DelegateClientLoadingStep(Func<IProgress<float>, CancellationToken, Task> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public Task ExecuteAsync(IProgress<float> progress, CancellationToken cancellationToken) =>
            _execute(progress, cancellationToken);
    }

    public sealed class ClientLoadingPipeline
    {
        private readonly ClientLoadingPipelineDefinition _definition;
        private readonly IClientLoadingStepResolver _resolver;

        public ClientLoadingPipeline(
            ClientLoadingPipelineDefinition definition,
            IClientLoadingStepResolver resolver)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        public async Task ExecuteAsync(
            IProgress<ClientLoadingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var totalWeight = 0;
            for (var i = 0; i < _definition.Steps.Count; i++) totalWeight += _definition.Steps[i].Weight;

            var completedWeight = 0;
            var index = 0;
            while (index < _definition.Steps.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var first = _definition.Steps[index];
                var count = 1;
                if (first.ParallelGroup > 0)
                {
                    while (index + count < _definition.Steps.Count &&
                           _definition.Steps[index + count].ParallelGroup == first.ParallelGroup)
                    {
                        count++;
                    }
                }

                await ExecuteGroupAsync(
                    index,
                    count,
                    completedWeight,
                    totalWeight,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                for (var offset = 0; offset < count; offset++)
                {
                    completedWeight += _definition.Steps[index + offset].Weight;
                }

                var completedStage = _definition.Steps[index + count - 1].Id;
                progress?.Report(new ClientLoadingProgress(
                    completedStage,
                    ToPercent(completedWeight, totalWeight),
                    1f));
                index += count;
            }
        }

        private async Task ExecuteGroupAsync(
            int start,
            int count,
            int completedWeight,
            int totalWeight,
            IProgress<ClientLoadingProgress>? progress,
            CancellationToken cancellationToken)
        {
            var stageProgress = new float[count];
            var gate = new object();
            var tasks = new Task[count];
            for (var offset = 0; offset < count; offset++)
            {
                var localOffset = offset;
                var definition = _definition.Steps[start + offset];
                var step = _resolver.Resolve(definition);
                var stepProgress = new ImmediateProgress<float>(value =>
                {
                    var clamped = Math.Max(0f, Math.Min(1f, value));
                    int overall;
                    lock (gate)
                    {
                        if (clamped < stageProgress[localOffset]) return;
                        stageProgress[localOffset] = clamped;
                        var activeWeight = 0f;
                        for (var i = 0; i < count; i++)
                        {
                            activeWeight += _definition.Steps[start + i].Weight * stageProgress[i];
                        }

                        overall = ToPercent(completedWeight + activeWeight, totalWeight);
                    }

                    progress?.Report(new ClientLoadingProgress(definition.Id, overall, clamped));
                });
                tasks[offset] = ExecuteStepAsync(definition, step, stepProgress, cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static async Task ExecuteStepAsync(
            ClientLoadingStepDefinition definition,
            IClientLoadingStep step,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            try
            {
                progress.Report(0f);
                await step.ExecuteAsync(progress, cancellationToken).ConfigureAwait(false);
                progress.Report(1f);
            }
            catch when (!definition.Required && !cancellationToken.IsCancellationRequested)
            {
                progress.Report(1f);
            }
        }

        private static int ToPercent(float weight, int totalWeight) =>
            totalWeight <= 0
                ? 0
                : Math.Max(0, Math.Min(100, (int)Math.Round(weight * 100f / totalWeight)));

        private sealed class ImmediateProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public ImmediateProgress(Action<T> report)
            {
                _report = report;
            }

            public void Report(T value) => _report(value);
        }
    }

    public sealed class ClientLoadingProgressUploadOptions
    {
        public TimeSpan SampleInterval { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MaxSilence { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(150);
        public int MinimumProgressDelta { get; set; } = 2;
        public int MaxFinalAttempts { get; set; } = 4;
    }

    public sealed class ClientLoadingProgressRelay : IProgress<ClientLoadingProgress>
    {
        private readonly object _gate = new object();
        private int _latestProgress;
        private string _latestStageId = string.Empty;
        private bool _completed;

        public int LatestProgress
        {
            get { lock (_gate) return _latestProgress; }
        }

        public string LatestStageId
        {
            get { lock (_gate) return _latestStageId; }
        }

        public void Report(ClientLoadingProgress value)
        {
            lock (_gate)
            {
                if (value.OverallProgress < _latestProgress) return;
                _latestProgress = value.OverallProgress;
                _latestStageId = value.StageId;
            }
        }

        public void Complete(string stageId = "complete")
        {
            lock (_gate)
            {
                _latestProgress = 100;
                _latestStageId = stageId ?? string.Empty;
                _completed = true;
            }
        }

        public async Task UploadUntilCompletedAsync(
            Func<int, CancellationToken, Task> upload,
            ClientLoadingProgressUploadOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (upload == null) throw new ArgumentNullException(nameof(upload));
            options ??= new ClientLoadingProgressUploadOptions();
            var minimumDelta = Math.Max(1, options.MinimumProgressDelta);
            var sampleInterval = options.SampleInterval > TimeSpan.Zero
                ? options.SampleInterval
                : TimeSpan.FromMilliseconds(100);
            var maxSilence = options.MaxSilence > TimeSpan.Zero
                ? options.MaxSilence
                : TimeSpan.FromSeconds(1);
            var lastReported = 0;
            var lastSuccessAt = DateTime.UtcNow;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int latest;
                bool completed;
                lock (_gate)
                {
                    latest = _latestProgress;
                    completed = _completed;
                }

                var final = completed && latest >= 100;
                var due = latest > lastReported &&
                          (final || latest - lastReported >= minimumDelta || DateTime.UtcNow - lastSuccessAt >= maxSilence);
                if (due)
                {
                    var uploaded = await TryUploadAsync(
                        upload,
                        latest,
                        final ? Math.Max(1, options.MaxFinalAttempts) : 1,
                        options.RetryDelay,
                        cancellationToken).ConfigureAwait(false);
                    if (uploaded)
                    {
                        lastReported = latest;
                        lastSuccessAt = DateTime.UtcNow;
                    }
                    else if (final)
                    {
                        throw new InvalidOperationException("Final loading progress could not be uploaded.");
                    }
                }

                if (final && lastReported >= 100) return;
                await Task.Delay(sampleInterval, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<bool> TryUploadAsync(
            Func<int, CancellationToken, Task> upload,
            int progress,
            int attempts,
            TimeSpan retryDelay,
            CancellationToken cancellationToken)
        {
            Exception? lastError = null;
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    await upload(progress, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt + 1 < attempts)
                    {
                        await Task.Delay(retryDelay > TimeSpan.Zero ? retryDelay : TimeSpan.FromMilliseconds(100), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }

            _ = lastError;
            return false;
        }
    }
}
