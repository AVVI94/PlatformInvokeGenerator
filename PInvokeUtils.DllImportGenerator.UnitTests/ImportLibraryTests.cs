using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;
using static PlatformInvokeGenerator.UnitTests.GeneratorHarness;

namespace PlatformInvokeGenerator.UnitTests;

public class ImportLibraryTests
{
    /// <summary>
    /// Values of the generated imports: ExternClass defaults, overridden by DllImportFor(), overridden by DllImportFor(dll, platform).
    /// </summary>
    [Fact]
    public void Import_ClassAndMethodValues_OverriddenInPrecedenceOrder()
    {
        var result = Run("""
            using System.Runtime.InteropServices;
            using PlatformInvokeGenerator;
            namespace Libraries
            {
                internal partial class Native
                {
                    [ExternClass(GeneratedClassName = "Native", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
                    [ImportLibrary("win64.dll", ImportPlatform.Win64)]
                    [ImportLibrary("linux.so", ImportPlatform.Linux64)]
                    static class Declarations
                    {
                        // class libraries and class defaults
                        [DllImportFor]
                        public static extern int ClassDefaults();

                        // own values for all class libraries
                        [DllImportFor(EntryPoint = "renamed", CharSet = CharSet.Ansi, SetLastError = true)]
                        public static extern int MethodValues();

                        // class libraries, Win64 replaced, Win32 added, the platform specific attribute overrides the method values
                        [DllImportFor(EntryPoint = "renamed")]
                        [DllImportFor("own64.dll", ImportPlatform.Win64, CallingConvention = CallingConvention.Cdecl)]
                        [DllImportFor("own32.dll", ImportPlatform.Win32)]
                        public static extern int Overrides();

                        // own libraries only, class libraries are not used
                        [DllImportFor("only.dll", ImportPlatform.Win64)]
                        public static extern int OwnLibrariesOnly();
                    }
                }
            }
            """);
        Assert.Empty(result.AnalyzerDiagnostics);
        var native = result.Load().GetType("Libraries.Native", throwOnError: true)!;

        string Import(string method, string platform)
        {
            var import = native.GetMethod($"__{platform}_{method}", AllStatic);
            if (import is null)
                return "-";
            var a = import.GetCustomAttribute<DllImportAttribute>()!;
            //CharSet.None behaves exactly as CharSet.Ansi, the generator does not emit the default Ansi
            var charSet = a.CharSet == CharSet.None ? CharSet.Ansi : a.CharSet;
            return $"{a.Value} {a.EntryPoint} {a.CallingConvention} {charSet} {a.SetLastError}";
        }

        Assert.Equal("win64.dll ClassDefaults StdCall Unicode False", Import("ClassDefaults", "Win64"));
        Assert.Equal("linux.so ClassDefaults StdCall Unicode False", Import("ClassDefaults", "Linux64"));

        Assert.Equal("win64.dll renamed StdCall Ansi True", Import("MethodValues", "Win64"));
        Assert.Equal("linux.so renamed StdCall Ansi True", Import("MethodValues", "Linux64"));

        Assert.Equal("own64.dll renamed Cdecl Unicode False", Import("Overrides", "Win64"));
        Assert.Equal("own32.dll renamed StdCall Unicode False", Import("Overrides", "Win32"));
        Assert.Equal("linux.so renamed StdCall Unicode False", Import("Overrides", "Linux64"));

        Assert.Equal("only.dll OwnLibrariesOnly StdCall Unicode False", Import("OwnLibrariesOnly", "Win64"));
        Assert.Equal("-", Import("OwnLibrariesOnly", "Linux64"));
    }
}
