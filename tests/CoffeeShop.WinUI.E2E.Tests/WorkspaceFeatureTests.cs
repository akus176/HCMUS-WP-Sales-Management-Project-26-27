using CoffeeShop.Contracts;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;

namespace CoffeeShop.WinUI.E2E.Tests;

public sealed partial class CounterSaleTests
{
    [Fact]
    public async Task CustomerProfileAndDefaultAddressPersistThroughDeactivation()
    {
        const string name = "E2E Morgan Tran";
        const string phone = "+84000000717";
        const string email = "morgan-e2e@example.test";
        const string updatedEmail = "morgan-orders-e2e@example.test";
        const string address = "42 Demo Courtyard, District 3";
        const string label = "Office";
        var searchPath = $"customers?search={Escape(phone)}";

        await RunWorkspaceAsync(async (main, _, api) =>
        {
            Navigate(main, "Customers");
            Press(main, "NewCustomerButton");
            TypeText(NamedControl(main, "Full name", ControlType.Edit), name);
            TypeText(NamedControl(main, "Phone", ControlType.Edit), phone);
            TypeText(NamedControl(main, "Email (optional)", ControlType.Edit), email);
            SaveDialog(main, "Save");
            ExpectText(main, "DetailHeading", name);
            var profile = Assert.Single(await ReadAsync<CustomerResponse[]>(api, searchPath));
            Assert.Equal(name, profile.FullName);
            Assert.Equal(email, profile.Email);
            Assert.Equal(0, profile.LoyaltyPointsBalance);

            Press(main, "EditCustomerButton");
            TypeText(NamedControl(main, "Email (optional)", ControlType.Edit), updatedEmail);
            SaveDialog(main, "Save");
            ExpectText(main, "ContactDetails", $"{phone}\n{updatedEmail}");
            Press(main, "AddAddressButton");
            TypeText(NamedControl(main, "Label (optional)", ControlType.Edit), label);
            TypeText(NamedControl(main, "Address", ControlType.Edit), address);
            NamedControl(main, "Default delivery address", ControlType.CheckBox).Patterns.Toggle.Pattern.Toggle();
            SaveDialog(main, "Save");
            WaitFor("default address on the customer profile", () => FindRow(main, "AddressesList", $"{label}, {address}, default"));

            Press(main, "DeactivateCustomerButton");
            SaveDialog(main, "Deactivate");
            Assert.Empty(await ReadAsync<CustomerResponse[]>(api, searchPath));
            Find(main, "IncludeInactive").Patterns.Toggle.Pattern.Toggle();
            Search(main, phone);
            WaitFor("inactive customer in the directory", () => FindRow(main, "CustomersList", $"{name}, {phone}, inactive"));
            ClickRow(main, "CustomersList", name);
            WaitFor("preserved address on the inactive customer", () => FindRow(main, "AddressesList", $"{label}, {address}, default"));

            var stored = await ReadAsync<CustomerDetailResponse>(api, $"customers/{profile.Id}");
            Assert.False(stored.IsActive);
            Assert.Equal(updatedEmail, stored.Email);
            var savedAddress = Assert.Single(stored.Addresses);
            Assert.Equal(address, savedAddress.AddressLine);
            Assert.True(savedAddress.IsDefault);
        });
    }

