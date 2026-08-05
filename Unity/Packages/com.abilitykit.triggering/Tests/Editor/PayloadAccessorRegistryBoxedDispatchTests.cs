using AbilityKit.Triggering.Payload;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests
{
    public sealed class PayloadAccessorRegistryBoxedDispatchTests
    {
        [Test]
        public void BoxedClassPayload_UsesRuntimeTypeAccessor()
        {
            var registry = new PayloadAccessorRegistry();
            var accessor = new TestPayloadAccessor();
            registry.RegisterIntAccessor<TestPayload>(accessor);
            registry.RegisterDoubleAccessor<TestPayload>(accessor);
            object payload = new TestPayload { ActorId = 42, Value = 12.5 };

            Assert.IsTrue(registry.TryGetInt(in payload, TestPayloadAccessor.ActorIdField, out var actorId));
            Assert.AreEqual(42, actorId);
            Assert.IsTrue(registry.TryGetDouble(in payload, TestPayloadAccessor.ActorIdField, out var actorIdAsDouble));
            Assert.AreEqual(42d, actorIdAsDouble);
            Assert.IsTrue(registry.TryGetDouble(in payload, TestPayloadAccessor.ValueField, out var value));
            Assert.AreEqual(12.5d, value);
        }

        private sealed class TestPayload
        {
            public int ActorId;
            public double Value;
        }

        private sealed class TestPayloadAccessor : IPayloadIntAccessor<TestPayload>, IPayloadDoubleAccessor<TestPayload>
        {
            public const int ActorIdField = 1;
            public const int ValueField = 2;

            public bool TryGet(in TestPayload args, int fieldId, out int value)
            {
                if (args != null && fieldId == ActorIdField)
                {
                    value = args.ActorId;
                    return true;
                }

                value = default;
                return false;
            }

            public bool TryGet(in TestPayload args, int fieldId, out double value)
            {
                if (args != null && fieldId == ValueField)
                {
                    value = args.Value;
                    return true;
                }

                value = default;
                return false;
            }
        }
    }
}
