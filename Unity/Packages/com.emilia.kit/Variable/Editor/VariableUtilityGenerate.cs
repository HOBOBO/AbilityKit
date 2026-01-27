using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Emilia.Variables.Editor
{
    public static class VariableUtilityGenerate
    {
        public static void Generate(VariableGenerateSetting variableSetting)
        {
            Type[] types = TypeCache.GetTypesDerivedFrom<Variable>().ToArray();

            List<Type> equalTypes = new List<Type>();
            List<Type> notEqualTypes = new List<Type>();
            List<Type> greaterOrEqual = new List<Type>();
            List<Type> greater = new List<Type>();
            List<Type> smallerOrEqual = new List<Type>();
            List<Type> smaller = new List<Type>();
            List<Type> add = new List<Type>();
            List<Type> subtract = new List<Type>();
            List<Type> multiply = new List<Type>();
            List<Type> divide = new List<Type>();

            int amount = types.Length;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract) continue;

                if (HasOverloadedEqualityOperator(type)) equalTypes.Add(type);
                if (HasOverloadedInequalityOperator(type)) notEqualTypes.Add(type);
                if (HasOverloadedGreaterOperator(type)) greater.Add(type);
                if (HasOverloadedGreaterOrEqualOperator(type)) greaterOrEqual.Add(type);
                if (HasOverloadedSmallerOperator(type)) smaller.Add(type);
                if (HasOverloadedSmallerOrEqualOperator(type)) smallerOrEqual.Add(type);
                if (HasOverloadedAddOperator(type)) add.Add(type);
                if (HasOverloadedSubtractOperator(type)) subtract.Add(type);
                if (HasOverloadedMultiplyOperator(type)) multiply.Add(type);
                if (HasOverloadedDivideOperator(type)) divide.Add(type);
            }

            string createContent = GetCreateContent(types);
            string createFromPoolContent = GetCreateFromPoolContent(types);
            string convertContext = GetConvertContent(types);
            string equalContent = GetEqualContent(equalTypes);
            string notEqualContent = GetNotEqualContent(notEqualTypes);
            string greaterOrEqualContent = GetGreaterOrEqualContent(greaterOrEqual);
            string greaterContent = GetGreaterContent(greater);
            string smallerOrEqualContent = GetSmallerOrEqualContent(smallerOrEqual);
            string smallerContent = GetSmallerContent(smaller);
            string addContent = GetAddContent(add);
            string subtractContent = GetSubtractContent(subtract);
            string multiplyContent = GetMultiplyContent(multiply);
            string divideContent = GetDivideContent(divide);

            string template = VariableGenerateSetting.TemplateText;
            template = template.Replace(VariableGenerateSetting.CreateIdentifier, createContent);
            template = template.Replace(VariableGenerateSetting.CreateFromPoolIdentifier, createFromPoolContent);
            template = template.Replace(VariableGenerateSetting.ConvertIdentifier, convertContext);
            template = template.Replace(VariableGenerateSetting.EqualIdentifier, equalContent);
            template = template.Replace(VariableGenerateSetting.NotEqualIdentifier, notEqualContent);
            template = template.Replace(VariableGenerateSetting.GreaterThanOrEqualIdentifier, greaterOrEqualContent);
            template = template.Replace(VariableGenerateSetting.GreaterThanIdentifier, greaterContent);
            template = template.Replace(VariableGenerateSetting.LessThanOrEqualIdentifier, smallerOrEqualContent);
            template = template.Replace(VariableGenerateSetting.LessThanIdentifier, smallerContent);
            template = template.Replace(VariableGenerateSetting.AddIdentifier, addContent);
            template = template.Replace(VariableGenerateSetting.SubtractIdentifier, subtractContent);
            template = template.Replace(VariableGenerateSetting.MultiplyIdentifier, multiplyContent);
            template = template.Replace(VariableGenerateSetting.DivideIdentifier, divideContent);

            string projectPath = Directory.GetParent(Application.dataPath).ToString();
            string fullPath = $"{projectPath}/{variableSetting.filePath}/{VariableGenerateSetting.FileName}.cs";
            if (File.Exists(fullPath)) File.Delete(fullPath);
            File.WriteAllText(fullPath, template);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static string GetCreateContent(Type[] types)
        {
            string content = "";
            int amount = types.Length;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract) continue;
                Type genericType = GetVariableGenericTypeType(type);
                if (genericType == null) continue;

                content += $"\t\t\t {{typeof({GetTypeFullName(genericType)}), () => new {type.FullName}()}},\n";
            }

            return content;
        }

        public static string GetCreateFromPoolContent(Type[] types)
        {
            string content = "";
            int amount = types.Length;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract) continue;
                Type genericType = GetVariableGenericTypeType(type);
                if (genericType == null) continue;

                content += $"\t\t\t {{typeof({GetTypeFullName(genericType)}), () => Emilia.Reference.ReferencePool.Acquire<{type.FullName}>()}},\n";
            }

            return content;
        }

        public static string GetConvertContent(Type[] types)
        {
            string content = "";
            int amount = types.Length;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                if (type.IsAbstract) continue;

                MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
                foreach (MethodInfo method in methods)
                {
                    if (method.Name != "op_Implicit" && method.Name != "op_Explicit") continue;

                    Type to = method.ReturnType;
                    if (to != type) continue;

                    Type form = method.GetParameters().FirstOrDefault()?.ParameterType;
                    if (typeof(Variable).IsAssignableFrom(form) == false) continue;

                    content += $"\t\t\t {{new ConvertMatching(typeof({form.FullName}), typeof({to.FullName})), variable => ({to.FullName}) variable}},\n";
                }
            }

            return content;
        }

        private static Type GetVariableGenericTypeType(Type type)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Variable<>)) return type.GetGenericArguments().FirstOrDefault();
                type = type.BaseType;
            }
            return null;
        }

        public static string GetTypeFullName(Type type)
        {
            string genericTypeFullName = GetGenericTypeFullName(type);
            if (string.IsNullOrEmpty(genericTypeFullName) == false) return genericTypeFullName;
            return type.FullName;
        }

        public static string GetGenericTypeFullName(Type genericType)
        {
            if (genericType.IsGenericType == false) return string.Empty;

            Type[] genericArguments = genericType.GetGenericArguments();
            if (genericArguments.Length == 0) return string.Empty;

            string genericContent = "";
            for (int j = 0; j < genericArguments.Length; j++)
            {
                Type argument = genericArguments[j];
                if (argument.IsGenericParameter) continue;
                genericContent += argument.FullName;
                if (j < genericArguments.Length - 1) genericContent += ", ";
            }

            string genericTypeName = genericType.Name;
            if (genericTypeName.Contains("`")) genericTypeName = genericTypeName.Substring(0, genericTypeName.IndexOf("`", StringComparison.Ordinal));

            return $"{genericType.Namespace}.{genericTypeName}<{genericContent}>";
        }

        public static string GetEqualContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left == ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetNotEqualContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left != ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetGreaterOrEqualContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left >= ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetGreaterContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left > ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetSmallerOrEqualContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left <= ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetSmallerContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left < ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetAddContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left + ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetSubtractContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left - ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetMultiplyContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left * ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static string GetDivideContent(List<Type> types)
        {
            string content = "";
            int amount = types.Count;
            for (int i = 0; i < amount; i++)
            {
                Type type = types[i];
                content += $"\t\t\t {{typeof({type.FullName}), (left, right) => ({type.FullName}) left / ({type.FullName}) right}},\n";
            }
            return content;
        }

        public static bool HasOverloadedEqualityOperator(Type type)
        {
            return type.GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedInequalityOperator(Type type)
        {
            return type.GetMethod("op_Inequality", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedGreaterOperator(Type type)
        {
            return type.GetMethod("op_GreaterThan", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedGreaterOrEqualOperator(Type type)
        {
            return type.GetMethod("op_GreaterThanOrEqual", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedSmallerOperator(Type type)
        {
            return type.GetMethod("op_LessThan", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedSmallerOrEqualOperator(Type type)
        {
            return type.GetMethod("op_LessThanOrEqual", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedAddOperator(Type type)
        {
            return type.GetMethod("op_Addition", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedSubtractOperator(Type type)
        {
            return type.GetMethod("op_Subtraction", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedMultiplyOperator(Type type)
        {
            return type.GetMethod("op_Multiply", BindingFlags.Static | BindingFlags.Public) != null;
        }

        public static bool HasOverloadedDivideOperator(Type type)
        {
            return type.GetMethod("op_Division", BindingFlags.Static | BindingFlags.Public) != null;
        }
    }
}