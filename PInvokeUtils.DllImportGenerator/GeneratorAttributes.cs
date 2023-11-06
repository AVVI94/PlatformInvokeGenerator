using System;
using System.Runtime.InteropServices;

#nullable enable

namespace PlatformInvokeGenerator
{
    internal enum ImportPlatform
    {
        Win32,
        Win64,
        Linux64,
        Osx
    }

    internal enum AccessModifiers
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
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dllName">Library path and name, same value as if this was normal DllImport attribute</param>
        /// <param name="platform">Target platform</param>
        public DllImportForAttribute(string dllName, ImportPlatform platform)
        {
            DllName = dllName;
            Platform = platform;
        }
        /// <summary>
        /// Library name (and path)
        /// </summary>
        public string DllName { get; }
        /// <summary>
        /// Target platform
        /// </summary>
        public ImportPlatform Platform { get; }
        /// <summary>
        /// Native method entrypoint
        /// </summary>
        public string? EntryPoint;
        /// <summary>
        /// Native method calling convention, default value is Cdecl
        /// </summary>
        public CallingConvention CallingConvention;
        /// <summary>
        /// Is the generated call method static
        /// </summary>
        public bool StaticCallMethod;
        /// <summary>
        /// CharSet value that is used as CharSet value in generated DllImport
        /// </summary>
        public CharSet CharSet;
        /// <summary>
        /// SetLastError, same behavior as standard DllImport
        /// </summary>
        public bool SetLastError;
    }

    /// <summary>
    /// Marks this class as extern class, this class will be used as source class for the PlatformInvokeGenerator source generator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal class ExternClassAttribute : Attribute
    {
        /// <summary>
        /// Name of the generated class. If value is not provided, name of the decorated class will be used, the decorated class must be partial for this use.
        /// </summary>
        public string? GeneratedClassName;
        /// <summary>
        /// Namespace where the generated class will live. This value is used only if the GeneratedClassName is provided.
        /// </summary>
        public string? GeneratedClassNamespace;
        /// <summary>
        /// Generated class visibility modifier. This value is used only if the GeneratedClassName is provided. Default value is Internal.
        /// </summary>
        public AccessModifiers ClassAccessModifier;
        /// <summary>
        /// Visibility of the call methods. Default value is Internal.
        /// </summary>
        public AccessModifiers CallMethodsAccessModifier;
        /// <summary>
        /// Set to true if you want the generated class to be static, default value is false.
        /// </summary>
        public bool GenerateStaticClass;
        /// <summary>
        /// Set to true if you want the generated class to be marked as unsafe, default value is false.
        /// </summary>
        public bool GenerateUnsafeClass;
    }
}