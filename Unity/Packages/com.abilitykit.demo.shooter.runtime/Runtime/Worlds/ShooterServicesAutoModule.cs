using System;
using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Shooter.Runtime
{
    public sealed class ShooterServicesAutoModule : IWorldModule, IWorldModuleInfo
    {
        private readonly Assembly _targetAssembly;

        public string Id => "abilitykit.demo.shooter.services";
        public int Order => 100;
        public Type[] DependsOn => new[] { typeof(AbilityKit.World.Svelto.SveltoWorldModule) };
        public Type[] ConflictsWith => Array.Empty<Type>();

        public static readonly string[] TargetNamespacePrefixes =
        {
            "AbilityKit.Demo.Shooter.Runtime"
        };

        public ShooterServicesAutoModule()
            : this(typeof(ShooterServicesAutoModule).Assembly)
        {
        }

        public ShooterServicesAutoModule(Assembly targetAssembly)
        {
            _targetAssembly = targetAssembly ?? typeof(ShooterServicesAutoModule).Assembly;
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddModule(new AttributeWorldServicesModule(
                WorldServiceProfile.All,
                assemblies: new[] { _targetAssembly },
                namespacePrefixes: TargetNamespacePrefixes));
        }
    }
}
