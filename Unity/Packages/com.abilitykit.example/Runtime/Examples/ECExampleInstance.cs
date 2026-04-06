using System;
using AbilityKit.World.ECS;

namespace AbilityKit.Ability.Impl.Examples
{
    public sealed class ECExampleInstance
    {
        public EntityWorld World;
        public IEntity Root { get; private set; }
        public IEntity Player { get; private set; }
        public IEntity Pet { get; private set; }

        public ECExampleInstance()
        {
            World = new EntityWorld();
        }

        public void Start()
        {
            Root = World.Create();
            Root.WithRef(new NameComponent("Root"));

            Player = Root.AddChild();
            Player
                .WithRef(new NameComponent("Player"))
                .With(new HealthComponent(100));

            Pet = Player.AddChild();
            Pet
                .WithRef(new NameComponent("Pet"))
                .With(new HealthComponent(30));
        }

        public void DealDamageToPlayer(int damage)
        {
            if (!Player.IsValid) throw new InvalidOperationException("Player is not valid");

            var hp = Player.Get<HealthComponent>();
            hp = hp.Damage(damage);
            Player.With(hp);
        }

        public void DealDamageToPet(int damage)
        {
            if (!Pet.IsValid) throw new InvalidOperationException("Pet is not valid");

            var hp = Pet.Get<HealthComponent>();
            hp = hp.Damage(damage);
            Pet.With(hp);
        }

        public void StopAndCleanup()
        {
            if (Root.IsValid)
            {
                World.DestroyRecursive(Root.Id);
            }
        }
    }

    public sealed class NameComponent
    {
        public string Value;

        public NameComponent(string value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct HealthComponent
    {
        public readonly int Current;

        public HealthComponent(int current)
        {
            Current = current;
        }

        public HealthComponent Damage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            var newCurrent = Current - amount;
            if (newCurrent < 0) newCurrent = 0;
            return new HealthComponent(newCurrent);
        }

        public HealthComponent Heal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return new HealthComponent(Current + amount);
        }
    }
}
