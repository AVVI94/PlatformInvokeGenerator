using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static PlatformInvokeGenerator.UnitTests.GeneratorHarness;

namespace PlatformInvokeGenerator.UnitTests;

public class CodeGenerationTests
{
    /// <summary>
    /// Every subset of the platforms with every string marshalling, as extern methods of a new class and as partial methods
    /// of the decorated class. Catches structural errors of the generated platform checks and string conversions.
    /// </summary>
    [Theory]
    [InlineData(LanguageVersion.CSharp7_3)]
    [InlineData(LanguageVersion.Latest)]
    public void Generator_EveryPlatformCombinationAndStringMarshalling_GeneratedCodeCompiles(LanguageVersion languageVersion)
    {
        var classes = new StringBuilder();
        foreach (var marshalling in new[] { "Default", "Utf8", "WideChar" })
        {
            var externs = new StringBuilder();
            var partials = new StringBuilder();
            for (var mask = 1; mask < 1 << Platforms.Length; mask++)
            {
                var attributes = string.Concat(Platforms.Where((_, i) => (mask & (1 << i)) != 0)
                                                        .Select(p => $"[DllImportFor(\"lib_{p}\", ImportPlatform.{p})] "));
                externs.AppendLine($"        {attributes}public static extern int E{mask}(string s, ref int b, string t);");
                //void partial methods without accessibility are allowed in C# 7.3
                partials.AppendLine($"        {attributes}static partial void P{mask}(string s, ref int b, string t);");
            }
            classes.AppendLine($$"""
                    [ExternClass(GeneratedClassName = "Externs{{marshalling}}", StringMarshalling = NativeStringMarshalling.{{marshalling}})]
                    static class Declarations{{marshalling}}
                    {
                {{externs}}
                    }

                    [ExternClass(StringMarshalling = NativeStringMarshalling.{{marshalling}})]
                    static partial class Partials{{marshalling}}
                    {
                {{partials}}
                    }
                """);
        }

        var result = Run($$"""
            using PlatformInvokeGenerator;
            namespace Combinations
            {
            {{classes}}
            }
            """, languageVersion);

        Assert.Empty(result.AnalyzerDiagnostics);
        Assert.Empty(result.CompilerErrors);
    }

