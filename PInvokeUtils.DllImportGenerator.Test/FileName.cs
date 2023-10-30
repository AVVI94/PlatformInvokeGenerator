using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PlatformInvokeGenerator.Test;

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

