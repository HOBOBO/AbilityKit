#nullable enable

using System;
using System.Threading.Tasks;

namespace AbilityKit.Network.Room
{
    public readonly struct RoomGatewayRestoreFirstConnectionResult<TResult>
    {
        public RoomGatewayRestoreFirstConnectionResult(TResult result, bool usedFallbackCreate, Exception? restoreFailure)
        {
            Result = result;
            UsedFallbackCreate = usedFallbackCreate;
            RestoreFailure = restoreFailure;
        }

        public TResult Result { get; }

        public bool UsedFallbackCreate { get; }

        public Exception? RestoreFailure { get; }
    }

    public static class RoomGatewayRestoreFirstConnectionPolicy
    {
        public static async Task<RoomGatewayRestoreFirstConnectionResult<TResult>> ConnectAsync<TResult>(
            Func<Task<TResult>> restoreAsync,
            Func<Task<TResult>> fallbackCreateAsync,
            bool allowFallbackCreate)
        {
            if (restoreAsync == null) throw new ArgumentNullException(nameof(restoreAsync));
            if (fallbackCreateAsync == null) throw new ArgumentNullException(nameof(fallbackCreateAsync));

            try
            {
                var restored = await restoreAsync().ConfigureAwait(false);
                return new RoomGatewayRestoreFirstConnectionResult<TResult>(restored, false, null);
            }
            catch (Exception ex) when (allowFallbackCreate)
            {
                var created = await fallbackCreateAsync().ConfigureAwait(false);
                return new RoomGatewayRestoreFirstConnectionResult<TResult>(created, true, ex);
            }
        }
    }
}
