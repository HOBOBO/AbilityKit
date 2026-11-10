using System;
using System.Globalization;
using System.IO;
using AbilityKit.Ability.Host;

namespace AbilityKit.Game.Battle.Diagnostics.Capture
{
    /// <summary>
    /// L1 local capture writer. It records only observations at existing input
    /// boundaries; it is deliberately independent from combat execution.
    /// </summary>
    internal sealed class BattleCaptureSession : IBattleCaptureSink, IDisposable
    {
        private readonly object _gate = new object();
        private StreamWriter _events;
        private bool _disposed;
        private long _sequence;
        private int? _firstFrame;
        private int? _lastFrame;

        private BattleCaptureSession(string directory, string caseId, string runId)
        {
            Directory = directory;
            CaseId = caseId;
            RunId = runId;
        }

        public string Directory { get; }
        public string CaseId { get; }
        public string RunId { get; }
        public bool IsEnabled => !_disposed && _events != null;

        public static bool TryStart(
            string rootDirectory,
            string caseId,
            string runId,
            out BattleCaptureSession session,
            out string error)
        {
            session = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Capture root is empty.");
                if (string.IsNullOrWhiteSpace(caseId)) throw new ArgumentException("Capture caseId is empty.");
                if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("Capture runId is empty.");

                var directory = Path.GetFullPath(Path.Combine(rootDirectory, caseId, runId));
                System.IO.Directory.CreateDirectory(directory);
                var candidate = new BattleCaptureSession(directory, caseId, runId);
                candidate._events = new StreamWriter(
                    Path.Combine(directory, "input.jsonl"), false, new System.Text.UTF8Encoding(false))
                { AutoFlush = true };
                candidate.WriteManifest("active");
                session = candidate;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                session?.Dispose();
                session = null;
                return false;
            }
        }

        public void RecordHudEvent(int frame, string kind, int slot, int skillId, float inputX, float inputY, float resolvedX, float resolvedY)
        {
            Write("hud", "\"frame\":" + frame + ",\"kind\":\"" + Escape(kind) + "\",\"slot\":" + slot + ",\"skillId\":" + skillId +
                ",\"inputX\":" + Number(inputX) + ",\"inputY\":" + Number(inputY) +
                ",\"resolvedX\":" + Number(resolvedX) + ",\"resolvedY\":" + Number(resolvedY), frame);
        }

        // Compatibility helper for existing internal callers and older tests.
        public void RecordHudEvent(string kind, int slot, int skillId, float inputX, float inputY, float resolvedX, float resolvedY) =>
            RecordHudEvent(0, kind, slot, skillId, inputX, inputY, resolvedX, resolvedY);

        public void RecordInputCommand(in PlayerInputCommand command, bool accepted)
        {
            var payload = command.Payload == null ? string.Empty : Convert.ToBase64String(command.Payload);
            Write("input_command", "\"frame\":" + command.Frame.Value + ",\"player\":\"" +
                Escape(command.Player.Value) + "\",\"opCode\":" + command.OpCode +
                ",\"accepted\":" + (accepted ? "true" : "false") +
                ",\"payloadBase64\":\"" + payload + "\"", command.Frame.Value);
        }

        public void Mark(string label, int frame)
        {
            Write("mark", "\"label\":\"" + Escape(label) + "\",\"frame\":" + frame, frame);
        }

        private void Write(string type, string fields, int? frame = null)
        {
            lock (_gate)
            {
                if (!IsEnabled) return;
                try
                {
                    if (frame.HasValue)
                    {
                        _firstFrame = !_firstFrame.HasValue ? frame.Value : Math.Min(_firstFrame.Value, frame.Value);
                        _lastFrame = !_lastFrame.HasValue ? frame.Value : Math.Max(_lastFrame.Value, frame.Value);
                    }
                    var seq = ++_sequence;
                    _events.WriteLine("{\"schema\":\"moba-battle-capture.v1\",\"seq\":" + seq +
                        ",\"utc\":\"" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                        "\",\"type\":\"" + Escape(type) + "\"," + fields + "}");
                }
                catch
                {
                    // Diagnostics must be fail-open and must never affect a battle tick.
                    try { _events.Dispose(); } catch { }
                    _events = null;
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                try { _events?.Dispose(); } catch { }
                _events = null;
                try
                {
                    WriteManifest("closed");
                }
                catch { }
            }
        }

        private void WriteManifest(string status)
        {
            var first = _firstFrame.HasValue ? _firstFrame.Value.ToString(CultureInfo.InvariantCulture) : "null";
            var last = _lastFrame.HasValue ? _lastFrame.Value.ToString(CultureInfo.InvariantCulture) : "null";
            File.WriteAllText(Path.Combine(Directory, "capture-manifest.json"),
                "{\"schema\":\"moba-battle-capture.v1\",\"captureProtocolVersion\":1,\"caseId\":\"" + Escape(CaseId) +
                "\",\"runId\":\"" + Escape(RunId) + "\",\"source\":\"unity-hud-l1\",\"status\":\"" + status +
                "\",\"firstFrame\":" + first + ",\"lastFrame\":" + last + "}",
                new System.Text.UTF8Encoding(false));
        }

        private static string Number(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? "0" : value.ToString("R", CultureInfo.InvariantCulture);

        private static string Escape(string value)
        {
            if (value == null) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
