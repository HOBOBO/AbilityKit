using System;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaModifierOwnerScope
    {
        None = 0,
        Actor = 1,
        SkillRuntime = 2,
        Launcher = 3,
        Projectile = 4,
        Summon = 5
    }

    public readonly struct MobaModifierOwnerRef
    {
        public MobaModifierOwnerRef(MobaModifierOwnerScope scope, int id)
        {
            Scope = scope;
            Id = id;
        }

        public MobaModifierOwnerScope Scope { get; }
        public int Id { get; }
        public bool IsValid => Scope != MobaModifierOwnerScope.None && Id > 0;

        public static MobaModifierOwnerRef Actor(int actorId)
        {
            return new MobaModifierOwnerRef(MobaModifierOwnerScope.Actor, actorId);
        }

        public static MobaModifierOwnerRef SkillRuntime(int skillRuntimeId)
        {
            return new MobaModifierOwnerRef(MobaModifierOwnerScope.SkillRuntime, skillRuntimeId);
        }

        public static MobaModifierOwnerRef Launcher(int launcherActorId)
        {
            return new MobaModifierOwnerRef(MobaModifierOwnerScope.Launcher, launcherActorId);
        }

        public static MobaModifierOwnerRef Projectile(int projectileActorId)
        {
            return new MobaModifierOwnerRef(MobaModifierOwnerScope.Projectile, projectileActorId);
        }

        public static MobaModifierOwnerRef Summon(int summonActorId)
        {
            return new MobaModifierOwnerRef(MobaModifierOwnerScope.Summon, summonActorId);
        }
    }

    public readonly struct MobaModifierResolveContext
    {
        public MobaModifierResolveContext(
            int actorId = 0,
            int skillRuntimeId = 0,
            int launcherActorId = 0,
            int projectileActorId = 0,
            int summonActorId = 0)
        {
            ActorId = actorId;
            SkillRuntimeId = skillRuntimeId;
            LauncherActorId = launcherActorId;
            ProjectileActorId = projectileActorId;
            SummonActorId = summonActorId;
        }

        public int ActorId { get; }
        public int SkillRuntimeId { get; }
        public int LauncherActorId { get; }
        public int ProjectileActorId { get; }
        public int SummonActorId { get; }

        public MobaModifierOwnerRef Actor => MobaModifierOwnerRef.Actor(ActorId);
        public MobaModifierOwnerRef SkillRuntime => MobaModifierOwnerRef.SkillRuntime(SkillRuntimeId);
        public MobaModifierOwnerRef Launcher => MobaModifierOwnerRef.Launcher(LauncherActorId);
        public MobaModifierOwnerRef Projectile => MobaModifierOwnerRef.Projectile(ProjectileActorId);
        public MobaModifierOwnerRef Summon => MobaModifierOwnerRef.Summon(SummonActorId);

        public int WriteActorChain(Span<MobaModifierOwnerRef> destination)
        {
            return WriteChain(destination, Actor, default, default);
        }

        public int WriteLauncherThenActorChain(Span<MobaModifierOwnerRef> destination)
        {
            return WriteChain(destination, Launcher, Actor, default);
        }

        public int WriteProjectileThenLauncherThenActorChain(Span<MobaModifierOwnerRef> destination)
        {
            return WriteChain(destination, Projectile, Launcher, Actor);
        }

        public int WriteSummonThenActorChain(Span<MobaModifierOwnerRef> destination)
        {
            return WriteChain(destination, Summon, Actor, default);
        }

        private static int WriteChain(
            Span<MobaModifierOwnerRef> destination,
            MobaModifierOwnerRef first,
            MobaModifierOwnerRef second,
            MobaModifierOwnerRef third)
        {
            var count = 0;
            WriteOwner(destination, ref count, first);
            WriteOwner(destination, ref count, second);
            WriteOwner(destination, ref count, third);
            return count;
        }

        private static void WriteOwner(
            Span<MobaModifierOwnerRef> destination,
            ref int count,
            MobaModifierOwnerRef owner)
        {
            if (!owner.IsValid) return;
            if ((uint)count >= (uint)destination.Length)
            {
                throw new ArgumentException("Modifier owner chain destination is too small.", nameof(destination));
            }

            destination[count++] = owner;
        }
    }
}
