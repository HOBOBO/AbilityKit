#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Triggering.CodeGen;
using AbilityKit.Triggering.Editor.IdMapping;
using AbilityKit.Triggering.Registry;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Triggering.Editor.CodeGen
{
    internal static class TriggerCodegenMenu
    {
        private const string OutputPath = "Packages/com.abilitykit.triggering/Runtime/Generated/Triggering.GeneratedIds.cs";

        [MenuItem("AbilityKit/Triggering/Codegen/Generate Ids")]
        public static void GenerateIds()
        {
            try
            {
                var actions = FindAttributedMethods<TriggerActionAttribute>()
                    .Select(x => new Item(x.Attr.Name, x.Attr.DisplayName, x.Method))
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .ToList();

                var actionClasses = FindAttributedClasses<TriggerActionAttribute>()
                    .Select(x => new Item(x.Attr.Name, x.Attr.DisplayName, x.Type))
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .ToList();
                actions.AddRange(actionClasses);

                var functions = FindAttributedMethods<TriggerFunctionAttribute>()
                    .Select(x => new Item(x.Attr.Name, x.Attr.DisplayName, x.Method))
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .ToList();

                var functionClasses = FindAttributedClasses<TriggerFunctionAttribute>()
                    .Select(x => new Item(x.Attr.Name, x.Attr.DisplayName, x.Type))
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .ToList();
                functions.AddRange(functionClasses);

                var fields = FindPayloadFields()
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .ToList();

                var conditions = FindConditionConfigs()
                    .Where(x => !string.IsNullOrEmpty(x.Name))
                    .ToList();

                var code = Emit(actions, functions, fields, conditions);

                var full = Path.GetFullPath(OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, code);

                AssetDatabase.Refresh();
                Debug.Log($"[TriggerCodegen] Generated: {full}\nActions={actions.Count}, Functions={functions.Count}, Fields={fields.Count}, Conditions={conditions.Count}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        private readonly struct Found<TAttr> where TAttr : Attribute
        {
            public readonly MethodInfo Method;
            public readonly TAttr Attr;

            public Found(MethodInfo method, TAttr attr)
            {
                Method = method;
                Attr = attr;
            }
        }

        private readonly struct FoundType<TAttr> where TAttr : Attribute
        {
            public readonly Type Type;
            public readonly TAttr Attr;

            public FoundType(Type type, TAttr attr)
            {
                Type = type;
                Attr = attr;
            }
        }

        private readonly struct Item
        {
            public readonly string Name;
            public readonly string DisplayName;
            public readonly MemberInfo Member;
            public readonly TriggerParamAttribute[] Params;

            public Item(string name, string displayName, MemberInfo member)
            {
                Name = name;
                DisplayName = displayName;
                Member = member;
                if (member is MethodInfo mi)
                {
                    Params = mi.GetCustomAttributes<TriggerParamAttribute>(false).ToArray();
                }
                else if (member is Type t)
                {
                    Params = t.GetCustomAttributes<TriggerParamAttribute>(false).ToArray();
                }
                else
                {
                    Params = null;
                }
            }
        }

        private static List<Found<TAttr>> FindAttributedMethods<TAttr>() where TAttr : Attribute
        {
            var list = new List<Found<TAttr>>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null) continue;

                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    for (int m = 0; m < methods.Length; m++)
                    {
                        var method = methods[m];
                        var attr = method.GetCustomAttribute<TAttr>(false);
                        if (attr == null) continue;
                        list.Add(new Found<TAttr>(method, attr));
                    }
                }
            }

            return list;
        }

        private static List<FoundType<TAttr>> FindAttributedClasses<TAttr>() where TAttr : Attribute
        {
            var list = new List<FoundType<TAttr>>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null) continue;

                    var attr = type.GetCustomAttribute<TAttr>(false);
                    if (attr == null) continue;
                    list.Add(new FoundType<TAttr>(type, attr));
                }
            }

            return list;
        }

        private static List<Item> FindPayloadFields()
        {
            var list = new List<Item>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null) continue;

                    var members = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    for (int i = 0; i < members.Length; i++)
                    {
                        var mem = members[i];
                        var attr = mem.GetCustomAttribute<TriggerPayloadFieldAttribute>(true);
                        if (attr == null) continue;
                        list.Add(new Item(attr.Name, attr.DisplayName, mem));
                    }
                }
            }

            return list;
        }

        private static List<Item> FindConditionConfigs()
        {
            var list = new List<Item>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                var asm = assemblies[a];
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                if (types == null) continue;

                for (int t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null) continue;

                    var attr = type.GetCustomAttribute<TriggerConditionAttribute>(false);
                    if (attr == null) continue;

                    list.Add(new Item(attr.Type, attr.DisplayName, type));
                }
            }

            return list;
        }

        private static string Emit(List<Item> actions, List<Item> functions, List<Item> fields, List<Item> conditions)
        {
            actions = actions.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            functions = functions.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            fields = fields.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            conditions = conditions.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();

            var nl = "\n";
            var code = "";
            code += "// <auto-generated />" + nl;
            code += "using System;" + nl;
            code += "using AbilityKit.Triggering.Registry;" + nl;
            code += "using AbilityKit.Triggering.CodeGen;" + nl;
            code += "using AbilityKit.Triggering.Runtime;" + nl;
            code += nl;
            code += "namespace AbilityKit.Triggering.Generated" + nl;
            code += "{" + nl;

            code += "    public static class GeneratedActionIds" + nl;
            code += "    {" + nl;
            for (int i = 0; i < actions.Count; i++)
            {
                var name = actions[i].Name;
                var id = StableStringId.Get("action:" + name);
                code += $"        public static readonly ActionId {ToIdent(name)} = new ActionId({id});" + nl;
            }
            code += "    }" + nl;
            code += nl;

            code += "    public static class GeneratedFunctionIds" + nl;
            code += "    {" + nl;
            for (int i = 0; i < functions.Count; i++)
            {
                var name = functions[i].Name;
                var id = StableStringId.Get("func:" + name);
                code += $"        public static readonly FunctionId {ToIdent(name)} = new FunctionId({id});" + nl;
            }
            code += "    }" + nl;
            code += nl;

            code += "    public static class GeneratedFieldIds" + nl;
            code += "    {" + nl;
            for (int i = 0; i < fields.Count; i++)
            {
                var name = fields[i].Name;
                var id = StableStringId.Get("field:" + name);
                code += $"        public const int {ToIdent(name)} = {id};" + nl;
            }
            code += "    }" + nl;

            code += nl;
            code += "    public static class GeneratedIdNames" + nl;
            code += "    {" + nl;
            code += "        public static void RegisterAll(IIdNameRegistry registry)" + nl;
            code += "        {" + nl;
            code += "            if (registry == null) throw new ArgumentNullException(nameof(registry));" + nl;

            for (int i = 0; i < actions.Count; i++)
            {
                var name = actions[i].Name;
                var idv = StableStringId.Get("action:" + name);
                code += $"            registry.RegisterAction(new ActionId({idv}), \"{name}\");" + nl;
            }

            for (int i = 0; i < functions.Count; i++)
            {
                var name = functions[i].Name;
                var idv = StableStringId.Get("func:" + name);
                code += $"            registry.RegisterFunction(new FunctionId({idv}), \"{name}\");" + nl;
            }

            for (int i = 0; i < fields.Count; i++)
            {
                var name = fields[i].Name;
                var idv = StableStringId.Get("field:" + name);
                code += $"            registry.RegisterField({idv}, \"{name}\");" + nl;
            }

            code += "        }" + nl;
            code += "    }" + nl;

            code += nl;
            code += "    public static class GeneratedSchemas" + nl;
            code += "    {" + nl;

            for (int i = 0; i < actions.Count; i++)
            {
                EmitParamArray(ref code, nl, "Action", actions[i]);
            }

            for (int i = 0; i < functions.Count; i++)
            {
                EmitParamArray(ref code, nl, "Function", functions[i]);
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                EmitParamArray(ref code, nl, "Condition", conditions[i]);
            }

            code += "        public static bool TryGetActionParams(ActionId id, out TriggerParamDesc[] parameters)" + nl;
            code += "        {" + nl;
            code += "            switch (id.Value)" + nl;
            code += "            {" + nl;
            for (int i = 0; i < actions.Count; i++)
            {
                var name = actions[i].Name;
                var idv = StableStringId.Get("action:" + name);
                code += $"                case {idv}: parameters = Params_Action_{ToIdent(name)}; return true;" + nl;
            }
            code += "                default: parameters = null; return false;" + nl;
            code += "            }" + nl;
            code += "        }" + nl;

            code += nl;
            code += "        public static bool TryGetFunctionParams(FunctionId id, out TriggerParamDesc[] parameters)" + nl;
            code += "        {" + nl;
            code += "            switch (id.Value)" + nl;
            code += "            {" + nl;
            for (int i = 0; i < functions.Count; i++)
            {
                var name = functions[i].Name;
                var idv = StableStringId.Get("func:" + name);
                code += $"                case {idv}: parameters = Params_Function_{ToIdent(name)}; return true;" + nl;
            }
            code += "                default: parameters = null; return false;" + nl;
            code += "            }" + nl;
            code += "        }" + nl;

            code += nl;
            code += "        public static bool TryGetConditionParams(string type, out TriggerParamDesc[] parameters)" + nl;
            code += "        {" + nl;
            code += "            if (type == null) { parameters = null; return false; }" + nl;
            for (int i = 0; i < conditions.Count; i++)
            {
                var type = conditions[i].Name;
                code += $"            if (string.Equals(type, \"{type}\", StringComparison.Ordinal)) {{ parameters = Params_Condition_{ToIdent(type)}; return true; }}" + nl;
            }
            code += "            parameters = null; return false;" + nl;
            code += "        }" + nl;

            code += "    }" + nl;

            code += "}" + nl;
            return code;
        }

        private static void EmitParamArray(ref string code, string nl, string kind, Item item)
        {
            var name = item.Name;
            var ident = ToIdent(name);
            var ps = item.Params;
            if (ps == null || ps.Length == 0)
            {
                code += $"        private static readonly TriggerParamDesc[] Params_{kind}_{ident} = new TriggerParamDesc[0];" + nl;
                return;
            }

            ps = ps.OrderBy(p => p.Index).ToArray();

            code += $"        private static readonly TriggerParamDesc[] Params_{kind}_{ident} = new TriggerParamDesc[]" + nl;
            code += "        {" + nl;
            for (int i = 0; i < ps.Length; i++)
            {
                var p = ps[i];
                code += $"            new TriggerParamDesc({p.Index}, \"{p.Name}\", ETriggerParamType.{p.Type}, (ETriggerParamSource){(int)p.AllowedSources})," + nl;
            }
            code += "        };" + nl;
        }

        private static string ToIdent(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";

            // Very small sanitizer: keep letters/digits/underscore, replace others with underscore.
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok) chars[i] = '_';
            }

            var s = new string(chars);
            if (s.Length > 0 && (s[0] >= '0' && s[0] <= '9')) s = "_" + s;
            return s;
        }
    }
}
#endif
