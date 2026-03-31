using System;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Extensions;
using AbilityKit.Triggering.Runtime.Plan;
using NUnit.Framework;

namespace AbilityKit.Triggering.Tests.Editor
{
    public sealed class RpnNumericExprParserExtensionsTests
    {
        [Test]
        public void ParseWithAttributes_BlackboardKeyId_UsesDomainAndKey()
        {
            var nodes = RpnNumericExprParserExtensions.ParseWithAttributes("bb:combat:hp bb:enemy:hp +");

            Assert.AreEqual(3, nodes.Length);
            Assert.AreEqual(ERpnNumericNodeKind.Push, nodes[0].Kind);
            Assert.AreEqual(ERpnNumericNodeKind.Push, nodes[1].Kind);
            Assert.AreEqual(ERpnNumericNodeKind.Add, nodes[2].Kind);

            Assert.AreEqual(StableStringId.Get("bb:combat"), nodes[0].Value.BoardId);
            Assert.AreEqual(StableStringId.Get("bb:enemy"), nodes[1].Value.BoardId);
            Assert.AreEqual(StableStringId.Get("bb:combat:hp"), nodes[0].Value.KeyId);
            Assert.AreEqual(StableStringId.Get("bb:enemy:hp"), nodes[1].Value.KeyId);
            Assert.AreNotEqual(nodes[0].Value.KeyId, nodes[1].Value.KeyId);
        }

        [Test]
        public void ParseWithAttributesStrict_Throws_WhenDomainUnregistered()
        {
            RpnNumericExprParserExtensions.RescanAccessors();

            var ex = Assert.Throws<InvalidOperationException>(
                () => RpnNumericExprParserExtensions.ParseWithAttributesStrict("bb:unknown:hp 1 +"));

            StringAssert.Contains("unknown", ex.Message);
        }

        [Test]
        public void ParseWithAttributesStrict_Allows_RegisteredDomain()
        {
            RpnNumericExprParserExtensions.RescanAccessors();

            var nodes = RpnNumericExprParserExtensions.ParseWithAttributesStrict("bb:test:hp 1 +");
            Assert.AreEqual(3, nodes.Length);
            Assert.AreEqual(StableStringId.Get("bb:test"), nodes[0].Value.BoardId);
            Assert.AreEqual(StableStringId.Get("bb:test:hp"), nodes[0].Value.KeyId);
        }

        [BlackboardResolver("test")]
        private sealed class TestBlackboardResolver : IBlackboard
        {
            public bool TryGetInt(int keyId, out int value) { value = default; return false; }
            public void SetInt(int keyId, int value) { }
            public bool TryGetBool(int keyId, out bool value) { value = default; return false; }
            public void SetBool(int keyId, bool value) { }
            public bool TryGetFloat(int keyId, out float value) { value = default; return false; }
            public void SetFloat(int keyId, float value) { }
            public bool TryGetDouble(int keyId, out double value) { value = default; return false; }
            public void SetDouble(int keyId, double value) { }
            public bool TryGetString(int keyId, out string value) { value = default; return false; }
            public void SetString(int keyId, string value) { }
        }
    }
}
