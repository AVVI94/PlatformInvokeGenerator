using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;
using static PlatformInvokeGenerator.UnitTests.GeneratorHarness;

namespace PlatformInvokeGenerator.UnitTests;

/// <summary>
/// Executes the generated call methods on the current OS and process architecture. The libraries do not exist,
/// the name in DllNotFoundException shows which platform import was selected.
/// </summary>
public class DispatchTests
{
    public static TheoryData<string[]> PlatformSets => new()
    {
        new[] { "Win32", "Win64", "WinArm64", "Linux64", "LinuxArm64", "Osx", "OsxX64", "OsxArm64" },
        //OS fallback imports only
        new[] { "Win32", "Win64", "Linux64", "Osx" },
        //architecture specific imports with Win64 as the only fallback
        new[] { "Win64", "WinArm64", "LinuxArm64", "OsxX64", "OsxArm64" },
        new[] { "Linux64", "Osx" },
        new[] { "Win32", "Win64" },
        new[] { "WinArm64", "LinuxArm64", "OsxArm64" },
        new[] { "Linux64" },
    };

    [Theory]
    [MemberData(nameof(PlatformSets))]
    public void CallMethod_ImportedPlatforms_CallsImportOfCurrentPlatform(string[] platforms)
    {
        var attributes = string.Concat(platforms.Select(p => $"[DllImportFor(\"lib_{p}\", ImportPlatform.{p})] "));
        var result = Run($$"""
            using PlatformInvokeGenerator;
            namespace Dispatch
            {
                internal partial class Native
                {
                    [ExternClass(GeneratedClassName = "Native")]
                    static class Declarations
                    {
                        {{attributes}}
                        public static extern int Call(int value);
                    }
                }
            }
            """);
        Assert.Empty(result.AnalyzerDiagnostics);

        var call = result.Load().GetType("Dispatch.Native", throwOnError: true)!.GetMethod("Call", AllStatic)!;
        var exception = Assert.Throws<TargetInvocationException>(() => call.Invoke(null, [1])).InnerException;

        var expected = ExpectedPlatform(platforms);
        if (expected is null)
            Assert.IsType<NotSupportedException>(exception);
        else
        {
            Assert.IsType<DllNotFoundException>(exception);
            Assert.Contains($"'lib_{expected}'", exception!.Message);
        }
    }

    /// <summary>
    /// The import selected by the documented rules, null when NotSupportedException is expected.
    /// </summary>
    static string? ExpectedPlatform(string[] platforms)
    {
        //single import, no checks
        if (platforms.Length == 1)
            return platforms[0];

        string? os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Win"
                   : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux"
                   : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Osx"
                   : null;
        //windows imports only, the OS check is skipped
        if (platforms.All(p => p.StartsWith("Win")))
            os = "Win";
        if (os is null || !platforms.Any(p => p.StartsWith(os)))
            return null;

        //architecture specific import first
        var architectureSpecific = os + RuntimeInformation.ProcessArchitecture;
        if (platforms.Contains(architectureSpecific))
            return architectureSpecific;

        //OS fallback import
        return os switch
        {
            "Win" when platforms.Contains("Win32") && platforms.Contains("Win64") => Environment.Is64BitProcess ? "Win64" : "Win32",
            "Win" => platforms.FirstOrDefault(p => p is "Win32" or "Win64"),
            "Linux" => platforms.Contains("Linux64") ? "Linux64" : null,
            _ => platforms.Contains("Osx") ? "Osx" : null,
        };
    }
}
