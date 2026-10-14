using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static PlatformInvokeGenerator.UnitTests.GeneratorHarness;

namespace PlatformInvokeGenerator.UnitTests;

public class DiagnosticTests
{
    const string WIN64 = "[DllImportFor(\"a\", ImportPlatform.Win64)]";

    //diagnostics of the class point to [ExternClass], of a method to its [DllImportFor]
    public static TheoryData<string, string> InvalidDeclarations => new()
    {
        { "PI1001", $$"""[ExternClass(GeneratedClassName = "1abc")] static class C { {{WIN64}} static extern void M(); }""" },
        { "PI1002", $$"""[ExternClass(GeneratedClassName = "G")] static class C { [DllImport("a")] {{WIN64}} static extern void M(); }""" },
        { "PI1003", $$"""[ExternClass(GeneratedClassName = "G")] static class C { {{WIN64}} [DllImportFor("b", ImportPlatform.Win64)] static extern void M(); }""" },
        { "PI1004", $$"""[ExternClass(GeneratedClassName = "G", ClassAccessModifier = AccessModifiers.Private)] static class C { {{WIN64}} static extern void M(); }""" },
        { "PI1005", $$"""[ExternClass(GeneratedClassName = "G", GeneratedClassNamespace = "Bad.1ns")] static class C { {{WIN64}} static extern void M(); }""" },
        { "PI1006", $$"""[ExternClass] static class C { {{WIN64}} static extern void M(); }""" },
        { "PI1007", $$"""[ExternClass(GeneratedClassName = "G")] static class C { [DllImportFor("a", ImportPlatform.Win64, StaticCallMethod = true)] [DllImportFor("b", ImportPlatform.Win32, StaticCallMethod = false)] static extern void M(); }""" },
        { "PI1008", $$"""[ExternClass] static partial class C<T> { {{WIN64}} static partial void M(); }""" },
        { "PI1010", $$"""[ExternClass(GeneratedClassName = "G")] static partial class C { {{WIN64}} static partial void M(); }""" },
        { "PI1012", $$"""[ExternClass(GeneratedClassName = "G")] static class C { {{WIN64}} static void M() { } }""" },
        { "PI1013", $$"""[ExternClass(GeneratedClassName = "G")] static class C { {{WIN64}} static extern void M<T>(); }""" },
        { "PI1014", $$"""[ExternClass(GeneratedClassName = "G")] static class C { [DllImportFor("a", (ImportPlatform)42)] static extern void M(); }""" },
        { "PI1015", $$"""[ExternClass(GeneratedClassName = "G")] [ImportLibrary("a", ImportPlatform.Win64)] static class C { [DllImportFor] [DllImportFor(EntryPoint = "X")] static extern void M(); }""" },
        { "PI1016", $$"""[ExternClass(GeneratedClassName = "G")] static class C { [DllImportFor] static extern void M(); }""" },
        { "PI1017", $$"""[ExternClass(GeneratedClassName = "G")] static class C { [DllImportFor("a", ImportPlatform.Win64, StringMarshalling = NativeStringMarshalling.Utf8)] static extern string M(); }""" },
        { "PI1017", $$"""[ExternClass(GeneratedClassName = "G", StringMarshalling = NativeStringMarshalling.WideChar)] static class C { {{WIN64}} static extern void M(ref string s); }""" },
    };

    /// <summary>
    /// The analyzer reports the problem and the generated code does not add follow-up compiler errors.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidDeclarations))]
    public void Analyzer_InvalidDeclaration_ReportsOnlyAnalyzerDiagnostic(string expectedId, string declaration)
    {
        var source = $$"""
            using System.Runtime.InteropServices;
            using PlatformInvokeGenerator;
            namespace Invalid
            {
                {{declaration}}
            }
            """;
        var result = Run(source);

        Assert.Equal([expectedId], result.AnalyzerDiagnosticIds);
        Assert.Empty(result.CompilerErrors);

        //the diagnostic points to the attribute (or the method) causing it
        var location = result.AnalyzerDiagnostics.Single().Location;
        var reported = source.Substring(location.SourceSpan.Start, location.SourceSpan.Length);
        var expectedStart = expectedId switch
        {
            "PI1001" or "PI1004" or "PI1005" or "PI1006" or "PI1008" => "ExternClass",
            _ => "DllImportFor",
        };
        Assert.StartsWith(expectedStart, reported);
    }

