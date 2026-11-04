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

    [Fact]
    public void PreparationTicket_ExposesOnlyOperationalFields()
    {
        var ticket = new PreparationTicketResponse(Guid.NewGuid(), "ORD-12", "confirmed", "T4",
            [new PreparationTicketLine("Latte", "Large", 2, ["Oat milk"])]);
        var json = JsonSerializer.Serialize(ticket, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.Equal("ORD-12", document.RootElement.GetProperty("orderNumber").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("customerId", out _));
    }

    [Fact]
    public void DiscountAndInventoryContracts_PreserveMoneyAndQuantityAsDecimals()
    {
        var discount = new PendingDiscountResponse(Guid.NewGuid(), Guid.NewGuid(), "ORD-12",
            "percentage", 10m, "Manager approval", 2.50m, 22.50m);
        var balance = new IngredientBalanceResponse(Guid.NewGuid(), "Milk", "litre", 1.25m, 2m);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        using var discountJson = JsonDocument.Parse(JsonSerializer.Serialize(discount, options));
        using var balanceJson = JsonDocument.Parse(JsonSerializer.Serialize(balance, options));

        Assert.Equal(2.50m, discountJson.RootElement.GetProperty("computedAmount").GetDecimal());
        Assert.Equal(1.25m, balanceJson.RootElement.GetProperty("availableQuantity").GetDecimal());
        Assert.Equal(-0.5m, JsonSerializer.Deserialize<StockAdjustmentRequest>(
            "{\"quantityDelta\":-0.5,\"reason\":\"Damaged\"}", options)!.QuantityDelta);
    }

    [Fact]
    public void CourierListOmitsPrivateDetails_AndMutationRequestsCannotSupplyActor()
    {
        var assignment = new CourierAssignmentResponse(Guid.NewGuid(), "ORD-14", "assigned");
        var json = JsonSerializer.Serialize(assignment, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);

        Assert.Equal("assigned", document.RootElement.GetProperty("deliveryStatus").GetString());
        Assert.False(document.RootElement.TryGetProperty("contactNumber", out _));
        Assert.False(document.RootElement.TryGetProperty("addressLine", out _));
        Assert.Null(typeof(StockAdjustmentRequest).GetProperty("ActorId"));
        Assert.Null(typeof(FailDeliveryRequest).GetProperty("ActorId"));
    }
}
