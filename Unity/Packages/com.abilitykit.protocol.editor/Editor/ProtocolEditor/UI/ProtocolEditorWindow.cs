using System.IO;
using AbilityKit.ProtocolEditor.Generator;
using AbilityKit.ProtocolEditor.Schema;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ProtocolEditor.UI
{
    public sealed class ProtocolEditorWindow : EditorWindow
    {
        private ProtocolDefinition _definition;
        private DefaultAsset _outputFolder;
        private string _namespace = "AbilityKit.Protocol";

        [MenuItem("AbilityKit/Protocol/Protocol Editor")]
        private static void Open()
        {
            GetWindow<ProtocolEditorWindow>("Protocol Editor");
        }

        private void OnGUI()
        {
            _definition = (ProtocolDefinition)EditorGUILayout.ObjectField("Definition", _definition, typeof(ProtocolDefinition), false);
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);
            _namespace = EditorGUILayout.TextField("Namespace", _namespace);

            if (_definition != null)
            {
                EditorGUILayout.LabelField("RegistryId", _definition.RegistryId);
                EditorGUILayout.LabelField("Domain", _definition.Domain);
            }

            using (new EditorGUI.DisabledScope(_definition == null || _outputFolder == null))
            {
                if (GUILayout.Button("Generate OpCodes.g.cs"))
                {
                    var folderPath = AssetDatabase.GetAssetPath(_outputFolder);
                    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    {
                        Debug.LogError("Invalid output folder.");
                        return;
                    }

                    ProtocolCodeGenerator.GenerateOpCodes(_definition, folderPath, _namespace);
                }

                if (GUILayout.Button("Generate SnapshotRouting Glue"))
                {
                    var folderPath = AssetDatabase.GetAssetPath(_outputFolder);
                    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    {
                        Debug.LogError("Invalid output folder.");
                        return;
                    }

                    ProtocolCodeGenerator.GenerateSnapshotRoutingGlue(_definition, folderPath, _namespace);
                }

                if (GUILayout.Button("Generate CustomBinary Stubs"))
                {
                    var folderPath = AssetDatabase.GetAssetPath(_outputFolder);
                    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    {
                        Debug.LogError("Invalid output folder.");
                        return;
                    }

                    ProtocolCodeGenerator.GenerateCustomBinaryBackendStubs(_definition, folderPath, _namespace);
                }

                if (GUILayout.Button("Generate All"))
                {
                    var folderPath = AssetDatabase.GetAssetPath(_outputFolder);
                    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    {
                        Debug.LogError("Invalid output folder.");
                        return;
                    }

                    ProtocolCodeGenerator.GenerateOpCodes(_definition, folderPath, _namespace);
                    ProtocolCodeGenerator.GenerateSnapshotRoutingGlue(_definition, folderPath, _namespace);
                    ProtocolCodeGenerator.GenerateCodecBackendStubs(_definition, folderPath, _namespace);
                }
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Import SnapshotRouting Declarations"))
            {
                SnapshotRoutingImporterWindow.OpenWindow();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Create a ProtocolDefinition asset and choose an output folder (suggested: your business protocol package Runtime/Generated).\n" +
                "This is the minimal generator. Next steps: codec backend + router glue.",
                MessageType.Info);
        }
    }
}
