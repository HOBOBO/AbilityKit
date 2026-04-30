using System;

namespace AbilityKit.Battle.SearchTarget.Rules
{
    [TargetRule(0x0101, "CircleShape", 0)]
    public sealed class CircleShapeRule : ITargetRule
    {
        private readonly IVec2 _origin;
        private readonly float _radius;
        private readonly float _radiusSqr;

        public CircleShapeRule(IVec2 origin, float radius)
        {
            _origin = origin;
            _radius = radius;
            _radiusSqr = radius * radius;
        }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            if (_radius <= 0f) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPosition(candidate, out var p)) return false;

            var d = p.Subtract(_origin);
            return d.SqrMagnitude <= _radiusSqr;
        }
    }

    [TargetRule(0x0102, "SectorShape", 1)]
    public sealed class SectorShapeRule : ITargetRule
    {
        private readonly IVec2 _origin;
        private readonly IVec2 _forward;
        private readonly float _radius;
        private readonly float _radiusSqr;
        private readonly float _cosHalfAngle;

        public SectorShapeRule(IVec2 origin, IVec2 forward, float radius, float halfAngleDegrees)
        {
            _origin = origin;
            _radius = radius;
            _radiusSqr = radius * radius;

            var mag = forward.SqrMagnitude;
            if (mag > 0f)
            {
                var inv = 1f / (float)Math.Sqrt(mag);
                _forward = forward.Multiply(inv);
            }
            else
            {
                _forward = Vec2.Up;
            }

            var halfAngleRad = halfAngleDegrees * (float)Math.PI / 180f;
            _cosHalfAngle = (float)Math.Cos(halfAngleRad);
        }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            if (_radius <= 0f) return false;
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            if (!pos.TryGetPosition(candidate, out var p)) return false;

            var rel = p.Subtract(_origin);
            if (rel.SqrMagnitude > _radiusSqr) return false;

            var mag = rel.Magnitude;
            if (mag <= 1e-6f) return true;

            var dir = rel.Multiply(1f / mag);
            return dir.Dot(_forward) >= _cosHalfAngle;
        }
    }

    [TargetRule(0x0201, "Whitelist", 100)]
    public sealed class WhitelistRule : ITargetRule
    {
        private readonly IActorIdSet _set;

        public WhitelistRule(IActorIdSet set)
        {
            _set = set;
        }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            return _set != null && _set.Contains(candidate.ActorId);
        }
    }

    [TargetRule(0x0202, "Blacklist", 100)]
    public sealed class BlacklistRule : ITargetRule
    {
        private readonly IActorIdSet _set;

        public BlacklistRule(IActorIdSet set)
        {
            _set = set;
        }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            return _set == null || !_set.Contains(candidate.ActorId);
        }
    }

    [TargetRule(0x0203, "ExcludeEntity", 100)]
    public sealed class ExcludeEntityRule : ITargetRule
    {
        private readonly IEntityId _excluded;

        public ExcludeEntityRule(IEntityId excluded)
        {
            _excluded = excluded;
        }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            return candidate.ActorId != _excluded.ActorId;
        }
    }

    [TargetRule(0x0204, "RequireValidId", 200)]
    public sealed class RequireValidIdRule : ITargetRule
    {
        public static readonly RequireValidIdRule Instance = new RequireValidIdRule();

        public RequireValidIdRule() { }

        public bool RequiresPosition => false;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            return candidate.IsValid;
        }
    }

    [TargetRule(0x0205, "RequireHasPosition", 200)]
    public sealed class RequireHasPositionRule : ITargetRule
    {
        public static readonly RequireHasPositionRule Instance = new RequireHasPositionRule();

        private RequireHasPositionRule() { }

        public bool RequiresPosition => true;

        public bool Test(in SearchQuery query, SearchContext context, IEntityId candidate)
        {
            if (!context.TryGetService<IPositionProvider>(out var pos) || pos == null) return false;
            return pos.TryGetPosition(candidate, out _);
        }
    }
}
