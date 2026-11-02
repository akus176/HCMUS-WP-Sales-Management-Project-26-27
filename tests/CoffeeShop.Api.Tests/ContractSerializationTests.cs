using System.Text.Json;
using CoffeeShop.Contracts;

namespace CoffeeShop.Api.Tests;

public sealed class ContractSerializationTests
{
    [Fact]
    public void CategoryResponse_UsesStableCamelCaseFields()
    {
        var id = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new CategoryResponse(id, "Coffee", 2, true),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(id, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Coffee", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("displayOrder").GetInt32());
        Assert.True(document.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public void PageResponse_ExposesPagingMetadata()
    {
        var result = new PageResponse<CategoryResponse>([], 1, 20, 0);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        Assert.Equal(0, document.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, document.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public void CreateCategoryRequest_ReadsWebJson()
    {
        var request = JsonSerializer.Deserialize<CreateCategoryRequest>(
            "{\"name\":\"Coffee\",\"displayOrder\":2}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal("Coffee", request!.Name);
        Assert.Equal(2, request.DisplayOrder);
    }
}
