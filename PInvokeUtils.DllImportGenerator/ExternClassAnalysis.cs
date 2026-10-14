using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PlatformInvokeGenerator;

/// <summary>
/// Validated class decorated with ExternClassAttribute. Shared by the analyzer, which reports the diagnostics, and by the generator,
/// which generates the valid methods, a throwing stub for the methods with errors and nothing for a class with errors.
/// </summary>
internal sealed class ExternClassAnalysis
{
    const string DLL_IMPORT_ATTRIBUTE_NAME = "System.Runtime.InteropServices.DllImportAttribute";

    ExternClassAnalysis(INamedTypeSymbol symbol, ClassDeclarationSyntax declaration)
    {
        Symbol = symbol;
        Declaration = declaration;
    }

    public INamedTypeSymbol Symbol { get; }
    //the declaration with ExternClassAttribute
    public ClassDeclarationSyntax Declaration { get; }
    public string? GeneratedClassName { get; private set; }
    public string? GeneratedNamespace { get; private set; }
    public AccessModifiers ClassAccessModifier { get; private set; } = AccessModifiers.Internal;
    public AccessModifiers CallMethodsAccessModifier { get; private set; } = AccessModifiers.Internal;
    public bool GenerateStaticClass { get; private set; }
    public bool GenerateUnsafeClass { get; private set; }
    public bool IsPartial => Declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    //without a name the methods are generated into the decorated class itself
    public bool IntoDecoratedClass => string.IsNullOrWhiteSpace(GeneratedClassName);
    public List<Diagnostic> Diagnostics { get; } = new();
    //errors of the class itself (name, namespace, libraries...), no code is generated for the class
    public bool HasClassErrors { get; private set; }
    //methods to generate, methods with errors have no imports and get a throwing stub
    public List<AnalyzedMethod> Methods { get; } = new();

