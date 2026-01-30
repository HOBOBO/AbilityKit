using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.WorldBlueprints
{
    public interface IWorldBlueprint
    {
        string WorldType { get; }

        void Configure(WorldCreateOptions options);
    }

    public sealed class DelegateWorldBlueprint : IWorldBlueprint
    {
        private readonly string _worldType;
        private readonly Action<WorldCreateOptions> _configure;

        public DelegateWorldBlueprint(string worldType, Action<WorldCreateOptions> configure)
        {
            if (string.IsNullOrEmpty(worldType)) throw new ArgumentException(nameof(worldType));
            _configure = configure ?? throw new ArgumentNullException(nameof(configure));
            _worldType = worldType;
        }

        public string WorldType => _worldType;

        public void Configure(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _configure(options);
        }
    }
}
