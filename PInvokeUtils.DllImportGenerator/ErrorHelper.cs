using System;
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

    public static readonly DiagnosticDescriptor ErrorInvalidClassName = new(PI1001,
                                                                            "Invalid class name",
                                                                            "Class name {0} is not valid",
                                                                            "Naming",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorDllImport = new(PI1002,
                                                                            "Invalid attributes combination",
                                                                            "Cannot apply DllImportForAttribute toghether with DllImportAttribute",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);

    public static readonly DiagnosticDescriptor ErrorSamePlatforms = new(PI1003,
                                                                            "ImportPlatform specified multiple times",
                                                                            "Cannot spcify the ImportPlatform ImportPlatforms.{0} more than once",
                                                                            "",
                                                                            DiagnosticSeverity.Error,
                                                                            true);
    public static readonly DiagnosticDescriptor ErrorClassAccessModifier = new(PI1004,
                                                                            "Invalid AccessModifier specified",
                                                                            "The generated class cannot have AccessModifier {0}, only AccessModifiers.Public and AccessModifier.Internal are allowed",
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

    public static bool IsNameInvalid(string? name) => !Syntax.IsValidIdentifier(name);
    public static bool IsClassAccessModInvalid(AccessModifiers mod) => mod is not AccessModifiers.Public and not AccessModifiers.Internal;

    public static bool IsNamespaceInvalid(string? @namespace)
    {
        var nameParts = @namespace?.Split('.') ?? Array.Empty<string>();
        return nameParts.Any(IsNameInvalid);
    }
}
