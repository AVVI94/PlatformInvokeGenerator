using System.Runtime.InteropServices;

namespace PlatformInvokeGenerator.Test
{
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
        [DllImportFor(DLL_NAME_LINUX, ImportPlatform.LinuxArm64, EntryPoint = "GetFileNameA")]
        [DllImportFor(DLL_NAME_LINUX, ImportPlatform.OsxX64, EntryPoint = "GetFileNameA")]
        [DllImportFor(DLL_NAME_LINUX, ImportPlatform.OsxArm64, EntryPoint = "GetFileNameA")]
        static extern void GetFileName(string file);

        [DllImportFor(DLL_NAME, ImportPlatform.Win32, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, StaticCallMethod = true, SetLastError = true)]
        [DllImportFor(DLL_NAME_X64, ImportPlatform.Win64, CharSet = CharSet.Unicode)]
        [DllImportFor(DLL_NAME_LINUX, ImportPlatform.Linux64, EntryPoint = "GetFileNameBoolA")]
        [DllImportFor(DLL_NAME_LINUX, ImportPlatform.LinuxArm64, EntryPoint = "GetFileNameBoolA")]
        [DllImportFor(DLL_NAME_LINUX, ImportPlatform.OsxX64, EntryPoint = "GetFileNameBoolA")]
        [DllImportFor(DLL_NAME_LINUX, ImportPlatform.OsxArm64, EntryPoint = "GetFileNameBoolA")]
        [return: MarshalAs(UnmanagedType.I1)]
        static extern bool GetFileNameBool(string file);
    }

}
