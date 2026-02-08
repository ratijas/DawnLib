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

    public void Execute(GeneratorExecutionContext context)
    {
        if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.rootnamespace", out string? rootNamespace) || string.IsNullOrWhiteSpace(rootNamespace))
        {
            context.ReportDiagnostic(Diagnostic.Create(DawnLibDiagnostics.MissingRootNamespace, Location.None));
            return;
        }

        var @class = new GeneratedClass(Visibility.Public, "Tags") // todo: e.g. MeltdownTags
        {
            IsPartial = true,
            IsStatic = true,
            Attributes = { DawnLibSourceGenConstants.CodeGenAttribute }
        };

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
            GeneratedField field = new(Visibility.Public, "NamespacedKey", fieldName)
            {
                IsStatic = true
            };

            if (parts.Length >= 2 && parts[0] == "lethal_company")
            {
                field.Value = $"NamespacedKey.Vanilla(\"{parts[1]}\")";
            }
            else if (parts.Length >= 2)
            {
                field.Value = $"NamespacedKey.From(\"{parts[0]}\", \"{parts[1]}\")";
            }
            else
            {
                continue;
            }

            @class.Members.Add(field);
        }

        if (@class.Members.Count == 0)
        {
            // don't generate tags class if there are no tags.
            return;
        }

        var file = new GeneratedCodeFile()
        {
            Namespace = rootNamespace,
            Usings = ["Dawn"],
            Symbols = [@class]
        };

        var visitor = new FileWriterVisitor();
        visitor.Accept(file);

        context.AddSource($"{@class.Name}.g.cs", SourceText.From(visitor.ToString(), Encoding.UTF8));
    }
}