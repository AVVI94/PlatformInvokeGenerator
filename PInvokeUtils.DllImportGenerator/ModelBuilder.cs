using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PlatformInvokeGenerator;

/// <summary>
/// Builds the generator model from the analysis of the class decorated with ExternClassAttribute.
/// Everything is rendered fully qualified, so the generated code does not depend on the usings of the source file.
/// </summary>
internal static class ModelBuilder
{
    const string MARSHAL_AS_ATTRIBUTE_NAME = "System.Runtime.InteropServices.MarshalAsAttribute";

    //global:: qualified names, keywords escaped, special types (int, string...) and without nullable reference annotations
    static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat;

    public static ClassInfo? Build(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
            return null;

        //ExternClassAttribute does not allow multiple usage
        var analysis = ExternClassAnalysis.Analyze(symbol, context.Attributes[0], context.SemanticModel.Compilation, cancellationToken);
        //the errors are reported by the analyzer, the generated code would only add follow-up errors
        if (analysis is null || analysis.HasClassErrors)
            return null;

        var sourceNamespace = symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();
        string? @namespace;
        EquatableArray<string> containingTypes;
        string classDeclarationText;
        bool useImplSuffix;
        var unsafeModifier = analysis.GenerateUnsafeClass ? "unsafe " : "";

        if (analysis.IntoDecoratedClass)
        {
            @namespace = sourceNamespace;
            containingTypes = new EquatableArray<string>(GetContainingTypeDeclarations(symbol));
            //the accessibility is taken from the other parts of the partial class
            classDeclarationText = (symbol.IsStatic ? "static " : "") + unsafeModifier + "partial class " + GetNameWithTypeParameters(symbol);
            //extern source methods collide with the call methods, partial methods are implemented directly
            useImplSuffix = true;
        }
        else
        {
            @namespace = string.IsNullOrWhiteSpace(analysis.GeneratedNamespace) ? sourceNamespace : analysis.GeneratedNamespace;
            containingTypes = EquatableArray<string>.Empty;
            classDeclarationText = GetAccessModifier(analysis.ClassAccessModifier) + " " + (analysis.GenerateStaticClass ? "static " : "") + unsafeModifier + "partial class " + analysis.GeneratedClassName;
            //generated class is the decorated class, the call methods would collide with the source extern methods
            useImplSuffix = analysis.IsPartial && symbol.ContainingType is null && symbol.Name == analysis.GeneratedClassName && @namespace == sourceNamespace;
        }

        return new ClassInfo(GetHintName(symbol),
                             @namespace,
                             containingTypes,
                             classDeclarationText,
                             GetAccessModifier(analysis.CallMethodsAccessModifier),
                             useImplSuffix,
                             new EquatableArray<MethodGroup>(analysis.Methods.Select(CreateMethodGroup).ToArray()));
    }

    /// <summary>
    /// Method without imports (a method with errors) only throws, so its callers still compile.
    /// </summary>
    static MethodGroup CreateMethodGroup(AnalyzedMethod analyzed)
    {
        var method = analyzed.Symbol;
        return new MethodGroup(method.Name,
                               method.ReturnType.ToDisplayString(TypeFormat),
                               analyzed.Imports is null ? "" : string.Join(", ", method.GetReturnTypeAttributes().Select(RenderAttribute)),
                               new EquatableArray<ParameterModel>(method.Parameters.Select(CreateParameter).ToArray()),
                               analyzed.IsStatic,
                               method.IsPartialDefinition ? GetPartialImplementationModifiers(method) : null,
                               //attributes and default values belong to the partial definition only (CS0579, CS1066)
                               string.Join(", ", method.Parameters.Select(p => RenderParameter(p, false))),
                               new EquatableArray<ImportInfo>(analyzed.Imports ?? Array.Empty<ImportInfo>()));
    }

