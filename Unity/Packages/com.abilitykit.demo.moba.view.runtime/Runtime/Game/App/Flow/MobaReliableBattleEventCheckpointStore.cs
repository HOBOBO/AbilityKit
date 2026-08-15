using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Flow
{
    internal sealed class MobaReliableBattleEventCheckpointStore :
        IMobaReliableBattleEventCheckpointStore,
        IDisposable
    {
        private readonly IReliableEventCheckpointStore _store;
        private readonly IDisposable _ownedStore;
        private readonly ReliableEventCheckpointLifecycleCoordinator _lifecycle;

        public MobaReliableBattleEventCheckpointStore(
            IReliableEventCheckpointStore store = null,
            bool ownsStore = false,
            ReliableEventCheckpointLifecycleOptions lifecycleOptions = null)
        {
            _store = store ?? new InMemoryReliableEventCheckpointStore();
            _ownedStore = ownsStore ? _store as IDisposable : null;
            _lifecycle = new ReliableEventCheckpointLifecycleCoordinator(
                _store,
                lifecycleOptions);
        }

        public bool TryLoad(
            string battleId,
            out MobaReliableBattleEventCheckpoint checkpoint)
        {
            if (_store.TryLoad(battleId, out var value))
            {
                checkpoint = new MobaReliableBattleEventCheckpoint(
                    value.StreamId,
                    value.TimelineId,
                    value.LastAcknowledgedSequence);
                return true;
            }

            checkpoint = default;
            return false;
        }

        public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid)
            {
                return;
            }

            var value = new ReliableEventCheckpoint(
                checkpoint.BattleId,
                checkpoint.Epoch,
                checkpoint.LastAcknowledgedSequence);
            _store.Save(in value);
        }

        public bool Remove(string battleId)
        {
            return _store.Remove(battleId);
        }

        /// <summary>等待底层检查点存储完成已排队写入。</summary>
        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            return _lifecycle.FlushAsync(
                ReliableEventCheckpointFlushTrigger.Manual,
                cancellationToken);
        }

        /// <summary>按指定生命周期原因等待底层检查点存储完成已排队写入。</summary>
        public Task<ReliableEventCheckpointFlushResult> FlushAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            return _lifecycle.FlushAsync(trigger, cancellationToken);
        }

        /// <summary>获取检查点生命周期的累计诊断。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics LifecycleDiagnostics =>
            _lifecycle.GetDiagnostics();

        /// <summary>检查点 flush 失败且完成诊断记录后触发。</summary>
        public event Action<ReliableEventCheckpointLifecycleFailure> LifecycleFailure
        {
            add => _lifecycle.Failure += value;
            remove => _lifecycle.Failure -= value;
        }

        public void Dispose()
        {
            try
            {
                _lifecycle.FlushAsync(
                    ReliableEventCheckpointFlushTrigger.Dispose).GetAwaiter().GetResult();
            }
            finally
            {
                _ownedStore?.Dispose();
            }
        }
    }

#if UNITY_5_3_OR_NEWER
    /// <summary>MOBA 示例的 Unity 检查点后端工厂。</summary>
    public static class MobaUnityReliableEventCheckpointStores
    {
        private const string DefaultPlayerPrefsPrefix = "abilitykit.moba.reliable-event.";

        /// <summary>创建适合高频 ACK 的持久化文件缓冲后端。</summary>
        public static IReliableEventCheckpointStore CreateBufferedFile()
        {
            return new BufferedReliableEventCheckpointStore(
                new FileReliableEventCheckpointStore(
                    System.IO.Path.Combine(
                        UnityEngine.Application.persistentDataPath,
                        "abilitykit-moba-reliable-events.chk")));
        }

        /// <summary>创建 PlayerPrefs 后端；调用方必须从 Unity 主线程访问该实例。</summary>
        public static IReliableEventCheckpointStore CreatePlayerPrefs(
            string keyPrefix = DefaultPlayerPrefsPrefix)
        {
            if (string.IsNullOrWhiteSpace(keyPrefix))
            {
                throw new ArgumentException("PlayerPrefs 键前缀不能为空。", nameof(keyPrefix));
            }

            return new DelegatingReliableEventCheckpointStore(
                streamId => LoadPlayerPrefs(keyPrefix, streamId),
                checkpoint => SavePlayerPrefs(keyPrefix, checkpoint),
                streamId => RemovePlayerPrefs(keyPrefix, streamId),
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    UnityEngine.PlayerPrefs.Save();
                    return Task.CompletedTask;
                });
        }

        private static ReliableEventCheckpoint? LoadPlayerPrefs(
            string prefix,
            string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) return null;
            var key = BuildPlayerPrefsKey(prefix, streamId);
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return null;

            var fields = UnityEngine.PlayerPrefs.GetString(key, string.Empty).Split('|');
            if (fields.Length != 2 ||
                !long.TryParse(
                    fields[0],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var sequence))
            {
                return null;
            }

            try
            {
                var timelineId = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(fields[1]));
                var checkpoint = new ReliableEventCheckpoint(streamId, timelineId, sequence);
                return checkpoint.IsValid ? checkpoint : (ReliableEventCheckpoint?)null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static void SavePlayerPrefs(
            string prefix,
            ReliableEventCheckpoint checkpoint)
        {
            var timeline = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(checkpoint.TimelineId));
            var value = checkpoint.LastAcknowledgedSequence.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + "|" + timeline;
            UnityEngine.PlayerPrefs.SetString(
                BuildPlayerPrefsKey(prefix, checkpoint.StreamId),
                value);
        }

        private static bool RemovePlayerPrefs(string prefix, string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) return false;
            var key = BuildPlayerPrefsKey(prefix, streamId);
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return false;
            UnityEngine.PlayerPrefs.DeleteKey(key);
            return true;
        }

        private static string BuildPlayerPrefsKey(string prefix, string streamId)
        {
            var encodedStream = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(streamId))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return prefix + encodedStream;
        }
    }
#endif
}
