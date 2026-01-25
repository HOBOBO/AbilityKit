using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace AbilityKit.Editor.GamplayTag
{
    internal sealed class GameplayTagTreeView : TreeView
    {
        public Action OnDatabaseChanged;

        private readonly GameplayTagDatabase _db;

        private readonly Dictionary<int, string> _idToFullName = new Dictionary<int, string>();
        private readonly Dictionary<string, GameplayTagDatabase.Entry> _byName = new Dictionary<string, GameplayTagDatabase.Entry>(StringComparer.Ordinal);
        private int _nextId = 1;

        public GameplayTagTreeView(TreeViewState state, MultiColumnHeader header, GameplayTagDatabase db)
            : base(state, header)
        {
            _db = db;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            rowHeight = EditorGUIUtility.singleLineHeight + 2f;
            Reload();
        }

        public static MultiColumnHeaderState.Column[] CreateDefaultColumns()
        {
            return new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Tag"),
                    width = 400,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Comment"),
                    width = 320,
                    autoResize = true,
                    allowToggleVisibility = false
                }
            };
        }

        protected override TreeViewItem BuildRoot()
        {
            _idToFullName.Clear();
            _byName.Clear();
            _nextId = 1;

            var root = new TreeViewItem(0, -1, "ROOT");

            var entries = _db?.Entries ?? Array.Empty<GameplayTagDatabase.Entry>();
            var normalized = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;
                if (GameplayTagValidation.TryNormalize(e.Name, out var n))
                {
                    normalized.Add(n);
                    _byName[n] = e;
                }
            }

            normalized.Sort(StringComparer.Ordinal);

            var map = new Dictionary<string, TreeViewItem>(StringComparer.Ordinal)
            {
                [string.Empty] = root
            };

            for (int i = 0; i < normalized.Count; i++)
            {
                var full = normalized[i];
                var parts = full.Split('.');

                var curPath = string.Empty;
                TreeViewItem parent = root;

                for (int p = 0; p < parts.Length; p++)
                {
                    curPath = curPath.Length == 0 ? parts[p] : curPath + "." + parts[p];
                    if (map.TryGetValue(curPath, out var existing))
                    {
                        parent = existing;
                        continue;
                    }

                    var id = _nextId++;
                    var item = new TreeViewItem(id, parent.depth + 1, parts[p]);
                    _idToFullName[id] = curPath;
                    map[curPath] = item;

                    parent.AddChild(item);
                    parent = item;
                }
            }

            if (root.children == null) root.children = new List<TreeViewItem>();
            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = args.item;
            for (int i = 0; i < args.GetNumVisibleColumns(); i++)
            {
                var rect = args.GetCellRect(i);
                CenterRectUsingSingleLineHeight(ref rect);

                if (i == 0)
                {
                    var indent = GetContentIndent(item);

                    // Foldout
                    if (item != null && item.hasChildren)
                    {
                        var foldoutRect = rect;
                        foldoutRect.xMin += indent - 14f;
                        foldoutRect.width = 14f;

                        var expanded = IsExpanded(item.id);
                        var nextExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none);
                        if (nextExpanded != expanded)
                        {
                            SetExpanded(item.id, nextExpanded);
                        }
                    }

                    // Label
                    var labelRect = rect;
                    labelRect.xMin += indent;
                    EditorGUI.LabelField(labelRect, GetFullName(item));
                }
                if (i == 1)
                {
                    EditorGUI.LabelField(rect, GetComment(item));

                    var e = Event.current;
                    if (e != null && e.type == EventType.MouseDown && e.button == 0 && e.clickCount == 2 && rect.Contains(e.mousePosition))
                    {
                        var full = GetFullName(item);
                        if (!string.IsNullOrEmpty(full))
                        {
                            EditComment(full);
                            e.Use();
                        }
                    }
                }
            }
        }

        private string GetFullName(TreeViewItem item)
        {
            if (item == null || item.id == 0) return string.Empty;
            return _idToFullName.TryGetValue(item.id, out var n) ? n : item.displayName;
        }

        private string GetComment(TreeViewItem item)
        {
            var full = GetFullName(item);
            if (string.IsNullOrEmpty(full)) return string.Empty;
            return _byName.TryGetValue(full, out var e) && e != null ? (e.Comment ?? string.Empty) : string.Empty;
        }

        protected override void ContextClickedItem(int id)
        {
            var full = _idToFullName.TryGetValue(id, out var s) ? s : null;
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Add Child"), false, () => AddChild(full));

            if (!string.IsNullOrEmpty(full))
            {
                menu.AddItem(new GUIContent("Rename"), false, () => Rename(full));
                menu.AddItem(new GUIContent("Edit Comment"), false, () => EditComment(full));
                menu.AddItem(new GUIContent("Delete"), false, () => Delete(full));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Rename"));
                menu.AddDisabledItem(new GUIContent("Edit Comment"));
                menu.AddDisabledItem(new GUIContent("Delete"));
            }

            menu.ShowAsContext();
        }

        protected override void ContextClicked()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Add Root Tag"), false, () => AddChild(string.Empty));
            menu.ShowAsContext();
        }

        private void AddChild(string parentFull)
        {
            var parentLabel = string.IsNullOrEmpty(parentFull) ? "<root>" : parentFull;
            GameplayTagTextPrompt.Open(
                title: "Add Tag",
                label: "New segment under: " + parentLabel,
                defaultText: "NewTag",
                onOk: segment =>
                {
                    if (string.IsNullOrWhiteSpace(segment)) return;
                    segment = segment.Trim();
                    if (!GameplayTagValidation.IsValidSegment(segment))
                    {
                        EditorUtility.DisplayDialog("Gameplay Tags", "Invalid segment. No whitespace, no '.'", "Close");
                        return;
                    }

                    var full = string.IsNullOrEmpty(parentFull) ? segment : parentFull + "." + segment;
                    if (!GameplayTagValidation.TryNormalize(full, out var normalized))
                    {
                        EditorUtility.DisplayDialog("Gameplay Tags", "Invalid tag name", "Close");
                        return;
                    }

                    if (_db.TryGetEntry(normalized, out _))
                    {
                        EditorUtility.DisplayDialog("Gameplay Tags", "Tag already exists: " + normalized, "Close");
                        return;
                    }

                    Undo.RecordObject(_db, "Add Gameplay Tag");
                    _db.GetOrCreate(normalized);
                    _db.SortAndDedup();
                    OnDatabaseChanged?.Invoke();
                }
            );
        }

        private void Rename(string full)
        {
            if (string.IsNullOrEmpty(full)) return;

            var lastDot = full.LastIndexOf('.');
            var parent = lastDot > 0 ? full.Substring(0, lastDot) : string.Empty;

            var oldSeg = lastDot >= 0 ? full.Substring(lastDot + 1) : full;
            GameplayTagTextPrompt.Open(
                title: "Rename Tag",
                label: "New segment for: " + full,
                defaultText: oldSeg,
                onOk: newSeg =>
                {
                    if (string.IsNullOrWhiteSpace(newSeg)) return;
                    newSeg = newSeg.Trim();
                    if (!GameplayTagValidation.IsValidSegment(newSeg))
                    {
                        EditorUtility.DisplayDialog("Gameplay Tags", "Invalid segment. No whitespace, no '.'", "Close");
                        return;
                    }

                    var newFull = string.IsNullOrEmpty(parent) ? newSeg : parent + "." + newSeg;
                    RenamePrefix(full, newFull);
                }
            );
        }

        private void Delete(string full)
        {
            if (string.IsNullOrEmpty(full)) return;

            if (!EditorUtility.DisplayDialog("Delete Tag", $"Delete '{full}' and all children?", "Delete", "Cancel"))
            {
                return;
            }

            Undo.RecordObject(_db, "Delete Gameplay Tag");
            _db.RemoveByPrefix(full);
            _db.SortAndDedup();
            OnDatabaseChanged?.Invoke();
        }

        private void EditComment(string full)
        {
            if (string.IsNullOrEmpty(full)) return;
            var e = _db.GetOrCreate(full);
            if (e == null) return;

            GameplayTagTextPrompt.Open(
                title: "Edit Comment",
                label: "Comment for: " + full,
                defaultText: e.Comment ?? string.Empty,
                multiline: true,
                onOk: comment =>
                {
                    Undo.RecordObject(_db, "Edit Tag Comment");
                    e.Comment = comment ?? string.Empty;
                    OnDatabaseChanged?.Invoke();
                }
            );
        }

        protected override bool CanStartDrag(CanStartDragArgs args) => true;

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            var dragged = args.draggedItemIDs;
            if (dragged == null || dragged.Count == 0) return;

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData("GameplayTagTreeView", dragged.ToArray());
            DragAndDrop.StartDrag("Move GameplayTag");
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            var data = DragAndDrop.GetGenericData("GameplayTagTreeView") as int[];
            if (data == null || data.Length == 0) return DragAndDropVisualMode.None;

            var dropParent = args.parentItem;
            var parentFull = GetFullName(dropParent);

            if (args.performDrop)
            {
                Undo.RecordObject(_db, "Move Gameplay Tag");
                for (int i = 0; i < data.Length; i++)
                {
                    if (!_idToFullName.TryGetValue(data[i], out var fromFull)) continue;
                    if (string.IsNullOrEmpty(fromFull)) continue;

                    var newFull = MoveUnder(fromFull, parentFull);
                    if (!string.IsNullOrEmpty(newFull))
                    {
                        RenamePrefix(fromFull, newFull);
                    }
                }
            }

            return DragAndDropVisualMode.Move;
        }

        private string MoveUnder(string fromFull, string newParentFull)
        {
            var seg = fromFull;
            var lastDot = fromFull.LastIndexOf('.');
            if (lastDot >= 0) seg = fromFull.Substring(lastDot + 1);

            if (!GameplayTagValidation.IsValidSegment(seg)) return null;
            if (string.Equals(fromFull, newParentFull, StringComparison.Ordinal)) return null;

            var newFull = string.IsNullOrEmpty(newParentFull) ? seg : newParentFull + "." + seg;
            if (!GameplayTagValidation.TryNormalize(newFull, out var normalized)) return null;
            return normalized;
        }

        private void RenamePrefix(string fromFull, string toFull)
        {
            if (string.IsNullOrEmpty(fromFull) || string.IsNullOrEmpty(toFull)) return;

            var list = _db.Tags;
            var fromPrefix = fromFull + ".";
            var toPrefix = toFull + ".";

            // detect self cycle
            if (toFull.StartsWith(fromPrefix, StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Gameplay Tags", "Invalid move: cannot make a tag a child of itself.", "Close");
                return;
            }

            var exists = new HashSet<string>(list, StringComparer.Ordinal);
            if (exists.Contains(toFull))
            {
                EditorUtility.DisplayDialog("Gameplay Tags", "Target tag already exists: " + toFull, "Close");
                return;
            }

            Undo.RecordObject(_db, "Rename Gameplay Tag");

            _db.RenamePrefix(fromFull, toFull);

            _db.SortAndDedup();
            OnDatabaseChanged?.Invoke();
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            var full = GetFullName(item);
            var comment = GetComment(item);
            return (!string.IsNullOrEmpty(full) && full.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(comment) && comment.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