    static ParameterModel CreateParameter(IParameterSymbol parameter)
    {
        var attributes = parameter.GetAttributes();
        return new ParameterModel(attributes.Length > 0 ? "[" + string.Join(", ", attributes.Select(RenderAttribute)) + "] " : "",
                                  (parameter.IsParams ? "params " : "") + GetRefKindModifier(parameter.RefKind),
                                  GetArgumentModifier(parameter.RefKind),
                                  parameter.Type.ToDisplayString(TypeFormat),
                                  EscapeIdentifier(parameter.Name),
                                  parameter.HasExplicitDefaultValue ? " = " + FormatConstant(parameter.ExplicitDefaultValue, parameter.Type, castNumbers: false) : "",
                                  parameter.Type.SpecialType == SpecialType.System_String
                                      && parameter.RefKind == RefKind.None
                                      && !attributes.Any(a => a.AttributeClass?.ToDisplayString() == MARSHAL_AS_ATTRIBUTE_NAME));
    }

    /// <summary>
    /// Modifiers of the partial method implementation, the accessibility has to be the same as in the definition, including no accessibility.
    /// </summary>
    static string GetPartialImplementationModifiers(IMethodSymbol method)
    {
        var modifiers = (method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as MethodDeclarationSyntax)?.Modifiers;
        var accessibility = modifiers?.Where(m => m.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.PrivateKeyword)
                                      .Select(m => m.Text + " ");
        return string.Concat(accessibility ?? Enumerable.Empty<string>()) + "static " + (modifiers?.Any(SyntaxKind.UnsafeKeyword) == true ? "unsafe " : "") + "partial";
    }

    static string RenderParameter(IParameterSymbol parameter, bool withAttributesAndDefault)
    {
        var sb = new StringBuilder();
        var attributes = parameter.GetAttributes();
        if (withAttributesAndDefault && attributes.Length > 0)
            sb.Append('[').Append(string.Join(", ", attributes.Select(RenderAttribute))).Append("] ");
        if (parameter.IsParams)
            sb.Append("params ");
        sb.Append(GetRefKindModifier(parameter.RefKind))
          .Append(parameter.Type.ToDisplayString(TypeFormat))
          .Append(' ')
          .Append(EscapeIdentifier(parameter.Name));
        if (withAttributesAndDefault && parameter.HasExplicitDefaultValue)
            sb.Append(" = ").Append(FormatConstant(parameter.ExplicitDefaultValue, parameter.Type, castNumbers: false));
        return sb.ToString();
    }

    static string RenderAttribute(AttributeData attribute)
    {
        var name = attribute.AttributeClass!.ToDisplayString(TypeFormat);
        var args = attribute.ConstructorArguments.Select(FormatTypedConstant)
                            .Concat(attribute.NamedArguments.Select(a => EscapeIdentifier(a.Key) + " = " + FormatTypedConstant(a.Value)))
                            .ToArray();
        return args.Length == 0 ? name : name + "(" + string.Join(", ", args) + ")";
    }

    static string FormatTypedConstant(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";
        return constant.Kind switch
        {
            TypedConstantKind.Array => "new " + ((IArrayTypeSymbol)constant.Type!).ElementType.ToDisplayString(TypeFormat) + "[] { "
                                       + string.Join(", ", constant.Values.Select(FormatTypedConstant)) + " }",
            TypedConstantKind.Type => "typeof(" + ((ITypeSymbol)constant.Value!).ToDisplayString(TypeFormat) + ")",
            _ => FormatConstant(constant.Value, constant.Type!, castNumbers: true),
        };
    }

