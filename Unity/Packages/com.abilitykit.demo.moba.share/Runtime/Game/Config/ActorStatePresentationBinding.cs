using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Data-only binding of an authoritative actor phase to an Animator action.</summary>
    public sealed class ActorStatePresentationBinding
    {
        public string ProfileId { get; set; }
        public string StatePath { get; set; }
        public string AnimatorState { get; set; }
        public float ClipDurationSeconds { get; set; }
        public float PlaybackDurationSeconds { get; set; }

        public static ActorStatePresentationBinding[] Load(string json)
        {
            var entries = JsonConvert.DeserializeObject<ActorStatePresentationBinding[]>(json,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error })
                ?? Array.Empty<ActorStatePresentationBinding>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.ProfileId) ||
                    string.IsNullOrWhiteSpace(entry.StatePath) || string.IsNullOrWhiteSpace(entry.AnimatorState) ||
                    !(entry.ClipDurationSeconds > 0) || float.IsInfinity(entry.ClipDurationSeconds) ||
                    !(entry.PlaybackDurationSeconds > 0) || float.IsInfinity(entry.PlaybackDurationSeconds) ||
                    !keys.Add(entry.ProfileId + ":" + entry.StatePath))
                    throw new InvalidOperationException("Invalid or duplicate actor state presentation binding.");
            }
            return entries;
        }
    }
}
