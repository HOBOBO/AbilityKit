using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Core.Common.SnapshotRouting;
using AbilityKit.ProtocolEditor.Schema;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ProtocolEditor.UI
{
    public sealed class SnapshotRoutingImporterWindow : EditorWindow
    {
        private DefaultAsset _outputFolder;
        private string _domain = "Imported";
        private ProtocolDefinition.CodecBackend _defaultBackend = ProtocolDefinition.CodecBackend.CustomBinary;

        internal static void OpenWindow()
        {
            GetWindow<SnapshotRoutingImporterWindow>("Import SnapshotRouting");
        }

        [MenuItem("AbilityKit/Protocol/Import SnapshotRouting Declarations")]
        private static void Open()
        {
            OpenWindow();
        }

        private void OnGUI()
        {
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);
            _domain = EditorGUILayout.TextField("Domain", _domain);
            _defaultBackend = (ProtocolDefinition.CodecBackend)EditorGUILayout.EnumPopup("Default Backend", _defaultBackend);

            using (new EditorGUI.DisabledScope(_outputFolder == null))
            {
                if (GUILayout.Button("Scan & Generate ProtocolDefinition Assets"))
                {
                    var folderPath = AssetDatabase.GetAssetPath(_outputFolder);
                    if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    {
                        Debug.LogError("Invalid output folder.");
                        return;
                    }

                    Import(folderPath);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Scans all assemblies for methods marked with SnapshotRouting attributes and generates ProtocolDefinition assets grouped by RegistryId.",
                MessageType.Info);
        }

        private void Import(string outputFolder)
        {
            var groups = new Dictionary<string, List<ProtocolDefinition.MessageDefinition>>(StringComparer.Ordinal);

            foreach (var method in EnumerateAllMethods())
            {
                foreach (var attrObj in method.GetCustomAttributes(inherit: false))
                {
                    if (attrObj is SnapshotDecoderAttribute dec)
                    {
                        Add(groups, dec.RegistryId, new ProtocolDefinition.MessageDefinition
                        {
                            Name = DeriveMessageName(method.Name, ProtocolDefinition.ChannelKind.SnapshotDecoder),
                            OpCode = dec.OpCode,
                            Channel = ProtocolDefinition.ChannelKind.SnapshotDecoder,
                            PayloadTypeName = CSharpTypeNameUtility.ToCSharpTypeName(dec.PayloadType),
                            PipelineOrder = 0,
                            Backend = _defaultBackend,
                        });
                    }
                    else if (attrObj is SnapshotCmdHandlerAttribute cmd)
                    {
                        Add(groups, cmd.RegistryId, new ProtocolDefinition.MessageDefinition
                        {
                            Name = DeriveMessageName(method.Name, ProtocolDefinition.ChannelKind.SnapshotCmdHandler),
                            OpCode = cmd.OpCode,
                            Channel = ProtocolDefinition.ChannelKind.SnapshotCmdHandler,
                            PayloadTypeName = CSharpTypeNameUtility.ToCSharpTypeName(cmd.PayloadType),
                            PipelineOrder = 0,
                            Backend = _defaultBackend,
                        });
                    }
                    else if (attrObj is SnapshotPipelineStageAttribute stage)
                    {
                        Add(groups, stage.RegistryId, new ProtocolDefinition.MessageDefinition
                        {
                            Name = DeriveMessageName(method.Name, ProtocolDefinition.ChannelKind.SnapshotPipelineStage),
                            OpCode = stage.OpCode,
                            Channel = ProtocolDefinition.ChannelKind.SnapshotPipelineStage,
                            PayloadTypeName = CSharpTypeNameUtility.ToCSharpTypeName(stage.PayloadType),
                            PipelineOrder = stage.Order,
                            Backend = _defaultBackend,
                        });
                    }
                }
            }

            foreach (var kv in groups)
            {
                var registryId = kv.Key;
                var messages = kv.Value;

                var assetName = $"ProtocolDefinition.{SanitizeAssetName(registryId)}.asset";
                var assetPath = Path.Combine(outputFolder, assetName).Replace('\\', '/');

                var asset = AssetDatabase.LoadAssetAtPath<ProtocolDefinition>(assetPath);
                if (asset == null)
                {
                    asset = CreateInstance<ProtocolDefinition>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                }

                asset.RegistryId = registryId;
                asset.Domain = _domain;
                asset.Messages = Deduplicate(messages);

                EditorUtility.SetDirty(asset);
                Debug.Log($"Imported ProtocolDefinition: {assetPath} (messages: {asset.Messages.Count})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Add(Dictionary<string, List<ProtocolDefinition.MessageDefinition>> groups, string registryId, ProtocolDefinition.MessageDefinition msg)
        {
            if (string.IsNullOrWhiteSpace(registryId)) return;

            if (!groups.TryGetValue(registryId, out var list))
            {
                list = new List<ProtocolDefinition.MessageDefinition>();
                groups.Add(registryId, list);
            }

            list.Add(msg);
        }

        private static List<ProtocolDefinition.MessageDefinition> Deduplicate(List<ProtocolDefinition.MessageDefinition> input)
        {
            var map = new Dictionary<string, ProtocolDefinition.MessageDefinition>(StringComparer.Ordinal);

            foreach (var m in input)
            {
                if (m == null) continue;
                var key = $"{m.Channel}|{m.OpCode}|{m.PayloadTypeName}|{m.PipelineOrder}|{m.Name}";
                if (!map.ContainsKey(key)) map[key] = m;
            }

            return map.Values.OrderBy(x => x.OpCode).ThenBy(x => x.Channel).ToList();
        }

        private static IEnumerable<MethodInfo> EnumerateAllMethods()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var asm in assemblies)
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    MethodInfo[] methods;
                    try
                    {
                        methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var m in methods)
                        yield return m;
                }
            }
        }

        private static string SanitizeAssetName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Registry";

            var chars = name.Where(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.').ToArray();
            var result = new string(chars);
            if (string.IsNullOrWhiteSpace(result)) return "Registry";
            return result;
        }

        private static string DeriveMessageName(string methodName, ProtocolDefinition.ChannelKind kind)
        {
            if (string.IsNullOrWhiteSpace(methodName)) return "Msg";

            var prefix = kind switch
            {
                ProtocolDefinition.ChannelKind.SnapshotDecoder => "Decode",
                ProtocolDefinition.ChannelKind.SnapshotCmdHandler => "Handle",
                ProtocolDefinition.ChannelKind.SnapshotPipelineStage => "Stage",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(prefix) && methodName.StartsWith(prefix, StringComparison.Ordinal))
            {
                var rest = methodName.Substring(prefix.Length);
                if (!string.IsNullOrWhiteSpace(rest)) return rest;
            }

            return methodName;
        }
    }
}
