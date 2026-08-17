using System;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleHudSkillFailureFeed
    {
        private const int QueryLimit = 64;

        private IBattleDiagnosticEventReadStore _store;
        private int _actorId;
        private long _observedRevision = -1;
        private long _lastSequence;
        private long _requestId;

        public void Bind(IBattleDiagnosticEventReadStore store, int actorId)
        {
            if (store == null || actorId <= 0)
            {
                Reset();
                return;
            }

            if (ReferenceEquals(_store, store) && _actorId == actorId) return;

            _store = store;
            _actorId = actorId;
            _observedRevision = store.Revision;
            _lastSequence = QueryLatestSequence(store, actorId);
        }

        public bool TryReadLatest(out string message)
        {
            message = string.Empty;
            if (_store == null || _actorId <= 0) return false;
            if (_observedRevision == _store.Revision) return false;

            _observedRevision = _store.Revision;
            var result = _store.Query(CreateQuery(_store.Revision, _actorId));
            BattleDiagnosticSkillFailurePayload latestFailure = default;
            var latestFailureSequence = _lastSequence;
            var highestSequence = _lastSequence;
            var foundFailure = false;

            for (var i = 0; i < result.Items.Count; i++)
            {
                var item = result.Items[i];
                if (item.Sequence > highestSequence) highestSequence = item.Sequence;
                if (item.Sequence <= latestFailureSequence ||
                    item.Kind != BattleDiagnosticEventKind.SkillFailure ||
                    !item.Payload.TryGetSkillFailure(out var failure))
                {
                    continue;
                }

                latestFailure = failure;
                latestFailureSequence = item.Sequence;
                foundFailure = true;
            }

            _lastSequence = highestSequence;
            if (!foundFailure) return false;

            message = BattleHudSkillFailureText.Format(latestFailure.Code, latestFailure.Message);
            return true;
        }

        public void Reset()
        {
            _store = null;
            _actorId = 0;
            _observedRevision = -1;
            _lastSequence = 0;
            _requestId = 0;
        }

        private long QueryLatestSequence(IBattleDiagnosticEventReadStore store, int actorId)
        {
            var result = store.Query(CreateQuery(store.Revision, actorId, 1));
            return result.Items.Count > 0 ? result.Items[0].Sequence : 0;
        }

        private BattleDiagnosticEventQuery CreateQuery(long revision, int actorId, int limit = QueryLimit)
        {
            var filter = new BattleDiagnosticFilter(
                BattleDiagnosticFilter.Default.Frames,
                BattleDiagnosticEventChannel.Skill,
                actorId,
                BattleDiagnosticActorRelation.Source,
                failuresOnly: true);
            return new BattleDiagnosticEventQuery(
                ++_requestId,
                filter,
                new BattleDiagnosticPageRequest(revision, 0, limit),
                newestFirst: true);
        }
    }

    internal static class BattleHudSkillFailureText
    {
        public static string Format(string code, string message)
        {
            var detail = ((code ?? string.Empty) + " " + (message ?? string.Empty)).ToLowerInvariant();
            if (ContainsAny(detail, "not_enough_mana", "not enough mana", "insufficient mana"))
                return "蓝量不足";
            if (ContainsAny(detail, "cooldown", "cool down"))
                return "技能冷却中";
            if (ContainsAny(detail, "alreadyrunning", "already running"))
                return "技能正在释放";
            if (ContainsAny(detail, "outofrange", "out of range", "outside cast range"))
                return "超出施法范围";
            if (ContainsAny(detail, "targetmissing", "target missing", "no valid target"))
                return "没有有效目标";
            if (ContainsAny(detail, "invalidslot", "missingskill", "skill not found"))
                return "技能不可用";
            if (ContainsAny(detail, "resource", "not_enough"))
                return "资源不足";
            return "技能释放失败";
        }

        private static bool ContainsAny(string value, params string[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
            {
                if (value.IndexOf(candidates[i], StringComparison.Ordinal) >= 0) return true;
            }

            return false;
        }
    }
}
