using System;
using System.Collections.Generic;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Tiny.View
{
    public enum TinySyncMode { State, Frame, Hybrid }

    internal static class TinySyncModeConfiguration
    {
        public static IReadOnlyDictionary<string, string> CreateTags(TinySyncMode mode)
        {
            var model = mode == TinySyncMode.State ? NetworkSyncModel.AuthoritativeInterpolation
                : mode == TinySyncMode.Frame ? NetworkSyncModel.Lockstep
                : NetworkSyncModel.HybridHeroPrediction;
            var template = mode == TinySyncMode.State ? TinySyncTemplates.State
                : mode == TinySyncMode.Frame ? TinySyncTemplates.Frame
                : TinySyncTemplates.Hybrid;
            return new Dictionary<string, string>
            {
                [RoomGatewaySyncTagKeys.SyncTemplateId] = template,
                [RoomGatewaySyncTagKeys.SyncModel] = ((int)model).ToString(System.Globalization.CultureInfo.InvariantCulture),
                [RoomGatewaySyncTagKeys.InputDelayFrames] =
                    TinySyncSettings.InputDelayFrames.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        public static TinySyncMode FromProfile(string profileName)
        {
            if (string.Equals(profileName, nameof(NetworkSyncModel.AuthoritativeInterpolation), StringComparison.Ordinal))
                return TinySyncMode.State;
            if (string.Equals(profileName, nameof(NetworkSyncModel.Lockstep), StringComparison.Ordinal))
                return TinySyncMode.Frame;
            if (string.Equals(profileName, nameof(NetworkSyncModel.HybridHeroPrediction), StringComparison.Ordinal))
                return TinySyncMode.Hybrid;
            throw new InvalidOperationException("Unsupported Tiny sync profile: " + profileName);
        }
    }
}
