using System.Net.WebSockets;
using System.Text;
using AbilityKit.Orleans.Contracts.Hello;
using AbilityKit.Orleans.Gateway.HttpApi;
using AbilityKit.Orleans.Gateway.TcpGateway;
using Orleans.Configuration;
using Orleans.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<TcpGatewayOptions>()
    .Bind(builder.Configuration.GetSection("TcpGateway"));

builder.Services.AddSingleton<ITcpGatewaySessionRegistry, TcpGatewaySessionRegistry>();

builder.Services.AddSingleton<ITcpGatewayRequestHandler, HelloRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, GuestLoginRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, CreateRoomRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, JoinRoomRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, LeaveRoomRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, ListRoomsRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, CloseRoomRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, RenewSessionRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, LogoutRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, CreateSessionForAccountRequestHandler>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, TimeSyncRequestHandler>();
builder.Services.AddSingleton<FrameSyncObserverHub>();
builder.Services.AddSingleton<ITcpGatewayRequestHandler, SubmitFrameInputRequestHandler>();
builder.Services.AddSingleton<TcpGatewayRequestRouter>();
builder.Services.AddHostedService<TcpGatewayListener>();

builder.Host.UseOrleansClient(client =>
{
    client.UseLocalhostClustering();
    client.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "abilitykit-dev";
        options.ServiceId = "abilitykit-orleans";
    });
});

var app = builder.Build();

app.UseStaticFiles();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapGet("/health", () => Results.Ok("OK"));

app.MapGet("/debug", () => Results.Redirect("/debug/"));

app.MapGatewayHttpApi();

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();

    var grainFactory = context.RequestServices.GetRequiredService<Orleans.IClusterClient>();
    var buffer = new byte[4 * 1024];

    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(buffer, context.RequestAborted);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", context.RequestAborted);
            break;
        }

        var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
        if (string.IsNullOrWhiteSpace(msg))
        {
            continue;
        }

        var hello = grainFactory.GetGrain<IHelloGrain>("default");
        var reply = await hello.SayHello(msg.Trim());

        var outBytes = Encoding.UTF8.GetBytes(reply);
        await socket.SendAsync(outBytes, WebSocketMessageType.Text, endOfMessage: true, context.RequestAborted);
    }
});

app.Run("http://localhost:5001");
