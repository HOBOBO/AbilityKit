using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.EC;
using AbilityKit.Game.EntityDebug;
using UnityEditor;
using UnityEngine;
using EC = AbilityKit.Ability.EC;

namespace AbilityKit.Game.EntityDebug.Editor
{
    [CustomEditor(typeof(EntityComponentView))]
    public sealed class EntityComponentViewEditor : UnityEditor.Editor
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Dictionary<string, bool> Foldouts = new Dictionary<string, bool>();
        private const int MaxDepth = 4;

        public override void OnInspectorGUI()
        {
            var view = (EntityComponentView)target;
            if (view == null)
            {
                base.OnInspectorGUI();
                return;
            }

            var entityView = view.Entity;
            if (entityView == null || !entityView.IsBound)
            {
                EditorGUILayout.LabelField("Entity", "<unbound>");
                base.OnInspectorGUI();
                return;
            }

            if (!entityView.TryGetEntity(out var entity))
            {
                EditorGUILayout.LabelField("Entity", "<dead>");
                base.OnInspectorGUI();
                return;
            }

            EditorGUILayout.LabelField("EntityId", entity.Id.ToString());

            var world = entity.World;
            if (world == null)
            {
                base.OnInspectorGUI();
                return;
            }

            world.ForEachComponent(entity.Id, (typeId, obj) =>
            {
                if (obj == null) return;

                var t = ComponentTypeId.TryGetType(typeId, out var type) ? type : obj.GetType();
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField(t.Name);

                    var drawn = new HashSet<string>();

                    foreach (var f in t.GetFields(Flags))
                    {
                        if (f.IsStatic) continue;
                        if (!ShouldShow(f)) continue;
                        if (!drawn.Add(f.Name)) continue;

                        var canWrite = !(f.IsInitOnly || f.IsLiteral);
                        if (!canWrite)
                        {
                            DrawMember(f.Name, SafeGet(() => f.GetValue(obj)));
                            continue;
                        }

                        var current = SafeGet(() => f.GetValue(obj));
                        if (TryDrawEditable(f.Name, f.FieldType, current, out var next))
                        {
                            SafeSet(() => f.SetValue(obj, next));
                            view.MarkDirty();
                        }
                        else
                        {
                            DrawMember(f.Name, current);
                        }
                    }

                    foreach (var p in t.GetProperties(Flags))
                    {
                        if (!p.CanRead) continue;
                        if (p.GetIndexParameters().Length != 0) continue;
                        var getter = p.GetGetMethod(true);
                        if (getter == null || getter.IsStatic) continue;
                        if (!ShouldShow(p, getter)) continue;
                        if (!drawn.Add(p.Name)) continue;

                        var setter = p.GetSetMethod(true);
                        var canWrite = setter != null && !setter.IsStatic;

                        var current = SafeGet(() => p.GetValue(obj));
                        if (!canWrite)
                        {
                            DrawMember(p.Name, current);
                            continue;
                        }

                        if (TryDrawEditable(p.Name, p.PropertyType, current, out var next))
                        {
                            SafeSet(() => p.SetValue(obj, next));
                            view.MarkDirty();
                        }
                        else
                        {
                            DrawMember(p.Name, current);
                        }
                    }
                }
            });

