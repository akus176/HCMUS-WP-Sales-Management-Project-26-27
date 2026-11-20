using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeShop.Contracts;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Xunit;
using Xunit.Abstractions;

namespace CoffeeShop.WinUI.E2E.Tests;

public sealed class CounterSaleTests(ITestOutputHelper output)
{
    private const string Drink = "House Espresso";
    private const string CustomerPhone = "+84000000001";

    [Fact]
    public async Task CompletedSaleUpdatesAllSixWorkspaces()
    {
        using var api = new HttpClient { BaseAddress = new Uri(Required("COFFEESHOP_API_BASE_URL")) };
        var username = Required("COFFEESHOP_E2E_USERNAME");
        var password = Required("COFFEESHOP_E2E_PASSWORD");
        using var signIn = await api.PostAsJsonAsync("auth/login", new LoginRequest(username, password));
        signIn.EnsureSuccessStatusCode();
        var token = await signIn.Content.ReadFromJsonAsync<TokenResponse>();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var menu = Assert.Single(await ReadAsync<MenuItemResponse[]>(api, $"menu-items?search={Escape(Drink)}"),
            item => item.Name == Drink);
        var size = Assert.Single(menu.SizeVariants);
        var recipe = await ReadAsync<RecipeResponse>(api, $"recipes/{size.Id}");
        var ingredient = Assert.Single(recipe.Lines);
        Assert.Equal(18m, ingredient.Quantity);
        var inventoryPath = $"ingredients/{ingredient.IngredientId}";
        var stockBefore = await ReadAsync<IngredientBalanceResponse>(api, inventoryPath);
        var customer = Assert.Single(await ReadAsync<CustomerResponse[]>(api, $"customers?search={Escape(CustomerPhone)}"),
            profile => profile.Phone == CustomerPhone);
        var loyalty = await ReadAsync<LoyaltySettingsResponse>(api, "loyalty-settings");
        var expectedPoints = (int)decimal.Floor(size.Price * loyalty.EarnPointsPerCurrencyUnit);
        Assert.True(expectedPoints > 0);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        var revenuePath = $"reports/revenue?from={firstDay:yyyy-MM-dd}&to={today:yyyy-MM-dd}&timeZone={Escape(TimeZoneInfo.Local.Id)}";
        var revenueBefore = await ReadAsync<RevenueReportResponse>(api, revenuePath);
        var activeOrdersBefore = (await ReadAsync<OperationalOrderResponse[]>(api, "operations/orders"))
            .Select(order => order.Id).ToHashSet();

        var exe = Required("COFFEESHOP_E2E_EXE");
        using var app = Application.Launch(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)! });
        using var automation = new UIA3Automation();
        try
        {
            output.WriteLine("Signing into the Windows app.");
            var login = WaitFor("sign-in window", () =>
                app.GetAllTopLevelWindows(automation).FirstOrDefault(window => FindId(window, "UsernameBox") is not null));
            TypeText(Find(login, "UsernameBox"), username);
            TypeText(Find(login, "PasswordBox"), password);
            Press(login, "SignInButton");
            var main = WaitFor("service console after sign-in", () =>
                app.GetAllTopLevelWindows(automation).FirstOrDefault(window => FindId(window, "SignOutButton") is not null));
            main.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);

            output.WriteLine("Checking the menu recipe, stock, loyalty and reports before the sale.");
            Navigate(main, "Menu");
            Search(main, Drink);
            ClickRow(main, "DrinksList", Drink);
            ExpectText(main, "RecipeSummary", $"{ingredient.Quantity:0.###} {ingredient.Unit} · {stockBefore.Name}");
            ExpectName(Find(main, "VariantsList"), size.Price.ToString("0.00"));
            ExpectStock(main, stockBefore.Name, stockBefore.AvailableQuantity);
            OpenCustomer(main, customer);
            ExpectText(main, "BalanceText", $"{customer.LoyaltyPointsBalance:N0} points");
            ExpectRevenue(main, revenueBefore.Revenue, revenueBefore.OrderCount);

            output.WriteLine("Attaching the customer and confirming a takeaway order from Counter.");
            Navigate(main, "Counter");
            Search(main, Drink);
            var suggestBox = Find(main, "CustomerSearchBox");
            var customerInput = WaitFor("Counter customer search input",
                () => suggestBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit)));
            TypeText(customerInput, customer.Phone);
            WaitFor($"customer suggestion for {customer.FullName}", () =>
                FindName(main, customer.FullName) ?? FindName(automation.GetDesktop(), customer.FullName));
            Keyboard.Press(VirtualKeyShort.DOWN);
            Keyboard.Press(VirtualKeyShort.ENTER);
            WaitFor("attached customer name", () => customerInput.AsTextBox().Text == customer.FullName ? customerInput : null);
            ClickRow(main, "MenuGrid", Drink);
            PressNamed(main, "Add to ticket");
            Find(main, "TakeawayTab").Patterns.SelectionItem.Pattern.Select();
            ExpectText(main, "TicketTotalText", size.Price.ToString("0.00"));
            Press(main, "SaveButton");
            var ticketNumber = WaitFor("saved ticket number", () =>
                FindId(main, "TicketKickerText") is { Name: var number }
                && number.StartsWith("POS-", StringComparison.Ordinal) ? number : null);
            ExpectText(main, "CustomerText", customer.FullName);
            ExpectText(main, "TicketTotalText", size.Price.ToString("0.00"));
            Press(main, "ConfirmButton");
            PressNamed(main, "Confirm order", excludeId: "ConfirmButton");

            output.WriteLine("Checking the same customer, drink and total on Service board, then fulfilling the ticket.");
            ExpectText(main, "SelectedKicker", $"TICKET {ticketNumber}");
            var created = Assert.Single(await ReadAsync<OperationalOrderResponse[]>(api, "operations/orders"),
                order => !activeOrdersBefore.Contains(order.Id));
            var orderPath = $"orders/{created.Id}";
            var confirmed = await ReadAsync<OrderResponse>(api, orderPath);
            Assert.Equal(customer.Id, confirmed.CustomerId);
            Assert.Equal("takeaway", confirmed.Channel);
            Assert.Equal("confirmed", confirmed.Status);
            var line = Assert.Single(confirmed.Lines);
            Assert.Equal(Drink, line.ItemName);
            Assert.Equal(size.SizeName, line.SizeName);
            Assert.Equal(1, line.Quantity);
            Assert.Equal(size.Price, confirmed.Total);
            Assert.Equal(ticketNumber, confirmed.OrderNumber);
            ExpectText(main, "SelectedCustomer", customer.FullName);
            ExpectText(main, "TotalText", size.Price.ToString("0.00"));
            ExpectName(Find(main, "OrderLinesList"), Drink);
            ExpectText(main, "SelectedState", "Confirmed · Takeaway");
            Assert.Equal(stockBefore.AvailableQuantity - ingredient.Quantity,
                (await ReadAsync<IngredientBalanceResponse>(api, inventoryPath)).AvailableQuantity);
            Press(main, "PrepareButton");
            ExpectText(main, "SelectedState", "Preparing · Takeaway");
            Press(main, "ReadyButton");
            ExpectText(main, "SelectedState", "Ready · Takeaway");
            Press(main, "ReviewPaymentButton");
            Press(main, "PayButton");
            ExpectText(main, "BalanceText", $"Paid {size.Price:0.00} · Due {0m:0.00}");
            Press(main, "CompleteButton");
            WaitFor("completed ticket removed from the active board", () =>
                FindId(main, "SelectedKicker") is { } kicker && kicker.Name != $"TICKET {confirmed.OrderNumber}" ? kicker : null);
            Assert.Equal("completed", (await ReadAsync<OrderResponse>(api, orderPath)).Status);
            Assert.DoesNotContain(await ReadAsync<OperationalOrderResponse[]>(api, "operations/orders"), order => order.Id == created.Id);

            output.WriteLine("Reconciling stock, customer loyalty and recognized revenue after completion.");
            ExpectStock(main, stockBefore.Name, stockBefore.AvailableQuantity - ingredient.Quantity);
            var stockMovements = await ReadAsync<StockMovementResponse[]>(api, $"{inventoryPath}/movements");
            var used = Assert.Single(stockMovements, movement => movement.OrderId == created.Id);
            Assert.Equal(-ingredient.Quantity, used.QuantityDelta);
            OpenCustomer(main, customer);
            ExpectText(main, "BalanceText", $"{customer.LoyaltyPointsBalance + expectedPoints:N0} points");
            var orderRow = WaitFor("completed order in customer history", () =>
                Find(main, "OrdersList").FindAllDescendants().FirstOrDefault(element =>
                    element.Name.StartsWith($"{confirmed.OrderNumber}, completed,", StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(size.Price.ToString("N2"), orderRow.Name, StringComparison.Ordinal);
            WaitFor("customer ledger entry linked to the order", () =>
                Find(main, "HistoryList").FindAllDescendants().FirstOrDefault(element =>
                    element.Name.Contains($"order {created.Id}", StringComparison.OrdinalIgnoreCase)));
            var history = await ReadAsync<LoyaltyHistoryResponse>(api, $"customers/{customer.Id}/loyalty-transactions");
            var earned = Assert.Single(history.Items, entry => entry.OrderId == created.Id);
            Assert.Equal(expectedPoints, earned.Points);
            Assert.Equal(customer.LoyaltyPointsBalance + expectedPoints, earned.BalanceAfter);

            ExpectRevenue(main, revenueBefore.Revenue + size.Price, revenueBefore.OrderCount + 1);
            var revenueAfter = await ReadAsync<RevenueReportResponse>(api, revenuePath);
            Assert.Equal(revenueBefore.Revenue + size.Price, revenueAfter.Revenue);
            Assert.Equal(revenueBefore.OrderCount + 1, revenueAfter.OrderCount);
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

    private void WriteDesktopDiagnostics(Application app, UIA3Automation automation)
    {
        try
        {
            using var runner = Process.GetCurrentProcess();
            output.WriteLine($"Runner session {runner.SessionId}, interactive: {Environment.UserInteractive}, app exited: {app.HasExited}.");
            foreach (var window in app.GetAllTopLevelWindows(automation))
            {
                output.WriteLine($"Window: {window.Name} ({window.BoundingRectangle})");
                foreach (var element in window.FindAllDescendants().Take(200))
                {
                    var kind = element.Properties.ControlType.ValueOrDefault;
                    var label = kind == ControlType.Edit ? "" : element.Properties.Name.ValueOrDefault;
                    var id = element.Properties.AutomationId.ValueOrDefault;
                    if (!string.IsNullOrEmpty(label) || !string.IsNullOrEmpty(id))
                        output.WriteLine($"{kind} {id} '{label}'");
                }
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"Desktop diagnostics unavailable: {ex.Message}");
        }
    }

    private static void ExpectRevenue(Window window, decimal revenue, int count)
    {
        Navigate(window, "Reports");
        ExpectText(window, "RevenueTotal", revenue.ToString("N2"));
        ExpectText(window, "RevenueOrderCount", count.ToString("N0"));
    }

    private static void ExpectStock(Window window, string ingredient, decimal balance)
    {
        Navigate(window, "Stock room");
        TypeText(Find(window, "SearchBox"), ingredient);
        ClickRow(window, "IngredientsListView", ingredient);
        ExpectText(window, "DrawerIngredientTitle", ingredient);
        ExpectText(window, "DrawerBalanceValue", balance.ToString("#,0.###"));
    }

    private static void OpenCustomer(Window window, CustomerResponse customer)
    {
        Navigate(window, "Customers");
        Search(window, customer.Phone);
        ClickRow(window, "CustomersList", customer.FullName);
    }

    private static void Navigate(Window window, string workspace)
    {
        var name = $"Open {workspace} workspace";
        if (FindName(window, name) is not { IsOffscreen: false }
            && FindId(window, "MenuToggle") is { IsOffscreen: false } toggle)
            toggle.AsButton().Invoke();
        var item = WaitFor(name, () => FindName(window, name) is { IsEnabled: true, IsOffscreen: false } target ? target : null);
        if (item.Patterns.SelectionItem.PatternOrDefault is { } selection) selection.Select();
        else item.Click();
        ExpectText(window, "BreadcrumbText", workspace.ToUpperInvariant());
    }

    private static void Search(Window window, string query)
    {
        TypeText(Find(window, "SearchBox"), query);
        Keyboard.Press(VirtualKeyShort.ENTER);
    }

    private static void TypeText(AutomationElement input, string text)
    {
        input.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(text);
    }

    private static void ClickRow(AutomationElement window, string listId, string startsWith)
    {
        var list = WaitFor(listId, () => FindId(window, listId) ?? (listId == "DrinksList" ? FindName(window, "Drinks and recipes") : null));
        var item = WaitFor($"{startsWith} in {listId}", () => list.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
            .FirstOrDefault(row => row.IsEnabled
                && row.Properties.Name.ValueOrDefault?.StartsWith(startsWith, StringComparison.Ordinal) == true));
        if (item.Patterns.Invoke.PatternOrDefault is { } invoke) invoke.Invoke();
        else item.Patterns.SelectionItem.Pattern.Select();
    }

    private static void Press(AutomationElement root, string automationId) =>
        WaitFor($"enabled {automationId}", () => FindId(root, automationId) is { IsEnabled: true } button
            ? button : null).AsButton().Invoke();

    private static void PressNamed(AutomationElement root, string name, string? excludeId = null) =>
        WaitFor($"enabled {name} button", () =>
            root.FindAllDescendants(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)))
                .FirstOrDefault(button => (excludeId is null || button.Properties.AutomationId.ValueOrDefault != excludeId)
                    && button.IsEnabled && !button.IsOffscreen))
            .AsButton().Invoke();

    private static void ExpectText(AutomationElement root, string id, string expected)
    {
        string? actual = null;
        WaitFor($"{id} to show '{expected}'", () =>
        {
            var element = FindId(root, id);
            actual = element?.Properties.Name.ValueOrDefault;
            return actual == expected ? element : null;
        }, () => $" Last value: '{actual}'.");
    }

    private static void ExpectName(AutomationElement root, string name) =>
        WaitFor($"'{name}' inside {root.AutomationId}", () => FindName(root, name));

    private static AutomationElement Find(AutomationElement root, string id) =>
        WaitFor(id, () => FindId(root, id));

    private static AutomationElement? FindId(AutomationElement root, string id) =>
        root.FindFirstDescendant(cf => cf.ByAutomationId(id));

    private static AutomationElement? FindName(AutomationElement root, string name) =>
        root.FindFirstDescendant(cf => cf.ByName(name));

    private static T WaitFor<T>(string description, Func<T?> find, Func<string>? details = null) where T : class
    {
        var timer = Stopwatch.StartNew();
        do
        {
            if (find() is { } result) return result;
            Thread.Sleep(150);
        } while (timer.Elapsed < TimeSpan.FromSeconds(25));
        throw new TimeoutException($"Timed out waiting for {description}.{details?.Invoke()}");
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, string path) =>
        await client.GetFromJsonAsync<T>(path) ?? throw new InvalidOperationException($"Empty response from {path}.");

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} before running Windows E2E.");
}
