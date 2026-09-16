#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.Deterministic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Editor
{
    internal sealed class PreviewBlackboardSetupWindow : EditorWindow
    {
        private readonly Dictionary<string, PropertyValue> _overrides = new(StringComparer.Ordinal);
        private readonly HashSet<string> _invalidKeys = new(StringComparer.Ordinal);
        private Action<IReadOnlyDictionary<string, PropertyValue>>? _start;
        private BlackboardSchema? _schema;
        private Button? _startButton;

        internal static void Open(
            EditorWindow owner,
            BlackboardSchema schema,
            Action<IReadOnlyDictionary<string, PropertyValue>> start)
        {
            var window = CreateInstance<PreviewBlackboardSetupWindow>();
            window._schema = schema;
            window._start = start;
            window.titleContent = new GUIContent("预览初始黑板");
            window.minSize = new Vector2(380f, 260f);
            var height = Mathf.Min(620f, Mathf.Max(310f, 130f + schema.Keys.Count * 74f));
            window.position = new Rect(
                owner.position.center.x - 230f,
                owner.position.center.y - height / 2f,
                460f,
                height);
            window.BuildUi();
            window.ShowUtility();
            window.Focus();
        }

        private void OnDisable()
        {
            _start = null;
        }

        private void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            var heading = new Label("预览初始黑板")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 14f,
                    paddingTop = 12f,
                    paddingBottom = 8f,
                },
            };
            rootVisualElement.Add(heading);

            var list = new ScrollView { style = { flexGrow = 1f, paddingLeft = 14f, paddingRight = 14f } };
            foreach (var key in _schema!.Keys)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        paddingTop = 6f,
                        paddingBottom = 7f,
                        borderBottomWidth = 1f,
                        borderBottomColor = new Color(0.5f, 0.5f, 0.5f, 0.22f),
                    },
                };
                row.Add(new Label(key.Name + "  [" + key.Type + "]")
                {
                    tooltip = key.Name,
                    style = { unityFontStyleAndWeight = FontStyle.Bold, whiteSpace = WhiteSpace.Normal },
                });
                var controls = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap },
                };
                var overrideToggle = new Toggle("覆盖") { style = { minWidth = 75f } };
                controls.Add(overrideToggle);
                var initialValue = key.Default ?? DefaultOf(key.Type);
                var valueField = CreateValueField(key, initialValue);
                valueField.style.flexGrow = 1f;
                valueField.style.minWidth = 125f;
                valueField.SetEnabled(false);
                controls.Add(valueField);
                overrideToggle.RegisterValueChangedCallback(evt =>
                {
                    valueField.SetEnabled(evt.newValue);
                    if (evt.newValue) _overrides[key.Name] = initialValue;
                    else
                    {
                        _overrides.Remove(key.Name);
                        _invalidKeys.Remove(key.Name);
                        if (key.Type == ValueType.Fixed64 && valueField is TextField fixedField)
                        {
                            fixedField.SetValueWithoutNotify(Fixed64.FromRaw(initialValue.Fixed64Raw).ToString());
                            fixedField.style.borderBottomColor = StyleKeyword.Null;
                            fixedField.tooltip = "定点数（十进制）";
                        }
                        RefreshStartButton();
                    }
                });
                row.Add(controls);
                list.Add(row);
            }
            rootVisualElement.Add(list);

            var footer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    paddingLeft = 14f,
                    paddingRight = 14f,
                    paddingTop = 8f,
                    paddingBottom = 10f,
                },
            };
            footer.Add(new Button(Close) { text = "取消", style = { minWidth = 68f, marginRight = 6f } });
            _startButton = new Button(() =>
            {
                var start = _start;
                var snapshot = new Dictionary<string, PropertyValue>(_overrides, StringComparer.Ordinal);
                Close();
                start?.Invoke(snapshot);
            }) { text = "开始预览", style = { minWidth = 88f } };
            footer.Add(_startButton);
            rootVisualElement.Add(footer);
        }

        private VisualElement CreateValueField(BlackboardKeyDefinition key, PropertyValue initial)
        {
            switch (key.Type)
            {
                case ValueType.Bool:
                    var boolField = new Toggle { value = initial.BoolValue };
                    boolField.RegisterValueChangedCallback(evt => UpdateOverride(key.Name, PropertyValue.Of(evt.newValue)));
                    return boolField;
                case ValueType.Int64:
                    var intField = new LongField { value = initial.Int64Value };
                    intField.RegisterValueChangedCallback(evt => UpdateOverride(key.Name, PropertyValue.Of(evt.newValue)));
                    return intField;
                case ValueType.Fixed64:
                    var fixedField = new TextField
                    {
                        value = Fixed64.FromRaw(initial.Fixed64Raw).ToString(),
                        tooltip = "定点数（十进制）",
                    };
                    fixedField.RegisterValueChangedCallback(evt =>
                    {
                        try
                        {
                            if (!decimal.TryParse(evt.newValue, NumberStyles.Float,
                                    CultureInfo.InvariantCulture, out var number))
                                throw new FormatException();
                            var raw = checked((long)decimal.Round(
                                number * Fixed64.OneRaw, 0, MidpointRounding.AwayFromZero));
                            UpdateOverride(key.Name, PropertyValue.Of(Fixed64.FromRaw(raw)));
                            _invalidKeys.Remove(key.Name);
                            fixedField.style.borderBottomColor = StyleKeyword.Null;
                            fixedField.tooltip = "定点数（十进制）";
                        }
                        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
                        {
                            _invalidKeys.Add(key.Name);
                            fixedField.style.borderBottomColor = new Color(0.9f, 0.25f, 0.25f);
                            fixedField.tooltip = "请输入有效范围内的十进制定点数";
                        }
                        RefreshStartButton();
                    });
                    return fixedField;
                case ValueType.String:
                    var stringField = new TextField { value = initial.StringValue };
                    stringField.RegisterValueChangedCallback(evt => UpdateOverride(
                        key.Name, PropertyValue.Of(evt.newValue ?? "")));
                    return stringField;
                default:
                    return new Label("不支持的类型");
            }
        }

        private void UpdateOverride(string name, PropertyValue value)
        {
            if (_overrides.ContainsKey(name)) _overrides[name] = value;
        }

        private void RefreshStartButton() => _startButton?.SetEnabled(_invalidKeys.Count == 0);

        private static PropertyValue DefaultOf(ValueType type) => type switch
        {
            ValueType.Bool => PropertyValue.Of(false),
            ValueType.Int64 => PropertyValue.Of(0L),
            ValueType.Fixed64 => PropertyValue.Of(Fixed64.Zero),
            ValueType.String => PropertyValue.Of(""),
            _ => throw new InvalidOperationException("不支持的黑板类型：" + type),
        };
    }
}
