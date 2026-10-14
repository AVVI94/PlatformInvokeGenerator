using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace PlatformInvokeGenerator.UnitTests;

/// <summary>
/// Result of running the generator and the analyzer over a compilation.
/// </summary>
internal sealed class GeneratorResult(CSharpCompilation input, Compilation output, ImmutableArray<Diagnostic> analyzerDiagnostics, GeneratorDriver driver)
{
    public CSharpCompilation Input { get; } = input;
    public Compilation Output { get; } = output;
    public ImmutableArray<Diagnostic> AnalyzerDiagnostics { get; } = analyzerDiagnostics;
    public GeneratorDriver Driver { get; } = driver;

    public string[] AnalyzerDiagnosticIds => AnalyzerDiagnostics.Select(d => d.Id).OrderBy(id => id).ToArray();

    public Diagnostic[] CompilerErrors => Output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

    public string GeneratedSource(string hintName)
        => Driver.GetRunResult().Results.Single().GeneratedSources.Single(s => s.HintName == hintName).SourceText.ToString();

    public byte[] Emit()
    {
        using var stream = new MemoryStream();
        var result = Output.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return stream.ToArray();
    }

    public Assembly Load()
    {
        using var stream = new MemoryStream(Emit());
        return new AssemblyLoadContext(Output.AssemblyName).LoadFromStream(stream);
    }
}

internal static class GeneratorHarness
{
    public const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Assemblies of the running .NET, the generated code can be loaded and executed.
    /// </summary>
    public static readonly ImmutableArray<MetadataReference> HostReferences =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();

    /// <summary>
    /// .NET Framework 4.6.1 reference assemblies (no System.Runtime.InteropServices.RuntimeInformation).
    /// </summary>
    public static ImmutableArray<MetadataReference> Net461References =>
        Basic.Reference.Assemblies.Net461.References.All.Cast<MetadataReference>().ToImmutableArray();

    /// <summary>
    /// Names of all ImportPlatform values, taken from the generator so new platforms are tested automatically.
    /// </summary>
    public static readonly string[] Platforms = Enum.GetNames(typeof(Generator).Assembly.GetType("PlatformInvokeGenerator.ImportPlatform", throwOnError: true)!);

    public static GeneratorResult Run(string source,
                                      LanguageVersion languageVersion = LanguageVersion.Latest,
                                      IEnumerable<MetadataReference>? references = null,
                                      string assemblyName = "GeneratorTests")
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var input = CSharpCompilation.Create(assemblyName,
                                             [CSharpSyntaxTree.ParseText(source, parseOptions)],
                                             references ?? HostReferences,
                                             new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create([new Generator().AsSourceGenerator()],
                                                              parseOptions: parseOptions,
                                                              driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out var output, out var generatorDiagnostics);

        //an exception stops the generator for the whole compilation (CS8785)
        Assert.Null(driver.GetRunResult().Results.Single().Exception);
        //the problems are reported by the analyzer only
        Assert.Empty(generatorDiagnostics);

        //analyzers run over the compilation including the generated sources, as in the build
        var analyzerDiagnostics = output.WithAnalyzers([new ExternClassAnalyzer()]).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        return new GeneratorResult(input, output, analyzerDiagnostics, driver);
    }
}
