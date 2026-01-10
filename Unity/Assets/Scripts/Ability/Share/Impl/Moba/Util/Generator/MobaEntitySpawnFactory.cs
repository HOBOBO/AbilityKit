using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Impl.Moba.Util.Generator
{
    public enum MobaEntityKind
    {
        Unknown = 0,
        Hero = 1,
        Minion = 2,
        Monster = 3,
    }

    public readonly struct MobaEntityInfo
    {
        public readonly int ActorId;
        public readonly MobaEntityKind Kind;
        public readonly Transform3 Transform;

        public readonly int TeamId;
        public readonly int TemplateId;


        public MobaEntityInfo(
            int actorId,
            MobaEntityKind kind,
            in Transform3 transform,
            int teamId = 0,
            int templateId = 0,
            bool hasCollider = false,
            in ColliderShape collider = default,
            bool hasCollisionLayer = false,
            int collisionLayerMask = 0)
        {
            ActorId = actorId;
            Kind = kind;
            Transform = transform;
            TeamId = teamId;
            TemplateId = templateId;

        }
    }

    public static class MobaEntitySpawnFactory
    {
        public delegate ActorEntity CreateHandler(ActorContext context, in MobaEntityInfo info);

        private static readonly Dictionary<MobaEntityKind, CreateHandler> _handlers = new Dictionary<MobaEntityKind, CreateHandler>
        {
            { MobaEntityKind.Hero, CreateHero },
            { MobaEntityKind.Minion, CreateMinion },
            { MobaEntityKind.Monster, CreateMonster },
        };

        public static void Register(MobaEntityKind kind, CreateHandler handler)
        {
            if (kind == MobaEntityKind.Unknown) throw new ArgumentException("kind cannot be Unknown", nameof(kind));
            _handlers[kind] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public static bool TryCreate(ActorContext context, in MobaEntityInfo info, out ActorEntity entity)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            if (_handlers.TryGetValue(info.Kind, out var handler) && handler != null)
            {
                entity = handler(context, in info);
                return entity != null;
            }

            entity = null;
            return false;
        }

        public static ActorEntity Create(ActorContext context, in MobaEntityInfo info)
        {
            if (TryCreate(context, in info, out var e)) return e;
            throw new InvalidOperationException($"No spawn handler registered for kind={info.Kind}");
        }

        private static ActorEntity CreateHero(ActorContext context, in MobaEntityInfo info)
        {
            var b = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion()
                .WithMoveInput();

            //其他组件
            return b.Build();
        }

        private static ActorEntity CreateMinion(ActorContext context, in MobaEntityInfo info)
        {
            var b = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion();

            //其他组件
            return b.Build();
        }

        private static ActorEntity CreateMonster(ActorContext context, in MobaEntityInfo info)
        {
            var b = ActorEntityFactory.Create(context)
                .WithActorId(info.ActorId)
                .WithTransform(info.Transform)
                .WithMotion();

            //其他组件
            return b.Build();
        }
    }
}