    [Fact]
    public async Task SupplierDeliveryPostsReceiptAndUpdatesIngredientOnce()
    {
        const string ingredientName = "E2E Citrus concentrate";
        const string supplierName = "Hearthline Roastery";
        const string receiptNumber = "E2E-CITRUS-RECEIPT";
        const decimal quantity = 250.5m;
        const decimal unitCost = 120m;

        await RunWorkspaceAsync(async (main, automation, api) =>
        {
            Navigate(main, "Stock room");
            Press(main, "NewIngredientButton");
            TypeText(Find(main, "NameBox"), ingredientName);
            SelectOption(main, automation, Find(main, "UnitBox"), "Milliliters (ml)");
            TypeText(Find(main, "ThresholdBox"), "50");
            SaveDialog(main, "Create");
            TypeText(Find(main, "SearchBox"), ingredientName);
            WaitFor("new ingredient with zero starting stock", () =>
                FindRow(main, "IngredientsListView", $"{ingredientName}. 0 ml on hand."));
            var inventory = await ReadAsync<PageResponse<IngredientBalanceResponse>>(api, $"ingredients?search={Escape(ingredientName)}");
            var ingredient = Assert.Single(inventory.Items, item => item.Name == ingredientName);
            Assert.Equal(50m, ingredient.ReorderThreshold);

            Press(main, "ReceiveDeliveryButton");
            SelectOption(main, automation, Find(main, "SupplierBox"), supplierName);
            TypeText(Find(main, "ReceiptNumberBox"), receiptNumber);
            SelectOption(main, automation, "Ingredient", ingredientName);
            TypeText(NamedControl(main, "Qty (ml)", ControlType.Edit), quantity.ToString("0.###"));
            TypeText(NamedControl(main, "Unit cost", ControlType.Edit), unitCost.ToString("0.00"));
            SaveDialog(main, "Receive into stock");
            WaitFor("received balance in the inventory table", () =>
                FindRow(main, "IngredientsListView", $"{ingredientName}. {quantity:#,0.###} ml on hand."));

            ExpectText(main, "StatusMessageText", $"Done: Receipt #{receiptNumber} posted. Stock balances are up to date.");
            Find(main, "TabReceipts").Click();
            ExpectName(Find(main, "ReceiptsListView"), $"Receipt #{receiptNumber}");
            var suppliers = await ReadAsync<SupplierResponse[]>(api, "suppliers");
            var supplier = Assert.Single(suppliers, item => item.Name == supplierName);
            var receipts = await ReadAsync<ReceiptResponse[]>(api, "purchase-receipts");
            var receipt = Assert.Single(receipts, item => item.ReceiptNumber == receiptNumber);
            Assert.Equal(supplier.Id, receipt.SupplierId);
            Assert.Equal("posted", receipt.Status);
            var line = Assert.Single(receipt.Lines);
            Assert.Equal(ingredient.IngredientId, line.IngredientId);
            Assert.Equal(quantity, line.Quantity);
            Assert.Equal(unitCost, line.UnitCost);
            var inventoryPath = $"ingredients/{ingredient.IngredientId}";
            Assert.Equal(quantity, (await ReadAsync<IngredientBalanceResponse>(api, inventoryPath)).AvailableQuantity);
            var movements = await ReadAsync<StockMovementResponse[]>(api, $"{inventoryPath}/movements");
            var movement = Assert.Single(movements, item => item.PurchaseReceiptId == receipt.Id);
            Assert.Equal(quantity, movement.QuantityDelta);
        });
    }

    [Fact]
    public async Task StockRoomThresholdAndLifecycleActionsRefreshTheNativeWorkspace()
    {
        const string ingredientName = "E2E Threshold Cocoa";
        const string supplierName = "E2E Stock Supplier";
        const string supplierPhone = "0281234599";
        const string updatedPhone = "0281234588";

        await RunWorkspaceAsync(async (main, automation, api) =>
        {
            Navigate(main, "Stock room");
            Press(main, "NewIngredientButton");
            TypeText(Find(main, "NameBox"), ingredientName);
            SelectOption(main, automation, Find(main, "UnitBox"), "Grams (g)");
            TypeText(Find(main, "BalanceBox"), "10");
            TypeText(Find(main, "ThresholdBox"), "5");
            SaveDialog(main, "Create");
            TypeText(Find(main, "SearchBox"), ingredientName);
            var row = WaitFor("new ingredient row", () =>
                FindRow(main, "IngredientsListView", $"{ingredientName}. 10 g on hand."));
            row.Click();
            Press(main, "EditIngredientButton");
            TypeText(Find(main, "ThresholdBox"), "15");
            SaveDialog(main, "Save");
            ExpectText(main, "DrawerStatusText", "LOW STOCK");
            var ingredient = Assert.Single((await ReadAsync<PageResponse<IngredientBalanceResponse>>(
                api, $"ingredients?search={Escape(ingredientName)}")).Items);
            Assert.Equal(10m, ingredient.AvailableQuantity);
            Assert.Equal(15m, ingredient.ReorderThreshold);

            Press(main, "ToggleIngredientButton");
            SaveDialog(main, "Deactivate");
            ExpectText(main, "DrawerStatusText", "INACTIVE");
            Assert.False((await ReadAsync<IngredientBalanceResponse>(api,
                $"ingredients/{ingredient.IngredientId}")).IsActive);
            Press(main, "ToggleIngredientButton");
            SaveDialog(main, "Activate");
            ExpectText(main, "DrawerStatusText", "LOW STOCK");
            Assert.True((await ReadAsync<IngredientBalanceResponse>(api,
                $"ingredients/{ingredient.IngredientId}")).IsActive);

            Find(main, "TabSuppliers").Click();
            Press(main, "NewSupplierButton");
            TypeText(Find(main, "NameBox"), supplierName);
            TypeText(Find(main, "PhoneBox"), supplierPhone);
            SaveDialog(main, "Create");
            var supplier = Assert.Single(await ReadAsync<SupplierResponse[]>(api, "suppliers"),
                item => item.Name == supplierName);
            PressNamed(main, $"Deactivate supplier {supplierName}");
            SaveDialog(main, "Deactivate");
            PressNamed(main, $"Activate supplier {supplierName}");
            Assert.False(Assert.Single(await ReadAsync<SupplierResponse[]>(api, "suppliers"),
                item => item.Id == supplier.Id).IsActive);
            SaveDialog(main, "Activate");
            Assert.True(Assert.Single(await ReadAsync<SupplierResponse[]>(api, "suppliers"),
                item => item.Id == supplier.Id).IsActive);
            PressNamed(main, $"Edit supplier {supplierName}");
            TypeText(Find(main, "PhoneBox"), updatedPhone);
            SaveDialog(main, "Save");
            Assert.Equal(updatedPhone, Assert.Single(await ReadAsync<SupplierResponse[]>(api, "suppliers"),
                item => item.Id == supplier.Id).Phone);
        });
    }

