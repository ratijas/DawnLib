using System.Collections.Generic;
using System.Collections.Immutable;
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

        var classesToGenerate = new Dictionary<string, ClassToGenerate>();

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
                string type = $"NamespacedKey<{values["__type"]}>";

                var fieldsToGenerate = new List<FieldToGenerate>();

                foreach (var value in values)
                {
                    var field = GetFieldToGenerate(type, value.Key, value.Value);
                    if (field != null)
                    {
                        fieldsToGenerate.Add(field.Value);
                    }
                }

                if (!classesToGenerate.TryGetValue(className, out var classToGenerate))
                {
                    classToGenerate = new(className, type);
                    classesToGenerate.Add(className, classToGenerate);
                }
                else
                {
                    // Duplicate class name found in different JSON files.
                    // Ensure types in all definitions are the same.
                    if (type != classToGenerate.Type)
                    {
                        // Maybe emit some diagnostic?
                    }
                }

                var fieldsFile = new FieldsFileToGenerate(path, classToGenerate, fieldsToGenerate.ToImmutableArray());

                EmitFields(context, rootNamespace, fieldsFile);
            }
        }

        foreach (var classToGenerate in classesToGenerate.Values)
        {
            EmitCodeGenAttributeAndMethods(context, rootNamespace, classToGenerate);
        }
    }

    static FieldToGenerate? GetFieldToGenerate(string type, string name, string value)
    {
        if (type == "__type")
            return null;

        string[] parts = value.Split(':');
        if (parts.Length != 2)
        {
            // Maybe emit a diagnostic?
            return null;
        }

        return new(type, name, parts[0], parts[1]);
    }

    static GeneratedClass CreateGeneratedClass(string className) =>
        new(Visibility.Public, className)
        {
            IsStatic = true,
            IsPartial = true,
        };

    static void EmitCodeGenAttributeAndMethods(GeneratorExecutionContext context, string rootNamespace, ClassToGenerate classToGenerate)
    {
        var className = classToGenerate.ClassName;
        var type = classToGenerate.Type;
        var @class = CreateGeneratedClass(className);
        var getReflectionMethod = new GeneratedMethod(Visibility.Public, $"{type}?", "GetByReflection")
        {
            IsStatic = true,
            Params = ["string name"],
            Body =
            [
                $"return ({type}?)typeof({className}).GetField(name)?.GetValue(null);"
            ]
        };
        @class.Members.Add(getReflectionMethod);
        @class.Attributes.Add(DawnLibSourceGenConstants.CodeGenAttribute);

        var file = new GeneratedCodeFile()
        {
            Namespace = rootNamespace,
            Usings = ["Dawn"],
            Symbols = [@class]
        };

        var visitor = new FileWriterVisitor();
        visitor.Accept(file);

        var fileName = $"{className}.g.cs";
        context.AddSource(fileName, SourceText.From(visitor.ToString(), Encoding.UTF8));
    }

    static void EmitFields(GeneratorExecutionContext context, string rootNamespace, FieldsFileToGenerate data)
    {
        var className = data.Class.ClassName;
        var @class = CreateGeneratedClass(className);
        foreach (var f in data.Fields)
        {
            var field = new GeneratedField(Visibility.Public, f.Type, f.FieldName)
            {
                IsStatic = true
            };

            if (f.Namespace == "lethal_company")
            {
                field.Value = $"""{f.Type}.Vanilla("{f.Key}")""";
            }
            else
            {
                field.Value = $"""{f.Type}.From("{f.Namespace}", "{f.Key}")""";
            }
            @class.Members.Add(field);
        }
        var file = new GeneratedCodeFile()
        {
            Namespace = rootNamespace,
            Usings = ["Dawn"],
            Symbols = [@class]
        };

        var visitor = new FileWriterVisitor();
        visitor.Accept(file);

        var fileName = $"{Path.GetFileNameWithoutExtension(data.FilePath).Split('.')[0]}.{className}.g.cs";
        context.AddSource(fileName, SourceText.From(visitor.ToString(), Encoding.UTF8));
    }

    record struct ClassToGenerate(string ClassName, string Type);

    record struct FieldToGenerate(string Type, string FieldName, string Namespace, string Key);

    record struct FieldsFileToGenerate(string FilePath, ClassToGenerate Class, ImmutableArray<FieldToGenerate> Fields);
}