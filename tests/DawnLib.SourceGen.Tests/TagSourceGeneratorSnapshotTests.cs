using Microsoft.CodeAnalysis.CSharp;
using Dawn.SourceGen.Tests.Utils;
using Microsoft.CodeAnalysis;

namespace Dawn.SourceGen.Tests;

public class TagSourceGeneratorSnapshotTests
{
    // Use DawnLib/data/tags/cold.tag.json
    static AdditionalText SomeTestTagFile => new InMemoryAdditionalText("some_test_filename.tag.json", """
{
  "tag": "lethal_company:cold",
  "values": [
    "lethal_company:rend",
    "lethal_company:dine",
    "lethal_company:titan"
  ]
}
""");

    [Fact]
    public Task VanillaTagTest()
    {
        var compilation = CSharpCompilation.Create(nameof(VanillaTagTest));
        var generator = new TagSourceGenerator();

        var globalValues = new Dictionary<string,string>
        {
            // Corresponds to <RootNamespace> in .csproj
            ["build_property.rootnamespace"] = "My.Test.Namespace",
        };
        var provider = new SimpleAnalyzerConfigOptionsProvider(new DictAnalyzerConfigOptions(globalValues));
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([SomeTestTagFile])
            .WithUpdatedAnalyzerConfigOptions(provider)
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        return Verify(driver, Settings.Instance);
    }

    [Fact]
    public Task DiagnosticDAWN001Test()
    {
        var compilation = CSharpCompilation.Create(nameof(DiagnosticDAWN001Test));
        var generator = new TagSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .AddAdditionalTexts([SomeTestTagFile])
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        return Verify(driver, Settings.Instance);
    }
}