    [Fact]
    public async Task CancellingConfirmedTicketRestoresStockAndRedeemedPoints()
    {
        const string reason = "Guest cancelled before preparation.";
        const int points = 2;
        await RunWorkspaceAsync(async (main, automation, api) =>
        {
            var product = Assert.Single(await ReadAsync<MenuItemResponse[]>(api, $"menu-items?search={Escape(Drink)}"),
                item => item.Name == Drink);
            var variant = Assert.Single(product.SizeVariants);
            var recipe = await ReadAsync<RecipeResponse>(api, $"recipes/{variant.Id}");
            var ingredient = Assert.Single(recipe.Lines);
            var inventoryPath = $"ingredients/{ingredient.IngredientId}";
            var stock = await ReadAsync<IngredientBalanceResponse>(api, inventoryPath);
            var revenuePath = $"reports/revenue?{CurrentPeriodQuery()}";
            var revenueBefore = await ReadAsync<RevenueReportResponse>(api, revenuePath);
            var customer = Assert.Single(await ReadAsync<CustomerResponse[]>(api, $"customers?search={Escape(CustomerPhone)}"),
                profile => profile.Phone == CustomerPhone);
            var customerPath = $"customers/{customer.Id}";
            var loyalty = await ReadAsync<LoyaltySettingsResponse>(api, "loyalty-settings");

            Navigate(main, "Counter");
            Search(main, Drink);
            AttachCustomer(main, automation, customer);
            ClickRow(main, "MenuGrid", Drink);
            PressNamed(main, "Add to ticket");
            Find(main, "TakeawayTab").Patterns.SelectionItem.Pattern.Select();
            Press(main, "SaveButton");
            var number = WaitFor("saved ticket number", () =>
                FindId(main, "TicketKickerText") is { Name: var ticket } && ticket.StartsWith("POS-", StringComparison.Ordinal)
                    ? ticket : null);
            Press(main, "LoyaltyButton");
            TypeText(Find(main, "LoyaltyPointsBox"), points.ToString());
            PressNamed(main, "Apply points");
            var payable = variant.Price - points * loyalty.RedeemValuePerPoint;
            ExpectText(main, "TicketTotalText", payable.ToString("0.00"));
            Assert.Equal(customer.LoyaltyPointsBalance - points,
                (await ReadAsync<CustomerDetailResponse>(api, customerPath)).LoyaltyPointsBalance);
            Press(main, "ConfirmButton");
            PressNamed(main, "Confirm order", excludeId: "ConfirmButton");
            ExpectText(main, "SelectedKicker", $"TICKET {number}");
            var active = await ReadAsync<OperationalOrderResponse[]>(api, "operations/orders");
            var ticket = Assert.Single(active, order => order.OrderNumber == number);
            Assert.Equal(stock.AvailableQuantity - ingredient.Quantity,
                (await ReadAsync<IngredientBalanceResponse>(api, inventoryPath)).AvailableQuantity);

            Find(main, "CancelSection").Patterns.ExpandCollapse.Pattern.Expand();
            Press(main, "CancelButton");
            WaitFor("required cancellation reason", () => FindName(main, "Enter a cancellation reason under Cancel order."));
            TypeText(Find(main, "CancelReasonBox"), reason);
            Press(main, "CancelButton");
            WaitFor("order cancellation feedback", () => FindName(main, "Order cancelled. Fulfillment stopped and compensating work was requested."));
            Assert.Equal("cancelled", (await ReadAsync<OrderResponse>(api, $"orders/{ticket.Id}")).Status);
            Assert.DoesNotContain(await ReadAsync<OperationalOrderResponse[]>(api, "operations/orders"), order => order.Id == ticket.Id);
            var movements = await ReadAsync<StockMovementResponse[]>(api, $"{inventoryPath}/movements");
            var adjustments = movements.Where(movement => movement.OrderId == ticket.Id).Select(movement => movement.QuantityDelta).Order().ToArray();
            Assert.Equal(new[] { -ingredient.Quantity, ingredient.Quantity }, adjustments);
            var loyaltyHistory = await ReadAsync<LoyaltyHistoryResponse>(api, $"{customerPath}/loyalty-transactions");
            var pointsByOrder = loyaltyHistory.Items.Where(entry => entry.OrderId == ticket.Id).Select(entry => entry.Points).Order().ToArray();
            Assert.Equal(new[] { -points, points }, pointsByOrder);
            ExpectStock(main, stock, stock.AvailableQuantity);
            OpenCustomer(main, customer);
            ExpectText(main, "BalanceText", $"{customer.LoyaltyPointsBalance:N0} points");
            ExpectRevenue(main, revenueBefore.Revenue, revenueBefore.OrderCount);
        });
    }

