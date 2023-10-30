using System;
using System.Runtime.InteropServices;

namespace PlatformInvokeGenerator
{
    public enum ImportPlatform
    {
        Win32,
        Win64,
        Linux64,
        Osx
    }

    public enum AccessModifiers
    {
        Public,
        Protected,
        Internal,
        Private,
        ProtectedInternal,
        PrivateProtected,
    }

    /// <summary>
    /// Indicates that the attributed method is exposed by an unmanaged dynamic-link library (DLL) as a static entry point.
    /// An extern methods will be generated for each attribute. 
    /// One common method that can be called will be generated, when call, this method will decide what platform-specific extern method to call.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal class DllImportForAttribute : Attribute
    {
        public DllImportForAttribute(string dllName, ImportPlatform platform)
        {
            DllName = dllName;
            Platform = platform;
        }

        public string DllName { get; }
        public ImportPlatform Platform { get; }
        public string? EntryPoint;
        public CallingConvention CallingConvention;
        public bool StaticCallMethod;
        public CharSet CharSet;
        public bool SetLastError;
    }

    [AttributeUsage(AttributeTargets.Class)]
    internal class ExternClassAttribute : Attribute
    {
        public string? GeneratedClassName;
        public string? GeneratedClassNamespace;
        public AccessModifiers ClassAccessModifier;
        public AccessModifiers CallMethodsAccessModifier;
        public bool GenerateStaticClass;
        public bool GenerateUnsafeClass;
    }
}
