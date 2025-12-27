using System;
using AbilityKit.Ability.EC;

namespace AbilityKit.Ability.Impl.Examples
{
    public sealed class ECExampleInstance
    {
        public readonly EntityWorld World;
        public Entity Root { get; private set; }
        public Entity Player { get; private set; }
        public Entity Pet { get; private set; }

        public ECExampleInstance()
        {
            World = new EntityWorld();
        }

        public void Start()
        {
            Root = World.Create();
            Root.AddComponent(new NameComponent("Root"));

            Player = Root.AddChild();
            Player.AddComponent(new NameComponent("Player"));
            Player.AddComponent(new HealthComponent(100));

            Pet = Player.AddChild();
            Pet.AddComponent(new NameComponent("Pet"));
            Pet.AddComponent(new HealthComponent(30));
        }

        public void DealDamageToPlayer(int damage)
        {
            if (!Player.IsValid) throw new InvalidOperationException("Player is not valid");

            var hp = Player.GetComponent<HealthComponent>();
            hp.Damage(damage);
        }

        public void DealDamageToPet(int damage)
        {
            if (!Pet.IsValid) throw new InvalidOperationException("Pet is not valid");

            var hp = Pet.GetComponent<HealthComponent>();
            hp.Damage(damage);
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

    public sealed class HealthComponent
    {
        public int Current;

        public HealthComponent(int current)
        {
            Current = current;
        }

        public void Damage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Current -= amount;
            if (Current < 0) Current = 0;
        }

        public void Heal(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Current += amount;
        }
    }
}
