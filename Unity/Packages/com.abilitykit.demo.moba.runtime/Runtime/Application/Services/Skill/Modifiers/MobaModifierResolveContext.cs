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
        public bool IsValid => Scope == MobaModifierOwnerScope.Actor && Id > 0;

        public static MobaModifierOwnerRef Actor(int actorId)
        {
            return new MobaModifierOwnerRef(MobaModifierOwnerScope.Actor, actorId);
        }

    }

}