    /// <summary>
    /// Formats a compile time constant (default parameter value, attribute argument) as C# expression of the given type.
    /// A number gets a cast in attribute arguments, where the exact type matters (a constructor parameter of type object), a default
    /// value is converted to the type of the parameter by the compiler.
    /// </summary>
    static string FormatConstant(object? value, ITypeSymbol type, bool castNumbers)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            if (value is null)
                return "null";
            //constant of the underlying type is implicitly converted, cast to T? is not a constant expression
            type = nullable.TypeArguments[0];
        }

        if (value is null)
            return type.IsReferenceType ? "null" : "default";

        var typeName = type.ToDisplayString(TypeFormat);
        if (type.TypeKind == TypeKind.Enum)
        {
            var member = type.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, value));
            return member is not null
                ? typeName + "." + EscapeIdentifier(member.Name)
                : "(" + typeName + ")(" + FormatPrimitive(value) + ")";
        }

        var literal = FormatPrimitive(value);
        return value is string or char or bool || !castNumbers ? literal : "(" + typeName + ")(" + literal + ")";
    }

    static string FormatPrimitive(object value) => value switch
    {
        string s => '"' + Escape(s, '"') + '"',
        char c => "'" + Escape(c.ToString(), '\'') + "'",
        bool b => b ? "true" : "false",
        float f when float.IsNaN(f) => "global::System.Single.NaN",
        float f when float.IsPositiveInfinity(f) => "global::System.Single.PositiveInfinity",
        float f when float.IsNegativeInfinity(f) => "global::System.Single.NegativeInfinity",
        float f => f.ToString("R", CultureInfo.InvariantCulture) + "F",
        double d when double.IsNaN(d) => "global::System.Double.NaN",
        double d when double.IsPositiveInfinity(d) => "global::System.Double.PositiveInfinity",
        double d when double.IsNegativeInfinity(d) => "global::System.Double.NegativeInfinity",
        double d => d.ToString("R", CultureInfo.InvariantCulture) + "D",
        decimal m => m.ToString(CultureInfo.InvariantCulture) + "M",
        uint u => u.ToString(CultureInfo.InvariantCulture) + "U",
        long l => l.ToString(CultureInfo.InvariantCulture) + "L",
        ulong ul => ul.ToString(CultureInfo.InvariantCulture) + "UL",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    static string Escape(string value, char quote)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '\0': sb.Append(@"\0"); break;
                case '\a': sb.Append(@"\a"); break;
                case '\b': sb.Append(@"\b"); break;
                case '\f': sb.Append(@"\f"); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                case '\v': sb.Append(@"\v"); break;
                default:
                    if (c == quote)
                        sb.Append('\\').Append(c);
                    else if (char.IsControl(c) || char.IsSurrogate(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                        sb.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    public static string FormatStringLiteral(string value) => FormatPrimitive(value);

    static string GetRefKindModifier(RefKind refKind) => refKind switch
    {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.In => "in ",
        RefKind.RefReadOnlyParameter => "ref readonly ",
        _ => "",
    };

    //modifier of the argument passed to a parameter
    static string GetArgumentModifier(RefKind refKind) => refKind == RefKind.RefReadOnlyParameter ? "in " : GetRefKindModifier(refKind);

    public static string EscapeIdentifier(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    static string GetNameWithTypeParameters(INamedTypeSymbol type)
        => type.TypeParameters.Length == 0
            ? EscapeIdentifier(type.Name)
            : EscapeIdentifier(type.Name) + "<" + string.Join(", ", type.TypeParameters.Select(t => EscapeIdentifier(t.Name))) + ">";

    /// <summary>
    /// Declarations of the containing types, outermost first. Accessibility and constraints are taken from the other partial declarations.
    /// </summary>
    static string[] GetContainingTypeDeclarations(INamedTypeSymbol symbol)
    {
        var result = new List<string>();
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType)
        {
            var kind = type.TypeKind switch
            {
                TypeKind.Struct => type.IsRecord ? "record struct" : "struct",
                TypeKind.Interface => "interface",
                _ => type.IsRecord ? "record" : "class",
            };
            result.Add((type.IsStatic ? "static " : "") + "partial " + kind + " " + GetNameWithTypeParameters(type));
        }
        result.Reverse();
        return result.ToArray();
    }

    //hint name has to be unique within the generator, the generated class name is not (same name in different namespaces,
    //multiple declaration classes generating one partial class), so the fully qualified name of the source class is used
    static string GetHintName(INamedTypeSymbol symbol)
    {
        var name = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var sb = new StringBuilder(name.Length + 5);
        foreach (var c in name)
            sb.Append(char.IsLetterOrDigit(c) || c is '.' or '_' ? c : '_');
        return sb.Append(".g.cs").ToString();
    }

    static string GetAccessModifier(AccessModifiers modifier) => modifier switch
    {
        AccessModifiers.Public => "public",
        AccessModifiers.Protected => "protected",
        AccessModifiers.Internal => "internal",
        AccessModifiers.Private => "private",
        AccessModifiers.ProtectedInternal => "protected internal",
        AccessModifiers.PrivateProtected => "private protected",
        _ => "internal",
    };
}
