using System;
using UnityEditor;
using UnityEngine;
using UnityHFSM.Graph;

namespace UnityHFSM.Editor
{
    /// <summary>
    /// Panel for editing HFSM parameters.
    /// </summary>
    public class HfsmParameterPanel
    {
        private HfsmEditorContext _context;
        private Vector2 _scrollPosition;
        private string _newParameterName = "New Parameter";
        private HfsmParameterType _newParameterType = HfsmParameterType.Bool;

        public void Initialize(HfsmEditorContext context)
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

            _newParameterType = (HfsmParameterType)EditorGUILayout.EnumPopup(_newParameterType, GUILayout.Width(80));

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

        private void DrawParameter(HfsmParameter parameter, int index)
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

            GUILayout.FlexibleSpace();

            // Delete button
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                DeleteParameter(parameter);
            }

            EditorGUILayout.EndHorizontal();
        }

        private string GetTypeLabel(HfsmParameterType type)
        {
            return type switch
            {
                HfsmParameterType.Bool => "Bool",
                HfsmParameterType.Float => "Float",
                HfsmParameterType.Int => "Int",
                HfsmParameterType.Trigger => "Trigger",
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
            var parameter = new HfsmParameter(_newParameterName, _newParameterType);
            _context.GraphAsset.AddParameter(parameter);
            EditorUtility.SetDirty(_context.GraphAsset);

            // Reset and increment name for next parameter
            _newParameterName = "New Parameter_" + (_context.GraphAsset.Parameters.Count + 1);
        }

        private void DeleteParameter(HfsmParameter parameter)
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
