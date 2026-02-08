using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Dawn.SourceGen.AST;
using Dawn.SourceGen.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace Dawn.SourceGen;

[Generator]
public class TagSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rootNamespaceProvider = context.AnalyzerConfigOptionsProvider.Select((opts, ct) =>
        {
            if (opts.GlobalOptions.TryGetValue("build_property.rootnamespace", out var rootNamespace) && !string.IsNullOrWhiteSpace(rootNamespace))
            {
                return rootNamespace;
            }
            return null;
        });

        var tags = context.AdditionalTextsProvider
            .Select(GetTagToGenerate)
            .Where(tag => tag != null)
            .Select((tag, _) => tag!.Value);

        var tagsCount = tags.Collect().Select((items, _) => items.Count()).Combine(rootNamespaceProvider);

        context.RegisterSourceOutput(tagsCount, (spc, pair) =>
        {
            var (count, rootNamespace) = pair;
            if (count == 0)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(rootNamespace))
            {
                spc.ReportDiagnostic(Diagnostic.Create(DawnLibDiagnostics.MissingRootNamespace, Location.None));
                return;
            }
            // C# compiler can not deduce that rootNamespace is not null here
            EmitCodeGenAttribute(spc, rootNamespace!);
        });

        var tagsAndNamespace = tags.Combine(rootNamespaceProvider);

        context.RegisterSourceOutput(tagsAndNamespace, (spc, pair) =>
        {
            var (tag, rootNamespace) = pair;
            if (string.IsNullOrWhiteSpace(rootNamespace))
            {
                return;
            }
            EmitTagSource(spc, tag, rootNamespace!);
        });
    }

    void EmitCodeGenAttribute(SourceProductionContext context, string rootNamespace)
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

    TagToGenerate? GetTagToGenerate(AdditionalText additionalFile, CancellationToken cancellationToken)
    {
        if (additionalFile == null)
            return null;

        var path = additionalFile.Path;
        if (path == null || !additionalFile.Path.EndsWith("tag.json"))
            return null;

        var text = additionalFile.GetText(cancellationToken);
        if (text == null)
            return null;

        string fieldName = Path.GetFileName(additionalFile.Path).Split('.')[0];
        fieldName = string.Join("", fieldName.Split('_').Select(it => it.ToCapitalized()));

        TagDefinition? definition;
        try
        {
            definition = JsonConvert.DeserializeObject<TagDefinition>(text.ToString());
        }
        catch
        {
            return null;
        }

        if (definition == null)
            return null;

        string[] parts = definition.Tag.Split(':');
        if (parts.Length != 2)
        {
            // Maybe emit a diagnostic?
            return null;
        }

        return new(fieldName, parts[0], parts[1]);
    }

    private void EmitTagSource(SourceProductionContext context, TagToGenerate tag, string rootNamespace)
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

    record struct TagToGenerate(string FieldName, string Namespace, string Key);
}