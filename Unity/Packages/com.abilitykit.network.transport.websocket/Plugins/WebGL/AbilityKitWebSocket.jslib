mergeInto(LibraryManager.library, {
  AbilityKitWebSocketCreate: function(urlPtr, onOpen, onMessage, onClose, onError) {
    var url = UTF8ToString(urlPtr);
    var id = (globalThis.__abilityKitWebSocketNextId || 0) + 1;
    globalThis.__abilityKitWebSocketNextId = id;
    globalThis.__abilityKitWebSockets = globalThis.__abilityKitWebSockets || {};
    var socket = new WebSocket(url);
    socket.binaryType = 'arraybuffer';
    globalThis.__abilityKitWebSockets[id] = socket;
    socket.onopen = function() { {{{ makeDynCall('vi', 'onOpen') }}}(id); };
    socket.onmessage = function(event) {
      if (!(event.data instanceof ArrayBuffer)) {
        {{{ makeDynCall('vii', 'onError') }}}(id, 2);
        return;
      }
      var data = new Uint8Array(event.data);
      var ptr = _malloc(data.length);
      HEAPU8.set(data, ptr);
      try {
        {{{ makeDynCall('viii', 'onMessage') }}}(id, ptr, data.length);
      } finally {
        _free(ptr);
      }
    };
    socket.onclose = function() {
      var sockets = globalThis.__abilityKitWebSockets;
      if (sockets) delete sockets[id];
      {{{ makeDynCall('vi', 'onClose') }}}(id);
    };
    socket.onerror = function() { {{{ makeDynCall('vii', 'onError') }}}(id, 1); };
    return id;
  },
  AbilityKitWebSocketSendBytes: function(id, bytes, offset, length) {
    var socket = globalThis.__abilityKitWebSockets && globalThis.__abilityKitWebSockets[id];
    if (!socket || socket.readyState !== WebSocket.OPEN) return;
    socket.send(HEAPU8.slice(bytes + offset, bytes + offset + length));
  },
  AbilityKitWebSocketClose: function(id) {
    var sockets = globalThis.__abilityKitWebSockets;
    var socket = sockets && sockets[id];
    if (socket) socket.close();
    if (sockets) delete sockets[id];
  }
});