    /// <summary>
    /// A method with errors is skipped, the other methods of the class are generated and the callers of the broken methods still compile.
    /// </summary>
    [Fact]
    public void Generator_MethodWithErrors_OtherMethodsGenerated()
    {
        var result = Run("""
            using PlatformInvokeGenerator;
            namespace Mixed
            {
                [ExternClass]
                internal static partial class Partials
                {
                    [DllImportFor("lib_w32", ImportPlatform.Win32)]
                    [DllImportFor("lib_w64", ImportPlatform.Win64)]
                    public static partial int Valid(int x);

                    // PI1003, partial method with accessibility and return value must still get an implementation
                    [DllImportFor("a", ImportPlatform.Win64)]
                    [DllImportFor("b", ImportPlatform.Win64)]
                    public static partial int Broken(int x);
                }

                [ExternClass(GeneratedClassName = "Externs")]
                static class Declarations
                {
                    [DllImportFor("lib_w64", ImportPlatform.Win64)]
                    public static extern int ValidExtern();

                    // PI1014, the call method must still exist for the callers
                    [DllImportFor("a", (ImportPlatform)42)]
                    public static extern int BrokenExtern();
                }

                static class Callers
                {
                    public static int Valid() => Partials.Valid(1) + Externs.ValidExtern();
                    public static int Broken() => Partials.Broken(1);
                    public static int BrokenExtern() => Externs.BrokenExtern();
                }
            }
            """);

        Assert.Equal(["PI1003", "PI1014"], result.AnalyzerDiagnosticIds);
        Assert.Empty(result.CompilerErrors);

        var callers = result.Load().GetType("Mixed.Callers", throwOnError: true)!;
        Assert.IsType<DllNotFoundException>(InvokeException(callers, "Valid"));
        Assert.IsType<NotSupportedException>(InvokeException(callers, "Broken"));
        Assert.IsType<NotSupportedException>(InvokeException(callers, "BrokenExtern"));
    }

    static Exception? InvokeException(Type type, string method)
        => Assert.Throws<TargetInvocationException>(() => type.GetMethod(method, AllStatic)!.Invoke(null, null)).InnerException;

    /// <summary>
    /// .NET Framework 4.6.1 without the RuntimeInformation package: only the methods with OS or architecture checks are reported,
    /// Win32 + Win64 needs Environment.Is64BitProcess only. The generated code is C# 7.3.
    /// </summary>
    [Fact]
    public void Analyzer_Net461WithoutRuntimeInformation_ReportsOnlyMethodsWithPlatformChecks()
    {
        var result = Run("""
            using PlatformInvokeGenerator;
            namespace Framework
            {
                [ExternClass(GeneratedClassName = "Native")]
                static class Declarations
                {
                    [DllImportFor("lib_w32", ImportPlatform.Win32)]
                    [DllImportFor("lib_w64", ImportPlatform.Win64)]
                    public static extern int WindowsOnly();

                    [DllImportFor("lib_w64", ImportPlatform.Win64)]
                    [DllImportFor("lib_linux", ImportPlatform.Linux64)]
                    public static extern int WindowsAndLinux();

                    [DllImportFor("lib_w64", ImportPlatform.Win64)]
                    [DllImportFor("lib_arm64", ImportPlatform.WinArm64)]
                    public static extern int WindowsArchitectures();
                }

                static class Callers
                {
                    static int Call() => Native.WindowsOnly() + Native.WindowsAndLinux() + Native.WindowsArchitectures();
                }
            }
            """, LanguageVersion.CSharp7_3, Net461References);

        Assert.Equal(["PI1011", "PI1011"], result.AnalyzerDiagnosticIds);
        Assert.DoesNotContain(result.AnalyzerDiagnostics, d => d.GetMessage().Contains("WindowsOnly"));
        Assert.Empty(result.CompilerErrors);
    }

    /// <summary>
    /// .NET Framework cannot load a class with extern methods without DllImport, extern methods in the decorated class are reported.
    /// </summary>
    [Fact]
    public void Analyzer_ExternMethodInDecoratedClassOnNetFramework_ReportsWarning()
    {
        var result = Run($$"""
            using PlatformInvokeGenerator;
            namespace Framework
            {
                [ExternClass]
                static partial class Native { {{WIN64}} static extern void M(); }
            }
            """, LanguageVersion.CSharp7_3, Net461References);

        Assert.Equal(["PI1009"], result.AnalyzerDiagnosticIds);
        Assert.Empty(result.CompilerErrors);
    }
}
