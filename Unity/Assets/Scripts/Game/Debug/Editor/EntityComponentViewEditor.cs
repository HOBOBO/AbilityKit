using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.EC;
using AbilityKit.Game.Debug;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Debug.Editor
{
    [CustomEditor(typeof(EntityComponentView))]
    public sealed class EntityComponentViewEditor : UnityEditor.Editor
    {
        private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
            EditorGUILayout.LabelField(name, value == null ? "null" : value.ToString());
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
