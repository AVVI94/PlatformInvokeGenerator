using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace PlatformInvokeGenerator;

internal readonly record struct ClassInfo
{
    public readonly string GeneratedClassName;
    public readonly string SourceClassName;
    public readonly string? Namespace;
    public readonly List<List<MethodInfo>> Methods;
    public readonly AccessModifiers ClassAccessModifier;
    public readonly AccessModifiers MethodsAccessModifier;
    public readonly bool GenerateStaticClass;
    public readonly bool GenerateUnsafeClass;
    public readonly bool IsDecoratedClassPartial;
    public readonly string UsingStatements;
    public readonly TextSpan ExternClassAttributeTextSpan;
    public readonly FileLinePositionSpan ExternClassAttributeLineSpan;

    public ClassInfo(string sourceClassName, string generatedClassName, string? @namespace, List<List<MethodInfo>> methods, AccessModifiers classAccessModifier, bool isStatic, string usingStatements, TextSpan textSpan, AccessModifiers methodsAccessModifier, FileLinePositionSpan externClassLineSpan, bool isPartial, bool generateUnsafeClass)
    {
        GeneratedClassName = generatedClassName;
        Namespace = @namespace;
        Methods = methods;
        ClassAccessModifier = classAccessModifier;
        GenerateStaticClass = isStatic;
        UsingStatements = usingStatements;
        ExternClassAttributeTextSpan = textSpan;
        MethodsAccessModifier = methodsAccessModifier;
        ExternClassAttributeLineSpan = externClassLineSpan;
        IsDecoratedClassPartial = isPartial;
        SourceClassName = sourceClassName;
        GenerateUnsafeClass = generateUnsafeClass;
    }
}

internal readonly record struct MethodInfo
{
    public readonly string NameWithoutPlatform;
    public readonly string ReturnType;
    public readonly string ReturnTypeAttributes;
    public readonly string Parameters;
    public readonly string ParametersNames;
    public readonly string DllName;
    public readonly string EntryPoint;
    public readonly ImportPlatform Platform;
    public readonly CallingConvention CallingConvention;
    public readonly CharSet CharSet;
    public readonly bool SetLastError;

    public readonly bool HasDllImport;
    public readonly TextSpan DllImportForText;
    public readonly FileLinePositionSpan DllImportForTextLinePosSpan;
    public readonly bool IsCallMethod;
    public readonly bool IsStatic;

    public MethodInfo(
        string nameWithoutPlatform,
                      string returnTypeAttributes,
                      string returnType,
                      string parameters,
                      string dllName,
                      string entryPoint,
                      ImportPlatform platform,
                      CallingConvention callingConvention,
                      bool hasDllImport,
                      TextSpan dllImportForText,
                      FileLinePositionSpan dllImportForTextLinePosSpan,
                      bool isCallMethod,
                      bool isStatic,
                      string parametersNames,
                      CharSet charSet,
                      bool setLastError)
    {
        NameWithoutPlatform = nameWithoutPlatform;
        ReturnTypeAttributes = returnTypeAttributes;
        ReturnType = returnType;
        Parameters = parameters;
        DllName = dllName;
        EntryPoint = entryPoint;
        Platform = platform;
        CallingConvention = callingConvention;
        HasDllImport = hasDllImport;
        DllImportForText = dllImportForText;
        DllImportForTextLinePosSpan = dllImportForTextLinePosSpan;
        IsCallMethod = isCallMethod;
        IsStatic = isStatic;
        ParametersNames = parametersNames;
        CharSet = charSet;
        SetLastError = setLastError;
    }
}
