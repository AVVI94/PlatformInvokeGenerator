using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PlatformInvokeGenerator.Test;
[ExternClass(GenerateUnsafeClass = true, CallMethodsAccessModifier = AccessModifiers.Private)]
public static partial class FileName
{
    const string fuck = "fuc\\k";
    [DllImportFor(fuck, ImportPlatform.Win32, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, StaticCallMethod = true, SetLastError =true)]
    //[LibraryImport("test")]
    public static extern unsafe void Test(string f = null);
}

