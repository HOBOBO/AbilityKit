using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.ActionTimeline
{
    public sealed class MobaClipHandlerRegistry
    {
        private readonly Dictionary<string, IMobaClipHandler> _handlers = new Dictionary<string, IMobaClipHandler>();

        public void Register(string clipType, IMobaClipHandler handler)
        {
            if (string.IsNullOrEmpty(clipType)) throw new ArgumentException(nameof(clipType));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[clipType] = handler;
        }

        public bool TryGet(string clipType, out IMobaClipHandler handler)
        {
            if (string.IsNullOrEmpty(clipType))
            {
                handler = null;
                return false;
            }

            return _handlers.TryGetValue(clipType, out handler);
        }
    }
}
