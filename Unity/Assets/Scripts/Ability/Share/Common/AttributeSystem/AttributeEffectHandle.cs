using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    public sealed class AttributeEffectHandle : IDisposable
    {
        private readonly AttributeContext _ctx;
        private readonly List<Entry> _entries;
        private bool _disposed;

        internal AttributeEffectHandle(AttributeContext ctx, List<Entry> entries)
        {
            _ctx = ctx;
            _entries = entries;
        }

        public bool IsValid => !_disposed && _entries != null && _entries.Count > 0;

        public void Dispose()
        {
            Remove();
        }

        public void Remove()
        {
            if (_disposed) return;
            _disposed = true;

            if (_ctx == null || _entries == null) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                _ctx.RemoveModifier(e.Attribute, e.Handle);
            }

            _entries.Clear();
        }

        internal readonly struct Entry
        {
            public readonly AttributeId Attribute;
            public readonly AttributeModifierHandle Handle;

            public Entry(AttributeId attribute, AttributeModifierHandle handle)
            {
                Attribute = attribute;
                Handle = handle;
            }
        }
    }
}
