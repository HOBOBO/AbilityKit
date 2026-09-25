#nullable enable

using System;
using AbilityKit.Network.Room;
using UnityEngine;

namespace AbilityKit.Demo.Tiny.View
{
    internal sealed class TinyHudPresenter
    {
        private string _joinRoomId = string.Empty;
        private TinySyncMode _createMode;
        private Vector2 _scrollPosition;

        public void Draw(TinyBattleSession? session, string status, string error,
            bool busy, bool recovering, bool canReturn, Action<TinySyncMode> createRoom,
            Action<string> joinRoom, Action ready, Action start, Action back)
        {
            var width = Mathf.Min(360, Mathf.Max(1, Screen.width - 24));
            var height = Mathf.Min(310, Mathf.Max(1, Screen.height - 24));
            var previousEnabled = GUI.enabled;
            GUILayout.BeginArea(new Rect(12, 12, width, height), GUI.skin.window);
            try
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
                GUILayout.Label("Tiny");
                GUILayout.Label(status);
                if (!string.IsNullOrEmpty(error)) GUILayout.Label(error);
                if (!string.IsNullOrEmpty(session?.RoomId))
                    GUILayout.TextField(session.RoomId);

                if (session != null && session.Telemetry.InBattle)
                    DrawTelemetry(session.Telemetry);

                GUI.enabled = session != null && !busy && !recovering && !session.ConnectionUnavailable;
                if (session != null && string.IsNullOrEmpty(session.RoomId))
                {
                    _createMode = (TinySyncMode)GUILayout.Toolbar((int)_createMode,
                        new[] { "State", "Frame", "Hybrid" });
                    if (GUILayout.Button("Create room")) createRoom(_createMode);
                    _joinRoomId = GUILayout.TextField(_joinRoomId);
                    if (GUILayout.Button("Join room")) joinRoom(_joinRoomId);
                }
                else if (session?.Room?.Phase == RoomGatewaySessionPhase.Lobby)
                {
                    if (GUILayout.Button("Ready")) ready();
                    GUI.enabled = GUI.enabled && session.CanStart;
                    if (GUILayout.Button("Start")) start();
                }
                GUI.enabled = canReturn;
                if (GUILayout.Button("Back")) back();
            }
            finally
            {
                GUI.enabled = previousEnabled;
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }
        }

        private static void DrawTelemetry(in TinySyncTelemetry telemetry)
        {
            GUILayout.Label(telemetry.Mode + "  |  " + telemetry.Connection);
            if (telemetry.AwaitingBaseline) GUILayout.Label("Waiting for baseline");
            else if (telemetry.NeedsFullSnapshot) GUILayout.Label("Resynchronizing");
            GUILayout.Label("Authority " + telemetry.AuthoritativeFrame +
                "  |  Local " + telemetry.PredictedFrame);
            GUILayout.Label("Predictions " + telemetry.LocalPredictions +
                "  |  Rollbacks " + telemetry.Rollbacks);
            GUILayout.Label("Corrections " + telemetry.SnapshotCorrections +
                "  |  Recovery " + telemetry.RecoveryRequests +
                "  |  Overflow " + telemetry.FrameOverflows);
        }
    }
}
