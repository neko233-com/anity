using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Anity.Core.Analyzers;

/// <summary>
/// Analyzes code for AOT/IL2CPP compatibility issues.
/// Detects patterns that are unsafe in AOT compilation environments.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AotCompatibilityAnalyzer : DiagnosticAnalyzer
{
  public const string DiagnosticId = "ANITY_AOT001";

  private static readonly LocalizableString Title = "AOT/IL2CPP Incompatible Code Detected";
  private static readonly LocalizableString MessageFormat = "Usage of '{0}' is not compatible with AOT/IL2CPP compilation";
  private static readonly LocalizableString Description =
    "This code pattern is not compatible with AOT (Ahead-of-Time) compilation used by IL2CPP. " +
    "Consider using alternatives that are safe for AOT environments.";

  private static readonly DiagnosticDescriptor Rule = new(
    DiagnosticId,
    Title,
    MessageFormat,
    "AOT",
    DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: Description);

  public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

  public override void Initialize(AnalysisContext context)
  {
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ObjectCreationExpression);
    context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.TypeOfExpression);
  }

  private void AnalyzeNode(SyntaxNodeAnalysisContext context)
  {
    var semanticModel = context.SemanticModel;
    var node = context.Node;

    switch (node)
    {
      case InvocationExpressionSyntax invocation:
        AnalyzeInvocation(context, invocation, semanticModel);
        break;
      case ObjectCreationExpressionSyntax objectCreation:
        AnalyzeObjectCreation(context, objectCreation, semanticModel);
        break;
      case TypeOfExpressionSyntax typeOf:
        AnalyzeTypeOf(context, typeOf, semanticModel);
        break;
    }
  }

  private void AnalyzeInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, SemanticModel semanticModel)
  {
    var symbolInfo = semanticModel.GetSymbolInfo(invocation, context.CancellationToken);
    if (symbolInfo.Symbol is not IMethodSymbol method)
      return;

    var containingType = method.ContainingType;
    if (containingType == null)
      return;

    var typeName = containingType.ToDisplayString();
    var methodName = method.Name;

    // Check for reflection-only AOT-incompatible APIs
    if (IsAotIncompatibleApi(typeName, methodName, out var reason))
    {
      var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), reason);
      context.ReportDiagnostic(diagnostic);
    }

    // Check for System.Reflection.Emit usage
    if (typeName.StartsWith("System.Reflection.Emit", System.StringComparison.Ordinal))
    {
      var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), "System.Reflection.Emit");
      context.ReportDiagnostic(diagnostic);
    }

    // Check for System.Activator.CreateInstance with Type parameter (potential AOT issue)
    if (typeName == "System.Activator" && methodName == "CreateInstance" && method.Parameters.Length > 0)
    {
      if (method.Parameters[0].Type.Name == "Type")
      {
        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), "Activator.CreateInstance(Type)");
        context.ReportDiagnostic(diagnostic);
      }
    }
  }

  private void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, ObjectCreationExpressionSyntax objectCreation, SemanticModel semanticModel)
  {
    var symbolInfo = semanticModel.GetSymbolInfo(objectCreation, context.CancellationToken);
    if (symbolInfo.Symbol is not IMethodSymbol constructor)
      return;

    var containingType = constructor.ContainingType;
    if (containingType == null)
      return;

    var typeName = containingType.ToDisplayString();

    // Check for Reflection.Emit types
    if (typeName.StartsWith("System.Reflection.Emit", System.StringComparison.Ordinal))
    {
      var diagnostic = Diagnostic.Create(Rule, objectCreation.GetLocation(), typeName);
      context.ReportDiagnostic(diagnostic);
    }
  }

  private void AnalyzeTypeOf(SyntaxNodeAnalysisContext context, TypeOfExpressionSyntax typeOf, SemanticModel semanticModel)
  {
    // typeof() itself is AOT-safe, but we check for patterns like typeof(T).GetMethods() etc.
    // This is handled in the invocation analysis
  }

  private static bool IsAotIncompatibleApi(string typeName, string methodName, out string reason)
  {
    reason = string.Empty;

    // System.Reflection.Emit is completely AOT-incompatible
    if (typeName.StartsWith("System.Reflection.Emit", System.StringComparison.Ordinal))
    {
      reason = "System.Reflection.Emit";
      return true;
    }

    // System.Runtime.CompilerServices.DynamicAttribute
    if (typeName == "System.Runtime.CompilerServices.RuntimeFeature" && methodName == "IsDynamicCodeSupported")
    {
      reason = "RuntimeFeature.IsDynamicCodeSupported";
      return true;
    }

    // System.Type.InvokeMember
    if (typeName == "System.Type" && methodName == "InvokeMember")
    {
      reason = "Type.InvokeMember";
      return true;
    }

    // System.Reflection.MethodBase.Invoke
    if (typeName == "System.Reflection.MethodBase" && methodName == "Invoke")
    {
      reason = "MethodBase.Invoke";
      return true;
    }

    return false;
  }
}
