using System;
using System.Threading.Tasks;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Network.Sdk;

namespace AbilityKit.Game.Flow
{
    public static class MobaUnityReliableEventCheckpointStores
    {
        private const string DefaultPlayerPrefsPrefix = "abilitykit.moba.reliable-event.";

        public static IReliableEventCheckpointStore CreateBufferedFile()
        {
            return new BufferedReliableEventCheckpointStore(
                new FileReliableEventCheckpointStore(
                    System.IO.Path.Combine(
                        UnityEngine.Application.persistentDataPath,
                        "abilitykit-moba-reliable-events.chk")));
        }

        // PlayerPrefs must be accessed on the Unity main thread.
        public static IReliableEventCheckpointStore CreatePlayerPrefs(
            string keyPrefix = DefaultPlayerPrefsPrefix)
        {
            if (string.IsNullOrWhiteSpace(keyPrefix))
                throw new ArgumentException("PlayerPrefs key prefix cannot be empty.", nameof(keyPrefix));

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
            string prefix, string streamId)
        {
            if (string.IsNullOrWhiteSpace(streamId)) return null;
            var key = BuildPlayerPrefsKey(prefix, streamId);
            if (!UnityEngine.PlayerPrefs.HasKey(key)) return null;

            var fields = UnityEngine.PlayerPrefs.GetString(key, string.Empty).Split('|');
            if (fields.Length != 2 ||
                !long.TryParse(fields[0],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var sequence)) return null;

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
            string prefix, ReliableEventCheckpoint checkpoint)
        {
            var timeline = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(checkpoint.TimelineId));
            var value = checkpoint.LastAcknowledgedSequence.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + "|" + timeline;
            UnityEngine.PlayerPrefs.SetString(
                BuildPlayerPrefsKey(prefix, checkpoint.StreamId), value);
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
}
