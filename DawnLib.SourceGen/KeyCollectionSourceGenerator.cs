using System.Collections.Generic;
using System.IO;
using System.Text;
using Dawn.SourceGen.AST;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;

namespace Dawn.SourceGen;

[Generator]
public class KeyCollectionSourceGenerator : ISourceGenerator
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

        List<string> alreadyGenerated = [];

        foreach (var additionalFile in context.AdditionalFiles)
        {
            if (additionalFile == null)
                continue;

            var path = additionalFile.Path;
            if (path == null || !path.EndsWith("namespaced_keys.json"))
                continue;

            var text = additionalFile.GetText();
            if (text == null)
                continue;

            Dictionary<string, Dictionary<string, string>>? definitions;
            try
            {
                definitions = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(text.ToString());
            }
            catch
            {
                continue;
            }

            if (definitions == null)
                continue;

            foreach (var className in definitions.Keys)
            {
                Dictionary<string, string> values = definitions[className];
                var @class = new GeneratedClass(Visibility.Public, className)
                {
                    IsStatic = true,
                    IsPartial = true
                };
                string type = $"NamespacedKey<{values["__type"]}>";

                foreach (var value in values)
                {
                    if (value.Key == "__type") continue;
                    string[] parts = value.Value.Split(':');

                    var field = new GeneratedField(Visibility.Public, type, value.Key)
                    {
                        IsStatic = true
                    };

                    if (parts[0] == "lethal_company")
                    {
                        field.Value = $"{type}.Vanilla(\"{parts[1]}\")";
                    }
                    else
                    {
                        field.Value = $"{type}.From(\"{parts[0]}\", \"{parts[1]}\")";
                    }
                    @class.Members.Add(field);
                }

                if (!alreadyGenerated.Contains(@class.Name))
                {
                    var getReflectionMethod = new GeneratedMethod(Visibility.Public, $"{type}?", "GetByReflection")
                    {
                        IsStatic = true,
                        Params = ["string name"],
                        Body =
                        [
                            $"return ({type}?)typeof({@class.Name}).GetField(name)?.GetValue(null);"
                        ]
                    };
                    @class.Members.Add(getReflectionMethod);
                    @class.Attributes.Add(DawnLibSourceGenConstants.CodeGenAttribute);
                }

                var file = new GeneratedCodeFile()
                {
                    Namespace = rootNamespace,
                    Usings = ["Dawn"],
                    Symbols = [@class]
                };

                var visitor = new FileWriterVisitor();
                visitor.Accept(file);

                alreadyGenerated.Add(@class.Name);
                var fileName = $"{Path.GetFileNameWithoutExtension(path).Split('.')[0]}.{className}.g.cs";
                context.AddSource(fileName, SourceText.From(visitor.ToString(), Encoding.UTF8));
            }
        }
    }
}