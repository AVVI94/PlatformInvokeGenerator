# PlatformInvokeGenerator

This .NET SourceGenerator was developed to simplify the creation of P/Invoke methods for various platforms, including Windows (x86, x64, Arm64), Linux (x64, Arm64) and macOS (x64, Arm64).

The SourceGenerator generates extern methods with parameters specified by the `DllImportFor` attribute. Additionally, it creates a calling method that determines the appropriate native method to invoke based on the current OS and process architecture.

## Usage

To use the source generator add [this](https://www.nuget.org/packages/PlatformInvokeGenerator) NuGet package to your project.

Use the `ExternClass` attribute to decorate a class where you declare the methods intended as a source for the generator. Make sure that the source methods are decorated with the `DllImportFor` attribute and **are not** annotated with the `DllImport` attribute.

### Generate the methods into the decorated class

Declare the methods as `static partial`, the generator implements them. The decorated class (and all its containing types) must be `partial` and must not be generic.

```cs
[ExternClass]
public static partial class FileName
{
    const string DLL_NAME = "filename.dll";
    const string DLL_NAME_X64 = "filename64.dll";
    const string DLL_NAME_LINUX = "filename.so";

    [DllImportFor(DLL_NAME, ImportPlatform.Win32, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    [DllImportFor(DLL_NAME_X64, ImportPlatform.Win64, CharSet = CharSet.Unicode)]
    [DllImportFor(DLL_NAME_LINUX, ImportPlatform.Linux64, EntryPoint = "GetFileNameA")]
    static partial void GetFileName(string file, bool res = true);
}
```

Generated output:

```cs
#pragma warning disable
#nullable disable

namespace PlatformInvokeGenerator.Test
{
    static partial class FileName
    {
        [global::System.Runtime.InteropServices.DllImportAttribute("filename.dll", EntryPoint = "GetFileName", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = global::System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern void __Win32_GetFileName(string file, bool res = true);

        [global::System.Runtime.InteropServices.DllImportAttribute("filename64.dll", EntryPoint = "GetFileName", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, CharSet = global::System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern void __Win64_GetFileName(string file, bool res = true);

        [global::System.Runtime.InteropServices.DllImportAttribute("filename.so", EntryPoint = "GetFileNameA", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private static extern void __Linux64_GetFileName(string file, bool res = true);

        static partial void GetFileName(string file, bool res)
        {
            if(global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows))
            {
                if(global::System.Environment.Is64BitProcess)
                {
                    __Win64_GetFileName(file, res);
                }
                else
                {
                    __Win32_GetFileName(file, res);
                }
            }
            else if(global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Linux))
            {
                __Linux64_GetFileName(file, res);
            }
            else
                throw new global::System.NotSupportedException("Current OSPlatform is not supported");
        }
    }
}
```

Methods declared as `static extern` are still supported, the generator then creates a call method with the `Impl` suffix (`GetFileNameImpl`). However, .NET Framework cannot load a class containing extern methods without `DllImport` (`TypeLoadException`), so `static partial` is the recommended way. The analyzer reports warning PI1009 for extern methods in the decorated class when the project can run on .NET Framework.

### Generate a new class

To generate a new class provide its name. You can also specify the namespace, an accessibility modifier for the class and for the call methods, and if the class should be static and/or unsafe. The generated class is always partial. The source methods have to be declared as `static extern`, the source class is used only as a declaration and is never loaded.

Example to generate a new public class `ExternFileName` in the `PlatformInvoke.ExternFile` namespace with public call methods:

```cs
[ExternClass(GeneratedClassName = "ExternFileName",
             GeneratedClassNamespace = "PlatformInvoke.ExternFile",
             ClassAccessModifier = AccessModifiers.Public,
             CallMethodsAccessModifier = AccessModifiers.Public)]
public static partial class FileName2
{
    const string DLL_NAME = "filename.dll";
    const string DLL_NAME_X64 = "filename64.dll";
    const string DLL_NAME_LINUX = "filename.so";

    [DllImportFor(DLL_NAME, ImportPlatform.Win32, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    [DllImportFor(DLL_NAME_X64, ImportPlatform.Win64, CharSet = CharSet.Unicode)]
    [DllImportFor(DLL_NAME_LINUX, ImportPlatform.Linux64, EntryPoint = "GetFileNameA")]
    static extern void GetFileName(string file);
}
```

Generated output:

```cs
#pragma warning disable
#nullable disable

namespace PlatformInvoke.ExternFile
{
    public partial class ExternFileName
    {
        [global::System.Runtime.InteropServices.DllImportAttribute("filename.dll", EntryPoint = "GetFileName", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.StdCall, CharSet = global::System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        private static extern void __Win32_GetFileName(string file);

        [global::System.Runtime.InteropServices.DllImportAttribute("filename64.dll", EntryPoint = "GetFileName", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, CharSet = global::System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern void __Win64_GetFileName(string file);

        [global::System.Runtime.InteropServices.DllImportAttribute("filename.so", EntryPoint = "GetFileNameA", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private static extern void __Linux64_GetFileName(string file);

        public static void GetFileName(string file)
        {
            if(global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows))
            {
                if(global::System.Environment.Is64BitProcess)
                {
                    __Win64_GetFileName(file);
                }
                else
                {
                    __Win32_GetFileName(file);
                }
            }
            else if(global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Linux))
            {
                __Linux64_GetFileName(file);
            }
            else
                throw new global::System.NotSupportedException("Current OSPlatform is not supported");
        }
    }
}
```

A common pattern is to nest the declaration class into the class it generates:

```cs
internal partial class NativeMethods
{
    [ExternClass(GeneratedClassName = nameof(NativeMethods))]
    static class Declarations
    {
        [DllImportFor("x64/native.dll", ImportPlatform.Win64)]
        public static extern int Foo();
    }
}
```

### Libraries declared on the class

When all methods import the same libraries, declare them once on the class by `ImportLibrary` (one attribute per platform) and use `DllImportFor` without a library on the methods. `CallingConvention`, `CharSet` and `StringMarshalling` set on `ExternClass` are the defaults of all methods of the class.

```cs
[ExternClass(GeneratedClassName = "NativeMethods", CharSet = CharSet.Unicode)]
[ImportLibrary("x86/native.dll", ImportPlatform.Win32)]
[ImportLibrary("x64/native.dll", ImportPlatform.Win64)]
[ImportLibrary("linux-x64/libnative.so", ImportPlatform.Linux64)]
static class Declarations
{
    // libraries and defaults of the class
    [DllImportFor]
    public static extern int Foo(int value);

    // own values for all libraries of the class
    [DllImportFor(EntryPoint = "Bar2", CharSet = CharSet.Ansi)]
    public static extern int Bar(string value);

    // libraries of the class, Win64 replaced by an own library
    [DllImportFor]
    [DllImportFor("x64/other.dll", ImportPlatform.Win64)]
    public static extern int Baz();
}
```

The values of an import are taken in this order: `DllImportFor(dll, platform)`, `DllImportFor()`, `ExternClass`, built-in defaults (`Cdecl`, `Ansi`). A method with only `DllImportFor(dll, platform)` attributes imports only its own libraries, the libraries of the class are used only with `DllImportFor()`.

### String marshalling

`StringMarshalling` (on `DllImportFor` or as the default on `ExternClass`) controls how `string` parameters passed by value are passed to the native method:

| `NativeStringMarshalling` | Native parameter | Passed as |
|---|---|---|
| `Default` | depends on `CharSet` | standard P/Invoke marshalling |
| `Utf8` | `const char*` (UTF-8) | null terminated UTF-8 copy on all platforms |
| `WideChar` | `const wchar_t*` | Windows: UTF-16 (the string itself, no copy), Linux and macOS: null terminated UTF-32 copy |

```cs
[DllImportFor(StringMarshalling = NativeStringMarshalling.WideChar)]
public static extern int SetPath(string path);   // wchar_t* on every OS
```

Every copy is one exactly sized `byte[]`, pinned by the runtime only for the time of the native call (no further copy), the method keeps its `string` parameters. If a call must not allocate, declare the parameter as `IntPtr` and pass your own buffer. Parameters with an explicit `MarshalAs` attribute are passed by their `MarshalAs`. String return values and `ref`/`out`/`in` strings cannot be converted (PI1017), the ownership of the native string is not known, use `IntPtr` and convert the string yourself. Strings in structures are not converted.

## Platforms

| `ImportPlatform` | Used when |
|---|---|
| `Win32` | Windows, 32-bit process |
| `Win64` | Windows, 64-bit process without a more specific import |
| `WinArm64` | Windows, Arm64 process |
| `Linux64` | Linux, process without a more specific import |
| `LinuxArm64` | Linux, Arm64 process |
| `Osx` | macOS, process without a more specific import |
| `OsxX64` | macOS, x64 process (Intel or Rosetta 2) |
| `OsxArm64` | macOS, Arm64 process (Apple Silicon) |

The generated call method first checks the OS, then the architecture specific imports of that OS, then the OS fallback import (`Win64`/`Win32` by process bitness, `Linux64`, `Osx`). When nothing matches, `NotSupportedException` is thrown. The OS check is skipped when only Windows imports are specified, all checks are skipped when only one import is specified.

```cs
[DllImportFor("x86/native.dll", ImportPlatform.Win32)]
[DllImportFor("x64/native.dll", ImportPlatform.Win64)]
[DllImportFor("arm64/native.dll", ImportPlatform.WinArm64)]
[DllImportFor("linux-x64/libnative.so", ImportPlatform.Linux64)]
[DllImportFor("linux-arm64/libnative.so", ImportPlatform.LinuxArm64)]
[DllImportFor("osx-x64/libnative.dylib", ImportPlatform.OsxX64)]
[DllImportFor("osx-arm64/libnative.dylib", ImportPlatform.OsxArm64)]
static partial int Foo();
```

Things to keep in mind:

- The **process** architecture decides, not the OS architecture. A process can load only libraries of its own architecture, so an x86/x64 process emulated on Arm64 Windows uses `Win32`/`Win64`, an x64 process running under Rosetta 2 uses `OsxX64`.
- The fallback imports do not make other architectures work. A native Arm64 process without `WinArm64` calls the `Win64` import and a native Arm64 Linux process without `LinuxArm64` calls the `Linux64` import, which fails with `BadImageFormatException` if the library is built for x64 (this is the same behavior as before the architecture specific imports were added). Use the fallback imports for architectures you actually ship, or universal (fat) binaries on macOS.
- The checks use only the official API: `System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform`, `RuntimeInformation.ProcessArchitecture` and `Environment.Is64BitProcess`. The generator does not add any helper types to the project.

### RuntimeInformation

Methods with imports for more operating systems, or with an architecture specific import (`WinArm64`, `LinuxArm64`, `OsxX64`, `OsxArm64`) together with another import, need `System.Runtime.InteropServices.RuntimeInformation`. It is part of .NET Standard 2.0, .NET Framework 4.7.1+ and .NET (Core). For .NET Framework 4.6.x reference the `System.Runtime.InteropServices.RuntimeInformation` package, otherwise the analyzer reports error PI1011. Methods with only `Win32` and `Win64` imports use `Environment.Is64BitProcess` and do not need it.

## Generated code

- Types, attributes and default values are fully qualified, the generated code does not depend on the usings of the source file.
- Parameter attributes (`MarshalAs`, `In`, `Out`...), `ref`/`out`/`in`/`params` modifiers, default values and return attributes are copied to the generated call methods. The private platform imports have no default values, the call method always passes all arguments.
- The generated code is C# 7.3 compatible. The `#nullable disable` directive is emitted only for C# 8 and newer.
- `ref readonly` parameters (C# 12) are supported, the generated call passes them with `in`.
- The attribute types (`ExternClassAttribute`, `DllImportForAttribute`, `ImportLibraryAttribute`, `ImportPlatform`, `AccessModifiers`, `NativeStringMarshalling`) are generated into every project as internal types. With a compiler supporting it (Roslyn 4.14+, .NET SDK 9.0.300+), they are marked with `Microsoft.CodeAnalysis.EmbeddedAttribute`, so they do not conflict (CS0436) between projects with `InternalsVisibleTo`.

## Diagnostics

The diagnostics are reported by `ExternClassAnalyzer`, shipped in the same package as the generator, so they are shown in the IDE while typing and can be configured in `.editorconfig` as any other analyzer diagnostics. The generator only leaves out the code with errors.

| ID | Severity | Description |
|---|---|---|
| PI1001 | Error | Invalid generated class name |
| PI1002 | Error | `DllImportFor` combined with `DllImport` |
| PI1003 | Error | `ImportPlatform` specified more than once for a method |
| PI1004 | Error | Generated class access modifier other than `Public` or `Internal` |
| PI1005 | Error | Invalid generated class namespace |
| PI1006 | Error | No `GeneratedClassName` and the decorated class is not partial |
| PI1007 | Error | `StaticCallMethod` differs between the `DllImportFor` attributes of a method |
| PI1008 | Error | Methods generated into a generic type (P/Invoke is not allowed in generic types) |
| PI1009 | Warning | Extern methods in the decorated class, .NET Framework cannot load the class |
| PI1010 | Error | Partial method combined with `GeneratedClassName` |
| PI1011 | Error | The method needs `RuntimeInformation`, but the project does not reference it (.NET Framework 4.6.x without the `System.Runtime.InteropServices.RuntimeInformation` package) |
| PI1012 | Warning | Method with `DllImportFor` is not `static extern` or `static partial`, it is ignored |
| PI1013 | Error | Generic method (P/Invoke is not allowed in generic methods) |
| PI1014 | Error | Invalid `ImportPlatform` value |
| PI1015 | Error | `DllImportFor` without a library specified more than once for a method |
| PI1016 | Error | `DllImportFor` without a library, but the class has no `ImportLibrary` |
| PI1017 | Error | `StringMarshalling` with a string return value or a `ref`/`out`/`in` string parameter |

Errors of the class (PI1001, PI1004, PI1005, PI1006, PI1008, invalid or duplicate `ImportLibrary`) stop the generation of the whole class. Errors of a method skip only that method, the other methods of the class are generated. A skipped method still gets a call method (or the implementation of the partial method) that throws `NotSupportedException`, so its callers compile and only the analyzer diagnostic is reported, without follow-up compiler errors.
