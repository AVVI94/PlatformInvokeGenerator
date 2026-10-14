using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PlatformInvokeGenerator;

/// <summary>
/// Reports the problems of the classes decorated with ExternClassAttribute. The generator only leaves out the code with errors.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExternClassAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ErrorHelper.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(static c =>
        {
            var symbol = (INamedTypeSymbol)c.Symbol;
            var attribute = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Helper.EXTERN_CLASS_ATTRIBUTE_NAME);
            if (attribute is null)
                return;

            var analysis = ExternClassAnalysis.Analyze(symbol, attribute, c.Compilation, c.CancellationToken);
            foreach (var diagnostic in analysis?.Diagnostics ?? Enumerable.Empty<Diagnostic>())
                c.ReportDiagnostic(diagnostic);
        }, SymbolKind.NamedType);
    }
}
