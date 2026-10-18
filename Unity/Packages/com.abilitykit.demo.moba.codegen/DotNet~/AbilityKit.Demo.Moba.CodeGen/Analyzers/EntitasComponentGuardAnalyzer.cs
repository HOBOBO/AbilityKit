using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AbilityKit.Demo.Moba.CodeGen
{
    /// <summary>
    /// Entitas 组件架构护栏（2026-10-12 落地）：
    /// 组件字段应保持平值（基元/字符串/枚举/平值数据类及其数组），blob 引用必须显式标注。
    ///
    /// AKSG9201：IComponent 类的公共字段类型不在平值白名单内（List&lt;运行时对象&gt;/管线对象/接口等），
    ///           且组件类、字段或字段类型均未标注 [EntitasBlob]。
    ///           blob 是显式的债：标注后走专属回滚 Provider（pin coverage），未标注的 blob 是新债，评审可见。
    /// AKSG9202：IComponent 类存在非 public 字段（会被代码生成静默漏掉）。
    ///
    /// 白名单与 tools/AbilityKit.Entitas.CodeGen 的可序列化判定同构：
    /// analyzer 干净 ≈ 可进入生成序列化集；[EntitasBlob] ≈ coverage 豁免注册表的编译期镜像。
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EntitasComponentGuardAnalyzer : DiagnosticAnalyzer
    {
        private const string BlobAttributeName = "EntitasBlob";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                MobaDiagnosticRules.EntitasComponentBlobFieldRule,
                MobaDiagnosticRules.EntitasComponentNonPublicFieldRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        }

        private static void AnalyzeFieldDeclaration(SyntaxNodeAnalysisContext context)
        {
            var fieldDecl = (FieldDeclarationSyntax)context.Node;
            if (fieldDecl.Parent is not BaseTypeDeclarationSyntax typeDecl) return;

            var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, context.CancellationToken) as INamedTypeSymbol;
            if (typeSymbol is null || !ImplementsIComponent(typeSymbol)) return;

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as IFieldSymbol;
                if (fieldSymbol is null) continue;

                if (!fieldSymbol.DeclaredAccessibility.HasFlag(Accessibility.Public))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MobaDiagnosticRules.EntitasComponentNonPublicFieldRule,
                        variable.GetLocation(), typeSymbol.Name, fieldSymbol.Name));
                    continue;
                }

                if (fieldSymbol.IsStatic || fieldSymbol.IsConst) continue;

                if (IsWhitelisted(fieldSymbol.Type, depth: 0, context.CancellationToken)) continue;
                if (HasBlobMarker(typeSymbol) || HasBlobMarker(fieldSymbol) || HasBlobMarker(fieldSymbol.Type)) continue;

                context.ReportDiagnostic(Diagnostic.Create(
                    MobaDiagnosticRules.EntitasComponentBlobFieldRule,
                    variable.GetLocation(),
                    typeSymbol.Name, fieldSymbol.Name, fieldSymbol.Type.ToDisplayString()));
            }
        }

        private static bool ImplementsIComponent(INamedTypeSymbol type)
        {
            foreach (var iface in type.AllInterfaces)
            {
                if (iface.Name == "IComponent") return true;
            }
            return false;
        }

        private static bool HasBlobMarker(ISymbol symbol)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                var name = attr.AttributeClass?.Name;
                if (name == BlobAttributeName || name == BlobAttributeName + "Attribute") return true;
            }
            return false;
        }

        /// <summary>平值白名单：基元/字符串/枚举/数组递归/源码内平值数据类递归（[EntitasBlob] 类型直接放行）。
        /// object 与接口不放行——它们是 blob 的典型形态。</summary>
        private static bool IsWhitelisted(ITypeSymbol type, int depth, System.Threading.CancellationToken ct)
        {
            if (depth > 4) return false;

            if (type.SpecialType != SpecialType.None &&
                type.SpecialType != SpecialType.System_Object) return true;   // 基元 + string；object 除外
            if (type.TypeKind == TypeKind.Enum) return true;
            if (type.TypeKind == TypeKind.Array)
            {
                return IsWhitelisted(((IArrayTypeSymbol)type).ElementType, depth + 1, ct);
            }
            if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)) return false; // 接口/委托等不放行
            if (type is not INamedTypeSymbol named) return false;
            if (HasBlobMarker(named)) return true;
            if (named.Locations.Length == 0 || !named.Locations[0].IsInSource) return false; // 外部程序集类型不可验证

            foreach (var member in named.GetMembers())
            {
                if (member is not IFieldSymbol field || field.IsStatic || field.IsConst) continue;
                if (!field.DeclaredAccessibility.HasFlag(Accessibility.Public)) continue; // 非公共成员不构成数据面
                if (!IsWhitelisted(field.Type, depth + 1, ct)) return false;
            }
            return true;
        }
    }
}
