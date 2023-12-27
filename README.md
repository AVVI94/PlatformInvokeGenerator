# PlatformInvokeGenerator

This .NET SourceGenerator was developed to simplify the creation of P/Invoke methods for various platforms, including Win32, Win64, and others.

The SourceGenerator will generate external methods with parameters specified by the DllImportFor attribute. Additionally, it will create a calling method that dynamically determines the appropriate native method to invoke based on the current platform.

## Usage

To use the source generator add [this](https://www.nuget.org/packages/PlatformInvokeGenerator) NuGet package to your project.

Use the `ExternClass` attribute to decorate a class where you declare external methods intended as a source for the generator. Make sure that the source methods are decorated with the `DllImportFor` attribute and **are not** annotated with the DllImport attribute.

### Generate partial class based on the decorated class

Example for generating a partial class with the same name as decorated class:

```cs
[ExternClass]
public static partial class FileName
{
    const string DLL_NAME = "filename.dll";
    const string DLL_NAME_X64 = "filename64.dll";
    const string DLL_NAME_LINUX = "filename.so";

    [DllImportFor(DLL_NAME, ImportPlatform.Win32, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, StaticCallMethod = true, SetLastError = true)]
    [DllImportFor(DLL_NAME_X64, ImportPlatform.Win64, CharSet = CharSet.Unicode)]
    [DllImportFor(DLL_NAME_LINUX, ImportPlatform.Linux64, EntryPoint = "GetFileNameA")]
    static extern void GetFileName(string file);
}
```

In this example the partial class FileName is decorated with `ExternClass` attribute. Method `GetFileName` is marked as extern (required) and is decorated with `DllImportFor` attributes for each platform.

Generated output:

```cs
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the PlatformInvokeGenerator source generator
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks; 

#nullable enable
namespace PlatformInvokeGenerator.Test
{
public static partial class FileName
{
        [DllImport(@"filename.dll", EntryPoint = "GetFileName", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern void __Win32_GetFileName(string file);

        [DllImport(@"filename64.dll", EntryPoint = "GetFileName", CharSet = CharSet.Unicode)]
        private static extern void __Win64_GetFileName(string file);

        [DllImport(@"filename.so", EntryPoint = "GetFileNameA")]
        private static extern void __Linux64_GetFileName(string file);

        internal static void GetFileNameImpl(string file)
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if(Environment.Is64BitProcess)
                {
                    __Win64_GetFileName(file);
                }
                else
                {
                    __Win32_GetFileName(file);
                }
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                 __Linux64_GetFileName(file);
            }
            else
                throw new NotSupportedException("Current OSPlatform is not supported");
        }

   }
}
```

### Generate new class

If the source generator generates a new class, the class is always marked as partial. To generate a new class you must provide a name for the new class. You can also specify an accessibility modifier for the class and also for the call methods, you can also say if the class should be static and/or unsafe.

Example to generate a new class with name `ExternFileName`, this class will be public and static, will live in `PlatformInvoke.ExternFile` namespace, the call methods will also be public.

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

    [DllImportFor(DLL_NAME, ImportPlatform.Win32, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, StaticCallMethod = true, SetLastError = true)]
    [DllImportFor(DLL_NAME_X64, ImportPlatform.Win64, CharSet = CharSet.Unicode)]
    [DllImportFor(DLL_NAME_LINUX, ImportPlatform.Linux64, EntryPoint = "GetFileNameA")]
    static extern void GetFileName(string file);
}
```

Generated output: 

```cs
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by the PlatformInvokeGenerator source generator
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks; 

#nullable enable
namespace PlatformInvokeGenerator.Test
{
public partial class ExternFileName
{
        [DllImport(@"filename.dll", EntryPoint = "GetFileName", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern void __Win32_GetFileName(string file);

        [DllImport(@"filename64.dll", EntryPoint = "GetFileName", CharSet = CharSet.Unicode)]
        private static extern void __Win64_GetFileName(string file);

        [DllImport(@"filename.so", EntryPoint = "GetFileNameA")]
        private static extern void __Linux64_GetFileName(string file);

        public static void GetFileName(string file)
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if(Environment.Is64BitProcess)
                {
                    __Win64_GetFileName(file);
                }
                else
                {
                    __Win32_GetFileName(file);
                }
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                 __Linux64_GetFileName(file);
            }
            else
                throw new NotSupportedException("Current OSPlatform is not supported");
        }

   }
}
```
