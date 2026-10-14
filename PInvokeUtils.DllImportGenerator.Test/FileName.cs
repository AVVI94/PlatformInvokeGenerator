using System.Runtime.InteropServices;

namespace PlatformInvokeGenerator.Test
{
    [ExternClass]
    [ImportLibrary(FileName.DLL_NAME, ImportPlatform.Win32)]
    [ImportLibrary(FileName.DLL_NAME_X64, ImportPlatform.Win64)]
    [ImportLibrary(FileName.DLL_NAME_LINUX, ImportPlatform.Linux64)]
    [ImportLibrary(FileName.DLL_NAME_LINUX, ImportPlatform.LinuxArm64)]
    [ImportLibrary(FileName.DLL_NAME_LINUX, ImportPlatform.OsxX64)]
    [ImportLibrary(FileName.DLL_NAME_LINUX, ImportPlatform.OsxArm64)]
    public static partial class FileName
    {
        const string DLL_NAME = "filename.dll";
        const string DLL_NAME_X64 = "filename64.dll";
        const string DLL_NAME_LINUX = "filename.so";

        [DllImportFor(StringMarshalling = NativeStringMarshalling.WideChar)]
        static partial void GetFileNameT(string file, bool res = true);

        // libraries of the class, the string is passed as wchar_t* on every platform (UTF-16 on Windows, UTF-32 on Linux and macOS)
        [DllImportFor(StringMarshalling = NativeStringMarshalling.WideChar)]
        static partial void GetFileName(string file, bool res = true);

        // libraries of the class with UTF-8 strings, the Win32 import has its own calling convention
        [DllImportFor(EntryPoint = "GetFileNameA", StringMarshalling = NativeStringMarshalling.Utf8)]
        [DllImportFor(DLL_NAME, ImportPlatform.Win32, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        static partial void GetFileNameUtf8(string file);
    }
}
