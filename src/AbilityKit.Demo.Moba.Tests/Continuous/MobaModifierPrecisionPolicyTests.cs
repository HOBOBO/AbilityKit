using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Demo.Moba.Diagnostics.Tests;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Continuous
{
    /// <summary>
    /// 修改器 Sim 出口精度策略的行为契约：直通空实现零成本位等价；
    /// 帧同步定点实现对齐 Q32.32（常用域位等价、退化小值截断、越界 fail-loud）；
    /// 世界初始化按注册解析策略，未注册回退直通。
    /// </summary>
    public sealed class MobaModifierPrecisionPolicyTests
    {
        private static int BitsOf(float value)
        {
            return MobaModifierNumericGoldenCases.BitsOf(value);
        }

        private static MobaSkillParamModifierService CreateService(
            IMobaModifierPrecisionPolicy policy = null)
        {
            var service = new MobaSkillParamModifierService();
            if (policy != null) service.UsePrecisionPolicyForTesting(policy);
            return service;
        }

        [Fact]
        public void Pass_through_policy_keeps_sim_outlet_bit_identical()
        {
            using var service = CreateService();
            var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
            service.AddModifier(MobaModifierOwnerRef.Actor(1),
                new ModifierData
                {
                    Key = MobaSkillParamModifierKeys.Projectile.Speed,
                    Op = ModifierOp.Add,
                    Magnitude = MagnitudeSource.Fixed(1.25f),
                    Priority = 0,
                    SourceId = 1,
                }, default);

            var plain = service.ResolveFloat(MobaModifierOwnerRef.Actor(1),
                MobaSkillParamModifierKeys.Projectile.Speed, 6f, in record);
            var sim = service.ResolveSimFloat(MobaModifierOwnerRef.Actor(1),
                MobaSkillParamModifierKeys.Projectile.Speed, 6f, in record);

            Assert.Equal(BitsOf(plain), BitsOf(sim));
        }

        [Fact]
        public void Fixed_policy_round_trips_representable_values_bit_identically()
        {
            var policy = new Fixed64ModifierPrecisionPolicy();
            foreach (var value in new[] { 0.1f, 0.25f, 1f, 6f, 7.25f, 1234.5f, 0.00390625f })
                Assert.Equal(BitsOf(value), BitsOf(policy.QuantizeSimFloat(value)));
        }

        [Fact]
        public void Fixed_policy_truncates_sub_grid_values_to_zero()
        {
            var policy = new Fixed64ModifierPrecisionPolicy();
            Assert.Equal(0f, policy.QuantizeSimFloat(1.5e-10f));
            Assert.Equal(0f, policy.QuantizeSimFloat(-1.5e-10f));
        }

        [Fact]
        public void Fixed_policy_fails_loud_beyond_q32_32_range()
        {
            var policy = new Fixed64ModifierPrecisionPolicy();
            Assert.Throws<OverflowException>(() => policy.QuantizeSimFloat(3e9f));
            Assert.Throws<OverflowException>(() => policy.QuantizeSimFloat(-3e9f));
        }

        [Fact]
        public void OnInit_picks_up_registered_policy_and_falls_back_to_pass_through()
        {
            using (var service = new MobaSkillParamModifierService())
            {
                service.OnInit(new StubResolver(
                    typeof(IMobaModifierPrecisionPolicy), new Fixed64ModifierPrecisionPolicy()));
                var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
                // 1.5e-10f 在直通策略下原样返回；定点策略下截断为零。
                var result = service.ResolveSimFloat(MobaModifierOwnerRef.Actor(1),
                    MobaSkillParamModifierKeys.Projectile.Speed, 1.5e-10f, in record);
                Assert.Equal(BitsOf(0f), BitsOf(result));
            }

            using (var service = new MobaSkillParamModifierService())
            {
                service.OnInit(new StubResolver());
                var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
                var result = service.ResolveSimFloat(MobaModifierOwnerRef.Actor(1),
                    MobaSkillParamModifierKeys.Projectile.Speed, 1.5e-10f, in record);
                Assert.Equal(BitsOf(1.5e-10f), BitsOf(result));
            }
        }

        private sealed class StubResolver : IWorldResolver
        {
            private readonly Dictionary<Type, object> _map;

            public StubResolver(params object[] services)
            {
                _map = new Dictionary<Type, object>();
                for (var i = 0; i + 1 < services.Length; i += 2)
                    _map[(Type)services[i]] = services[i + 1];
            }

            public object Resolve(Type serviceType)
            {
                if (TryResolve(serviceType, out var value)) return value;
                throw new InvalidOperationException($"Service not provided: {serviceType?.FullName}");
            }

            public T Resolve<T>()
            {
                return (T)Resolve(typeof(T));
            }

            public bool TryResolve(Type serviceType, out object instance)
            {
                instance = null;
                return serviceType != null && _map.TryGetValue(serviceType, out instance) && instance != null;
            }

            public bool TryResolve<T>(out T instance)
            {
                if (TryResolve(typeof(T), out var obj) && obj is T cast)
                {
                    instance = cast;
                    return true;
                }

                instance = default;
                return false;
            }
        }
    }
}
