namespace AbilityKit.Network.Host
{
    public readonly struct NetworkHostDiagnostics
    {
        public NetworkHostDiagnostics(
            int activeSessions,
            long acceptedSessions,
            long closedSessions,
            long rejectedSessions,
            long idleTimeouts,
            long listenerErrors,
            long sessionErrors,
            long requestsQueued,
            long requestsCompleted,
            long requestsFailed,
            long requestsRejected,
            long requestsCancelled,
            long establishmentTimeouts,
            long admissionRejections,
            long gracefulStops,
            long drainTimeouts)
        {
            ActiveSessions = activeSessions;
            AcceptedSessions = acceptedSessions;
            ClosedSessions = closedSessions;
            RejectedSessions = rejectedSessions;
            IdleTimeouts = idleTimeouts;
            ListenerErrors = listenerErrors;
            SessionErrors = sessionErrors;
            RequestsQueued = requestsQueued;
            RequestsCompleted = requestsCompleted;
            RequestsFailed = requestsFailed;
            RequestsRejected = requestsRejected;
            RequestsCancelled = requestsCancelled;
            EstablishmentTimeouts = establishmentTimeouts;
            AdmissionRejections = admissionRejections;
            GracefulStops = gracefulStops;
            DrainTimeouts = drainTimeouts;
        }

        public int ActiveSessions { get; }
        public long AcceptedSessions { get; }
        public long ClosedSessions { get; }
        public long RejectedSessions { get; }
        public long IdleTimeouts { get; }
        public long ListenerErrors { get; }
        public long SessionErrors { get; }
        public long RequestsQueued { get; }
        public long RequestsCompleted { get; }
        public long RequestsFailed { get; }
        public long RequestsRejected { get; }
        public long RequestsCancelled { get; }
        public long EstablishmentTimeouts { get; }
        public long AdmissionRejections { get; }
        public long GracefulStops { get; }
        public long DrainTimeouts { get; }
    }
}
