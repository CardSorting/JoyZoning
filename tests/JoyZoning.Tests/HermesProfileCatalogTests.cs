using JoyZoning.Agents.Hermes;
using Xunit;

namespace JoyZoning.Tests;

[Trait(TestCategories.Key, TestCategories.Unit)]
public class HermesProfileCatalogTests
{
    [Fact]
    public void ReadModel_inline_string()
    {
        var path = Path.Combine(Path.GetTempPath(), "jz-yaml-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, "model: openrouter/some-model\nother: true\n");
        try
        {
            var model = HermesConfigYamlReader.ReadModel(path);
            Assert.Equal("openrouter/some-model", model.Model);
            Assert.Equal(HermesModelFormat.InlineString, model.Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadModel_structured_block()
    {
        var path = Path.Combine(Path.GetTempPath(), "jz-yaml-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllText(path, """
            model:
              default: deepseek-v4-pro
              provider: nous
              base_url: https://inference-api.nousresearch.com/v1
            """);
        try
        {
            var model = HermesConfigYamlReader.ReadModel(path);
            Assert.Equal("deepseek-v4-pro", model.Model);
            Assert.Equal("nous", model.Provider);
            Assert.Equal("https://inference-api.nousresearch.com/v1", model.BaseUrl);
            Assert.Equal(HermesModelFormat.Structured, model.Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void BuildConfigSetCommands_structured()
    {
        var commands = HermesCliConfigurator.BuildConfigSetCommands(new HermesModelConfig
        {
            Model = "deepseek-v4-pro",
            Provider = "nous",
            BaseUrl = "https://example.com/v1",
            Format = HermesModelFormat.Structured,
        });

        Assert.Equal(3, commands.Count);
        Assert.Contains("model.default deepseek-v4-pro", commands);
        Assert.Contains("model.provider nous", commands);
    }

    [Fact]
    public void BuildConfigSetCommands_inline()
    {
        var commands = HermesCliConfigurator.BuildConfigSetCommands(new HermesModelConfig
        {
            Model = "google/gemini-3.1-pro-preview",
            Format = HermesModelFormat.InlineString,
        });

        Assert.Single(commands);
        Assert.Equal("model google/gemini-3.1-pro-preview", commands[0]);
    }
}
