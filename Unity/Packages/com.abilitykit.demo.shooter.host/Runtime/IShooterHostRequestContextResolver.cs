using AbilityKit.Network.Host;
using AbilityKit.Protocol.Shooter;

namespace AbilityKit.Demo.Shooter.Host
{
    public interface IShooterHostRequestContextResolver
    {
        bool TryResolve(
            IServerNetworkSession session,
            in ShooterHostInputRequest request,
            out ShooterHostSessionBinding binding);
    }

    public sealed class BoundShooterHostRequestContextResolver : IShooterHostRequestContextResolver
    {
        public static readonly BoundShooterHostRequestContextResolver Instance =
            new BoundShooterHostRequestContextResolver();

        private BoundShooterHostRequestContextResolver()
        {
        }

        public bool TryResolve(
            IServerNetworkSession session,
            in ShooterHostInputRequest request,
            out ShooterHostSessionBinding binding)
        {
            return ShooterHostSessionBindings.TryGet(session, out binding);
        }
    }
}
