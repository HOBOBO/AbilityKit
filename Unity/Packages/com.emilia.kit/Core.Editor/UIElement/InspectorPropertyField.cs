using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Emilia.Kit.Editor
{
    public class InspectorPropertyField : VisualElement
    {
        private static Dictionary<Type, Func<VisualElement>> createFiledElement = new Dictionary<Type, Func<VisualElement>>() {
            {typeof(int), () => new IntegerField()},
            {typeof(long), () => new IntegerField()},
            {typeof(float), () => new FloatField()},
            {typeof(double), () => new DoubleField()},
            {typeof(string), () => new TextField()},
            {typeof(char), () => new TextField()},
            {typeof(bool), () => new Toggle()},
            {typeof(Vector2), () => new Vector2Field()},
            {typeof(Vector3), () => new Vector3Field()},
            {typeof(Vector4), () => new Vector4Field()},
            {typeof(Vector2Int), () => new Vector2IntField()},
            {typeof(Vector3Int), () => new Vector3IntField()},
            {typeof(Color), () => new ColorField()},
            {typeof(Color32), () => new ColorField()},
            {typeof(Rect), () => new RectField()},
            {typeof(RectInt), () => new RectIntField()},
            {typeof(Bounds), () => new BoundsField()},
            {typeof(BoundsInt), () => new BoundsIntField()},
            {typeof(Gradient), () => new GradientField()},
            {typeof(LayerMask), () => new LayerMaskField()},
            {typeof(AnimationCurve), () => new CurveField()},
            {typeof(Hash128), () => new Hash128Field()},
        };

        protected struct FieldInfo
        {
            public InspectorProperty inspectorProperty;
            public VisualElement field;
        }

        private InspectorProperty _inspectorProperty;
        private List<FieldInfo> fieldInfos = new List<FieldInfo>();

        public InspectorProperty inspectorProperty => _inspectorProperty;

        public InspectorPropertyField(InspectorProperty inspectorProperty, bool forceImGUIDraw = false, bool label = true)
        {
            _inspectorProperty = inspectorProperty;

            if (forceImGUIDraw) IMGUIDraw();
            else
            {
                if (AllCanDrawProperty(inspectorProperty)) AddElement(this, inspectorProperty, label);
                else IMGUIDraw();
            }

            Update();

            int amount = this.fieldInfos.Count;
            for (int i = 0; i < amount; i++)
            {
                FieldInfo info = this.fieldInfos[i];
                if (info.field == null) continue;
                RegisterPropertyChanges(info);
            }
        }

        private bool AllCanDrawProperty(InspectorProperty current)
        {
            bool propertyDraw = true;

            Queue<InspectorProperty> queue = new Queue<InspectorProperty>();
            queue.Enqueue(current);

            while (queue.Count > 0)
            {
                InspectorProperty property = queue.Dequeue();
                if (property.Children.Count > 0)
                {
                    foreach (InspectorProperty child in property.Children) queue.Enqueue(child);
                }

                if (CanDrawProperty(property)) continue;
                propertyDraw = false;
                break;
            }

            return propertyDraw;
        }

        private void IMGUIDraw()
        {
            IMGUIContainer imguiContainer = new IMGUIContainer(() => {
                _inspectorProperty.Tree.BeginDraw(true);
                _inspectorProperty.Draw();
                _inspectorProperty.Tree.EndDraw();
            });

            Add(imguiContainer);
        }

        public void Update()
        {
            int amount = this.fieldInfos.Count;
            for (int i = 0; i < amount; i++)
            {
                FieldInfo info = this.fieldInfos[i];
                if (info.field == null) continue;
                info.inspectorProperty.ValueEntry.Update();
                
                ReflectUtility.Invoke(
                    info.field,
                    info.field.GetType(),
                    nameof(INotifyValueChanged<object>.SetValueWithoutNotify),
                    new object[] {info.inspectorProperty.ValueEntry.WeakSmartValue},
                    new Type[] {info.inspectorProperty.ValueEntry.TypeOfValue});
            }
        }

        private void AddElement(VisualElement parent, InspectorProperty currentProperty, bool label)
        {
            if (currentProperty.Children.Count > 0)
            {
                VisualElement foldout = CreateFoldout();
                foreach (InspectorProperty child in currentProperty.Children) AddElement(foldout, child, label);
            }
            else
            {
                VisualElement propertyGUI = CreatePropertyGUI(currentProperty);
                if (propertyGUI != null)
                {
                    if (label) ReflectUtility.SetValue(propertyGUI, nameof(BaseField<object>.label), currentProperty.NiceName);
                    parent.Add(propertyGUI);
                }
            }
        }

        private VisualElement CreateFoldout()
        {
            Foldout foldout = new Foldout();
            return foldout;
        }

        private bool CanDrawProperty(InspectorProperty property)
        {
            Type type = property.ValueEntry.TypeOfValue;
            if (createFiledElement.ContainsKey(type)) return true;
            if (typeof(Object).IsAssignableFrom(type)) return true;
            if (type.IsEnum) return true;
            if (type.IsArray) return true;
            return false;
        }

        private VisualElement CreatePropertyGUI(InspectorProperty property)
        {
            FieldInfo fieldInfo = new FieldInfo();
            fieldInfo.inspectorProperty = property;

            Type type = property.ValueEntry.TypeOfValue;

            createFiledElement.TryGetValue(type, out Func<VisualElement> func);

            if (func != null) fieldInfo.field = func();
            else if (typeof(Object).IsAssignableFrom(type)) fieldInfo.field = new ObjectField();
            else if (type.IsEnum) fieldInfo.field = new EnumField();
            else if (type.IsArray) fieldInfo.field = new ListView();

            if (fieldInfo.field == null) return null;

            fieldInfo.field.style.flexGrow = 1;

            this.fieldInfos.Add(fieldInfo);
            return fieldInfo.field;
        }

        private void RegisterPropertyChanges(FieldInfo pair)
        {
            pair.field.RegisterCallback<ChangeEvent<int>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<bool>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<float>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<double>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<string>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Color>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Object>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Enum>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Vector2>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Vector3>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Vector4>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Rect>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<AnimationCurve>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Bounds>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Gradient>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Quaternion>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Vector2Int>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Vector3Int>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Vector3Int>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<RectInt>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<BoundsInt>>((_) => PropertyChanges(pair));
            pair.field.RegisterCallback<ChangeEvent<Hash128>>((_) => PropertyChanges(pair));
        }

        private void PropertyChanges(FieldInfo info)
        {
            info.inspectorProperty.Tree.UpdateTree();
            info.inspectorProperty.Tree.RecordUndoForChanges = true;
            info.inspectorProperty.Tree.RootProperty.Update();

            info.inspectorProperty.ValueEntry.WeakSmartValue = ReflectUtility.GetValue(info.field, nameof(INotifyValueChanged<object>.value));

            info.inspectorProperty.Tree.ApplyChanges();
            Undo.FlushUndoRecordObjects();
        }
    }
}