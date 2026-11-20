using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AbilityKit.Entitas.CodeGen.Scanning;

/// <summary>
/// 两种扫描器共用的纯数据形状护栏，避免宽松字段提取遗漏隐藏状态或行为。
/// </summary>
internal static class PlainDataShape
{
    internal static string? Validate(BaseTypeDeclarationSyntax declaration)
    {
        if (declaration is not TypeDeclarationSyntax type) return null;
        if (type.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
            return "readonly data types require an explicit restore constructor";
        if (type.Members.OfType<FieldDeclarationSyntax>().Any(f => f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)))
            return "readonly field is not assignable during restore";
        if (type.Members.OfType<PropertyDeclarationSyntax>().Any())
            return "properties are not included in field serialization";
        if (type is ClassDeclarationSyntax && !type.Modifiers.Any(SyntaxKind.SealedKeyword) || type.Modifiers.Any(SyntaxKind.PartialKeyword) ||
            type.Modifiers.Any(SyntaxKind.StaticKeyword) || type.BaseList != null || type.TypeParameterList != null)
            return "managed data must be a sealed, non-partial, non-generic class without inheritance";
        foreach (var member in type.Members)
        {
            if (member is not FieldDeclarationSyntax field || !field.Modifiers.Any(SyntaxKind.PublicKeyword) ||
                field.Modifiers.Any(SyntaxKind.StaticKeyword) || field.Modifiers.Any(SyntaxKind.ConstKeyword) ||
                field.Modifiers.Any(SyntaxKind.VolatileKeyword) || field.AttributeLists.Count > 0 ||
                field.Declaration.Variables.Count != 1 || field.Declaration.Variables.Any(v => v.Initializer != null))
                return "managed data accepts only public mutable fields without initializers, hidden state or behavior";
        }
        return null;
    }
}
