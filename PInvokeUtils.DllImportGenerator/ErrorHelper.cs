using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Syntax = Microsoft.CodeAnalysis.CSharp.SyntaxFacts;

namespace PlatformInvokeGenerator;

static class ErrorHelper
{
    const string PI1001 = "PI1001";
    const string PI1002 = "PI1002";
    const string PI1003 = "PI1003";
    const string PI1004 = "PI1004";
    const string PI1005 = "PI1005";
    const string PI1006 = "PI1006";
    const string PI1007 = "PI1007";
    const string PI1008 = "PI1008";
    const string PI1009 = "PI1009";
    const string PI1010 = "PI1010";
    const string PI1011 = "PI1011";
    const string PI1012 = "PI1012";
    const string PI1013 = "PI1013";
    const string PI1014 = "PI1014";
    const string PI1015 = "PI1015";
    const string PI1016 = "PI1016";
    const string PI1017 = "PI1017";

    public static readonly DiagnosticDescriptor ErrorInvalidClassName = new(PI1001,
                                                                            "Invalid class name",
                                                                            "Class name {0} is not valid",
                                                                            "Naming",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorDllImport = new(PI1002,
                                                                            "Invalid attributes combination",
                                                                            "Cannot apply DllImportForAttribute together with DllImportAttribute",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorSamePlatforms = new(PI1003,
                                                                            "ImportPlatform specified multiple times",
                                                                            "Cannot specify ImportPlatform.{0} more than once",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);
    public static readonly DiagnosticDescriptor ErrorClassAccessModifier = new(PI1004,
                                                                            "Invalid AccessModifier specified",
                                                                            "The generated class cannot have AccessModifier {0}, only AccessModifiers.Public and AccessModifiers.Internal are allowed",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);
    public static readonly DiagnosticDescriptor ErrorInvalidNamespace = new(PI1005,
                                                                            "Invalid namespace name",
                                                                            "Namespace name {0} is not valid",
                                                                            "Naming",
                                                                            DiagnosticSeverity.Error,
                                                                            true);
    public static readonly DiagnosticDescriptor ErrorNotPartialClassWithoutName = new(PI1006,
                                                                            "Class name not provided",
                                                                            "ClassName of the class to be generated is not provided, either provide a name of the class or mark this class as partial",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorStaticCallMethodMismatch = new(PI1007,
                                                                            "StaticCallMethod mismatch",
                                                                            "StaticCallMethod must have the same value on all DllImportFor attributes of method {0}",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorGenericClass = new(PI1008,
                                                                            "Extern methods in generic type",
                                                                            "Extern methods cannot be generated into the generic type {0}, specify GeneratedClassName to generate a separate class",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor WarningExternInDecoratedClass = new(PI1009,
                                                                            "Extern methods without DllImport",
                                                                            "Class {0} contains extern methods without DllImport, .NET Framework cannot load such class (TypeLoadException). Declare the methods as 'static partial' instead of 'static extern'.",
                                                                            "",
                                                                            DiagnosticSeverity.Warning,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorPartialMethodInOtherClass = new(PI1010,
                                                                            "Partial method cannot be implemented",
                                                                            "Partial method {0} can be implemented only when the methods are generated into the decorated class, remove GeneratedClassName or declare the method as 'static extern'",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorRuntimeInformationMissing = new(PI1011,
                                                                            "RuntimeInformation not available",
                                                                            "Method {0} needs System.Runtime.InteropServices.RuntimeInformation to select the platform import, reference the System.Runtime.InteropServices.RuntimeInformation package",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor WarningMethodIgnored = new(PI1012,
                                                                            "Method with DllImportFor ignored",
                                                                            "Method {0} is ignored, methods with DllImportFor have to be 'static extern' or 'static partial'",
                                                                            "",
                                                                            DiagnosticSeverity.Warning,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorGenericMethod = new(PI1013,
                                                                            "Generic method",
                                                                            "Method {0} cannot be generic, DllImport is not allowed in generic methods",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorInvalidPlatform = new(PI1014,
                                                                            "Invalid ImportPlatform",
                                                                            "ImportPlatform value {0} is not valid",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorMoreDllImportForWithoutLibrary = new(PI1015,
                                                                            "DllImportFor without a library specified more times",
                                                                            "DllImportFor without a library can be specified only once for method {0}",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorNoImportLibrary = new(PI1016,
                                                                            "No ImportLibrary",
                                                                            "Method {0} uses DllImportFor without a library, but the class has no ImportLibrary attribute",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorStringMarshallingNotSupported = new(PI1017,
                                                                            "StringMarshalling not supported",
                                                                            "StringMarshalling {0} of method {1} supports only string parameters passed by value, {2} cannot be converted (use IntPtr and convert the string yourself)",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly ImmutableArray<DiagnosticDescriptor> All = ImmutableArray.Create(ErrorInvalidClassName,
                                                                                           ErrorDllImport,
                                                                                           ErrorSamePlatforms,
                                                                                           ErrorClassAccessModifier,
                                                                                           ErrorInvalidNamespace,
                                                                                           ErrorNotPartialClassWithoutName,
                                                                                           ErrorStaticCallMethodMismatch,
                                                                                           ErrorGenericClass,
                                                                                           WarningExternInDecoratedClass,
                                                                                           ErrorPartialMethodInOtherClass,
                                                                                           ErrorRuntimeInformationMissing,
                                                                                           WarningMethodIgnored,
                                                                                           ErrorGenericMethod,
                                                                                           ErrorInvalidPlatform,
                                                                                           ErrorMoreDllImportForWithoutLibrary,
                                                                                           ErrorNoImportLibrary,
                                                                                           ErrorStringMarshallingNotSupported);

    public static Diagnostic Create(DiagnosticDescriptor descriptor, SyntaxReference? syntax, params object[] messageArgs)
        => Diagnostic.Create(descriptor, syntax is null ? Location.None : Location.Create(syntax.SyntaxTree, syntax.Span), messageArgs);

    public static bool IsNameInvalid(string? name) => !Syntax.IsValidIdentifier(name);
    public static bool IsClassAccessModInvalid(AccessModifiers mod) => mod is not AccessModifiers.Public and not AccessModifiers.Internal;

    public static bool IsNamespaceInvalid(string? @namespace)
    {
        var nameParts = @namespace?.Split('.') ?? Array.Empty<string>();
        return nameParts.Any(IsNameInvalid);
    }
}