            if (view.ConsumeDirty())
            {
                Repaint();
            }
        }

        private static void SafeSet(Action set)
        {
            try
            {
                set();
            }
            catch
            {
            }
        }

        private static bool TryDrawEditable(string name, Type type, object current, out object next)
        {
            next = current;

            if (type == typeof(int))
            {
                var v = current is int i ? i : default;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.IntField(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(float))
            {
                var v = current is float f ? f : default;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.FloatField(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(double))
            {
                var v = current is double d ? d : default;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.DoubleField(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(bool))
            {
                var v = current is bool b && b;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.Toggle(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(string))
            {
                var v = current as string ?? string.Empty;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.TextField(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type.IsEnum)
            {
                var v = current as Enum;
                if (v == null)
                {
                    try { v = (Enum)Enum.GetValues(type).GetValue(0); }
                    catch { return false; }
                }

                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.EnumPopup(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(Vector2))
            {
                var v = current is Vector2 vv ? vv : default;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.Vector2Field(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(Vector3))
            {
                var v = current is Vector3 vv ? vv : default;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.Vector3Field(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(Vector4))
            {
                var v = current is Vector4 vv ? vv : default;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.Vector4Field(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (type == typeof(Color))
            {
                var v = current is Color c ? c : default;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.ColorField(name, v);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var v = current as UnityEngine.Object;
                EditorGUI.BeginChangeCheck();
                var nv = EditorGUILayout.ObjectField(name, v, type, true);
                if (!EditorGUI.EndChangeCheck()) return false;
                next = nv;
                return true;
            }

            return false;
        }

        private static void DrawMember(string name, object value)
        {
            DrawObject(name, value, 0, null);
        }

        private static void DrawObject(string name, object value, int depth, HashSet<object> visited)
        {
            if (depth > MaxDepth)
            {
                EditorGUILayout.LabelField(name, "<max depth>");
                return;
            }

            if (value == null)
            {
                EditorGUILayout.LabelField(name, "null");
                return;
            }

            var t = value.GetType();

            if (t == typeof(string) || t.IsPrimitive || t.IsEnum)
            {
                EditorGUILayout.LabelField(name, value.ToString());
                return;
            }

            if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4) || t == typeof(Color))
            {
                EditorGUILayout.LabelField(name, value.ToString());
                return;
            }

            if (value is UnityEngine.Object uo)
            {
                EditorGUILayout.ObjectField(name, uo, t, true);
                return;
            }

            if (visited == null) visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            if (!t.IsValueType)
            {
                if (!visited.Add(value))
                {
                    EditorGUILayout.LabelField(name, "<cycle>");
                    return;
                }
            }

            if (value is System.Collections.IList list)
            {
                DrawList(name, list, depth, visited);
                return;
            }

            if (t.IsArray)
            {
                var arr = value as Array;
                DrawArray(name, arr, depth, visited);
                return;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                DrawEnumerable(name, enumerable, depth, visited);
                return;
            }

            DrawSerializableObject(name, value, t, depth, visited);
        }

        private static void DrawArray(string name, Array array, int depth, HashSet<object> visited)
        {
            var count = array?.Length ?? 0;
            var key = GetFoldoutKey(name, array);
            var open = GetFoldout(key, defaultValue: false);
            open = EditorGUILayout.Foldout(open, $"{name} [{count}]", true);
            SetFoldout(key, open);

            if (!open || array == null) return;

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < array.Length; i++)
                {
                    DrawObject($"[{i}]", array.GetValue(i), depth + 1, visited);
                }
            }
        }

        private static void DrawList(string name, System.Collections.IList list, int depth, HashSet<object> visited)
        {
            var count = list?.Count ?? 0;
            var key = GetFoldoutKey(name, list);
            var open = GetFoldout(key, defaultValue: false);
            open = EditorGUILayout.Foldout(open, $"{name} [{count}]", true);
            SetFoldout(key, open);

            if (!open || list == null) return;

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < list.Count; i++)
                {
                    DrawObject($"[{i}]", list[i], depth + 1, visited);
                }
            }
        }

        private static void DrawEnumerable(string name, System.Collections.IEnumerable enumerable, int depth, HashSet<object> visited)
        {
            var key = GetFoldoutKey(name, enumerable);
            var open = GetFoldout(key, defaultValue: false);
            open = EditorGUILayout.Foldout(open, name, true);
            SetFoldout(key, open);

            if (!open || enumerable == null) return;

            using (new EditorGUI.IndentLevelScope())
            {
                int i = 0;
                foreach (var it in enumerable)
                {
                    DrawObject($"[{i}]", it, depth + 1, visited);
                    i++;
                    if (i >= 64)
                    {
                        EditorGUILayout.LabelField("...", "<truncated>");
                        break;
                    }
                }
            }
        }

        private static void DrawSerializableObject(string name, object value, Type type, int depth, HashSet<object> visited)
        {
            var key = GetFoldoutKey(name, value);
            var open = GetFoldout(key, defaultValue: depth == 0);
            open = EditorGUILayout.Foldout(open, name, true);
            SetFoldout(key, open);

            if (!open) return;

            using (new EditorGUI.IndentLevelScope())
            {
                var drawn = new HashSet<string>();

                foreach (var f in type.GetFields(Flags))
                {
                    if (f.IsStatic) continue;
                    if (!ShouldShow(f)) continue;
                    if (!drawn.Add(f.Name)) continue;

                    var current = SafeGet(() => f.GetValue(value));
                    DrawObject(f.Name, current, depth + 1, visited);
                }

                foreach (var p in type.GetProperties(Flags))
                {
                    if (!p.CanRead) continue;
                    if (p.GetIndexParameters().Length != 0) continue;
                    var getter = p.GetGetMethod(true);
                    if (getter == null || getter.IsStatic) continue;
                    if (!ShouldShow(p, getter)) continue;
                    if (!drawn.Add(p.Name)) continue;

                    var current = SafeGet(() => p.GetValue(value));
                    DrawObject(p.Name, current, depth + 1, visited);
                }
            }
        }

        private static string GetFoldoutKey(string name, object value)
        {
            var id = value == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            return $"{name}#{id}";
        }

        private static bool GetFoldout(string key, bool defaultValue)
        {
            return Foldouts.TryGetValue(key, out var v) ? v : defaultValue;
        }

        private static void SetFoldout(string key, bool value)
        {
            Foldouts[key] = value;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private static object SafeGet(Func<object> get)
        {
            try
            {
                return get();
            }
            catch (Exception e)
            {
                return e.GetType().Name;
            }
        }

        private static bool ShouldShow(FieldInfo f)
        {
            if (f.IsPublic) return true;
            return Attribute.IsDefined(f, typeof(EntityDebugFieldAttribute), true);
        }

        private static bool ShouldShow(PropertyInfo p, MethodInfo getter)
        {
            if (getter.IsPublic) return true;
            return Attribute.IsDefined(p, typeof(EntityDebugFieldAttribute), true);
        }
    }
}
