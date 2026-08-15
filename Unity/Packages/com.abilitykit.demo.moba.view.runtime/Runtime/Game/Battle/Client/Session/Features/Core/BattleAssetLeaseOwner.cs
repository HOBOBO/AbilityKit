using System;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Battle.Shared.Assets;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleAssetLeaseOwner : IDisposable
    {
        private IBattleAssetLease _lease;

        internal IBattleAssetLease Lease => _lease;
        internal IBattleAssetLookup AssetLookup => _lease as IBattleAssetLookup;

        internal void Adopt(IBattleAssetLease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            if (!lease.IsActive)
            {
                throw new InvalidOperationException("Cannot adopt an inactive battle asset lease.");
            }

            var previous = _lease;
            _lease = lease;
            if (previous == null || ReferenceEquals(previous, lease)) return;

            DisposeLease(previous, "replaced battle asset lease");
        }

        public void Dispose()
        {
            var lease = _lease;
            _lease = null;
            if (lease == null) return;

            DisposeLease(lease, "battle asset lease");
        }

        private static void DisposeLease(IBattleAssetLease lease, string resourceName)
        {
            try
            {
                lease.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, $"[BattleAssetLeaseOwner] Failed to release {resourceName}");
            }
        }
    }
}
