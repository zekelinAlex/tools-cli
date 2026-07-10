using System.Text.Json;
using TALXIS.CLI.Features.Environment.CustomApi;
using Xunit;

namespace TALXIS.CLI.Tests.Environment.CustomApi;

public sealed class CustomApiOpenApiBuilderTests
{
    [Fact]
    public void Build_GlobalAction_ProducesPostWithRequestBodyAndResponse()
    {
        var api = new CustomApiDefinition(
            "udpp_CalculateTotal", "Calculate Total", "Sums line items.", 0, null, false,
            [new CustomApiParameter("Quantity", "Quantity", 7, false), new CustomApiParameter("Comment", "Comment", 10, true)],
            [new CustomApiParameter("Total", "Total", 8, false)]);

        var doc = CustomApiOpenApiBuilder.Build([api], "Test", "1.0.0", "https://org.crm4.dynamics.com/");
        var json = JsonSerializer.SerializeToElement(doc);

        var operation = json.GetProperty("paths").GetProperty("/udpp_CalculateTotal").GetProperty("post");
        Assert.Equal("udpp_CalculateTotal", operation.GetProperty("operationId").GetString());

        var bodySchema = operation.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.Equal("integer", bodySchema.GetProperty("properties").GetProperty("Quantity").GetProperty("type").GetString());
        Assert.Equal(["Quantity"], bodySchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));

        var responseSchema = operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema");
        Assert.Equal("number", responseSchema.GetProperty("properties").GetProperty("Total").GetProperty("type").GetString());

        Assert.Equal("https://org.crm4.dynamics.com/api/data/v9.2",
            json.GetProperty("servers")[0].GetProperty("url").GetString());
    }

    [Fact]
    public void Build_GlobalFunction_ProducesGetWithQueryParameters()
    {
        var api = new CustomApiDefinition(
            "udpp_GetRate", "Get Rate", null, 0, null, true,
            [new CustomApiParameter("Currency", "Currency", 10, false)],
            []);

        var doc = CustomApiOpenApiBuilder.Build([api], "Test", "1.0.0", null);
        var json = JsonSerializer.SerializeToElement(doc);

        var operation = json.GetProperty("paths").GetProperty("/udpp_GetRate").GetProperty("get");
        var parameter = operation.GetProperty("parameters")[0];
        Assert.Equal("Currency", parameter.GetProperty("name").GetString());
        Assert.Equal("query", parameter.GetProperty("in").GetString());
        Assert.True(parameter.GetProperty("required").GetBoolean());

        Assert.True(operation.GetProperty("responses").TryGetProperty("204", out _));
        Assert.False(json.TryGetProperty("servers", out _));
    }

    [Fact]
    public void Build_EntityBoundAction_IncludesIdPathParameter()
    {
        var api = new CustomApiDefinition(
            "udpp_Approve", "Approve", null, 1, "udpp_warehouseitem", false, [], []);

        var doc = CustomApiOpenApiBuilder.Build([api], "Test", "1.0.0", null);
        var json = JsonSerializer.SerializeToElement(doc);

        var path = "/udpp_warehouseitem({id})/Microsoft.Dynamics.CRM.udpp_Approve";
        var operation = json.GetProperty("paths").GetProperty(path).GetProperty("post");
        var parameter = operation.GetProperty("parameters")[0];
        Assert.Equal("id", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
        Assert.Equal("uuid", parameter.GetProperty("schema").GetProperty("format").GetString());
    }

    [Theory]
    [InlineData(0, "boolean", null)]
    [InlineData(1, "string", "date-time")]
    [InlineData(8, "number", "decimal")]
    [InlineData(11, "array", null)]
    [InlineData(12, "string", "uuid")]
    public void ToOpenApiSchema_MapsTypeCodes(int code, string expectedType, string? expectedFormat)
    {
        var (type, format, _) = CustomApiMaps.ToOpenApiSchema(code);
        Assert.Equal(expectedType, type);
        Assert.Equal(expectedFormat, format);
    }
}
