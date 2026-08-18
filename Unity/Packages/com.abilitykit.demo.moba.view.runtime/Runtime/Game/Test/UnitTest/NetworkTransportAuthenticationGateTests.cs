using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Runtime.TcpGateway;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class NetworkTransportAuthenticationGateTests
    {
        private const uint RenewSessionOp = 101;
        private const uint PostAuthenticationOp = 102;
        private const uint SubmitInputOp = 103;

        [UnityTest]
        public IEnumerator SendInput_WaitsForRenewAndPostAuthentication()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection);

            transport.Connect();
            Assert.That(connection.Requests, Has.Count.EqualTo(1));
            Assert.That(connection.Requests[0].OpCode, Is.EqualTo(RenewSessionOp));

            var inputTask = transport.SendInputAsync(default(SubmitInputRequest));
            Assert.That(connection.Requests, Has.Count.EqualTo(1));
            Assert.That(transport.IsAuthenticated, Is.False);

            connection.ReplyOk(connection.Requests[0]);
            yield return WaitUntil(
                () => connection.Requests.Count == 2,
                "PostAuthentication request was not sent after RenewSession completed.");
            Assert.That(connection.Requests[1].OpCode, Is.EqualTo(PostAuthenticationOp));
            Assert.That(connection.Requests, Has.Count.EqualTo(2));

            connection.ReplyOk(connection.Requests[1]);
            yield return WaitUntil(
                () => connection.Requests.Count == 3,
                "Input request was not released after authentication completed.");
            Assert.That(connection.Requests[2].OpCode, Is.EqualTo(SubmitInputOp));
            Assert.That(transport.IsAuthenticated, Is.True);

            connection.ReplyOk(connection.Requests[2]);
            yield return WaitUntil(() => inputTask.IsCompleted, "Input task did not complete.");
            Assert.That(inputTask.GetAwaiter().GetResult().Accepted, Is.True);
        }

        [UnityTest]
        public IEnumerator InputQueuedBeforeConnect_UsesUpcomingAuthenticationGeneration()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection);

            var inputTask = transport.SendInputAsync(default(SubmitInputRequest));
            Assert.That(connection.Requests, Is.Empty);

            transport.Connect();
            Assert.That(connection.Requests, Has.Count.EqualTo(1));
            connection.ReplyOk(connection.Requests[0]);
            yield return WaitUntil(
                () => connection.Requests.Count == 2,
                "PostAuthentication request was not sent.");
            connection.ReplyOk(connection.Requests[1]);
            yield return WaitUntil(
                () => connection.Requests.Count == 3,
                "Queued input was not released after authentication.");
            connection.ReplyOk(connection.Requests[2]);

            yield return WaitUntil(() => inputTask.IsCompleted, "Queued input task did not complete.");
            Assert.That(inputTask.GetAwaiter().GetResult().Accepted, Is.True);
        }

        [UnityTest]
        public IEnumerator Reconnect_InvalidatesOldAuthenticationGeneration()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection);
            var failures = 0;
            transport.SubmitInputFailed += _ => Interlocked.Increment(ref failures);

            transport.Connect();
            var firstInput = transport.SendInputAsync(default(SubmitInputRequest));
            var oldRenew = connection.Requests[0];
            connection.RaiseDisconnected();

            yield return WaitUntil(() => failures == 1, "Queued input failure was not published.");
            yield return WaitUntil(() => firstInput.IsCompleted, "Queued input task did not complete.");
            Assert.That(firstInput.GetAwaiter().GetResult().Accepted, Is.False);

            transport.Connect();
            Assert.That(connection.Requests, Has.Count.EqualTo(2));
            connection.ReplyOk(oldRenew);
            yield return null;
            yield return null;
            Assert.That(connection.Requests, Has.Count.EqualTo(2), "Old generation advanced authentication.");
            Assert.That(transport.IsAuthenticated, Is.False);
        }

        [UnityTest]
        public IEnumerator AuthenticationFailure_FailsQueuedInputAndPublishesBothSignals()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection);
            var authenticationFailures = 0;
            var inputFailures = 0;
            transport.AuthenticationFailed += _ => Interlocked.Increment(ref authenticationFailures);
            transport.SubmitInputFailed += _ => Interlocked.Increment(ref inputFailures);

            transport.Connect();
            var inputTask = transport.SendInputAsync(default(SubmitInputRequest));
            connection.RaiseError(new InvalidOperationException("authentication request failed"));

            yield return WaitUntil(
                () => authenticationFailures == 1 && inputFailures == 1,
                "Authentication and queued-input failures were not both published.");
            yield return WaitUntil(() => inputTask.IsCompleted, "Failed input task did not complete.");
            Assert.That(inputTask.GetAwaiter().GetResult().Accepted, Is.False);
            Assert.That(transport.IsAuthenticated, Is.False);
            Assert.That(connection.Requests, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InputDiagnosticsBinding_TracksAcceptedAndFailureAndDetaches()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection, includeAuthentication: false);
            using var binding = new InputSubmissionDiagnosticsBinding();
            binding.Bind(transport);

            transport.Connect();
            yield return WaitUntil(() => transport.IsAuthenticated, "Transport did not authenticate.");
            var acceptedTask = transport.SendInputAsync(default(SubmitInputRequest));
            yield return WaitUntil(() => connection.Requests.Count == 1, "Input request was not sent.");
            connection.ReplyOk(connection.Requests[0]);
            yield return WaitUntil(() => acceptedTask.IsCompleted, "Accepted input task did not complete.");
            acceptedTask.GetAwaiter().GetResult();
            Assert.That(InputSubmissionStatsProvider.Current.CompletedCount, Is.EqualTo(1));
            Assert.That(InputSubmissionStatsProvider.Current.AcceptedCount, Is.EqualTo(1));

            var failedTask = transport.SendInputAsync(default(SubmitInputRequest));
            yield return WaitUntil(
                () => connection.Requests.Count == 2,
                "Second input request was not sent.");
            connection.RaiseDisconnected();
            yield return WaitUntil(
                () => InputSubmissionStatsProvider.Current.FailedCount == 1,
                "In-flight input failure was not counted.");
            yield return WaitUntil(() => failedTask.IsCompleted, "Failed input task did not complete.");
            failedTask.GetAwaiter().GetResult();

            var published = InputSubmissionStatsProvider.Current;
            binding.Dispose();
            Assert.That(InputSubmissionStatsProvider.Current, Is.Null);
            connection.RaiseDisconnected();
            Assert.That(published.FailedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InputDiagnosticsBinding_TracksFinalRejection()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection, includeAuthentication: false);
            using var binding = new InputSubmissionDiagnosticsBinding();
            binding.Bind(transport);

            transport.Connect();
            yield return WaitUntil(() => transport.IsAuthenticated, "Transport did not authenticate.");
            var inputTask = transport.SendInputAsync(default(SubmitInputRequest));
            yield return WaitUntil(() => connection.Requests.Count == 1, "Input request was not sent.");
            connection.Reply(connection.Requests[0], ResponseKind.Rejected);
            yield return WaitUntil(() => inputTask.IsCompleted, "Rejected input task did not complete.");

            var result = inputTask.GetAwaiter().GetResult();
            Assert.That(result.Accepted, Is.False);
            Assert.That(InputSubmissionStatsProvider.Current.CompletedCount, Is.EqualTo(1));
            Assert.That(InputSubmissionStatsProvider.Current.AcceptedCount, Is.Zero);
            Assert.That(InputSubmissionStatsProvider.Current.RejectedCount, Is.EqualTo(1));
            Assert.That(InputSubmissionStatsProvider.Current.FailedCount, Is.Zero);
            Assert.That(InputSubmissionStatsProvider.Current.LastReasonCode, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator InputDiagnosticsBinding_StaleRetryPublishesOnlyFinalResult()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection, includeAuthentication: false);
            using var binding = new InputSubmissionDiagnosticsBinding();
            binding.Bind(transport);

            transport.Connect();
            yield return WaitUntil(() => transport.IsAuthenticated, "Transport did not authenticate.");
            var inputTask = transport.SendInputAsync(default(SubmitInputRequest));
            yield return WaitUntil(() => connection.Requests.Count == 1, "Initial input request was not sent.");
            connection.Reply(connection.Requests[0], ResponseKind.Stale);
            yield return WaitUntil(() => connection.Requests.Count == 2, "Stale input was not retried.");
            Assert.That(InputSubmissionStatsProvider.Current.CompletedCount, Is.Zero);

            connection.Reply(connection.Requests[1], ResponseKind.Accepted);
            yield return WaitUntil(() => inputTask.IsCompleted, "Retried input task did not complete.");

            Assert.That(inputTask.GetAwaiter().GetResult().Accepted, Is.True);
            Assert.That(InputSubmissionStatsProvider.Current.CompletedCount, Is.EqualTo(1));
            Assert.That(InputSubmissionStatsProvider.Current.AcceptedCount, Is.EqualTo(1));
            Assert.That(InputSubmissionStatsProvider.Current.RejectedCount, Is.Zero);
            Assert.That(connection.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public void SessionRuntime_ReplicationBuild_ReplacesLightweightInputDiagnostics()
        {
            var connection = new ControllableConnection();
            using var transport = CreateTransport(connection, includeAuthentication: false);
            var session = new BattleSessionRuntime();
            session.InputSubmissionDiagnostics.Bind(transport);
            var lightweightStats = InputSubmissionStatsProvider.Current;

            var built = session.Replication.Build(
                transport,
                30,
                42UL,
                "battle",
                default,
                _ => { },
                _ => { },
                () => { },
                () => { });

            Assert.That(built, Is.True);
            Assert.That(session.InputSubmissionDiagnostics.IsBound, Is.False);
            Assert.That(InputSubmissionStatsProvider.Current, Is.Not.Null);
            Assert.That(InputSubmissionStatsProvider.Current, Is.Not.SameAs(lightweightStats));

            session.DisposeReplication();
            Assert.That(InputSubmissionStatsProvider.Current, Is.Null);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string failureMessage)
        {
            var deadline = DateTime.UtcNow.AddSeconds(1);
            while (!predicate() && DateTime.UtcNow < deadline)
            {
                yield return null;
            }

            Assert.That(predicate(), Is.True, failureMessage);
        }

        private static NetworkTransport CreateTransport(
            ControllableConnection connection,
            bool includeAuthentication = true)
        {
            return new NetworkTransport(new NetworkTransportOptions
            {
                ConnectionFactory = () => connection,
                OpRenewSession = includeAuthentication ? RenewSessionOp : 0,
                SessionToken = includeAuthentication ? "session" : null,
                SerializeRenewSession = _ => default,
                OpPostAuthentication = includeAuthentication ? PostAuthenticationOp : 0,
                SerializePostAuthentication = () => default,
                OpSubmitInput = SubmitInputOp,
                SerializeSubmitInput = _ => default,
                RewriteSubmitInputFrame = (request, _) => request,
                DeserializeSubmitInputResponse = DeserializeSubmitInputResponse
            });
        }

        private sealed class ControllableConnection : IConnection
        {
            internal readonly List<Request> Requests = new List<Request>();

            public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
            public bool IsConnected => State == ConnectionState.Connected;

            public event Action Connected;
            public event Action Disconnected;
            public event Action<Exception> Error;
            public event Action<uint, uint, ArraySegment<byte>> PacketReceived;
            public event Action<uint, ArraySegment<byte>> ServerPushReceived;
            public event Action<string, string> Kicked;

            public void Open(string host, int port)
            {
                State = ConnectionState.Connected;
                Connected?.Invoke();
            }

            public void Close()
            {
                RaiseDisconnected();
            }

            public void Tick(float deltaTime)
            {
            }

            public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
            {
                lock (Requests)
                {
                    Requests.Add(new Request(opCode, seq));
                }
            }

            public void Dispose()
            {
            }

            internal void ReplyOk(Request request)
            {
                Reply(request, ResponseKind.Accepted);
            }

            internal void Reply(Request request, ResponseKind kind)
            {
                var response = new byte[5];
                BinaryPrimitives.WriteInt32LittleEndian(response, (int)TcpGatewayStatusCode.Ok);
                response[4] = (byte)kind;
                PacketReceived?.Invoke(request.OpCode, request.Seq, new ArraySegment<byte>(response));
            }

            internal void RaiseDisconnected()
            {
                State = ConnectionState.Disconnected;
                Disconnected?.Invoke();
            }

            internal void RaiseError(Exception exception)
            {
                Error?.Invoke(exception);
            }

            internal readonly struct Request
            {
                internal Request(uint opCode, uint seq)
                {
                    OpCode = opCode;
                    Seq = seq;
                }

                internal uint OpCode { get; }
                internal uint Seq { get; }
            }
        }

        private static NetworkSubmitInputResponse DeserializeSubmitInputResponse(ArraySegment<byte> payload)
            {
                var kind = payload.Count > 0
                    ? (ResponseKind)payload.Array[payload.Offset]
                    : ResponseKind.Accepted;
                switch (kind)
                {
                    case ResponseKind.Rejected:
                        return new NetworkSubmitInputResponse(
                            accepted: false,
                            serverFrame: 20,
                            reasonCode: 7,
                            retryAtAuthoritativeFrame: false,
                            status: "rejected");
                    case ResponseKind.Stale:
                        return new NetworkSubmitInputResponse(
                            accepted: false,
                            serverFrame: 20,
                            reasonCode: 3,
                            retryAtAuthoritativeFrame: true,
                            status: "stale");
                    default:
                        return new NetworkSubmitInputResponse(
                            accepted: true,
                            serverFrame: 12,
                            reasonCode: 0,
                            retryAtAuthoritativeFrame: false,
                            status: "ok",
                            acceptedFrame: 12);
                }
            }

        private enum ResponseKind : byte
        {
            Accepted,
            Rejected,
            Stale
        }
    }
}
