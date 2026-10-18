using System;

namespace AbilityKit.Game.Flow
{
    public enum SessionSnapshotRoutingState
    {
        Idle = 0,
        Building = 1,
        Built = 2,
        Disposing = 3,
        DisposeFailed = 4,
    }

    public interface ISessionSnapshotRoutingPort<TBuildContext, TFrame>
    {
        void Create(TBuildContext context);

        void Publish();

        void Bind();

        void Subscribe(Action<TFrame> frameReceivedHandler);

        void Unsubscribe(Action<TFrame> frameReceivedHandler);

        void Unbind();

        void Unpublish();

        void Release();

        void Feed(TFrame frame);
    }

    public sealed class SessionSnapshotRoutingCleanupException : Exception
    {
        public SessionSnapshotRoutingCleanupException(
            string step,
            Exception innerException)
            : base(
                $"Snapshot routing cleanup step failed: {step}.",
                innerException)
        {
            Step = step ?? throw new ArgumentNullException(nameof(step));
        }

        public string Step { get; }
    }

    public sealed class SessionSnapshotRoutingController<TBuildContext, TFrame> :
        IDisposable
    {
        private readonly ISessionSnapshotRoutingPort<TBuildContext, TFrame> _port;
        private Action<TFrame> _frameReceivedHandler;
        private bool _created;
        private bool _published;
        private bool _bound;
        private bool _subscribed;

        public SessionSnapshotRoutingController(
            ISessionSnapshotRoutingPort<TBuildContext, TFrame> port)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
        }

        public SessionSnapshotRoutingState State { get; private set; }

        public int Generation { get; private set; }

        public bool IsBuilt => State == SessionSnapshotRoutingState.Built;

        public void Build(
            TBuildContext context,
            Action<TFrame> frameReceivedHandler)
        {
            RejectReentrantTransition("build");
            if (State != SessionSnapshotRoutingState.Idle)
            {
                Dispose();
            }

            State = SessionSnapshotRoutingState.Building;
            try
            {
                _created = true;
                _port.Create(context);

                _published = true;
                _port.Publish();

                _bound = true;
                _port.Bind();

                if (frameReceivedHandler != null)
                {
                    _frameReceivedHandler = frameReceivedHandler;
                    _subscribed = true;
                    _port.Subscribe(frameReceivedHandler);
                }

                Generation++;
                State = SessionSnapshotRoutingState.Built;
            }
            catch (Exception buildFailure)
            {
                try
                {
                    Cleanup();
                }
                catch (Exception cleanupFailure)
                {
                    throw new AggregateException(
                        "Snapshot routing build failed and its rollback did not complete.",
                        buildFailure,
                        cleanupFailure);
                }

                throw;
            }
        }

        public bool TryFeed(TFrame frame)
        {
            if (State != SessionSnapshotRoutingState.Built)
            {
                return false;
            }

            _port.Feed(frame);
            return true;
        }

        public void Dispose()
        {
            RejectReentrantTransition("dispose");
            if (State == SessionSnapshotRoutingState.Idle)
            {
                return;
            }

            Cleanup();
        }

        private void Cleanup()
        {
            State = SessionSnapshotRoutingState.Disposing;

            if (_subscribed)
            {
                ExecuteCleanupStep(
                    "subscription",
                    () => _port.Unsubscribe(_frameReceivedHandler),
                    () =>
                    {
                        _subscribed = false;
                        _frameReceivedHandler = null;
                    });
            }

            if (_bound)
            {
                ExecuteCleanupStep(
                    "context binding",
                    _port.Unbind,
                    () => _bound = false);
            }

            if (_published)
            {
                ExecuteCleanupStep(
                    "published handles",
                    _port.Unpublish,
                    () => _published = false);
            }

            if (_created)
            {
                ExecuteCleanupStep(
                    "routing resources",
                    _port.Release,
                    () => _created = false);
            }

            State = SessionSnapshotRoutingState.Idle;
        }

        private void ExecuteCleanupStep(
            string step,
            Action cleanup,
            Action markCompleted)
        {
            try
            {
                cleanup();
                markCompleted();
            }
            catch (Exception exception)
            {
                State = SessionSnapshotRoutingState.DisposeFailed;
                throw new SessionSnapshotRoutingCleanupException(
                    step,
                    exception);
            }
        }

        private void RejectReentrantTransition(string operation)
        {
            if (State != SessionSnapshotRoutingState.Building &&
                State != SessionSnapshotRoutingState.Disposing)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Cannot {operation} snapshot routing while a lifecycle transition is running.");
        }
    }
}
