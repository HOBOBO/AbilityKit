using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Emilia.Reflection.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Emilia.Kit.Editor
{
    public static class OpenScriptUtility
    {
        public static void OpenScript(object target)
        {
            MonoBehaviour behaviour = target as MonoBehaviour;
            if (behaviour != null)
            {
                MonoScript monoScript = MonoScript.FromMonoBehaviour(behaviour);
                if (monoScript != null)
                {
                    AssetDatabase.OpenAsset(monoScript);
                    return;
                }
            }

            ScriptableObject scriptableObject = target as ScriptableObject;
            if (scriptableObject != null)
            {
                MonoScript monoScript = MonoScript.FromScriptableObject(scriptableObject);
                if (monoScript != null)
                {
                    AssetDatabase.OpenAsset(monoScript);
                    return;
                }
            }

            if (EditorUtility.DisplayDialog("确认", "是否通过Roslyn打开脚本\n（此操作的时间会比较长）", "是", "否")) OpenScript(target.GetType());
        }

        public static void OpenScript(Type type)
        {
            RefreshScriptCache(() => {
                OpenScriptCache openScriptCache = OpenScriptCache.Get();
                string fullName = $"{type.FullName}, {type.Assembly.GetName().Name}";
                TypeInfo typeInfo = openScriptCache.typeInfos.GetValueOrDefault(fullName);
                if (typeInfo == null)
                {
                    Debug.LogError($"未找到脚本信息：{type}");
                    return;
                }

                string path = AssetDatabase.GUIDToAssetPath(typeInfo.guid);
                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript != null) AssetDatabase.OpenAsset(monoScript, typeInfo.line);
            });
        }

        private static ConcurrentDictionary<string, string[]> assemblyDefineCache = new ConcurrentDictionary<string, string[]>();

        public static void RefreshScriptCache(Action onCompleted = null)
        {
            OpenScriptCache openScriptCache = OpenScriptCache.Get();

            List<ScriptInfo> refreshList = new List<ScriptInfo>();
            List<ScriptInfo> successList = new List<ScriptInfo>();

            EditorUtility.DisplayProgressBar("刷新脚本缓存", $"正在刷新脚本缓存", 0);
            assemblyDefineCache.Clear();

            MonoScript[] monoScripts = EditorAssetKit.GetEditorResources<MonoScript>();
            int amount = monoScripts.Length;
            for (int i = 0; i < amount; i++)
            {
                MonoScript monoScript = monoScripts[i];

                string path = AssetDatabase.GetAssetPath(monoScript);
                string guid = AssetDatabase.AssetPathToGUID(path);

                ScriptInfo scriptInfo = openScriptCache.scriptInfos.GetValueOrDefault(guid);
                if (scriptInfo == null)
                {
                    ScriptInfo createScriptInfo = new ScriptInfo();
                    createScriptInfo.guid = guid;
                    createScriptInfo.hash = monoScript.text.GetHashCode();
                    openScriptCache.scriptInfos[guid] = createScriptInfo;

                    refreshList.Add(createScriptInfo);
                    RefreshCache(openScriptCache, createScriptInfo, () => OnCompleted(createScriptInfo, refreshList.Count));
                }
                else
                {
                    int hash = monoScript.text.GetHashCode();
                    if (scriptInfo.hash == hash) continue;
                    scriptInfo.hash = hash;

                    refreshList.Add(scriptInfo);
                    RefreshCache(openScriptCache, scriptInfo, () => OnCompleted(scriptInfo, refreshList.Count));
                }
            }

            if (refreshList.Count == 0)
            {
                onCompleted?.Invoke();
                EditorUtility.ClearProgressBar();
            }

            void OnCompleted(ScriptInfo scriptInfo, int totalCount)
            {
                successList.Add(scriptInfo);
                float t = (float) successList.Count / totalCount;
                EditorUtility.DisplayProgressBar("刷新脚本缓存", $"正在刷新脚本缓存({totalCount}/{successList.Count})", t);

                if (successList.Count != totalCount) return;

                OpenScriptCache.Save();
                onCompleted?.Invoke();
                EditorUtility.ClearProgressBar();
            }
        }

        static void RefreshCache(OpenScriptCache openScriptCache, ScriptInfo scriptInfo, Action onCompleted = null)
        {
            string path = AssetDatabase.GUIDToAssetPath(scriptInfo.guid);
            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            string code = monoScript.text;

            string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(path);
            assemblyName = assemblyName.Replace(".dll", "");

            string csprojPath = $"{EditorAssetKit.dataParentPath}/{assemblyName}.csproj";
            if (File.Exists(csprojPath) == false)
            {
                EditorApplication.delayCall += () => onCompleted();
                return;
            }

            Task.Run(() => {

                List<TypeInfo> typeInfos = new List<TypeInfo>();

                string[] defines = assemblyDefineCache.GetValueOrDefault(csprojPath);

                if (defines == null)
                {
                    string text = File.ReadAllText(csprojPath);
                    defines = text.Split(new[] {"<DefineConstants>"}, StringSplitOptions.None)[1].Split(new[] {"</DefineConstants>"}, StringSplitOptions.None)[0].Split(';');
                    assemblyDefineCache[csprojPath] = defines;
                }

                CSharpParseOptions options = new CSharpParseOptions(preprocessorSymbols: defines);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(code, options);
                SyntaxNode root = tree.GetRoot();

                BuildTypeInfo(root, null, "", AddTypeInfo);

                EditorApplication_Internals.CallDelayed_Internals(() => {
                    RefreshTypeCache();
                    onCompleted?.Invoke();
                }, 0.001d);

                void AddTypeInfo(string fullName, int line)
                {
                    string typeFullName = $"{fullName}, {assemblyName}";
                    TypeInfo createTypeInfo = new TypeInfo();
                    createTypeInfo.guid = scriptInfo.guid;
                    createTypeInfo.typeFullName = typeFullName;
                    createTypeInfo.line = line;

                    typeInfos.Add(createTypeInfo);
                }

                void RefreshTypeCache()
                {
                    scriptInfo.typeInfos = typeInfos;

                    int newTypeInfoCount = typeInfos.Count;
                    for (int j = 0; j < newTypeInfoCount; j++)
                    {
                        TypeInfo newTypeInfo = typeInfos[j];
                        openScriptCache.typeInfos[newTypeInfo.typeFullName] = newTypeInfo;
                    }
                }
            });
        }

        static void BuildTypeInfo(SyntaxNode syntaxNode, SyntaxNode parent, string fullName, Action<string, int> onAddTypeInfo)
        {
            if (syntaxNode is NamespaceDeclarationSyntax namespaceSyntax)
            {
                fullName += $"{namespaceSyntax.Name}.";
            }
            else if (syntaxNode is ClassDeclarationSyntax classSyntax)
            {
                if (parent != null) fullName += "+";

                string className = classSyntax.Identifier.Text;
                if (classSyntax.TypeParameterList != null)
                {
                    int typeParameterCount = classSyntax.TypeParameterList.Parameters.Count;
                    if (typeParameterCount > 0) className += $"`{typeParameterCount}";
                }

                fullName += className;

                onAddTypeInfo.Invoke(fullName, GetLineContext(classSyntax));

                parent = classSyntax;
            }
            else if (syntaxNode is StructDeclarationSyntax structSyntax)
            {
                if (parent != null) fullName += "+";

                string structName = structSyntax.Identifier.Text;
                if (structSyntax.TypeParameterList != null)
                {
                    int typeParameterCount = structSyntax.TypeParameterList.Parameters.Count;
                    if (typeParameterCount > 0) structName += $"`{typeParameterCount}";
                }

                fullName += structName;
                onAddTypeInfo.Invoke(fullName, GetLineContext(structSyntax));

                parent = structSyntax;
            }
            else if (syntaxNode is InterfaceDeclarationSyntax interfaceSyntax)
            {
                if (parent != null) fullName += "+";

                string interfaceName = interfaceSyntax.Identifier.Text;
                if (interfaceSyntax.TypeParameterList != null)
                {
                    int typeParameterCount = interfaceSyntax.TypeParameterList.Parameters.Count;
                    if (typeParameterCount > 0) interfaceName += $"`{typeParameterCount}";
                }

                fullName += interfaceName;
                onAddTypeInfo.Invoke(fullName, GetLineContext(interfaceSyntax));

                parent = interfaceSyntax;
            }
            else if (syntaxNode is EnumDeclarationSyntax enumSyntax)
            {
                if (parent != null) fullName += "+";
                fullName += $"{enumSyntax.Identifier.Text}";
                onAddTypeInfo.Invoke(fullName, GetLineContext(enumSyntax));
                return;
            }
            else if (syntaxNode is DelegateDeclarationSyntax delegateSyntax)
            {
                if (parent != null) fullName += "+";

                string delegateName = delegateSyntax.Identifier.Text;
                if (delegateSyntax.TypeParameterList != null)
                {
                    int typeParameterCount = delegateSyntax.TypeParameterList.Parameters.Count;
                    if (typeParameterCount > 0) delegateName += $"`{typeParameterCount}";
                }

                fullName += delegateName;

                onAddTypeInfo.Invoke(fullName, GetLineContext(delegateSyntax));
                return;
            }

            foreach (SyntaxNode child in syntaxNode.ChildNodes()) BuildTypeInfo(child, parent, fullName, onAddTypeInfo);
        }

        static int GetLineContext(CSharpSyntaxNode syntaxNode)
        {
            return syntaxNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        }
    }
}