using System.Runtime.InteropServices;

namespace PlatformInvokeGenerator;

//The model is passed between the incremental pipeline steps, it must not reference any symbols or syntax nodes
//and must be equatable by value, otherwise the generator output is never cached.

/// <summary>
/// Class decorated with ExternClassAttribute and everything needed to generate its counterpart.
/// </summary>
internal readonly record struct ClassInfo(
    string HintName,
    string? Namespace,
    //declarations of the types containing the generated class (outermost first), e.g. "partial class Outer"
    EquatableArray<string> ContainingTypes,
    //modifiers and name of the generated class, e.g. "internal static partial class Foo<T>"
    string ClassDeclaration,
    string CallMethodsAccessModifier,
    //call methods get the Impl suffix when they are generated into the class declaring the source extern methods
    bool UseImplSuffix,
    EquatableArray<MethodGroup> Methods);

/// <summary>
/// One source extern method with all its platform imports.
/// </summary>
internal readonly record struct MethodGroup(
    string Name,
    string ReturnType,
    //rendered return attributes without the "return:" target, can be empty
    string ReturnAttributes,
    EquatableArray<ParameterModel> Parameters,
    bool IsStatic,
    //modifiers of the implementation of a partial source method (e.g. "public static partial"), null for extern source method
    string? PartialModifiers,
    //parameters of the partial method implementation, without attributes and default values
    string ImplementationParameters,
    EquatableArray<ImportInfo> Imports);

/// <summary>
/// Parameter of a source method, rendered parts.
/// </summary>
internal readonly record struct ParameterModel(
    //"[Attribute1, Attribute2] " or empty
    string Attributes,
    //"params ", "ref "... or empty
    string Modifiers,
    //modifier of the argument passing the parameter on, e.g. "ref "
    string ArgumentModifier,
    string Type,
    //escaped identifier
    string Name,
    //" = value" or empty
    string DefaultValue,
    //string passed by value without MarshalAs, converted by NativeStringMarshalling
    bool IsPlainString)
{
    public string Render() => Attributes + Modifiers + Type + " " + Name + DefaultValue;
}

/// <summary>
/// One platform import of a method.
/// </summary>
internal readonly record struct ImportInfo(
    ImportPlatform Platform,
    string DllName,
    string EntryPoint,
    CallingConvention CallingConvention,
    CharSet CharSet,
    bool SetLastError,
    NativeStringMarshalling StringMarshalling);