    /// <summary>
    /// The generated platform methods and call methods have exactly the signature of the declaration: default values (formatted
    /// independently of the current culture), modifiers and the marshalling attributes. A difference changes the marshalling silently.
    /// </summary>
    [Fact]
    public void Generator_DefaultValuesModifiersAndAttributes_SignaturesMatchDeclaration()
    {
        const string source = """
            using System;
            using System.Runtime.InteropServices;
            using PlatformInvokeGenerator;

            namespace Signatures
            {
                public struct S { public int X; }
                [Flags] public enum E { None = 0, A = 1, B = 2, Negative = -5 }
                public delegate void Callback(int x);

                internal partial class Native
                {
                    [ExternClass(GeneratedClassName = "Native")]
                    static class Declarations
                    {
                        const int LENGTH = 16;

                        [DllImportFor("lib_w64", ImportPlatform.Win64)]
                        [DllImportFor("lib_linux", ImportPlatform.Linux64)]
                        public static extern int DefaultValues(double d = 1.5, float f = 2.25f, decimal m = 3.5m, long l = long.MinValue, ulong ul = ulong.MaxValue,
                                                               string s = "a\"b\\c\nd", char c = '\'', S st = default, E e = E.B, E flags = E.A | E.B,
                                                               E negative = E.Negative, E? nullableEnum = E.A, int? nullableInt = null, string nullString = null,
                                                               byte b = 255, sbyte sb = -128, double nan = double.NaN, double infinity = double.NegativeInfinity);

                        [DllImportFor("lib_w64", ImportPlatform.Win64)]
                        [DllImportFor("lib_linux", ImportPlatform.Linux64)]
                        [return: MarshalAs(UnmanagedType.I1)]
                        public static extern bool Modifiers([In, Out] int[] array, [MarshalAs(UnmanagedType.LPArray, SizeConst = LENGTH)] byte[] buffer,
                                                            ref int r, out int o, in S i, ref readonly S rr, Callback callback,
                                                            [MarshalAs(UnmanagedType.LPWStr)] string text, int @object, params string[] rest);
                    }
                }
            }
            """;

        //decimal separator of the current culture must not get into the generated code
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("cs-CZ");
        GeneratorResult result;
        try
        {
            result = Run(source);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
        Assert.Empty(result.AnalyzerDiagnostics);

        var assembly = result.Load();
        var declarations = assembly.GetType("Signatures.Native+Declarations", throwOnError: true)!;
        var native = assembly.GetType("Signatures.Native", throwOnError: true)!;
        foreach (var name in new[] { "DefaultValues", "Modifiers" })
        {
            var declaration = declarations.GetMethod(name, AllStatic)!;
            //the call method is managed, the return value is marshalled by the platform imports only
            AssertSameSignature(declaration, native.GetMethod(name, AllStatic)!, compareReturnMarshalling: false);
            foreach (var generatedName in new[] { "__Win64_" + name, "__Linux64_" + name })
                AssertSameSignature(declaration, native.GetMethod(generatedName, AllStatic)!, compareReturnMarshalling: true);
        }
    }

    static void AssertSameSignature(MethodInfo expected, MethodInfo actual, bool compareReturnMarshalling)
    {
        Assert.Equal(expected.ReturnType, actual.ReturnType);
        if (compareReturnMarshalling)
            Assert.Equal(Describe(expected.ReturnParameter), Describe(actual.ReturnParameter));
        Assert.Equal(expected.GetParameters().Select(Describe), actual.GetParameters().Select(Describe));

        //name, type, In/Out/Optional flags, default value and all attributes including MarshalAs with its arguments
        static string Describe(ParameterInfo p)
            => $"{p.Name} {p.ParameterType} {p.Attributes} default={(p.HasDefaultValue ? p.DefaultValue ?? "null" : "-")} "
               + string.Join(" ", p.GetCustomAttributesData().Select(a => a.ToString()).OrderBy(a => a, StringComparer.Ordinal));
    }

    /// <summary>
    /// Generated classes with the same name in different namespaces must not collide (hint names are unique within the generator).
    /// </summary>
    [Fact]
    public void Generator_SameClassNameInDifferentNamespaces_NoCollision()
    {
        var result = Run("""
            using PlatformInvokeGenerator;
            namespace First
            {
                [ExternClass(GeneratedClassName = "Native")]
                static class Declarations { [DllImportFor("a", ImportPlatform.Win64)] public static extern int A(); }
            }
            namespace Second
            {
                [ExternClass(GeneratedClassName = "Native")]
                static class Declarations { [DllImportFor("b", ImportPlatform.Win64)] public static extern int B(); }
            }
            namespace Usage
            {
                static class Caller { static int Call() => First.Native.A() + Second.Native.B(); }
            }
            """);

        Assert.Empty(result.AnalyzerDiagnostics);
        Assert.Empty(result.CompilerErrors);
    }

    /// <summary>
    /// The attribute types are generated into every project, they must not conflict (CS0436) through InternalsVisibleTo.
    /// </summary>
    [Fact]
    public void Attributes_InternalsVisibleToGeneratingProject_NoConflict()
    {
        var library = Run("""
            using PlatformInvokeGenerator;
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Application")]
            namespace Library
            {
                [ExternClass(GeneratedClassName = "LibraryNative")]
                static class Declarations { [DllImportFor("a", ImportPlatform.Win64)] public static extern int A(); }
            }
            """, assemblyName: "Library");
        var libraryReference = MetadataReference.CreateFromImage(library.Emit());

        var application = Run("""
            using PlatformInvokeGenerator;
            namespace Application
            {
                [ExternClass(GeneratedClassName = "ApplicationNative")]
                static class Declarations { [DllImportFor("b", ImportPlatform.Win64)] public static extern int B(); }
                static class Caller { static int Call() => Library.LibraryNative.A() + ApplicationNative.B(); }
            }
            """, references: HostReferences.Add(libraryReference), assemblyName: "Application");

        Assert.DoesNotContain(application.Output.GetDiagnostics(), d => d.Id == "CS0436");
        Assert.Empty(application.CompilerErrors);
    }

    /// <summary>
    /// A change unrelated to the decorated classes must not regenerate the sources (the model has to be equatable).
    /// </summary>
    [Fact]
    public void Generator_UnrelatedChange_OutputCached()
    {
        var result = Run("""
            using PlatformInvokeGenerator;
            [ExternClass(GeneratedClassName = "Native")]
            static class Declarations
            {
                [DllImportFor("lib_w64", ImportPlatform.Win64)]
                [DllImportFor("lib_linux", ImportPlatform.Linux64)]
                public static extern int Foo(double d = 1.5);
            }
            """);

        var driver = result.Driver.RunGenerators(result.Input.AddSyntaxTrees(CSharpSyntaxTree.ParseText("class Unrelated { }")));

        var outputs = driver.GetRunResult().Results.Single().TrackedOutputSteps.SelectMany(s => s.Value).SelectMany(s => s.Outputs).ToArray();
        Assert.NotEmpty(outputs);
        Assert.All(outputs, o => Assert.Equal(IncrementalStepRunReason.Cached, o.Reason));
    }
}
