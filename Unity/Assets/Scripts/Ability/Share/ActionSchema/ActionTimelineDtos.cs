using System;
using System.Collections.Generic;

namespace AbilityKit.ActionSchema
{
    [Serializable]
    public sealed class SkillAssetDto
    {
        public float length;
        public List<GroupDto> groups = new List<GroupDto>();
    }

    [Serializable]
    public sealed class GroupDto
    {
        public string name;
        public int actorId;
        public bool active;
        public bool locked;
        public bool collapsed;
        public List<TrackDto> tracks = new List<TrackDto>();
    }

    [Serializable]
    public sealed class TrackDto
    {
        public string type;
        public string name;
        public bool active;
        public bool locked;
        public List<ClipDto> clips = new List<ClipDto>();
    }

    [Serializable]
    public sealed class ClipDto
    {
        public string type;
        public float start;
        public float length;
        public float blendIn;
        public float blendOut;
        public Dictionary<string, string> args = new Dictionary<string, string>();
    }

    [Serializable]
    public sealed class SignalTrackDto
    {
        public string name;
        public bool active;
        public bool locked;
        public List<TriggerLogClipDto> clips = new List<TriggerLogClipDto>();
    }

    [Serializable]
    public sealed class TriggerLogClipDto
    {
        public float start;
        public string log;
    }
}