    [Fact]
    public async Task ReportsFiltersKeepPeriodTotalsAndAssistantRetainsItsConversation()
    {
        const string question = "Which ingredients are low on stock and need reordering first?";
        const string source = "Reports used · Current low-stock report";
        await RunWorkspaceAsync(async (main, automation, api) =>
        {
            var report = await ReadAsync<RevenueReportResponse>(api, $"reports/revenue?{CurrentPeriodQuery()}");
            ExpectRevenue(main, report.Revenue, report.OrderCount);
            Press(main, "FiltersButton");
            SelectOption(main, automation, Find(main, "PaymentFilter"), "Cash");
            SelectOption(main, automation, Find(main, "ChannelFilter"), "Takeaway");
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            ExpectText(main, "FilterSummary", "Cash · Takeaway");
            ExpectText(main, "RevenueTotal", report.Revenue.ToString("N2"));
            ExpectText(main, "RevenueOrderCount", report.OrderCount.ToString("N0"));
            var cash = report.ByPaymentMethod.FirstOrDefault(method => method.PaymentMethod == "cash");
            if (cash is not null) ExpectText(main, "PaymentOrderCount", cash.OrderCount.ToString("N0"));
            var channels = await ReadAsync<ChannelReportResponse>(api, $"reports/channels?{CurrentPeriodQuery()}");
            var takeaway = Assert.Single(channels.ByChannel, row => row.Channel == "takeaway");
            WaitFor("takeaway order count in the breakdown", () => FindName(main, $"{takeaway.OrderCount} orders"));
            Press(main, "FiltersButton");
            Press(main, "ResetFiltersButton");
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            ExpectText(main, "FilterSummary", "All payment methods · All channels");

            Press(main, "AssistantLauncherButton");
            TypeText(Find(main, "AssistantQuestionBox"), question);
            Press(main, "AssistantAskButton");
            WaitFor("answer sourced from current low stock", () => FindName(main, source));
            WaitFor("question preserved in the transcript", () => FindName(main, question));
            PressNamed(main, "Minimize assistant chat");
            Press(main, "AssistantLauncherButton");
            WaitFor("answer still in the transcript after reopening", () => FindName(main, source));
        });
    }

    private async Task RunWorkspaceAsync(Func<Window, UIA3Automation, HttpClient, Task> scenario)
    {
        using var api = await SignInToApiAsync();
        using var app = LaunchApp();
        using var automation = new UIA3Automation();
        try
        {
            await scenario(OpenConsole(app, automation), automation, api);
        }
        catch
        {
            WriteDesktopDiagnostics(app, automation);
            throw;
        }
        finally
        {
            if (!app.HasExited) app.Kill();
        }
    }

    private static void SaveDialog(Window window, string action)
    {
        PressNamed(window, action);
        WaitFor($"dialog to close after {action}", () =>
            window.FindFirstDescendant(cf => cf.ByName(action).And(cf.ByControlType(ControlType.Button))) is null ? window : null);
    }

    private static string CurrentPeriodQuery()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var first = new DateOnly(today.Year, today.Month, 1);
        return $"from={first:yyyy-MM-dd}&to={today:yyyy-MM-dd}&timeZone={Escape(TimeZoneInfo.Local.Id)}";
    }
}
