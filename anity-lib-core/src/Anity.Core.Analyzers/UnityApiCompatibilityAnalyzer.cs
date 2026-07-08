using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Anity.Core.Analyzers;

/// <summary>
/// Analyzes code to ensure it follows Unity API compatibility patterns.
/// Checks for common issues when migrating from Unity to Anity.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class UnityApiCompatibilityAnalyzer : DiagnosticAnalyzer
{
  public const string DiagnosticIdUnityApi = "ANITY_API001";
  public const string DiagnosticIdIl2CppSafe = "ANITY_API002";

  private static readonly LocalizableString TitleApi = "Unity API Compatibility Warning";
  private static readonly LocalizableString MessageFormatApi = "Type '{0}' should use Unity-compatible API pattern";

  private static readonly LocalizableString TitleIl2Cpp = "IL2CPP Safety Warning";
  private static readonly LocalizableString MessageFormatIl2Cpp = "Usage of '{0}' may not be safe in IL2CPP builds";

  private static readonly DiagnosticDescriptor RuleApi = new(
    DiagnosticIdUnityApi,
    TitleApi,
    MessageFormatApi,
    "API",
    DiagnosticSeverity.Info,
    isEnabledByDefault: true);

  private static readonly DiagnosticDescriptor RuleIl2Cpp = new(
    DiagnosticIdIl2CppSafe,
    TitleIl2Cpp,
    MessageFormatIl2Cpp,
    "API",
    DiagnosticSeverity.Warning,
    isEnabledByDefault: true);

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    ImmutableArray.Create(RuleApi, RuleIl2Cpp);

  public override void Initialize(AnalysisContext context)
  {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.ClassDeclaration);
    context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.StructDeclaration);
    context.RegisterSyntaxNodeAction(AnalyzeTypeDeclaration, SyntaxKind.InterfaceDeclaration);
  }

  private void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
  {
    var node = context.Node;
    var semanticModel = context.SemanticModel;

    var symbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);
    if (symbol == null)
      return;

    var fullyQualifiedName = symbol.ToDisplayString();

    // Check if this type is in a Unity namespace
    if (!IsUnityNamespace(fullyQualifiedName))
      return;

    // Check for IL2CPP-unsafe patterns
    CheckIl2CppSafety(context, symbol);
  }

  private void CheckIl2CppSafety(SyntaxNodeAnalysisContext context, ISymbol symbol)
  {
    if (symbol is not INamedTypeSymbol typeSymbol)
      return;

    // Check for generic type usage that may be problematic in IL2CPP
    if (typeSymbol.IsGenericType)
    {
      foreach (var typeArgument in typeSymbol.TypeArguments)
      {
        if (typeArgument.TypeKind == TypeKind.TypeParameter)
        {
          // Generic type parameters can be problematic in IL2CPP
          var diagnostic = Diagnostic.Create(RuleIl2Cpp, context.Node.GetLocation(), typeSymbol.Name);
          context.ReportDiagnostic(diagnostic);
          break;
        }
      }
    }

    // Check for abstract classes without [Preserve]
    if (typeSymbol.IsAbstract && !HasPreserveAttribute(typeSymbol))
    {
      var diagnostic = Diagnostic.Create(RuleIl2Cpp, context.Node.GetLocation(), typeSymbol.Name);
      context.ReportDiagnostic(diagnostic);
    }
  }

  private static bool IsUnityNamespace(string fullyQualifiedName)
  {
    return fullyQualifiedName.StartsWith("UnityEngine", System.StringComparison.Ordinal) ||
           fullyQualifiedName.StartsWith("UnityEditor", System.StringComparison.Ordinal) ||
           fullyQualifiedName.StartsWith("Unity.", System.StringComparison.Ordinal);
  }

  private static bool HasPreserveAttribute(INamedTypeSymbol typeSymbol)
  {
    foreach (var attribute in typeSymbol.GetAttributes())
    {
      if (attribute.AttributeClass?.Name == "PreserveAttribute" ||
          attribute.AttributeClass?.Name == "Preserve")
      {
        return true;
      }
    }

    return false;
  }
}
