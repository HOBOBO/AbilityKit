using System;

namespace AbilityKit.Network.Host
{
    public readonly struct ChannelAdmissionResult
    {
        private ChannelAdmissionResult(bool accepted, string reason)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
        }

        public bool Accepted { get; }
        public string Reason { get; }

        public static ChannelAdmissionResult Accept()
        {
            return new ChannelAdmissionResult(true, string.Empty);
        }

        public static ChannelAdmissionResult Reject(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A rejection reason is required.", nameof(reason));
            return new ChannelAdmissionResult(false, reason);
        }
    }

    /// <summary>Evaluates a transport-neutral channel before a Session is created.</summary>
    public interface IChannelAdmissionPolicy
    {
        ChannelAdmissionResult Evaluate(IServerChannel channel, int activeSessions);
    }
}
