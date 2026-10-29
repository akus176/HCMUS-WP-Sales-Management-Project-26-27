using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class CatalogPage : Page
{
    internal object VerificationState() => new { activeTab, search = SearchBox.Text, inactive = IncludeInactive.IsChecked, categoryFilterId, selected = model.SelectedItem?.Id };

    internal async Task<object> PrepareVerificationAsync()
    {
        while (model.IsBusy) await Task.Delay(50);
        await model.LoadAsync();
        if (model.Items.Count > 0) await model.SelectAsync(model.Items[0].Id);
        var search = model.Search;
        model.Search = SearchBox.Text = "__verification_no_matching_drink__";
        await model.LoadAsync();
        var emptySearchClearsDetail = model.SelectedItem is null && EmptyMessage.Visibility == Visibility.Visible;
        model.Search = SearchBox.Text = search;
        await model.LoadAsync();
        if (model.Items.Count > 0) await model.SelectAsync(model.Items[0].Id);
        return new { items = model.Items.Count, categories = model.Categories.Count, selected = model.SelectedItem?.Name, emptySearchClearsDetail, error = model.Error?.Code };
    }

    private readonly CatalogWorkspace model;
    private bool rendering;
    private string activeTab = "drinks";
    private IReadOnlyList<MenuItemSummary>? renderedItems;
    private IReadOnlyList<CategorySummary>? renderedCategories;
    private EntityId? categoryFilterId;
    private Func<ContentDialog, FrameworkElement, Task>? editorVerification;

    internal async Task<object> VerifyEditorAsync(Func<int, Task> resize)
    {
        object result = new { error = "Editor did not open" };
        var savedName = model.SelectedItem!.Name;
        editorVerification = async (dialog, fields) =>
        {
            var inputs = PresentationVerification.Elements(fields).OfType<TextBox>().ToArray();
            inputs[0].Text = savedName + " unsaved draft";
            var draft = inputs[0].Text;
            await resize(600);
            var compactPreserved = inputs[0].Text == draft;
            var compactTargets = PresentationVerification.ButtonTargets(dialog);
            await resize(1440);
            result = new { compactPreserved, expandedPreserved = inputs[0].Text == draft, savedRecordUnchanged = model.SelectedItem.Name == savedName, compactTargets, expandedTargets = PresentationVerification.ButtonTargets(dialog) };
            dialog.Hide();
        };
        try { await EditDrinkAsync(model.SelectedItem); }
        finally { editorVerification = null; }
        return result;
    }
    public sealed record CategoryOption(EntityId? Id, string Name);
    public sealed record CatalogRow(object Record, string Name, string StateDisplay);
    public sealed record DrinkRow(MenuItemSummary Item)
    {
        public string Name => Item.Name;
        public string CategoryName => $"{Item.CategoryName} · {Item.SizeVariants.Count} sizes";
        public string StateDisplay => Item.IsActive ? "ACTIVE" : "INACTIVE";
        public string PriceDisplay => Item.SizeVariants.Count == 0 ? "No sizes" : $"from {Item.SizeVariants.Min(size => size.Price):N2}";
    }

    public CatalogPage()
    {
        InitializeComponent();
        model = new CatalogWorkspace(App.Services.GetRequiredService<IServiceScopeFactory>());
        model.PropertyChanged += (_, _) => Render();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args) => await model.LoadAsync();

    private async void OnReload(object sender, RoutedEventArgs args) => await model.LoadAsync();

    private void OnInactiveChanged(object sender, RoutedEventArgs args)
    {
        if (model is not null && !rendering) OnSearch(sender, args);
    }

    private async void OnSearch(object sender, RoutedEventArgs args)
    {
        model.Search = SearchBox.Text;
        model.IncludeInactive = IncludeInactive.IsChecked == true;
        await model.LoadAsync();
    }

    private async void OnDrinkSelected(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering && DrinksList.SelectedItem is DrinkRow { Item: var item })
        {
            await model.SelectAsync(item.Id);
        }
    }

    private void OnDrinkContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is DrinkRow row)
        {
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(args.ItemContainer,
                $"{row.Name}, {row.CategoryName}, {row.PriceDisplay}, {row.StateDisplay}");
            PaintDrinkSelection(args.ItemContainer as GridViewItem, row.Item.Id == model.SelectedItem?.Id);
            if (args.Phase == 0 && !args.InRecycleQueue) args.RegisterUpdateCallback(OnDrinkContainerContentChanging);
        }
    }

    private static void PaintDrinkSelection(GridViewItem? container, bool selected)
    {
        if (container?.ContentTemplateRoot is not Border card) return;
        card.BorderBrush = (Brush)ResourcesLookup(selected ? "ConsoleAccentBrush" : "ConsoleRuleBrush");
        card.BorderThickness = new Thickness(0, selected ? 3 : 1, 0, 0);
        if (card.Child is Grid grid && grid.Children[0] is Border art)
            art.BorderBrush = card.BorderBrush;
    }

    private void Render()
    {
        rendering = true;
        try
        {
            LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
            WorkspaceContent.IsEnabled = !model.IsBusy;
            DrinksList.IsEnabled = CategoriesList.IsEnabled = AddonsList.IsEnabled = !model.IsBusy;
            ReloadButton.IsEnabled = NewButton.IsEnabled = !model.IsBusy;
            Status.Apply(model.Feedback);
            if (model.Feedback.Kind == WorkflowFeedbackKind.Ready) Status.IsOpen = false;
            CatalogCount.Text = $"MANAGE / CATALOG / {model.Items.Count} drinks · {model.Categories.Count} categories";
            CatalogHealth.Text = $"{model.Items.Count(item => item.IsActive)} active in results\n{model.Items.Count(item => !item.IsActive)} inactive in results";
            if (!ReferenceEquals(renderedCategories, model.Categories))
            {
                renderedCategories = model.Categories;
                var options = new[] { new CategoryOption(null, "All drinks") }.Concat(model.Categories.Select(item => new CategoryOption(item.Id, item.Name))).ToArray();
                CategoryFilter.ItemsSource = options;
                CategoryFilter.SelectedItem = options.FirstOrDefault(item => item.Id == categoryFilterId) ?? options[0];
                CompactCategoryFilter.ItemsSource = options;
                CompactCategoryFilter.SelectedItem = CategoryFilter.SelectedItem;
                var selectedId = (CategoriesList.SelectedItem as CatalogRow)?.Record is CategorySummary category ? category.Id : null;
                CategoriesList.ItemsSource = model.Categories.Select(item => new CatalogRow(item, item.Name, item.IsActive ? "Active" : "Inactive")).ToArray();
                CategoriesList.SelectedItem = ((IEnumerable<CatalogRow>)CategoriesList.ItemsSource).FirstOrDefault(row => ((CategorySummary)row.Record).Id == selectedId);
            }
            if (!ReferenceEquals(renderedItems, model.Items))
            {
                renderedItems = model.Items;
                ApplyDrinkFilter();
            }
            if (!ReferenceEquals(AddonsList.Tag, model.Addons))
            {
                var selectedId = (AddonsList.SelectedItem as CatalogRow)?.Record is AddonSummary addon ? addon.Id : null;
                AddonsList.Tag = model.Addons;
                AddonsList.ItemsSource = model.Addons.Select(item => new CatalogRow(item, item.Name, $"{item.Price:N2} · {(item.IsActive ? "Active" : "Inactive")}")).ToArray();
                AddonsList.SelectedItem = ((IEnumerable<CatalogRow>)AddonsList.ItemsSource).FirstOrDefault(row => ((AddonSummary)row.Record).Id == selectedId);
            }
            if (!model.IsBusy && DrinksList.ItemsSource is IEnumerable<DrinkRow> rows)
            {
                DrinksList.SelectedItem = rows.FirstOrDefault(row => row.Item.Id == model.SelectedItem?.Id);
                foreach (var row in rows) PaintDrinkSelection(DrinksList.ContainerFromItem(row) as GridViewItem, row.Item.Id == model.SelectedItem?.Id);
            }
            EmptyMessage.Visibility = activeTab == "drinks" && DrinksList.Items.Count == 0 && !model.IsBusy && !model.IsError ? Visibility.Visible : Visibility.Collapsed;
            CategoriesEmpty.Visibility = activeTab == "categories" && model.Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AddonsEmpty.Visibility = activeTab == "addons" && model.Addons.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CategoryLimit.Visibility = activeTab == "categories" && model.Categories.Count == 200 ? Visibility.Visible : Visibility.Collapsed;
            RenderDetail();
        }
        finally { rendering = false; }
    }
    private void ApplyDrinkFilter()
    {
        var rows = model.Items.Where(item => categoryFilterId is null || item.CategoryId == categoryFilterId).Select(item => new DrinkRow(item)).ToArray();
        DrinksList.ItemsSource = rows;
        DrinksList.SelectedItem = rows.FirstOrDefault(row => row.Item.Id == model.SelectedItem?.Id);
        EmptyMessage.Visibility = activeTab == "drinks" && rows.Length == 0 && !model.IsBusy && !model.IsError ? Visibility.Visible : Visibility.Collapsed;
    }
    private void RenderDetail()
    {
        var detail = activeTab == "drinks" ? model.SelectedItem : null;
        if (detail is not null && categoryFilterId is not null && detail.CategoryId != categoryFilterId) detail = null;
        ItemActions.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        RecordActions.Visibility = Visibility.Collapsed;
        DetailHeading.Text = detail?.Name ?? $"Select {(activeTab == "drinks" ? "a drink" : activeTab == "categories" ? "a category" : "an add-on")}";
        DetailDescription.Text = detail is null ? "Details and available actions appear here." : $"{detail.CategoryName} · {(detail.IsActive ? "Active" : "Inactive")}\n{detail.Description}";
        DetailArt.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        DetailMeta.Text = detail is null ? "" : $"{detail.CategoryName} · {detail.SizeVariants.Count} sizes · {(detail.IsActive ? "Active" : "Inactive")}";
        if (detail is not null) DetailDescription.Text = detail.Description ?? "";
        VariantsList.ItemsSource = detail?.SizeVariants;
        var recipeVariant = detail?.SizeVariants.FirstOrDefault(size => size.IsActive) ?? detail?.SizeVariants.FirstOrDefault();
        RecipeHeading.Text = recipeVariant is null ? "Recipe" : $"Recipe · {recipeVariant.SizeName}";
        RecipeButton.Tag = recipeVariant;
        RecipeButton.IsEnabled = recipeVariant is not null;
        RecipeSummary.Text = recipeVariant is null || recipeVariant.Recipe.Count == 0 ? "No recipe lines. Add ingredients to deduct stock per cup."
            : string.Join(" · ", recipeVariant.Recipe.Select(line => $"{line.Quantity:0.###} {line.Unit} {line.IngredientName}"));
        DeactivateDrinkButton.IsEnabled = detail?.IsActive == true;
        CompatibilityText.Text = detail is null || detail.CompatibleAddons.Count == 0 ? "No compatible add-ons selected."
            : string.Join(", ", detail.CompatibleAddons.Select(addon => $"{addon.Name} +{addon.Price:N2}{(addon.IsActive ? "" : " (inactive)")}"));
        var record = activeTab == "categories" ? CategoriesList.SelectedItem as CatalogRow : activeTab == "addons" ? AddonsList.SelectedItem as CatalogRow : null;
        if (record is null) return;
        DetailHeading.Text = record.Name;
        DetailDescription.Text = activeTab == "categories" ? "Category availability controls drinks on new orders." : "Each compatible add-on can be chosen once per drink.";
        RecordState.Text = record.StateDisplay;
        EditRecordButton.Tag = DeactivateRecordButton.Tag = record.Record;
        DeactivateRecordButton.IsEnabled = record.Record switch { CategorySummary category => category.IsActive, AddonSummary addon => addon.IsActive, _ => false };
        RecordActions.Visibility = Visibility.Visible;
    }
    private void OnRecordSelected(object sender, SelectionChangedEventArgs args) { if (!rendering) RenderDetail(); }
    private void OnTab(object sender, RoutedEventArgs args)
    {
        activeTab = (string)((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)sender).Tag;
        DrinksTab.IsChecked = activeTab == "drinks";
        CategoriesTab.IsChecked = activeTab == "categories";
        AddonsTab.IsChecked = activeTab == "addons";
        DrinksList.Visibility = CategoryRail.Visibility = SearchBox.Visibility = SearchControls.Visibility = activeTab == "drinks" ? Visibility.Visible : Visibility.Collapsed;
        CategoriesList.Visibility = activeTab == "categories" ? Visibility.Visible : Visibility.Collapsed;
        AddonsList.Visibility = activeTab == "addons" ? Visibility.Visible : Visibility.Collapsed;
        NewButton.Content = activeTab == "drinks" ? "+ New drink" : activeTab == "categories" ? "+ New category" : "+ New add-on";
        ConfigureLayout(); Render();
    }
    private async void OnNewRecord(object sender, RoutedEventArgs args)
    {
        if (activeTab == "drinks") await EditDrinkAsync(null);
        else if (activeTab == "categories") await EditCategoryAsync(null);
        else await EditAddonAsync(null);
    }
    private async void OnEditRecord(object sender, RoutedEventArgs args)
    {
        if (((Button)sender).Tag is CategorySummary category) await EditCategoryAsync(category);
        else if (((Button)sender).Tag is AddonSummary addon) await EditAddonAsync(addon);
    }
    private void OnDeactivateRecord(object sender, RoutedEventArgs args)
    {
        if (((Button)sender).Tag is CategorySummary) OnDeactivateCategory(sender, args);
        else OnDeactivateAddon(sender, args);
    }
    private void OnCategoryFilter(object sender, SelectionChangedEventArgs args)
    {
        if (rendering) return;
        categoryFilterId = (((Microsoft.UI.Xaml.Controls.Primitives.Selector)sender).SelectedItem as CategoryOption)?.Id;
        rendering = true;
        CategoryFilter.SelectedItem = CompactCategoryFilter.SelectedItem = ((IEnumerable<CategoryOption>)CategoryFilter.ItemsSource).First(option => option.Id == categoryFilterId);
        ApplyDrinkFilter();
        rendering = false;
        RenderDetail();
    }
    private void OnSearchKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter) { args.Handled = true; OnSearch(sender, new RoutedEventArgs()); }
    }
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => ConfigureLayout();
    private void ConfigureLayout()
    {
        if (DetailColumn is null) return;
        var wide = ActualWidth >= 1000;
        CategoryRail.Visibility = wide && activeTab == "drinks" ? Visibility.Visible : Visibility.Collapsed;
        CompactCategoryFilter.Visibility = !wide && activeTab == "drinks" ? Visibility.Visible : Visibility.Collapsed;
        RailColumn.Width = new GridLength(wide && activeTab == "drinks" ? 155 : 0);
        DetailColumn.Width = new GridLength(wide ? 330 : 0);
        Grid.SetColumn(DetailPanel, wide ? 2 : 1);
        Grid.SetRow(DetailPanel, wide ? 0 : 1);
        HeaderOrnament.Visibility = ActualWidth >= 850 ? Visibility.Visible : Visibility.Collapsed;
        Header.ColumnDefinitions[1].Width = new GridLength(ActualWidth >= 850 ? 116 : 0);
        HeaderActions.Orientation = ActualWidth >= 700 ? Orientation.Horizontal : Orientation.Vertical;
        var available = Math.Max(160, DrinksList.ActualWidth - 20);
        if (DrinksList.ItemsPanelRoot is not ItemsWrapGrid drinkWrap) return;
        drinkWrap.ItemWidth = Math.Max(160, available / (available >= 560 ? 2 : 1));
        drinkWrap.MaximumRowsOrColumns = available >= 560 ? 2 : 1;
    }

    private async void OnNewCategory(object sender, RoutedEventArgs args) => await EditCategoryAsync(null);

    private Task EditCategoryAsync(CategorySummary? category)
    {
        var name = TextField("Category name", category?.Name);
        return ShowEditorAsync(category is null ? "New category" : "Edit category", Form(name), () =>
            model.SaveAsync(async (commands, _) => category is null
                ? await commands.CreateCategoryAsync(new CreateCategoryCommand(name.Text))
                : await commands.UpdateCategoryAsync(new UpdateCategoryCommand(category.Id, name.Text)), "Category saved."));
    }

    private async void OnDeactivateCategory(object sender, RoutedEventArgs args)
    {
        var category = (CategorySummary)((Button)sender).Tag;
        await ConfirmAsync($"Deactivate {category.Name}?", "Its drinks become unavailable for new orders. Historical records are preserved.",
            (commands, _) => commands.DeactivateCategoryAsync(new DeactivateCategoryCommand(category.Id)));
    }

    private async void OnEditDrink(object sender, RoutedEventArgs args)
    {
        if (model.SelectedItem is { } item)
        {
            await EditDrinkAsync(item);
        }
    }

    private Task EditDrinkAsync(MenuItemDetail? item)
    {
        var name = TextField("Drink name", item?.Name);
        var description = TextField("Description (optional)", item?.Description);
        description.AcceptsReturn = true;
        var category = new ComboBox
        {
            Header = "Active category",
            DisplayMemberPath = "Name",
            MinHeight = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = model.Categories.Where(candidate => candidate.IsActive).ToArray()
        };
        category.SelectedItem = model.Categories.FirstOrDefault(candidate => candidate.Id == item?.CategoryId && candidate.IsActive);
        var size = TextField("Initial size name", "Regular");
        var price = TextField("Initial size price");
        var fields = new Grid { ColumnSpacing = 16, RowSpacing = 13, MinWidth = 280 };
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        fields.ColumnDefinitions.Add(new ColumnDefinition());
        fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        fields.Children.Add(name);
        Grid.SetColumn(category, 1);
        fields.Children.Add(category);
        Grid.SetRow(description, 1);
        Grid.SetColumnSpan(description, 2);
        description.Height = 112;
        fields.Children.Add(description);
        if (item is null)
        {
            Grid.SetRow(size, 2);
            Grid.SetRow(price, 2);
            Grid.SetColumn(price, 1);
            fields.Children.Add(size);
            fields.Children.Add(price);
        }

        return ShowEditorAsync(item is null ? "New drink" : $"Edit {item.Name}", fields, async () =>
        {
            if (category.SelectedItem is not CategorySummary selected)
            {
                model.ShowValidation("Select an active category. If none is available, create one in Categories first.");
                return false;
            }

            return await model.SaveAsync(async (commands, _) => item is null
                ? await commands.CreateMenuItemAsync(new CreateMenuItemCommand(selected.Id, name.Text, description.Text,
                    [new CreateSizeVariantInput(size.Text, CatalogNumberInput.Price(price.Text))]))
                : await commands.UpdateMenuItemAsync(new UpdateMenuItemCommand(item.Id, selected.Id, name.Text, description.Text)),
                "Drink saved. Select it to manage sizes, compatibility, and recipes.");
        }, "Save drink", wide: true);
    }

    private async void OnDeactivateDrink(object sender, RoutedEventArgs args)
    {
        if (model.SelectedItem is { } item)
        {
            await ConfirmAsync($"Deactivate {item.Name}?", "This drink will no longer be selectable for new orders. Historical orders are preserved.",
                (commands, _) => commands.DeactivateMenuItemAsync(new DeactivateMenuItemCommand(item.Id)));
        }
    }

    private async void OnNewVariant(object sender, RoutedEventArgs args) => await EditVariantAsync(null);

    private async void OnEditVariant(object sender, RoutedEventArgs args) => await EditVariantAsync((SizeVariantDetail)((FrameworkElement)sender).Tag);

    private Task EditVariantAsync(SizeVariantDetail? variant)
    {
        if (model.SelectedItem is not { } item)
        {
            return Task.CompletedTask;
        }

        var name = TextField("Size name", variant?.SizeName);
        var price = TextField("Price", variant?.Price.ToString("0.00"));
        return ShowEditorAsync(variant is null ? "Add size" : "Edit size and price", Form(name, price), () =>
            model.SaveAsync(async (commands, _) => variant is null
                ? await commands.CreateSizeVariantAsync(new CreateSizeVariantCommand(item.Id, name.Text, CatalogNumberInput.Price(price.Text)))
                : await commands.UpdateSizeVariantAsync(new UpdateSizeVariantCommand(item.Id, variant.Id, name.Text, CatalogNumberInput.Price(price.Text))),
                "Size saved."));
    }

    private async void OnDeactivateVariant(object sender, RoutedEventArgs args)
    {
        var variant = (SizeVariantDetail)((FrameworkElement)sender).Tag;
        await ConfirmAsync($"Deactivate {variant.SizeName}?", "This size becomes unavailable; its recipe and historical references remain.",
            (commands, _) => commands.DeactivateSizeVariantAsync(new DeactivateSizeVariantCommand(variant.Id)));
    }

    private Task EditAddonAsync(AddonSummary? addon)
    {
        var name = TextField("Add-on name", addon?.Name);
        var price = TextField("Price per drink", addon?.Price.ToString("0.00"));
        return ShowEditorAsync(addon is null ? "New add-on" : "Edit add-on", Form(name, price), () =>
            model.SaveAsync(async (commands, _) => addon is null
                ? await commands.CreateAddonAsync(new CreateAddonCommand(name.Text, CatalogNumberInput.Price(price.Text)))
                : await commands.UpdateAddonAsync(new UpdateAddonCommand(addon.Id, name.Text, CatalogNumberInput.Price(price.Text))), "Add-on saved."));
    }

    private async void OnDeactivateAddon(object sender, RoutedEventArgs args)
    {
        var addon = (AddonSummary)((Button)sender).Tag;
        await ConfirmAsync($"Deactivate {addon.Name}?", "This topping becomes unavailable for new orders. Existing order snapshots remain unchanged.",
            (commands, _) => commands.DeactivateAddonAsync(new DeactivateAddonCommand(addon.Id)));
    }

    private async void OnCompatibility(object sender, RoutedEventArgs args)
    {
        if (model.SelectedItem is not { } item)
        {
            return;
        }

        var fields = Form(new TextBlock { Text = "Select available toppings. Each may be chosen once per drink. Uncheck all to remove compatibility.", TextWrapping = TextWrapping.Wrap });
        var choices = model.Addons.Where(addon => addon.IsActive).Select(addon => new CheckBox
        {
            Content = $"{addon.Name} ({addon.Price:0.00})",
            Tag = addon.Id,
            MinHeight = 48,
            IsChecked = item.CompatibleAddons.Any(compatible => compatible.Id == addon.Id)
        }).ToArray();
        if (choices.Length == 0)
        {
            fields.Children.Add(new TextBlock { Text = "No active add-ons. Create one in Add-ons first, or save to clear compatibility.", TextWrapping = TextWrapping.Wrap });
        }

        foreach (var choice in choices)
        {
            fields.Children.Add(choice);
        }

        await ShowEditorAsync($"Toppings for {item.Name}", fields, () => model.SaveAsync((commands, _) =>
            commands.SetMenuItemAddonsAsync(new SetMenuItemAddonsCommand(item.Id,
                choices.Where(choice => choice.IsChecked == true).Select(choice => (EntityId)choice.Tag).ToArray())), "Compatibility saved."));
    }

    private async void OnRecipe(object sender, RoutedEventArgs args)
    {
        var variant = (SizeVariantDetail)((FrameworkElement)sender).Tag;
        var fields = Form(new TextBlock { Text = "Quantities are per one drink of this size. Use compatible units (g/kg, ml/l, pcs). Changes replace this size's recipe atomically.", TextWrapping = TextWrapping.Wrap });
        var lines = new List<(ComboBox Ingredient, TextBox Quantity, TextBox Unit)>();

        void AddLine(RecipeLineDetail? existing = null)
        {
            var options = model.Ingredients.ToList();
            if (existing is not null && options.All(option => option.Id != existing.IngredientId))
            {
                options.Add(new RecipeIngredientOption(existing.IngredientId, $"{existing.IngredientName} (unavailable - replace)", existing.Unit));
            }

            var ingredient = new ComboBox { Header = "Ingredient", DisplayMemberPath = "Name", ItemsSource = options, MinHeight = 48 };
            ingredient.SelectedItem = options.FirstOrDefault(option => option.Id == existing?.IngredientId);
            var quantity = TextField("Quantity", existing?.Quantity.ToString("0.###"));
            var unit = TextField("Unit", existing?.Unit);
            ingredient.SelectionChanged += (_, _) =>
            {
                if (ingredient.SelectedItem is RecipeIngredientOption option)
                {
                    unit.Text = option.BaseUnit;
                }
            };
            var remove = new Button { Content = "Remove this ingredient", MinHeight = 48 };
            var row = Form(ingredient, quantity, unit, remove);
            row.Margin = new Thickness(0, 8, 0, 8);
            var entry = (ingredient, quantity, unit);
            lines.Add(entry);
            remove.Click += (_, _) => { lines.Remove(entry); fields.Children.Remove(row); };
            fields.Children.Add(row);
        }

        foreach (var line in variant.Recipe)
        {
            AddLine(line);
        }

        if (model.Ingredients.Count == 0)
        {
            fields.Children.Add(new TextBlock { Text = "No active ingredients. Add or activate ingredients in Inventory, then reload this catalog.", TextWrapping = TextWrapping.Wrap });
        }

        var add = new Button { Content = "Add ingredient line", MinHeight = 48 };
        add.Click += (_, _) => AddLine();
        fields.Children.Add(add);
        await ShowEditorAsync($"{model.SelectedItem!.Name} / {variant.SizeName} recipe", fields, async () =>
        {
            var inputs = new List<RecipeLineInput>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line.Ingredient.SelectedItem is not RecipeIngredientOption option || !CatalogNumberInput.TryParse(line.Quantity.Text, out var quantity))
                {
                    model.ShowValidation($"Line {index + 1}: select an ingredient and enter a decimal quantity.");
                    return false;
                }

                inputs.Add(new RecipeLineInput(option.Id, quantity, line.Unit.Text));
            }

            return await model.SaveAsync((_, recipes) => recipes.ReplaceRecipeAsync(new ReplaceRecipeCommand(variant.Id, inputs)), "Recipe saved.");
        });
    }

    private Task ConfirmAsync(string title, string explanation, Func<CatalogCommandHandler, RecipeCommandHandler, Task<Result>> action) =>
        ShowEditorAsync(title, Form(new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap }),
            () => model.SaveAsync(action, "Deactivated. Historical references are preserved."), "Deactivate");

    private async Task ShowEditorAsync(string title, FrameworkElement fields, Func<Task<bool>> save, string button = "Save", bool wide = false)
    {
        var error = new InfoBar { IsOpen = false, Severity = InfoBarSeverity.Error, IsClosable = false };
        var progress = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };
        var editor = new ContentControl { Content = fields, HorizontalContentAlignment = HorizontalAlignment.Stretch, IsTabStop = false };
        var content = Form(error, progress, editor);
        var dialog = new ContentDialog
        {
            Title = new TextBlock { Text = title, FontFamily = (FontFamily)ResourcesLookup("ConsoleSerifFont"), FontSize = 32, TextWrapping = TextWrapping.Wrap },
            Style = (Style)ResourcesLookup("WorkspaceDialog"),
            PrimaryButtonText = button,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            FontFamily = (FontFamily)ResourcesLookup("ConsoleSansFont"),
            Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 }
        };
        if (wide)
        {
            dialog.Style = (Style)ResourcesLookup("WorkspaceDrinkDialog");
        }
        dialog.Resources["AccentButtonStyle"] = ResourcesLookup("WorkspacePrimary");
        if (editorVerification is { } verify)
            dialog.Opened += async (_, _) => await verify(dialog, fields);
        dialog.Closing += (_, args) => args.Cancel = model.IsBusy;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                error.IsOpen = false;
                progress.Visibility = Visibility.Visible;
                editor.IsEnabled = false;
                dialog.IsPrimaryButtonEnabled = false;
                args.Cancel = !await save();
                if (args.Cancel)
                {
                    error.Message = model.Message;
                    error.IsOpen = true;
                }
            }
            finally
            {
                progress.Visibility = Visibility.Collapsed;
                editor.IsEnabled = true;
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };
        await dialog.ShowAsync();
    }

    private static object ResourcesLookup(string key) => Microsoft.UI.Xaml.Application.Current.Resources[key];

    private static TextBox TextField(string header, string? value = null) => new()
    {
        Header = header,
        Text = value ?? "",
        MinHeight = 48,
        Style = (Style)ResourcesLookup("ConsoleFieldStyle")
    };

    private static StackPanel Form(params UIElement[] elements)
    {
        var panel = new StackPanel { Spacing = 16 };
        foreach (var element in elements)
        {
            panel.Children.Add(element);
        }

        return panel;
    }
}
