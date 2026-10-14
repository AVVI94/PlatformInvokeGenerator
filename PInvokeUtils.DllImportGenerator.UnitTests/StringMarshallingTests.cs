using System.Runtime.InteropServices;
using Xunit;
using static PlatformInvokeGenerator.UnitTests.GeneratorHarness;

namespace PlatformInvokeGenerator.UnitTests;

/// <summary>
/// Calls string functions of the C runtime of the current OS, the native side shows how the string was passed.
/// The libraries are declared by ImportLibraryAttribute on the class.
/// </summary>
public class StringMarshallingTests
{
    const string TEXT = "čřž😀";

    static readonly Lazy<Type> CRuntime = new(() =>
    {
        var result = Run("""
            using System;
            using PlatformInvokeGenerator;
            namespace Strings
            {
                [ExternClass(GeneratedClassName = "CRuntime", ClassAccessModifier = AccessModifiers.Public, CallMethodsAccessModifier = AccessModifiers.Public)]
                [ImportLibrary("msvcrt.dll", ImportPlatform.Win32)]
                [ImportLibrary("msvcrt.dll", ImportPlatform.Win64)]
                [ImportLibrary("msvcrt.dll", ImportPlatform.WinArm64)]
                [ImportLibrary("libc.so.6", ImportPlatform.Linux64)]
                [ImportLibrary("libSystem.dylib", ImportPlatform.Osx)]
                static class Declarations
                {
                    [DllImportFor(StringMarshalling = NativeStringMarshalling.Utf8)]
                    public static extern UIntPtr strlen(string s);

                    [DllImportFor(StringMarshalling = NativeStringMarshalling.Utf8)]
                    public static extern int strcmp(string a, string b);

                    [DllImportFor(StringMarshalling = NativeStringMarshalling.WideChar)]
                    public static extern UIntPtr wcslen(string s);

                    [DllImportFor(StringMarshalling = NativeStringMarshalling.WideChar)]
                    public static extern int wcscmp(string a, string b);
                }
            }
            """, assemblyName: "Strings");
        Assert.Empty(result.AnalyzerDiagnostics);
        return result.Load().GetType("Strings.CRuntime", throwOnError: true)!;
    });

    static T Function<T>(string name) where T : Delegate
        => (T)CRuntime.Value.GetMethod(name, AllStatic)!.CreateDelegate(typeof(T));

    [Fact]
    public void Utf8Marshalling_String_PassedAsNullTerminatedUtf8()
    {
        var strlen = Function<Func<string, UIntPtr>>("strlen");
        //č, ř, ž: 2 bytes each, the emoji: 4 bytes
        Assert.Equal((nuint)10, (nuint)strlen(TEXT));
        Assert.Equal((nuint)0, (nuint)strlen(""));
        Assert.Equal((nuint)5000, (nuint)strlen(new string('a', 5000)));
    }

    [Fact]
    public void WideCharMarshalling_String_PassedWithWcharSizeOfPlatform()
    {
        var wcslen = Function<Func<string, UIntPtr>>("wcslen");
        //Windows: UTF-16 code units (the emoji is a surrogate pair), Linux and macOS: UTF-32 code points
        var expected = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 5 : 4;
        Assert.Equal((nuint)expected, (nuint)wcslen(TEXT));
        Assert.Equal((nuint)5000, (nuint)wcslen(new string('a', 5000)));
    }

    [Theory]
    [InlineData("strcmp")]
    [InlineData("wcscmp")]
    public void StringMarshalling_MoreStringParameters_AllConverted(string name)
    {
        var compare = Function<Func<string, string, int>>(name);
        Assert.Equal(0, compare(TEXT, TEXT));
        Assert.True(compare("a", "b") < 0);
        Assert.True(compare(TEXT + "x", TEXT) > 0);
        Assert.True(compare(new string('a', 3000), new string('a', 2999)) > 0);
    }

    /// <summary>
    /// Aliases of the generator namespace, its attributes, string and InteropServices are resolved, a namespace System
    /// next to the generated class does not hide the System namespace used by the generated code.
    /// </summary>
    [Fact]
    public void Generator_AliasedNamespaceTypesAndAttributes_GeneratesWorkingCalls()
    {
        var result = Run("""
            global using Pig = PlatformInvokeGenerator;
            using Library = PlatformInvokeGenerator.ImportLibraryAttribute;
            using Str = System.String;
            using Iop = System.Runtime.InteropServices;
            namespace Aliased
            {
                namespace System { class Text { } class Runtime { } }

                [Pig.ExternClass(GeneratedClassName = "CRuntime")]
                [Library("msvcrt.dll", Pig.ImportPlatform.Win32)]
                [Library("msvcrt.dll", Pig.ImportPlatform.Win64)]
                [Library("msvcrt.dll", Pig.ImportPlatform.WinArm64)]
                [Library("libc.so.6", Pig.ImportPlatform.Linux64)]
                [Library("libSystem.dylib", Pig.ImportPlatform.Osx)]
                static class Declarations
                {
                    [Pig.DllImportFor(StringMarshalling = Pig.NativeStringMarshalling.Utf8)]
                    public static extern global::System.UIntPtr strlen(Str s);

                    [Pig.DllImportFor(StringMarshalling = Pig.NativeStringMarshalling.WideChar)]
                    public static extern global::System.UIntPtr wcslen(Str s);

                    //MarshalAs wins over the conversion
                    [Pig.DllImportFor(EntryPoint = "strlen", StringMarshalling = Pig.NativeStringMarshalling.WideChar)]
                    public static extern global::System.UIntPtr AnsiLength([Iop.MarshalAs(Iop.UnmanagedType.LPStr)] Str s);
                }
            }
            """, assemblyName: "Aliased");
        Assert.Empty(result.AnalyzerDiagnostics);
        var type = result.Load().GetType("Aliased.CRuntime", throwOnError: true)!;
        UIntPtr Call(string name, string s) => (UIntPtr)type.GetMethod(name, AllStatic)!.Invoke(null, [s])!;

        Assert.Equal((nuint)10, (nuint)Call("strlen", TEXT));
        Assert.Equal((nuint)(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 5 : 4), (nuint)Call("wcslen", TEXT));
        Assert.Equal((nuint)3, (nuint)Call("AnsiLength", "abc"));
    }
}
