# com.abilitykit.network.host.kcp

Optional KCP listener for `NetworkHost`. `KcpChannelListener` owns one UDP socket and
creates one `KcpServerChannel` per remote endpoint. Configure the same conversation ID
as the client transport:

```csharp
var listener = new KcpChannelListener(new KcpChannelListenerOptions
{
    Address = IPAddress.Any,
    Port = 4002,
    ConversationId = 1
});
using var host = new NetworkHost(listener, new NetworkHostOptions
{
    RequestHandler = handlers
});
host.Start();
```

The listener validates the KCP conversation ID before creating a channel and leaves
accepted channels to the `NetworkHost` owner. Stop closes the listener and all channels.
The implementation uses the same ET-derived KCP core as the optional client package;
its source license applies to redistribution.
