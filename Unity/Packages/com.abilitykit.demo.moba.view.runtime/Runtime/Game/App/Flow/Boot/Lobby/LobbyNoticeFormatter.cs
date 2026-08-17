using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    internal static class LobbyNoticeFormatter
    {
        public static string FormatMembership(ClientRoomMembershipChange change)
        {
            if (change == null) return string.Empty;

            var messages = new List<string>();
            for (var i = 0; i < change.LeftAccountIds.Count; i++)
            {
                messages.Add(change.LeftAccountIds[i] + " left the room.");
            }
            for (var i = 0; i < change.JoinedAccountIds.Count; i++)
            {
                messages.Add(change.JoinedAccountIds[i] + " joined the room.");
            }
            if (change.OwnerChanged && !string.IsNullOrWhiteSpace(change.CurrentOwnerAccountId))
            {
                messages.Add(change.CurrentOwnerAccountId + " is now room owner.");
            }

            return string.Join(" ", messages);
        }

        public static string FormatPlayerState(ClientRoomPlayerStateChanges changes)
        {
            if (changes?.Changes == null || changes.Changes.Count == 0) return string.Empty;

            var messages = new List<string>();
            for (var i = 0; i < changes.Changes.Count; i++)
            {
                var change = changes.Changes[i];
                if (change.OnlineChanged)
                {
                    messages.Add(change.CurrentOnline
                        ? change.AccountId + " reconnected."
                        : change.AccountId + " went offline.");
                }

                if (change.ReadyChanged)
                {
                    messages.Add(change.CurrentReady
                        ? change.AccountId + " is ready."
                        : change.AccountId + " is no longer ready.");
                }
                else if (change.LoadoutChanged && change.CurrentHeroId > 0)
                {
                    messages.Add(change.AccountId + " selected Hero " + change.CurrentHeroId + ".");
                }
            }

            return string.Join(" ", messages);
        }

        public static string FormatPhaseRollback(
            ClientRoomSnapshot previous,
            ClientRoomSnapshot current)
        {
            if (previous == null ||
                current == null ||
                (previous.Phase != ClientRoomPhase.Loading &&
                 previous.Phase != ClientRoomPhase.Starting) ||
                current.Phase != ClientRoomPhase.Lobby)
            {
                return string.Empty;
            }

            return current.PhaseReason switch
            {
                "LockedMemberLeft" => "Loading was cancelled because a player left.",
                "LoadingTimeout" => "Loading timed out. The room returned to the lobby.",
                _ => "Loading was cancelled. The room returned to the lobby."
            };
        }
    }
}
