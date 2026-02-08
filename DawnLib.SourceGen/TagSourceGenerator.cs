using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Dawn.SourceGen.AST;
using Dawn.SourceGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace Dawn.SourceGen;

[Generator]
public class TagSourceGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    void EmitCodeGenAttribute(GeneratorExecutionContext context, string rootNamespace)
    {
        var @class = new GeneratedClass(Visibility.Public, "Tags")
        {
            IsPartial = true,
            IsStatic = true,
            Attributes = { DawnLibSourceGenConstants.CodeGenAttribute }
        };

        var file = new GeneratedCodeFile()
        {
            Namespace = rootNamespace,
            Usings = ["Dawn"],
            Symbols = [@class]
        };

        var visitor = new FileWriterVisitor();
        visitor.Accept(file);

        var fileName = $"{@class.Name}.g.cs";
        context.AddSource(fileName, SourceText.From(visitor.ToString(), Encoding.UTF8));
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.rootnamespace", out string? rootNamespace) || string.IsNullOrWhiteSpace(rootNamespace))
        {
            context.ReportDiagnostic(Diagnostic.Create(DawnLibDiagnostics.MissingRootNamespace, Location.None));
            return;
        }

        List<TagToGenerate> tagsToGenerate = [];

        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (additionalFile == null)
                continue;

            var path = additionalFile.Path;
            if (path == null || !additionalFile.Path.EndsWith("tag.json"))
                continue;

            var text = additionalFile.GetText();
            if (text == null)
                continue;

            string fieldName = Path.GetFileName(additionalFile.Path).Split('.')[0];
            fieldName = string.Join("", fieldName.Split('_').Select(it => it.ToCapitalized()));

            TagDefinition? definition;
            try
            {
                definition = JsonConvert.DeserializeObject<TagDefinition>(text.ToString());
            }
            catch
            {
                continue;
            }

            if (definition == null)
                continue;

            string[] parts = definition.Tag.Split(':');
            if (parts.Length != 2)
            {
                // Maybe emit a diagnostic?
                continue;
            }

            tagsToGenerate.Add(new(fieldName, parts[0], parts[1]));
        }

        if (tagsToGenerate.Count != 0)
        {
            // Produce CodeGenAttribute only once, and only if there are any tags at all
            EmitCodeGenAttribute(context, rootNamespace);
        }

        foreach (var tag in tagsToGenerate)
        {
            var @class = new GeneratedClass(Visibility.Public, "Tags")
            {
                IsPartial = true,
                IsStatic = true,
            };

            GeneratedField field = new(Visibility.Public, "NamespacedKey", tag.FieldName)
            {
                IsStatic = true
            };

            if (tag.Namespace == "lethal_company")
            {
                field.Value = $"""NamespacedKey.Vanilla("{tag.Key}")""";
            }
            else
            {
                field.Value = $"""NamespacedKey.From("{tag.Namespace}", "{tag.Key}")""";
            }

            @class.Members.Add(field);

            var file = new GeneratedCodeFile()
            {
                Namespace = rootNamespace,
                Usings = ["Dawn"],
                Symbols = [@class]
            };

            var visitor = new FileWriterVisitor();
            visitor.Accept(file);

            var fileName = $"{@class.Name}.{tag.FieldName}.g.cs";
            context.AddSource(fileName, SourceText.From(visitor.ToString(), Encoding.UTF8));
        }
    }

    record struct TagToGenerate(string FieldName, string Namespace, string Key);
}