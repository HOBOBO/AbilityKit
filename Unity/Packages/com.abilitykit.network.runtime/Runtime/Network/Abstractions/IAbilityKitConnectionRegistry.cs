using System;

namespace AbilityKit.Network.Abstractions
{
    public interface IAbilityKitConnectionRegistry : IDisposable
    {
        IAbilityKitConnectionLease Acquire(AbilityKitConnectionDescriptor descriptor, Func<AbilityKitConnectionDescriptor, IConnection> factory);

        bool TryGet(AbilityKitConnectionRole role, out IConnection connection);

        IConnection GetRequired(AbilityKitConnectionRole role);

        void Register(AbilityKitConnectionRole role, IConnection connection, bool disposeOnReplace = true);

        IConnection GetOrCreate(AbilityKitConnectionDescriptor descriptor, Func<AbilityKitConnectionDescriptor, IConnection> factory);

        bool Remove(AbilityKitConnectionRole role, bool dispose = true);
    }

    public interface IAbilityKitConnectionLease : IDisposable
    {
        IConnection Connection { get; }

        bool OwnsLifecycle { get; }
    }
}
