using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Common.Numbers
{
    public sealed class NumberEffectHandle : IDisposable
    {
        private readonly NumberValue _value;
        private readonly List<NumberModifierHandle> _handles;

        public NumberEffectHandle(NumberValue value, List<NumberModifierHandle> handles)
        {
            _value = value;
            _handles = handles;
        }

        public void Dispose()
        {
            if (_value == null || _handles == null || _handles.Count == 0) return;

            for (int i = 0; i < _handles.Count; i++)
            {
                _value.Remove(_handles[i]);
            }

            _handles.Clear();
        }
    }
}