    public static ExternClassAnalysis? Analyze(INamedTypeSymbol symbol, AttributeData attribute, Compilation compilation, CancellationToken cancellationToken)
    {
        //attribute -> attribute list -> class declaration (not a record or other type)
        if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).Parent?.Parent is not ClassDeclarationSyntax declaration)
            return null;

        var analysis = new ExternClassAnalysis(symbol, declaration);
        analysis.ReadClassOptions(attribute);
        analysis.ValidateClass(attribute.ApplicationSyntaxReference);

        //defaults of the methods (CallingConvention, CharSet, StringMarshalling) and libraries of DllImportFor() methods
        var classOptions = ImportOptions.Default.With(attribute);
        var classLibraries = analysis.GetClassLibraries();

        //errors so far belong to the class
        analysis.HasClassErrors = analysis.Diagnostics.Any(IsError);

        var hasExternInDecoratedClass = false;
        foreach (var member in symbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol method)
                continue;

            var dllImportFors = method.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == Helper.DLL_IMPORT_FOR_ATTRIBUTE_NAME).ToArray();
            if (dllImportFors.Length == 0)
                continue;
            var methodSyntax = dllImportFors[0].ApplicationSyntaxReference;

            //errors of a method skip only that method, the other methods of the class are generated
            if (!method.IsStatic || !(method.IsExtern || method.IsPartialDefinition))
            {
                analysis.Diagnostics.Add(ErrorHelper.Create(ErrorHelper.WarningMethodIgnored, methodSyntax, method.Name));
                continue;
            }
            //DllImport is not allowed in generic methods (CS7042), no stub, it would have to be generic as well
            if (method.IsGenericMethod)
            {
                analysis.Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorGenericMethod, methodSyntax, method.Name));
                continue;
            }
            //partial method can be implemented only in its own class, no stub
            if (method.IsPartialDefinition && !analysis.IntoDecoratedClass)
            {
                analysis.Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorPartialMethodInOtherClass, methodSyntax, method.Name));
                continue;
            }

            var methodDiagnostics = new List<Diagnostic>();
            var imports = GetImports(method, dllImportFors, classLibraries, classOptions, methodDiagnostics, compilation, out var isStatic);
            analysis.Diagnostics.AddRange(methodDiagnostics);

            //a partial method with accessibility, a return value or out parameters must have an implementation (CS8795),
            //methods with errors get one that throws, so only their own diagnostic is reported
            if (imports is null || methodDiagnostics.Any(IsError))
            {
                analysis.Methods.Add(new AnalyzedMethod(method, null, true));
                continue;
            }

            hasExternInDecoratedClass |= method.IsExtern && analysis.IntoDecoratedClass;
            analysis.Methods.Add(new AnalyzedMethod(method, imports, isStatic));
        }

        //.NET Framework refuses to load a type with extern methods without DllImport (TypeLoadException "no RVA"),
        //.NET (Core) fails only when such method is called. netstandard libraries can run on .NET Framework as well.
        if (hasExternInDecoratedClass && compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly.Name is "mscorlib" or "netstandard")
            analysis.Diagnostics.Add(ErrorHelper.Create(ErrorHelper.WarningExternInDecoratedClass, attribute.ApplicationSyntaxReference, symbol.Name));

        return analysis;
    }

    //the default severity, the generated code must not depend on the severity configured by the user
    static bool IsError(Diagnostic diagnostic) => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error;

    void ReadClassOptions(AttributeData attribute)
    {
        GenerateUnsafeClass = Declaration.Modifiers.Any(SyntaxKind.UnsafeKeyword);
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Value.IsNull)
                continue;
            switch (arg.Key)
            {
                case nameof(ExternClassAttribute.GeneratedClassName):
                    GeneratedClassName = (string)arg.Value.Value!;
                    break;
                case nameof(ExternClassAttribute.GeneratedClassNamespace):
                    GeneratedNamespace = (string)arg.Value.Value!;
                    break;
                case nameof(ExternClassAttribute.ClassAccessModifier):
                    ClassAccessModifier = (AccessModifiers)arg.Value.Value!;
                    break;
                case nameof(ExternClassAttribute.CallMethodsAccessModifier):
                    CallMethodsAccessModifier = (AccessModifiers)arg.Value.Value!;
                    break;
                case nameof(ExternClassAttribute.GenerateStaticClass):
                    GenerateStaticClass = (bool)arg.Value.Value!;
                    break;
                case nameof(ExternClassAttribute.GenerateUnsafeClass):
                    GenerateUnsafeClass |= (bool)arg.Value.Value!;
                    break;
            }
        }
    }

    void ValidateClass(SyntaxReference attributeSyntax)
    {
        if (IntoDecoratedClass && !IsPartial)
            Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorNotPartialClassWithoutName, attributeSyntax));
        else if (!IntoDecoratedClass && ErrorHelper.IsNameInvalid(GeneratedClassName))
            Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorInvalidClassName, attributeSyntax, GeneratedClassName!));

        //DllImport is not allowed in generic types (CS7042)
        if (IntoDecoratedClass && IsPartial && IsInGenericType(Symbol))
            Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorGenericClass, attributeSyntax, Symbol.ToDisplayString()));

        if (!IntoDecoratedClass && ErrorHelper.IsClassAccessModInvalid(ClassAccessModifier))
            Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorClassAccessModifier, attributeSyntax, ClassAccessModifier.ToString()));

        if (!string.IsNullOrWhiteSpace(GeneratedNamespace) && ErrorHelper.IsNamespaceInvalid(GeneratedNamespace))
            Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorInvalidNamespace, attributeSyntax, GeneratedNamespace!));
    }

    /// <summary>
    /// Libraries of the class declared by ImportLibraryAttribute, invalid and duplicate platforms are errors of the class.
    /// </summary>
    List<(ImportPlatform Platform, string DllName)> GetClassLibraries()
    {
        var libraries = new List<(ImportPlatform, string)>();
        foreach (var attribute in Symbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == Helper.IMPORT_LIBRARY_ATTRIBUTE_NAME))
        {
            if (attribute.ConstructorArguments.Length != 2 || attribute.ConstructorArguments[0].Value is not string dllName || attribute.ConstructorArguments[1].Value is not int platformValue)
                continue;//invalid attribute usage is reported by the compiler
            if (!Enum.IsDefined(typeof(ImportPlatform), platformValue))
            {
                Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorInvalidPlatform, attribute.ApplicationSyntaxReference, platformValue.ToString(CultureInfo.InvariantCulture)));
                continue;
            }
            var platform = (ImportPlatform)platformValue;
            if (libraries.Any(l => l.Item1 == platform))
            {
                Diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorSamePlatforms, attribute.ApplicationSyntaxReference, platform.ToString()));
                continue;
            }
            libraries.Add((platform, dllName));
        }
        return libraries;
    }

    static ImportInfo[]? GetImports(IMethodSymbol method,
                                    AttributeData[] dllImportFors,
                                    List<(ImportPlatform Platform, string DllName)> classLibraries,
                                    ImportOptions classOptions,
                                    List<Diagnostic> diagnostics,
                                    Compilation compilation,
                                    out bool isStatic)
    {
        var hasDllImport = method.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == DLL_IMPORT_ATTRIBUTE_NAME);
        bool? staticCallMethod = null;
        foreach (var attribute in dllImportFors)
        {
            if (hasDllImport)
                diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorDllImport, attribute.ApplicationSyntaxReference));
            foreach (var arg in attribute.NamedArguments)
            {
                if (arg.Key != nameof(DllImportForAttribute.StaticCallMethod) || arg.Value.IsNull)
                    continue;
                var value = (bool)arg.Value.Value!;
                if (staticCallMethod is { } previous && previous != value)
                    diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorStaticCallMethodMismatch, attribute.ApplicationSyntaxReference, method.Name));
                staticCallMethod = value;
            }
        }
        isStatic = staticCallMethod ?? true;

        //DllImportFor() imports the libraries of the class, DllImportFor(dll, platform) adds or replaces one platform
        var withoutLibrary = dllImportFors.Where(a => a.ConstructorArguments.Length == 0).ToArray();
        var imports = new Dictionary<ImportPlatform, ImportInfo>();
        var methodOptions = classOptions;
        if (withoutLibrary.Length > 0)
        {
            if (withoutLibrary.Length > 1)
                diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorMoreDllImportForWithoutLibrary, withoutLibrary[1].ApplicationSyntaxReference, method.Name));
            if (classLibraries.Count == 0)
                diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorNoImportLibrary, withoutLibrary[0].ApplicationSyntaxReference, method.Name));

            methodOptions = classOptions.With(withoutLibrary[0]);
            foreach (var (platform, dllName) in classLibraries)
                imports[platform] = methodOptions.ToImport(platform, dllName, method.Name);
        }

        var ownPlatforms = new HashSet<ImportPlatform>();
        foreach (var attribute in dllImportFors.Where(a => a.ConstructorArguments.Length != 0))
        {
            var syntax = attribute.ApplicationSyntaxReference;
            if (attribute.ConstructorArguments.Length != 2 || attribute.ConstructorArguments[0].Value is not string dllName || attribute.ConstructorArguments[1].Value is not int platformValue)
                continue;//invalid attribute usage is reported by the compiler
            if (!Enum.IsDefined(typeof(ImportPlatform), platformValue))
            {
                diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorInvalidPlatform, syntax, platformValue.ToString(CultureInfo.InvariantCulture)));
                continue;
            }

            var platform = (ImportPlatform)platformValue;
            if (!ownPlatforms.Add(platform))
                diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorSamePlatforms, syntax, platform.ToString()));
            imports[platform] = methodOptions.With(attribute).ToImport(platform, dllName, method.Name);
        }

        if (imports.Count == 0)
            return null;

        //the converted strings are passed as pointers, only strings passed by value can be converted
        if (imports.Values.FirstOrDefault(i => i.StringMarshalling != NativeStringMarshalling.Default) is { DllName: not null } converting)
        {
            var unsupported = method.ReturnType.SpecialType == SpecialType.System_String
                ? "the return value"
                : method.Parameters.FirstOrDefault(p => p.Type.SpecialType == SpecialType.System_String && p.RefKind != RefKind.None)?.Name;
            if (unsupported is not null)
                diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorStringMarshallingNotSupported, dllImportFors[0].ApplicationSyntaxReference,
                                                   converting.StringMarshalling.ToString(), method.Name, unsupported));
        }

        //the OS and architecture checks use the official RuntimeInformation (e.g. the System.Runtime.InteropServices.RuntimeInformation package for .NET Framework 4.6.1)
        if (SourceEmitter.RequiresRuntimeInformation(imports.Keys) && !HasRuntimeInformation(compilation))
            diagnostics.Add(ErrorHelper.Create(ErrorHelper.ErrorRuntimeInformationMissing, dllImportFors[0].ApplicationSyntaxReference, method.Name));

        return imports.Values.ToArray();
    }

    static bool HasRuntimeInformation(Compilation compilation)
        => HasAccessibleType(compilation, "System.Runtime.InteropServices.RuntimeInformation")
           && HasAccessibleType(compilation, "System.Runtime.InteropServices.OSPlatform")
           && HasAccessibleType(compilation, "System.Runtime.InteropServices.Architecture");

    //GetTypeByMetadataName returns null also when more assemblies define the type
    static bool HasAccessibleType(Compilation compilation, string metadataName)
        => compilation.GetTypesByMetadataName(metadataName).Any(t => compilation.IsSymbolAccessibleWithin(t, compilation.Assembly));

    static bool IsInGenericType(INamedTypeSymbol symbol)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.IsGenericType)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Values of an import that can be set by ExternClassAttribute, DllImportForAttribute() and DllImportForAttribute(dll, platform).
    /// </summary>
    readonly record struct ImportOptions(string? EntryPoint, CallingConvention CallingConvention, CharSet CharSet, bool SetLastError, NativeStringMarshalling StringMarshalling)
    {
        public static readonly ImportOptions Default = new(null, CallingConvention.Cdecl, CharSet.Ansi, false, NativeStringMarshalling.Default);

        /// <summary>
        /// Options with the named arguments of the attribute applied
        /// </summary>
        public ImportOptions With(AttributeData attribute)
        {
            var options = this;
            foreach (var arg in attribute.NamedArguments)
            {
                if (arg.Value.IsNull)
                    continue;
                switch (arg.Key)
                {
                    case nameof(DllImportForAttribute.EntryPoint):
                        if (!string.IsNullOrWhiteSpace((string)arg.Value.Value!))
                            options = options with { EntryPoint = (string)arg.Value.Value! };
                        break;
                    case nameof(DllImportForAttribute.CallingConvention):
                        options = options with { CallingConvention = (CallingConvention)arg.Value.Value! };
                        break;
                    case nameof(DllImportForAttribute.CharSet):
                        options = options with { CharSet = (CharSet)arg.Value.Value! };
                        break;
                    case nameof(DllImportForAttribute.SetLastError):
                        options = options with { SetLastError = (bool)arg.Value.Value! };
                        break;
                    case nameof(DllImportForAttribute.StringMarshalling):
                        options = options with { StringMarshalling = (NativeStringMarshalling)arg.Value.Value! };
                        break;
                }
            }
            return options;
        }

        public ImportInfo ToImport(ImportPlatform platform, string dllName, string methodName)
            => new(platform, dllName, EntryPoint ?? methodName, CallingConvention, CharSet, SetLastError, StringMarshalling);
    }
}

/// <summary>
/// Method with DllImportFor to generate, without imports for a method with errors (the generated method only throws).
/// </summary>
internal readonly record struct AnalyzedMethod(IMethodSymbol Symbol, ImportInfo[]? Imports, bool IsStatic);
