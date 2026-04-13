using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Record.Core
{
    public sealed class RecordContainerBuilder
    {
        private readonly RecordProfile _profile;
        private readonly RecordIdRegistry _registry;

        public RecordContainerBuilder(RecordProfile profile, RecordIdRegistry registry = null)
        {
            _profile = profile ?? new RecordProfile();
            _registry = registry;
        }

        public RecordContainer Build()
        {
            var c = new RecordContainer
            {
                Meta = new Dictionary<string, object>(),
                Tracks = new Dictionary<RecordTrackId, RecordTrack>()
            };

            if (_profile.EnableInputs)
            {
                AddTrack(c, RecordTrackNames.Inputs, version: 1, schema: RecordTrackNames.Inputs);
            }

            if (_profile.EnableStateHash)
            {
                AddTrack(c, RecordTrackNames.StateHash, version: 1, schema: RecordTrackNames.StateHash);
            }

            if (_profile.EnableSnapshots)
            {
                AddTrack(c, RecordTrackNames.Snapshots, version: 1, schema: RecordTrackNames.Snapshots);
            }

            return c;
        }

        private void AddTrack(RecordContainer c, string name, int version, string schema)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));

            if (_registry != null)
            {
                _registry.TryRegister(name, out _);
            }

            var id = RecordTrackId.FromName(name);
            if (c.Tracks.ContainsKey(id)) return;

            c.Tracks[id] = new RecordTrack
            {
                Id = id,
                Version = version,
                Schema = schema,
                Events = new EventTrack(),
            };
        }
     }
}
