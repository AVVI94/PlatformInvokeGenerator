using System;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace PlatformInvokeGenerator;

[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(
            c => c.AddSource("PInvokeUtils.Attributes.g.cs",
                             SourceText.From(Helper.ATTRIBUTES, Encoding.UTF8))
            );

        IncrementalValueProvider<bool> hasRuntimeInformationClass = context.CompilationProvider.Select(
            (x, _) => x.GetTypeByMetadataName("System.Runtime.InteropServices.RuntimeInformation") is not null
            );

        IncrementalValuesProvider<ClassInfo?> classes = context.SyntaxProvider.ForAttributeWithMetadataName(Helper.EXTERN_CLASS_ATTRIBUTE_NAME,
                                                                          static (x, _) => x is ClassDeclarationSyntax,
                                                                          GetMethodsToGenerate)
                                                                         .Where(static c => c.HasValue);

        context.RegisterSourceOutput(classes.Combine(hasRuntimeInformationClass), static (ctx, tuple) =>
        {
            var (v, runtimeInfo) = tuple;
            if (v is null)
                return;//???

            if (!runtimeInfo)
            {
                ctx.AddSource("OperatingSystem.g.cs", OperatingSystemHelper.OPERATING_SYSTEM_CLASS);
                ctx.AddSource("OSPlatform.g.cs", OperatingSystemHelper.OS_PLATFORM_STRUCT);

            }

            var value = v!.Value;

            //if (value.GeneratePartialClass)
            //{

            //}
            //else if()

            if (string.IsNullOrWhiteSpace(value.GeneratedClassName) && !value.IsDecoratedClassPartial)
                ctx.ReportDiagnostic(Diagnostic.Create(ErrorHelper.ErrorNotPartialClassWithoutName,
                                                           Location.Create(value.ExternClassAttributeLineSpan.Path,
                                                           value.ExternClassAttributeTextSpan,
                                                           new LinePositionSpan(
                                                               value!.ExternClassAttributeLineSpan.StartLinePosition,
                                                               value.ExternClassAttributeLineSpan.EndLinePosition))));
            else if (ErrorHelper.IsNameInvalid(value.GeneratedClassName))
                ctx.ReportDiagnostic(Diagnostic.Create(ErrorHelper.ErrorInvalidClassName,
                                                               Location.Create(value.ExternClassAttributeLineSpan.Path,
                                                               value.ExternClassAttributeTextSpan,
                                                               new LinePositionSpan(
                                                                   value.ExternClassAttributeLineSpan.StartLinePosition,
                                                                   value.ExternClassAttributeLineSpan.EndLinePosition)),
                                                               value.GeneratedClassName));

            if (ErrorHelper.IsClassAccessModInvalid(value.ClassAccessModifier))
                ctx.ReportDiagnostic(Diagnostic.Create(ErrorHelper.ErrorInvalidClassName,
                                                       Location.Create(value.ExternClassAttributeLineSpan.Path,
                                                       value.ExternClassAttributeTextSpan,
                                                       new LinePositionSpan(
                                                           value.ExternClassAttributeLineSpan.StartLinePosition,
                                                           value.ExternClassAttributeLineSpan.EndLinePosition)),
                                                       value.ClassAccessModifier));


            if (!string.IsNullOrEmpty(value.Namespace) && ErrorHelper.IsNamespaceInvalid(value.Namespace))
                ctx.ReportDiagnostic(Diagnostic.Create(ErrorHelper.ErrorInvalidNamespace,
                                                       Location.Create(value.ExternClassAttributeLineSpan.Path,
                                                       value.ExternClassAttributeTextSpan,
                                                       new LinePositionSpan(
                                                           value.ExternClassAttributeLineSpan.StartLinePosition,
                                                           value.ExternClassAttributeLineSpan.EndLinePosition)),
                                                       value.Namespace));

            //check if platform isn't specified twice for a single method or if the method doesn't have DllImport attribute
            foreach (var methodCollection in value.Methods)
            {
                bool win32 = false, win64 = false, osx = false, linux64 = false;
                foreach (var method in methodCollection)
                {
                    if (method.HasDllImport)
                        ctx.ReportDiagnostic(Diagnostic.Create(ErrorHelper.ErrorDllImport,
                            Location.Create(method.DllImportForTextLinePosSpan.Path,
                                            method.DllImportForText,
                                            new LinePositionSpan(method.DllImportForTextLinePosSpan.StartLinePosition,
                                                                 method.DllImportForTextLinePosSpan.EndLinePosition))));
                    switch (method.Platform)
                    {
                        case ImportPlatform.Win32:
                            if (win32)
                                EmitSamePlatformsError(ctx, method, nameof(ImportPlatform.Win32));
                            win32 = true;
                            break;
                        case ImportPlatform.Win64:
                            if (win64)
                                EmitSamePlatformsError(ctx, method, nameof(ImportPlatform.Win64));
                            win64 = true;
                            break;
                        case ImportPlatform.Linux64:
                            if (linux64)
                                EmitSamePlatformsError(ctx, method, nameof(ImportPlatform.Linux64));
                            linux64 = true;
                            break;
                        case ImportPlatform.Osx:
                            if (osx)
                                EmitSamePlatformsError(ctx, method, nameof(ImportPlatform.Osx));
                            osx = true;
                            break;
                        default:
                            break;
                    }
                }
            }

            static void EmitSamePlatformsError(SourceProductionContext ctx, MethodInfo method, string platform)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(ErrorHelper.ErrorSamePlatforms,
                                                Location.Create(method.DllImportForTextLinePosSpan.Path,
                                                                method.DllImportForText,
                                                                new LinePositionSpan(method.DllImportForTextLinePosSpan.StartLinePosition,
                                                                                     method.DllImportForTextLinePosSpan.EndLinePosition)), platform));
            }

            var source = Helper.GenerateExternClass(value, !runtimeInfo);
            ctx.AddSource(value.GeneratedClassName + ".g.cs", source);

        });
    }

    static unsafe ClassInfo? GetMethodsToGenerate(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol || context.TargetNode is not ClassDeclarationSyntax classDecl)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        var mods = classDecl.Modifiers;
        ClassInfo classInfo = Helper.GetClassInfo(symbol, in mods);

        foreach (var member in symbol.GetMembers())
        {
            if (!member.IsExtern || !member.IsStatic || member is not IMethodSymbol method)
                continue;

            List<AttributeData> dllImportFors = Helper.ExtractDllImportrForAttributes(member, out var hasDllImport);

            if (dllImportFors.Count == 0)
                continue;

            List<MethodInfo> methods = new(dllImportFors.Count);

            var sb = new StringBuilder();
            foreach (var import in dllImportFors)
            {
                sb.Clear();
                MethodInfo methodInfo = Helper.GetMethodInfo(method, hasDllImport, sb, import);
                methods.Add(methodInfo);
            }
            classInfo.Methods!.Add(methods);
        }

        return classInfo;
    }
}
