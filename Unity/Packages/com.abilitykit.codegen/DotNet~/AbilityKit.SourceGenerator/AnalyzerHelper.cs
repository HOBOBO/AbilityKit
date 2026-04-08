using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Share.SourceGenerator
{
    public static class AnalyzerHelper
    {
        public static T? GetFirstChild<T>(this SyntaxNode syntaxNode) where T : SyntaxNode
        {
            foreach (SyntaxNode? childNode in syntaxNode.ChildNodes())
            {
                if (childNode.GetType() == typeof(T))
                {
                    return childNode as T;
                }
            }
            return null;
        }

        public static SyntaxNode? GetFirstChild(this SyntaxNode syntaxNode)
        {
            var childNodes = syntaxNode.ChildNodes();
            if (childNodes.Count() > 0)
            {
                return childNodes.First();
            }
            return null;
        }

        public static T? GetLastChild<T>(this SyntaxNode syntaxNode) where T : SyntaxNode
        {
            foreach (SyntaxNode? childNode in syntaxNode.ChildNodes().Reverse())
            {
                if (childNode.GetType() == typeof(T))
                {
                    return childNode as T;
                }
            }
            return null;
        }

        public static ClassDeclarationSyntax? GetParentClassDeclaration(this SyntaxNode syntaxNode)
        {
            SyntaxNode? parentNode = syntaxNode.Parent;
            while (parentNode != null)
            {
                if (parentNode is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    return classDeclarationSyntax;
                }
                parentNode = parentNode.Parent;
            }
            return null;
        }

        public static bool HasAttribute(this ITypeSymbol typeSymbol, string AttributeName)
        {
            foreach (AttributeData? attributeData in typeSymbol.GetAttributes())
            {
                if (attributeData.AttributeClass?.ToString() == AttributeName)
                {
                    return true;
                }
            }
            return false;
        }

        public static AttributeData? GetFirstAttribute(this INamedTypeSymbol namedTypeSymbol, string AttributeName)
        {
            foreach (AttributeData? attributeData in namedTypeSymbol.GetAttributes())
            {
                if (attributeData.AttributeClass?.ToString() == AttributeName)
                {
                    return attributeData;
                }
            }
            return null;
        }

        public static IEnumerable<ITypeSymbol> BaseTypes(this ITypeSymbol typeSymbol)
        {
            ITypeSymbol? baseType = typeSymbol.BaseType;
            while (baseType != null)
            {
                yield return baseType;
                baseType = baseType.BaseType;
            }
        }

        public static bool IsAssemblyNeedAnalyze(string? assemblyName, params string[] analyzeAssemblyNames)
        {
            if (assemblyName == null)
            {
                return false;
            }
            foreach (string analyzeAssemblyName in analyzeAssemblyNames)
            {
                if (assemblyName == analyzeAssemblyName)
                {
                    return true;
                }
            }
            return false;
        }

        public static string? GetNameSpace(this INamedTypeSymbol namedTypeSymbol)
        {
            INamespaceSymbol? namespaceSymbol = namedTypeSymbol.ContainingNamespace;
            string? namespaceName = namespaceSymbol?.Name;
            while (namespaceSymbol?.ContainingNamespace != null)
            {
                namespaceSymbol = namespaceSymbol.ContainingNamespace;
                if (string.IsNullOrEmpty(namespaceSymbol.Name))
                {
                    break;
                }
                namespaceName = $"{namespaceSymbol.Name}.{namespaceName}";
            }
            if (string.IsNullOrEmpty(namespaceName))
            {
                return null;
            }
            return namespaceName;
        }

        public static bool IsPartial(this ClassDeclarationSyntax classDeclaration)
        {
            foreach (var modifier in classDeclaration.Modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PartialKeyword))
                {
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<T> DescendantNodes<T>(this SyntaxNode syntaxNode) where T : SyntaxNode
        {
            foreach (var descendantNode in syntaxNode.DescendantNodes())
            {
                if (descendantNode is T node)
                {
                    yield return node;
                }
            }
        }

        public static bool IsETEntity(this ITypeSymbol typeSymbol)
        {
            string typeName = typeSymbol.ToString();
            string? baseType = typeSymbol.BaseType?.ToString();
            return typeName == Share.SourceGenerator.Definition.EntityType ||
                   baseType == Share.SourceGenerator.Definition.EntityType ||
                   baseType == Share.SourceGenerator.Definition.LSEntityType;
        }
    }
}