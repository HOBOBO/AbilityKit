using AbilityKit.Demo.Moba.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Editor.Diagnostics
{
    internal static class BattleDebugOfflineRelations
    {
        public static bool Draw(in BattleDebugContext ctx, BattleDiagnosticSessionScope scope,
            string role, BattleDiagnosticRuntimeObjectReference reference, int frame)
        {
            if (!(ctx.DiagnosticSession is BattleDiagnosticOfflineSession session)) return false;
            if (!reference.HasRuntimeId)
            {
                EditorGUILayout.LabelField($"{role}：未记录引用", EditorStyles.wordWrappedMiniLabel);
                return true;
            }

            var relation = session.ResolveRelation(scope, in reference, frame);
            EditorGUILayout.LabelField($"{role}：{Format(in reference, in relation)}", EditorStyles.wordWrappedMiniLabel);
            if (relation.ActorSnapshot.HasValue && ctx.SelectActor != null &&
                GUILayout.Button("查看同帧实体", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                ctx.WorkspaceState?.SetFrame(frame);
                ctx.SelectActor.Invoke(reference.RuntimeId);
            }
            return true;
        }

        public static bool DrawEvent(in BattleDebugContext ctx, in BattleDiagnosticEvent item)
        {
            if (!Draw(in ctx, item.Scope, "来源", item.SourceActor, item.Frame)) return false;
            Draw(in ctx, item.Scope, "目标", item.TargetActor, item.Frame);
            if (item.SubjectObject.HasRuntimeId &&
                ctx.DiagnosticSession is BattleDiagnosticOfflineSession session)
            {
                Draw(in ctx, item.Scope, "事件对象", item.SubjectObject, item.Frame);
                var reference = item.SubjectObject;
                var entry = session.ResolveRelation(item.Scope, in reference, item.Frame).CatalogEntry;
                if (entry.HasValue) DrawObjectActors(in ctx, entry.Value, item.Frame);
            }
            return true;
        }

        public static bool DrawObjectActors(in BattleDebugContext ctx, in BattleDiagnosticRuntimeObject item, int frame)
        {
            if (!(ctx.DiagnosticSession is BattleDiagnosticOfflineSession session)) return false;
            var scope = session.SessionInfo.Scope;
            DrawActor(in ctx, scope, "关联实体", item.RelatedActorId, frame);
            DrawActor(in ctx, scope, "对象来源", item.SourceActorId, frame);
            DrawActor(in ctx, scope, "所有者", item.OwnerActorId, frame);
            DrawActor(in ctx, scope, "对象目标", item.TargetActorId, frame);
            return true;
        }

        public static int ObjectFrame(in BattleDiagnosticRuntimeObject item) => item.CreatedFrame >= 0
            ? item.CreatedFrame
            : item.BackfilledFrame >= 0 ? item.BackfilledFrame : item.DestroyedFrame;

        public static bool DrawActor(in BattleDebugContext ctx, BattleDiagnosticSessionScope scope,
            string role, long id, int frame)
        {
            return Draw(in ctx, scope, role, BattleDiagnosticRuntimeObjectReference.Create(
                BattleDiagnosticRuntimeObjectKind.Actor, id, 0), frame);
        }

        internal static string Format(in BattleDiagnosticRuntimeObjectReference reference,
            in BattleDiagnosticOfflineRelation relation)
        {
            if (relation.IsAmbiguous) return $"{reference} · 同帧存在多代，未匹配";
            if (relation.ActorSnapshot.HasValue)
            {
                var actor = relation.ActorSnapshot.Value;
                var generation = actor.Generation > 0 ? $":{actor.Generation}" : "（未记录代次）";
                var owner = relation.CatalogEntry?.OwnerActorId ?? 0L;
                var ownerText = owner > 0 ? $" · 所有者 #{owner}" : string.Empty;
                var match = reference.IsResolved ? string.Empty : " · 按帧匹配，引用未记录代次";
                return $"#{actor.ActorId}{generation} · {BattleDebugDisplayText.ActorKind(actor.Kind)} {actor.DisplayName}" +
                       $" · 配置 {actor.ConfigId} · F{actor.Frame} 同帧快照 · 阵营 {actor.TeamId}" +
                       $" · HP {actor.Health:0.##}/{actor.MaximumHealth:0.##}{ownerText}{match}";
            }
            if (relation.CatalogEntry.HasValue)
            {
                var entry = relation.CatalogEntry.Value;
                var kind = entry.ActorKind != BattleDiagnosticActorKind.Unknown
                    ? BattleDebugDisplayText.ActorKind(entry.ActorKind) : BattleDebugDisplayText.RuntimeObjectKind(entry.Kind);
                var owner = entry.OwnerActorId > 0 ? $" · 所有者 #{entry.OwnerActorId}" : string.Empty;
                var match = reference.IsResolved ? string.Empty : " · 按帧匹配，引用未记录代次";
                return $"{entry.Reference} · {kind} {entry.DisplayName} · 配置 {entry.DefinitionId}" +
                       $" · 生命周期记录（无同帧状态）{owner}{match}";
            }
            return $"{reference} · 仅有引用，未找到对应证据";
        }
    }
}
