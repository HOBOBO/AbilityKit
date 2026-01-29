using System;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Game.Battle.Requests
{
    public readonly struct CreateWorldRequest
    {
        public readonly WorldCreateOptions Options;

        public readonly int OpCode;
        public readonly byte[] Payload;

        public CreateWorldRequest(WorldCreateOptions options, int opCode = 0, byte[] payload = null)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            OpCode = opCode;
            Payload = payload;
        }
    }
}
