#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Common.Rooms
{
    public readonly struct DemoAccountLoginResult
    {
        public DemoAccountLoginResult(
            bool success,
            string sessionToken,
            string accountId,
            long expireAtUnixMs,
            string kickedSessionToken,
            string message)
        {
            Success = success;
            SessionToken = sessionToken ?? string.Empty;
            AccountId = accountId ?? string.Empty;
            ExpireAtUnixMs = expireAtUnixMs;
            KickedSessionToken = kickedSessionToken ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string SessionToken { get; }
        public string AccountId { get; }
        public long ExpireAtUnixMs { get; }
        public string KickedSessionToken { get; }
        public string Message { get; }
    }

    public sealed class DemoRoomGatewayAccountClient : IDisposable
    {
        private readonly RequestClient _requestClient;

        public DemoRoomGatewayAccountClient(IConnection connection)
        {
            _requestClient = new RequestClient(
                connection ?? throw new ArgumentNullException(nameof(connection)));
        }

        public async Task<DemoAccountLoginResult> AccountLoginAsync(
            string accountId,
            int expireSeconds = 0,
            bool kickExisting = true,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("accountId is required.", nameof(accountId));
            }

            var request = new WireRoomAccountLoginReq
            {
                AccountId = accountId.Trim(),
                ExpireSeconds = Math.Max(0, expireSeconds),
                KickExisting = kickExisting
            };
            var payload = WireRoomGatewayBinary.Serialize(in request);
            var responsePayload = await _requestClient.SendRequestAsync(
                RoomGatewayOpCodes.AccountLogin,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var response = WireRoomGatewayBinary.Deserialize<WireRoomAccountLoginRes>(responsePayload);
            return new DemoAccountLoginResult(
                response.Success,
                response.SessionToken,
                response.AccountId,
                response.ExpireAtUnixMs,
                response.KickedSessionToken,
                response.Message);
        }

        public static async Task<DemoAccountLoginResult> LoginTcpAsync(
            string host,
            int port,
            string accountId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            using (var connection = new ConnectionManager(
                       () => new TcpTransport(),
                       new ConnectionOptions()))
            {
                connection.Open(host, port);

                // Open 是异步的（TcpTransport.Connect 在后台 Task 里连接），首条请求会立即 send。
                // 必须先等连接建立，否则 TcpTransport._stream 尚未就绪 → 首次 Send 抛 "Not connected"。
                var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
                var connected = await WaitForConnectedAsync(connection, effectiveTimeout, cancellationToken).ConfigureAwait(false);
                if (!connected)
                {
                    return new DemoAccountLoginResult(false, null, accountId, 0, null,
                        $"无法连接到服务器 {host}:{port}（超时 {effectiveTimeout.TotalSeconds}s）。请确认 Orleans Host（silo）与 Orleans Gateway 均已启动。");
                }

                using (var client = new DemoRoomGatewayAccountClient(connection))
                {
                    return await client.AccountLoginAsync(
                        accountId,
                        timeout: timeout,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static async Task<bool> WaitForConnectedAsync(
            IConnection connection,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (connection.IsConnected) return true;

            var deadline = DateTime.UtcNow + timeout;
            while (!connection.IsConnected && DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }

            return connection.IsConnected;
        }

        public void Dispose()
        {
            _requestClient.Dispose();
        }
    }
}
