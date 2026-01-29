using System;
using System.Collections.Generic;
using System.Text;
using AbilityKit.Ability.FrameSync;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public sealed class JsonRecordContainerSerializer : IRecordContainerSerializer
    {
        public byte[] Serialize(RecordContainer container)
        {
            var dto = ToDto(container);
            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            return Encoding.UTF8.GetBytes(json);
        }

        public RecordContainer Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            var json = Encoding.UTF8.GetString(data);
            var dto = JsonConvert.DeserializeObject<RecordContainerDto>(json);
            return FromDto(dto);
        }

        private static RecordContainerDto ToDto(RecordContainer container)
        {
            if (container == null) return null;

            var dto = new RecordContainerDto
            {
                Meta = container.Meta,
                Tracks = new List<RecordTrackDto>()
            };

            if (container.Tracks != null)
            {
                foreach (var kv in container.Tracks)
                {
                    var t = kv.Value;
                    if (t == null) continue;

                    var trackDto = new RecordTrackDto
                    {
                        Id = t.Id.Value,
                        Version = t.Version,
                        Schema = t.Schema,
                        Events = new List<RecordEventDto>()
                    };

                    var eventsByFrame = t.Events != null ? t.Events.Export() : null;
                    if (eventsByFrame != null)
                    {
                        foreach (var fe in eventsByFrame)
                        {
                            var list = fe.Value;
                            if (list == null) continue;
                            for (int i = 0; i < list.Count; i++)
                            {
                                var e = list[i];
                                trackDto.Events.Add(new RecordEventDto
                                {
                                    Frame = e.Frame.Value,
                                    EventType = e.EventType.Value,
                                    PayloadBase64 = e.Payload != null && e.Payload.Length > 0 ? Convert.ToBase64String(e.Payload) : string.Empty,
                                });
                            }
                        }
                    }

                    dto.Tracks.Add(trackDto);
                }
            }

            return dto;
        }

        private static RecordContainer FromDto(RecordContainerDto dto)
        {
            if (dto == null) return null;

            var container = new RecordContainer
            {
                Meta = dto.Meta,
                Tracks = new Dictionary<RecordTrackId, RecordTrack>()
            };

            if (dto.Tracks != null)
            {
                for (int i = 0; i < dto.Tracks.Count; i++)
                {
                    var t = dto.Tracks[i];
                    if (t == null) continue;

                    var track = new RecordTrack
                    {
                        Id = new RecordTrackId(t.Id),
                        Version = t.Version,
                        Schema = t.Schema,
                        Events = new EventTrack(),
                    };

                    if (t.Events != null)
                    {
                        for (int j = 0; j < t.Events.Count; j++)
                        {
                            var e = t.Events[j];
                            if (e == null) continue;

                            var payload = string.IsNullOrEmpty(e.PayloadBase64)
                                ? Array.Empty<byte>()
                                : Convert.FromBase64String(e.PayloadBase64);

                            track.Events.Append(new FrameIndex(e.Frame), new RecordEventType(e.EventType), payload);
                        }
                    }

                    container.Tracks[track.Id] = track;
                }
            }

            return container;
        }

        [Serializable]
        private sealed class RecordContainerDto
        {
            public Dictionary<string, object> Meta;
            public List<RecordTrackDto> Tracks;
        }

        [Serializable]
        private sealed class RecordTrackDto
        {
            public int Id;
            public int Version;
            public string Schema;
            public List<RecordEventDto> Events;
        }

        [Serializable]
        private sealed class RecordEventDto
        {
            public int Frame;
            public int EventType;
            public string PayloadBase64;
        }
    }
}
