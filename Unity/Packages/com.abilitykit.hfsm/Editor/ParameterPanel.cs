using System;
using UnityEditor;
using UnityEngine;
using AbilityKit.HFSM.Graph;

namespace AbilityKit.HFSM.Editor
{
    /// <summary>
    /// Panel for editing HFSM parameters.
    /// </summary>
    public class ParameterPanel
    {
        private EditorContext _context;
        private Vector2 _scrollPosition;
        private string _newParameterName = "New Parameter";
        private ParameterValueType _newParameterType = ParameterValueType.Bool;

        public void Initialize(EditorContext context)
        {
            _context = context;
        }

        public void OnGUI()
        {
            if (_context == null || _context.GraphAsset == null)
            {
                EditorGUILayout.HelpBox("No graph loaded.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Header
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            // Add new parameter section
            EditorGUILayout.BeginHorizontal();
            _newParameterName = EditorGUILayout.TextField(_newParameterName, GUILayout.Width(130));

            _newParameterType = (ParameterValueType)EditorGUILayout.EnumPopup(_newParameterType, GUILayout.Width(80));

            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                AddNewParameter();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Parameter list
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

            var parameters = _context.GraphAsset.Parameters;
            for (int i = 0; i < parameters.Count; i++)
            {
                DrawParameter(parameters[i], i);
            }

            EditorGUILayout.EndScrollView();

            if (parameters.Count == 0)
            {
                EditorGUILayout.HelpBox("No parameters defined. Add parameters to use in transition conditions.", MessageType.None);
            }
        }

        private void DrawParameter(Parameter parameter, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Parameter name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(parameter.Name, GUILayout.Width(150));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_context.GraphAsset, "Rename Parameter");
                parameter.Name = newName;
                EditorUtility.SetDirty(_context.GraphAsset);
            }

            // Parameter type (read-only display)
            EditorGUILayout.LabelField(GetTypeLabel(parameter.ParameterType), EditorStyles.miniLabel, GUILayout.Width(50));

            DrawDefaultValue(parameter);

            GUILayout.FlexibleSpace();

            // Delete button
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                DeleteParameter(parameter);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawDefaultValue(Parameter parameter)
        {
            var boolValue = parameter.DefaultBoolValue;
            var floatValue = parameter.DefaultFloatValue;
            var intValue = parameter.DefaultIntValue;
            EditorGUI.BeginChangeCheck();
            switch (parameter.ParameterType)
            {
                case ParameterValueType.Bool:
                    boolValue = EditorGUILayout.Toggle(boolValue, GUILayout.Width(45));
                    break;
                case ParameterValueType.Float:
                    floatValue = EditorGUILayout.FloatField(floatValue, GUILayout.Width(60));
                    break;
                case ParameterValueType.Int:
                    intValue = EditorGUILayout.IntField(intValue, GUILayout.Width(60));
                    break;
                case ParameterValueType.Trigger:
                    EditorGUILayout.LabelField("false", EditorStyles.miniLabel, GUILayout.Width(45));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_context.GraphAsset, "Change Parameter Default");
                parameter.DefaultBoolValue = boolValue;
                parameter.DefaultFloatValue = floatValue;
                parameter.DefaultIntValue = intValue;
                EditorUtility.SetDirty(_context.GraphAsset);
            }
        }

        private string GetTypeLabel(ParameterValueType type)
        {
            return type switch
            {
                ParameterValueType.Bool => "Bool",
                ParameterValueType.Float => "Float",
                ParameterValueType.Int => "Int",
                ParameterValueType.Trigger => "Trigger",
                _ => type.ToString()
            };
        }

        private void AddNewParameter()
        {
            if (string.IsNullOrWhiteSpace(_newParameterName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a parameter name.", "OK");
                return;
            }

            // Check for duplicate names
            foreach (var param in _context.GraphAsset.Parameters)
            {
                if (param.Name == _newParameterName)
                {
                    EditorUtility.DisplayDialog("Error", "A parameter with this name already exists.", "OK");
                    return;
                }
            }

            Undo.RecordObject(_context.GraphAsset, "Add Parameter");
            var parameter = new Parameter(_newParameterName, _newParameterType);
            _context.GraphAsset.AddParameter(parameter);
            EditorUtility.SetDirty(_context.GraphAsset);

            // Reset and increment name for next parameter
            _newParameterName = "New Parameter_" + (_context.GraphAsset.Parameters.Count + 1);
        }

        private void DeleteParameter(Parameter parameter)
        {
            if (EditorUtility.DisplayDialog("Delete Parameter",
                $"Are you sure you want to delete the parameter '{parameter.Name}'?",
                "Delete", "Cancel"))
            {
                Undo.RecordObject(_context.GraphAsset, "Delete Parameter");
                _context.GraphAsset.RemoveParameter(parameter);
                EditorUtility.SetDirty(_context.GraphAsset);
            }
        }
    }
}
