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

        [Test]
        public void GenericObjectPayload_UsesRuntimeTypeAccessor()
        {
            var registry = new PayloadAccessorRegistry();
            var accessor = new TestStructPayloadAccessor();
            var objectAccessor = new RejectingObjectPayloadAccessor();
            registry.RegisterIntAccessor<object>(objectAccessor);
            registry.RegisterDoubleAccessor<object>(objectAccessor);
            registry.RegisterIntAccessor<TestStructPayload>(accessor);
            registry.RegisterDoubleAccessor<TestStructPayload>(accessor);
            object payload = new TestStructPayload(42, 12.5);

            Assert.IsTrue(TryGetInt(registry, payload, TestStructPayloadAccessor.ActorIdField, out var actorId));
            Assert.AreEqual(42, actorId);
            Assert.IsTrue(TryGetDouble(registry, payload, TestStructPayloadAccessor.ValueField, out var value));
            Assert.AreEqual(12.5d, value);
        }

        private static bool TryGetInt<TArgs>(PayloadAccessorRegistry registry, TArgs payload, int fieldId, out int value)
        {
            return registry.TryGetInt(in payload, fieldId, out value);
        }

        private static bool TryGetDouble<TArgs>(PayloadAccessorRegistry registry, TArgs payload, int fieldId, out double value)
        {
            return registry.TryGetDouble(in payload, fieldId, out value);
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

        private readonly struct TestStructPayload
        {
            public TestStructPayload(int actorId, double value)
            {
                ActorId = actorId;
                Value = value;
            }

            public int ActorId { get; }
            public double Value { get; }
        }

        private sealed class TestStructPayloadAccessor : IPayloadIntAccessor<TestStructPayload>, IPayloadDoubleAccessor<TestStructPayload>
        {
            public const int ActorIdField = 3;
            public const int ValueField = 4;

            public bool TryGet(in TestStructPayload args, int fieldId, out int value)
            {
                value = args.ActorId;
                return fieldId == ActorIdField;
            }

            public bool TryGet(in TestStructPayload args, int fieldId, out double value)
            {
                value = args.Value;
                return fieldId == ValueField;
            }
        }

        private sealed class RejectingObjectPayloadAccessor :
            IPayloadIntAccessor<object>,
            IPayloadDoubleAccessor<object>
        {
            public bool TryGet(in object args, int fieldId, out int value)
            {
                value = default;
                return false;
            }

            public bool TryGet(in object args, int fieldId, out double value)
            {
                value = default;
                return false;
            }
        }
    }
}
