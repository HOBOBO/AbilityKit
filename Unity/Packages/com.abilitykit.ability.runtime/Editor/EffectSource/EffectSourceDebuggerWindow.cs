#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class EffectSourceDebuggerWindow : EditorWindow
    {
        private enum RootSortMode
        {
            LastTouchedFrameDesc = 0,
            KindThenConfigId = 1,
            ConfigIdThenKind = 2,
        }
        [MenuItem("Window/AbilityKit/Effect Source Debugger")]
        static void Open()
        {
            GetWindow<EffectSourceDebuggerWindow>(utility: false, title: "Effect Source Debugger");
        }

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        private long _selectedContextId;
        private long _selectedRootId;

        private string _search;
        private bool _onlyActive;
        private bool _autoRefresh = true;
        private double _lastAutoRefreshTime;
        private const double AutoRefreshIntervalSeconds = 0.5;

        private string _rootKindFilter;
        private int _rootConfigIdFilter;
        private RootSortMode _rootSortMode = RootSortMode.LastTouchedFrameDesc;

        private bool _anomalyMode;
        private int _inactiveBigTreeThreshold = 50;
        private int _staleFramesThreshold = 300;
        private int _activeCountMismatchThreshold = 1;

        private readonly Dictionary<long, bool> _foldouts = new Dictionary<long, bool>(256);

        private readonly List<long> _rootIds = new List<long>(128);
        private readonly List<long> _anomalyRootIds = new List<long>(64);
        private readonly List<EffectSourceSnapshot> _chain = new List<EffectSourceSnapshot>(64);
        private readonly List<KeyValuePair<int, int>> _bbInts = new List<KeyValuePair<int, int>>(64);
        private readonly List<long> _tmpChildren = new List<long>(16);

        void OnEnable()
        {
            EffectSourceLiveRegistry.Changed += Repaint;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EffectSourceLiveRegistry.Changed -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            if (!_autoRefresh) return;

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastAutoRefreshTime < AutoRefreshIntervalSeconds) return;
            _lastAutoRefreshTime = now;

            var reg = EffectSourceLiveRegistry.GetCurrent();
            if (reg == null) return;

            RebuildRootList(reg);
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                _selectedContextId = 0;
                _selectedRootId = 0;
                _rootIds.Clear();
            }
        }

        void OnGUI()
        {
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                EditorGUILayout.LabelField("Play Mode Only", EditorStyles.boldLabel);
                EditorGUILayout.Space(6);

                var reg = EffectSourceLiveRegistry.GetCurrent();
                if (reg == null)
                {
                    EditorGUILayout.HelpBox("No EffectSourceRegistry instance found. Ensure the world is running and EffectSourceRegistry is registered.", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _search = EditorGUILayout.TextField("Search", _search);
                        _onlyActive = EditorGUILayout.ToggleLeft("Only Active", _onlyActive, GUILayout.Width(90));
                        _autoRefresh = EditorGUILayout.ToggleLeft("Auto Refresh", _autoRefresh, GUILayout.Width(110));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _rootKindFilter = EditorGUILayout.TextField("Root Kind", _rootKindFilter);
                        _rootConfigIdFilter = EditorGUILayout.IntField("Root ConfigId", _rootConfigIdFilter);
                        _rootSortMode = (RootSortMode)EditorGUILayout.EnumPopup("Sort", _rootSortMode);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _anomalyMode = EditorGUILayout.ToggleLeft("Anomaly Mode", _anomalyMode, GUILayout.Width(120));
                        _inactiveBigTreeThreshold = EditorGUILayout.IntField("Inactive BigTree>=", _inactiveBigTreeThreshold);
                        _staleFramesThreshold = EditorGUILayout.IntField("StaleFrames>=", _staleFramesThreshold);
                        _activeCountMismatchThreshold = EditorGUILayout.IntField("CountMismatch>=", _activeCountMismatchThreshold);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh"))
                    {
                        RebuildRootList(reg);
                    }

                    if (GUILayout.Button("Expand Selected Root"))
                    {
                        ExpandSelectedRoot(reg);
                    }

                    if (GUILayout.Button("Collapse All"))
                    {
                        _foldouts.Clear();
                    }

                    if (GUILayout.Button("Copy Anomaly Report"))
                    {
                        CopyAnomalyReport(reg);
                    }

                    GUILayout.FlexibleSpace();

                    EditorGUILayout.LabelField($"Contexts: {reg.ContextCount}   Roots: {reg.RootCount}", GUILayout.Width(240));
                }

                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawLeftTree(reg);
                    DrawRightDetail(reg);
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use the Effect Source Debugger.", MessageType.Info);
            }
        }

        private void RebuildRootList(EffectSourceRegistry reg)
        {
            reg.CopyRootIdsTo(_rootIds);

            RebuildAnomalyList(reg);

            _rootIds.Sort((a, b) =>
            {
                var okA = reg.TryGetRootState(a, out var sa);
                var okB = reg.TryGetRootState(b, out var sb);
                if (!okA && !okB) return 0;
                if (!okA) return 1;
                if (!okB) return -1;

                if (_rootSortMode == RootSortMode.LastTouchedFrameDesc)
                {
                    return sb.LastTouchedFrame.CompareTo(sa.LastTouchedFrame);
                }

                var hasSnapA = reg.TryGetSnapshot(a, out var snapA);
                var hasSnapB = reg.TryGetSnapshot(b, out var snapB);
                if (!hasSnapA && !hasSnapB) return sb.LastTouchedFrame.CompareTo(sa.LastTouchedFrame);
                if (!hasSnapA) return 1;
                if (!hasSnapB) return -1;

                if (_rootSortMode == RootSortMode.KindThenConfigId)
                {
                    var k = string.Compare(snapA.Kind.ToString(), snapB.Kind.ToString(), StringComparison.Ordinal);
                    if (k != 0) return k;
                    var c = snapA.ConfigId.CompareTo(snapB.ConfigId);
                    if (c != 0) return c;
                    return sb.LastTouchedFrame.CompareTo(sa.LastTouchedFrame);
                }

                if (_rootSortMode == RootSortMode.ConfigIdThenKind)
                {
                    var c = snapA.ConfigId.CompareTo(snapB.ConfigId);
                    if (c != 0) return c;
                    var k = string.Compare(snapA.Kind.ToString(), snapB.Kind.ToString(), StringComparison.Ordinal);
                    if (k != 0) return k;
                    return sb.LastTouchedFrame.CompareTo(sa.LastTouchedFrame);
                }

                return sb.LastTouchedFrame.CompareTo(sa.LastTouchedFrame);
            });

            if (_selectedRootId != 0 && !_rootIds.Contains(_selectedRootId))
            {
                _selectedRootId = 0;
                _selectedContextId = 0;
            }
        }

        private void DrawLeftTree(EffectSourceRegistry reg)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.45f)))
            {
                EditorGUILayout.LabelField("Roots", EditorStyles.boldLabel);

                _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

                if (_anomalyMode)
                {
                    DrawAnomalyRoots(reg);
                    EditorGUILayout.Space(6);
                }

                if (_rootIds.Count == 0)
                {
                    EditorGUILayout.HelpBox("Click Refresh to collect roots.", MessageType.None);
                }
                else
                {
                    var lastKind = string.Empty;
                    for (int i = 0; i < _rootIds.Count; i++)
                    {
                        if (!reg.TryGetSnapshot(_rootIds[i], out var rootSnap))
                        {
                            continue;
                        }
                        var kindStr = rootSnap.Kind.ToString();
                        if (!string.Equals(kindStr, lastKind, StringComparison.Ordinal))
                        {
                            EditorGUILayout.Space(6);
                            EditorGUILayout.LabelField(kindStr, EditorStyles.boldLabel);
                            lastKind = kindStr;
                        }

                        DrawContextNodeRecursive(reg, _rootIds[i], depth: 0);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void RebuildAnomalyList(EffectSourceRegistry reg)
        {
            _anomalyRootIds.Clear();
            if (reg == null) return;
            if (_rootIds.Count == 0) return;

            for (int i = 0; i < _rootIds.Count; i++)
            {
                var rootId = _rootIds[i];
                if (!reg.TryGetRootStats(rootId, out var st)) continue;
                if (IsAnomalous(reg, in st))
                {
                    _anomalyRootIds.Add(rootId);
                }
            }

            _anomalyRootIds.Sort((a, b) =>
            {
                var okA = reg.TryGetRootStats(a, out var sa);
                var okB = reg.TryGetRootStats(b, out var sb);
                if (!okA && !okB) return 0;
                if (!okA) return 1;
                if (!okB) return -1;
                var pa = GetAnomalyScore(reg, in sa);
                var pb = GetAnomalyScore(reg, in sb);
                return pb.CompareTo(pa);
            });
        }

        private void DrawAnomalyRoots(EffectSourceRegistry reg)
        {
            EditorGUILayout.LabelField("Anomalies", EditorStyles.boldLabel);

            if (_anomalyRootIds.Count == 0)
            {
                EditorGUILayout.HelpBox("No anomalies detected with current thresholds.", MessageType.None);
                return;
            }

            for (int i = 0; i < _anomalyRootIds.Count; i++)
            {
                var rootId = _anomalyRootIds[i];
                if (!reg.TryGetRootStats(rootId, out var st)) continue;
                if (!reg.TryGetSnapshot(rootId, out var snap)) continue;

                var score = GetAnomalyScore(reg, in st);
                var label = $"[{score}] root={rootId} kind={snap.Kind} cfg={snap.ConfigId} nodes={st.SubtreeNodeCount} activeNodes={st.ActiveNodeCount} activeCount={st.ActiveCount} ext={st.ExternalRefCount} lastTouch={st.LastTouchedFrame}";

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(label, EditorStyles.miniButton))
                    {
                        _selectedRootId = rootId;
                        _selectedContextId = rootId;
                        ExpandSelectedRoot(reg);
                    }
                }
            }
        }

        private bool IsAnomalous(EffectSourceRegistry reg, in EffectSourceRegistry.RootStats st)
        {
            var nowFrame = reg != null ? reg.LastFrame : 0;

            // A) root 计数认为已经结束，但仍存在未结束节点（强烈怀疑漏 End）
            if (st.ActiveCount <= 0 && st.ActiveNodeCount >= _activeCountMismatchThreshold) return true;

            // B) root 已无外部引用/计数为 0，但子树非常大（常见于 keepEndedFrames 过长或 purge 没跑/没触发）
            if (st.ActiveCount == 0 && st.ExternalRefCount == 0 && st.SubtreeNodeCount >= _inactiveBigTreeThreshold) return true;

            // C) root 仍活跃，但长时间未触碰（卡死/未 tick/逻辑链断）
            if (st.ActiveCount > 0 && nowFrame > 0 && (nowFrame - st.LastTouchedFrame) >= _staleFramesThreshold) return true;

            return false;
        }

        private int GetAnomalyScore(EffectSourceRegistry reg, in EffectSourceRegistry.RootStats st)
        {
            var score = 0;
            var nowFrame = reg != null ? reg.LastFrame : 0;

            if (st.ActiveCount <= 0 && st.ActiveNodeCount >= _activeCountMismatchThreshold)
            {
                score += 1000 + st.ActiveNodeCount;
            }

            if (st.ActiveCount == 0 && st.ExternalRefCount == 0 && st.SubtreeNodeCount >= _inactiveBigTreeThreshold)
            {
                score += 200 + st.SubtreeNodeCount;
            }

            if (st.ActiveCount > 0 && nowFrame > 0)
            {
                var stale = nowFrame - st.LastTouchedFrame;
                if (stale >= _staleFramesThreshold)
                {
                    score += 100 + stale;
                }
            }

            return score;
        }

        private void ExpandSelectedRoot(EffectSourceRegistry reg)
        {
            if (reg == null) return;
            if (_selectedRootId <= 0) return;

            ExpandSubtree(_selectedRootId);

            void ExpandSubtree(long id)
            {
                if (!reg.TryGetChildren(id, out var children) || children == null || children.Count == 0) return;

                _foldouts[id] = true;
                for (int i = 0; i < children.Count; i++)
                {
                    ExpandSubtree(children[i]);
                }
            }
        }

        private void CopyAnomalyReport(EffectSourceRegistry reg)
        {
            if (reg == null) return;
            RebuildAnomalyList(reg);

            var text = $"EffectSource Anomaly Report (frame={reg.LastFrame})";
            for (int i = 0; i < _anomalyRootIds.Count; i++)
            {
                var rootId = _anomalyRootIds[i];
                if (!reg.TryGetRootStats(rootId, out var st)) continue;
                if (!reg.TryGetSnapshot(rootId, out var snap)) continue;
                var score = GetAnomalyScore(reg, in st);
                text += $"\n[{score}] root={rootId} kind={snap.Kind} cfg={snap.ConfigId} nodes={st.SubtreeNodeCount} activeNodes={st.ActiveNodeCount} oldestActiveCreated={st.OldestActiveCreatedFrame} activeCount={st.ActiveCount} ext={st.ExternalRefCount} lastTouch={st.LastTouchedFrame}";
            }

            EditorGUIUtility.systemCopyBuffer = text;
        }

        private void DrawContextNodeRecursive(EffectSourceRegistry reg, long contextId, int depth)
        {
            if (!reg.TryGetSnapshot(contextId, out var snap)) return;

            if (snap.ParentId == 0 && !ShouldShowRoot(reg, in snap))
            {
                return;
            }

            if (!ShouldShowNode(reg, in snap, out var selfMatch, out var descendantMatch))
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 14);

                var hasChildren = reg.TryGetChildren(contextId, out var childrenProbe) && childrenProbe != null && childrenProbe.Count > 0;
                if (hasChildren)
                {
                    var isOpen = GetFoldout(contextId, depth);
                    var foldoutRect = GUILayoutUtility.GetRect(12, EditorGUIUtility.singleLineHeight, GUILayout.Width(12));
                    var newOpen = EditorGUI.Foldout(foldoutRect, isOpen, GUIContent.none, false);
                    if (newOpen != isOpen) _foldouts[contextId] = newOpen;
                }
                else
                {
                    GUILayout.Space(14);
                }

                var label = $"{snap.ContextId} [{snap.Kind}] cfg={snap.ConfigId} src={snap.SourceActorId} tgt={snap.TargetActorId}";
                var isSelected = _selectedContextId == snap.ContextId;
                var style = isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton;

                var prevColor = GUI.color;
                if (snap.IsEnded) GUI.color = new Color(prevColor.r, prevColor.g, prevColor.b, 0.65f);

                var highlight = _selectedRootId != 0 && snap.RootId == _selectedRootId;
                if (highlight && !isSelected)
                {
                    GUI.color = Color.Lerp(GUI.color, new Color(1f, 0.92f, 0.55f, 1f), 0.35f);
                }

                if (GUILayout.Button(label, style))
                {
                    _selectedContextId = snap.ContextId;
                    _selectedRootId = snap.RootId;
                }

                GUI.color = prevColor;
            }

            if (GetFoldout(contextId, depth) && reg.TryGetChildren(contextId, out var children) && children != null)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    DrawContextNodeRecursive(reg, children[i], depth + 1);
                }
            }
        }

        private bool GetFoldout(long contextId, int depth)
        {
            if (contextId <= 0) return true;
            if (_foldouts.TryGetValue(contextId, out var v)) return v;

            // Default open for roots, closed for deeper nodes (but remembered after first interaction)
            var isOpen = depth == 0;
            _foldouts[contextId] = isOpen;
            return isOpen;
        }

        private bool ShouldShowRoot(EffectSourceRegistry reg, in EffectSourceSnapshot rootSnap)
        {
            if (string.IsNullOrWhiteSpace(_rootKindFilter) && _rootConfigIdFilter <= 0) return true;

            var ok = true;
            if (!string.IsNullOrWhiteSpace(_rootKindFilter))
            {
                ok &= rootSnap.Kind.ToString().IndexOf(_rootKindFilter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            }

            if (_rootConfigIdFilter > 0)
            {
                ok &= rootSnap.ConfigId == _rootConfigIdFilter;
            }

            return ok;
        }

        private bool ShouldShowNode(EffectSourceRegistry reg, in EffectSourceSnapshot snap, out bool selfMatch, out bool descendantMatch)
        {
            selfMatch = IsMatch(in snap);

            if (_onlyActive && snap.IsEnded)
            {
                selfMatch = false;
            }

            descendantMatch = false;
            if (reg.TryGetChildren(snap.ContextId, out var children) && children != null && children.Count > 0)
            {
                _tmpChildren.Clear();
                for (int i = 0; i < children.Count; i++) _tmpChildren.Add(children[i]);

                for (int i = 0; i < _tmpChildren.Count; i++)
                {
                    if (!reg.TryGetSnapshot(_tmpChildren[i], out var childSnap)) continue;
                    if (ShouldShowNode(reg, in childSnap, out _, out _))
                    {
                        descendantMatch = true;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(_search) && !_onlyActive)
            {
                return true;
            }

            return selfMatch || descendantMatch;
        }

        private bool IsMatch(in EffectSourceSnapshot snap)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            var s = _search.Trim();

            if (snap.ContextId.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (snap.RootId.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (snap.ParentId.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (snap.ConfigId.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (snap.SourceActorId.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (snap.TargetActorId.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (snap.Kind.ToString().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private void DrawRightDetail(EffectSourceRegistry reg)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Details", EditorStyles.boldLabel);

                _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

                if (_selectedContextId == 0)
                {
                    EditorGUILayout.HelpBox("Select a node from the left tree.", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                if (!reg.TryGetSnapshot(_selectedContextId, out var snap))
                {
                    EditorGUILayout.HelpBox("Selected node no longer exists (may have been purged).", MessageType.Warning);
                    EditorGUILayout.EndScrollView();
                    return;
                }

                EditorGUILayout.LabelField("Snapshot", EditorStyles.boldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy ContextId", GUILayout.Width(120)))
                    {
                        EditorGUIUtility.systemCopyBuffer = snap.ContextId.ToString();
                    }
                    if (GUILayout.Button("Copy RootId", GUILayout.Width(120)))
                    {
                        EditorGUIUtility.systemCopyBuffer = snap.RootId.ToString();
                    }
                    if (GUILayout.Button("Copy Chain", GUILayout.Width(120)))
                    {
                        CopyChain(reg, snap.ContextId);
                    }
                }

                DrawKeyValue("ContextId", snap.ContextId.ToString());
                DrawKeyValue("RootId", snap.RootId.ToString());
                DrawKeyValue("ParentId", snap.ParentId.ToString());
                DrawKeyValue("Kind", snap.Kind.ToString());
                DrawKeyValue("ConfigId", snap.ConfigId.ToString());
                DrawKeyValue("SourceActorId", snap.SourceActorId.ToString());
                DrawKeyValue("TargetActorId", snap.TargetActorId.ToString());
                DrawKeyValue("CreatedFrame", snap.CreatedFrame.ToString());
                DrawKeyValue("EndedFrame", snap.EndedFrame.ToString());
                DrawKeyValue("EndReason", snap.EndReason.ToString());

                EditorGUILayout.Space(8);

                if (reg.TryGetOrigin(_selectedContextId, out var os, out var ot))
                {
                    EditorGUILayout.LabelField("Origin", EditorStyles.boldLabel);
                    DrawKeyValue("OriginSource", os != null ? os.ToString() : "null");
                    DrawKeyValue("OriginTarget", ot != null ? ot.ToString() : "null");
                    EditorGUILayout.Space(8);
                }

                if (reg.TryGetRootState(snap.RootId, out var rs))
                {
                    EditorGUILayout.LabelField("RootState", EditorStyles.boldLabel);
                    DrawKeyValue("ActiveCount", rs.ActiveCount.ToString());
                    DrawKeyValue("ExternalRefCount", rs.ExternalRefCount.ToString());
                    DrawKeyValue("LastTouchedFrame", rs.LastTouchedFrame.ToString());
                    EditorGUILayout.Space(8);
                }

                EditorGUILayout.LabelField("Chain (to root)", EditorStyles.boldLabel);
                if (reg.TryBuildChain(_selectedContextId, _chain))
                {
                    for (int i = 0; i < _chain.Count; i++)
                    {
                        var c = _chain[i];
                        EditorGUILayout.LabelField($"{i}: {c.ContextId} [{c.Kind}] cfg={c.ConfigId}");
                    }
                }

                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Root Blackboard (int -> int)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Copy Blackboard", GUILayout.Width(140)))
                    {
                        CopyBlackboard(reg, snap.RootId);
                    }
                }
                if (reg.TryCopyRootBlackboardInts(snap.RootId, _bbInts))
                {
                    for (int i = 0; i < _bbInts.Count; i++)
                    {
                        var kv = _bbInts[i];
                        EditorGUILayout.LabelField($"{kv.Key} = {kv.Value}");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No root blackboard exists for this root yet.", MessageType.None);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawKeyValue(string key, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(key, GUILayout.Width(130));
                EditorGUILayout.SelectableLabel(value ?? string.Empty, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private void CopyChain(EffectSourceRegistry reg, long contextId)
        {
            if (reg == null) return;
            if (contextId <= 0) return;
            if (!reg.TryBuildChain(contextId, _chain)) return;

            var text = string.Empty;
            for (int i = 0; i < _chain.Count; i++)
            {
                var c = _chain[i];
                if (i > 0) text += "\n";
                text += $"{i}: {c.ContextId} root={c.RootId} parent={c.ParentId} kind={c.Kind} cfg={c.ConfigId} src={c.SourceActorId} tgt={c.TargetActorId} ended={c.EndedFrame}";
            }

            EditorGUIUtility.systemCopyBuffer = text;
        }

        private void CopyBlackboard(EffectSourceRegistry reg, long rootId)
        {
            if (reg == null) return;
            if (rootId <= 0) return;
            if (!reg.TryCopyRootBlackboardInts(rootId, _bbInts)) return;

            var text = string.Empty;
            for (int i = 0; i < _bbInts.Count; i++)
            {
                var kv = _bbInts[i];
                if (i > 0) text += "\n";
                text += $"{kv.Key}={kv.Value}";
            }

            EditorGUIUtility.systemCopyBuffer = text;
        }

        
    }
}

#endif
