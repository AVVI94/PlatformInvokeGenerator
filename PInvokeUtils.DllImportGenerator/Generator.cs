using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PlatformInvokeGenerator;

[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static c =>
        {
            //embedded types are not visible to other assemblies, not even through InternalsVisibleTo (no CS0436 conflicts
            //between projects using the generator), the attribute definition is shared by all generators of the project
            c.AddEmbeddedAttributeDefinition();
            c.AddSource("PInvokeUtils.Attributes.g.cs", SourceText.From(Helper.EmbeddedAttributes, Encoding.UTF8));
        });

        //#nullable directive is not available before C# 8
        IncrementalValueProvider<bool> supportsNullable = context.ParseOptionsProvider.Select(
            static (x, _) => x is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp8 }
            );

        IncrementalValuesProvider<ClassInfo> classes = context.SyntaxProvider.ForAttributeWithMetadataName(Helper.EXTERN_CLASS_ATTRIBUTE_NAME,
                                                                                                           static (x, _) => x is ClassDeclarationSyntax,
                                                                                                           ModelBuilder.Build)
                                                                             .Where(static c => c.HasValue)
                                                                             .Select(static (c, _) => c!.Value);

        //the diagnostics are reported by ExternClassAnalyzer, classes with errors are not in the model
        context.RegisterSourceOutput(classes.Combine(supportsNullable), static (ctx, tuple) =>
        {
            var (externClass, nullable) = tuple;
            ctx.AddSource(externClass.HintName, SourceEmitter.Emit(externClass, nullable));
        });
    }
}
