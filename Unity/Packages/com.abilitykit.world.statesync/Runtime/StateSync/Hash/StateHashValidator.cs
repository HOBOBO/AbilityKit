using System;

namespace AbilityKit.Ability.StateSync.Hash
{
    public enum DesyncType
    {
        None = 0,
        Position = 1,
        Rotation = 2,
        Velocity = 3,
        Health = 4,
        StateFlags = 5,
        AbilityMask = 6,
        ControlFlags = 7,
        Projectile = 8,
        Unknown = 99
    }

    public readonly struct ValidationResult
    {
        public bool IsValid { get; }
        public int Frame { get; }
        public StateHash ClientHash { get; }
        public StateHash ServerHash { get; }
        public DesyncType DesyncKind { get; }
        public long Timestamp { get; }

        public ValidationResult(
            bool isValid,
            int frame,
            StateHash clientHash,
            StateHash serverHash,
            DesyncType desyncKind)
        {
            IsValid = isValid;
            Frame = frame;
            ClientHash = clientHash;
            ServerHash = serverHash;
            DesyncKind = desyncKind;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public static ValidationResult Valid(int frame) => new ValidationResult(true, frame, StateHash.Invalid, StateHash.Invalid, DesyncType.None);
        public static ValidationResult Invalid(int frame, StateHash client, StateHash server, DesyncType kind) =>
            new ValidationResult(false, frame, client, server, kind);

        public override string ToString() => IsValid
            ? $"Valid(frame={Frame})"
            : $"Desync(frame={Frame}, kind={DesyncKind}, client={ClientHash}, server={ServerHash})";
    }

    public sealed class StateHashValidator
    {
        public Action<ValidationResult> OnValidationResult;

        public ValidationResult Validate(int frame, Snapshot.WorldStateSnapshot clientState, Snapshot.WorldStateSnapshot serverState)
        {
            if (clientState == null || serverState == null)
                return ValidationResult.Valid(frame);

            var clientHash = clientState.ComputeHash();
            var serverHash = serverState.ComputeHash();

            if (clientHash == serverHash)
            {
                return ValidationResult.Valid(frame);
            }

            var desyncType = DetectDesyncType(clientState, serverState);
            var result = ValidationResult.Invalid(frame, clientHash, serverHash, desyncType);

            OnValidationResult?.Invoke(result);

            return result;
        }

        public ValidationResult Validate(int frame, StateHash clientHash, StateHash serverHash)
        {
            if (clientHash == serverHash)
            {
                return ValidationResult.Valid(frame);
            }

            return ValidationResult.Invalid(frame, clientHash, serverHash, DesyncType.Unknown);
        }

        private DesyncType DetectDesyncType(Snapshot.WorldStateSnapshot client, Snapshot.WorldStateSnapshot server)
        {
            if (client.Entities.Count != server.Entities.Count)
                return DesyncType.Unknown;

            for (int i = 0; i < client.Entities.Count; i++)
            {
                var ce = client.Entities[i];
                var se = server.Entities[i];

                if (ce.EntityId != se.EntityId)
                    return DesyncType.Unknown;

                if (!ce.position.ApproximatelyEquals(se.position, 0.01f))
                    return DesyncType.Position;

                if (!ce.rotation.ApproximatelyEquals(se.rotation, 0.01f))
                    return DesyncType.Rotation;

                if (!ce.velocity.ApproximatelyEquals(se.velocity, 0.01f))
                    return DesyncType.Velocity;

                if (ce.healthPercent != se.healthPercent)
                    return DesyncType.Health;

                if (ce.StateFlags != se.StateFlags)
                    return DesyncType.StateFlags;

                if (ce.ActiveAbilityMask != se.ActiveAbilityMask)
                    return DesyncType.AbilityMask;

                if (ce.ControlFlags != se.ControlFlags)
                    return DesyncType.ControlFlags;
            }

            if (client.Projectiles.Count != server.Projectiles.Count)
                return DesyncType.Projectile;

            for (int i = 0; i < client.Projectiles.Count; i++)
            {
                var cp = client.Projectiles[i];
                var sp = server.Projectiles[i];

                if (cp.ProjectileId != sp.ProjectileId)
                    return DesyncType.Projectile;

                if (!cp.CurrentPosition.ApproximatelyEquals(sp.CurrentPosition, 0.01f))
                    return DesyncType.Projectile;
            }

            return DesyncType.Unknown;
        }
    }
}
