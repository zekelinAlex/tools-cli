using System.Text.Json;
using TALXIS.CLI.Features.Environment.CustomApi;
using Xunit;

namespace TALXIS.CLI.Tests.Environment.CustomApi;

public sealed class CustomApiMapsTests
{
    [Theory]
    [InlineData("Quantity:integer", "Quantity", 7, false)]
    [InlineData("Comment:string:optional", "Comment", 10, true)]
    [InlineData("Target:entityreference", "Target", 5, false)]
    [InlineData("When:DateTime", "When", 1, false)]
    public void ParseParameterSpec_ValidSpecs(string spec, string name, int typeCode, bool optional)
    {
        var result = CustomApiMaps.ParseParameterSpec(spec, out var error);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal((name, typeCode, optional), result.Value);
    }

    [Theory]
    [InlineData("NoType")]
    [InlineData(":integer")]
    [InlineData("Name:notatype")]
    [InlineData("Name:integer:banana")]
    [InlineData("Name:integer:optional:extra")]
    public void ParseParameterSpec_InvalidSpecs_ReturnError(string spec)
    {
        var result = CustomApiMaps.ParseParameterSpec(spec, out var error);

        Assert.Null(result);
        Assert.NotNull(error);
    }

    [Fact]
    public void ToSummary_MapsODataRecord()
    {
        var record = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["uniquename"] = "udpp_Approve",
            ["name"] = "Approve",
            ["bindingtype"] = 1,
            ["boundentitylogicalname"] = "udpp_warehouseitem",
            ["isfunction"] = false,
            ["isprivate"] = true,
            ["customapiid"] = "7e0edf40-ad7a-f111-ab0e-e4fb1ef8c9e5",
        });

        var summary = CustomApiListCliCommand.ToSummary(record);

        Assert.Equal("udpp_Approve", summary.UniqueName);
        Assert.Equal("entity", summary.BindingType);
        Assert.Equal("udpp_warehouseitem", summary.BoundEntity);
        Assert.False(summary.IsFunction);
        Assert.True(summary.IsPrivate);
        Assert.Equal(Guid.Parse("7e0edf40-ad7a-f111-ab0e-e4fb1ef8c9e5"), summary.Id);
    }

    [Fact]
    public void BuildDefinitions_JoinsParametersByApiId()
    {
        var apiId = "11111111-1111-1111-1111-111111111111";
        var otherId = "22222222-2222-2222-2222-222222222222";
        var apis = new[] { Element(new() { ["customapiid"] = apiId, ["uniquename"] = "udpp_A", ["bindingtype"] = 0, ["isfunction"] = false }) };
        var reqParams = new[]
        {
            Element(new() { ["uniquename"] = "Mine", ["type"] = 10, ["isoptional"] = false, ["_customapiid_value"] = apiId }),
            Element(new() { ["uniquename"] = "Foreign", ["type"] = 10, ["isoptional"] = false, ["_customapiid_value"] = otherId }),
        };
        var respProps = new[] { Element(new() { ["uniquename"] = "Out", ["type"] = 7, ["_customapiid_value"] = apiId }) };

        var definitions = CustomApiGenerateOpenApiCliCommand.BuildDefinitions(apis, reqParams, respProps);

        var definition = Assert.Single(definitions);
        Assert.Equal("Mine", Assert.Single(definition.RequestParameters).UniqueName);
        Assert.Equal("Out", Assert.Single(definition.ResponseProperties).UniqueName);
    }

    private static JsonElement Element(Dictionary<string, object?> values) =>
        JsonSerializer.SerializeToElement(values);
}
