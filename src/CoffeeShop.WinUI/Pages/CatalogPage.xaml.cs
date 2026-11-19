using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

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
        var emptySearchClearsDetail = model.SelectedItem is null && EmptyState.Visibility == Visibility.Visible;
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

    internal object VerificationLayout() => new
    {
        width = ActualWidth,
        detailAtSide = Grid.GetColumn(DetailHost) == 2,
        columns = (DrinksList.ItemsPanelRoot as ItemsWrapGrid)?.MaximumRowsOrColumns,
        contentFits = WorkspaceTabs.ActualWidth <= ActualWidth - 47,
        detailFits = DetailHost.ActualHeight <= WorkspaceContent.ActualHeight + 1,
        workspaceFits = WorkspaceTabs.ActualHeight <= WorkspaceContent.ActualHeight + 1,
        itemViewportHeight = DrinksList.ActualHeight,
        tabHeight = SectionBar.ActualHeight,
        tabsExclusive = new[] { DrinksTab, CategoriesTab, AddonsTab }.Count(tab => tab.IsChecked == true) == 1
    };

    internal async Task<object> VerifyMenuInteractionsAsync()
    {
        var originalSelection = model.SelectedItem?.Id;
        await Task.Delay(350);
        UpdateLayout();
        var listScroll = PresentationVerification.Elements(DrinksList).OfType<ScrollViewer>().FirstOrDefault();
        var headerPosition = SectionBar.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point());
        var detailsPosition = DetailHost.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point());
        var itemListScrollsIndependently = false;
        if (listScroll is { ScrollableHeight: > 0 })
        {
            listScroll.ChangeView(null, Math.Min(240, listScroll.ScrollableHeight), null, true);
            await Task.Delay(200);
            itemListScrollsIndependently = listScroll.VerticalOffset > 0
                && SectionBar.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point()) == headerPosition
                && DetailHost.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point()) == detailsPosition;
            await PresentationVerification.CaptureAsync(this, "native-menu-scrolled.png");
            listScroll.ChangeView(null, 0, null, true);
        }
        OnTab(CategoriesTab, new RoutedEventArgs());
        var categoriesWork = CategoriesTab.IsChecked == true && DrinksTab.IsChecked == false && AddonsTab.IsChecked == false
            && CategoriesList.Visibility == Visibility.Visible && SearchControls.Visibility == Visibility.Collapsed
            && DetailHost.Visibility == Visibility.Collapsed && DetailColumn.Width.Value == 0;
        await Task.Delay(350);
        var categoryRowActions = VerifyRecordRowActions(CategoriesList);
        await PresentationVerification.CaptureAsync(this, "native-menu-categories.png");
        OnTab(AddonsTab, new RoutedEventArgs());
        var addonsWork = AddonsTab.IsChecked == true && DrinksTab.IsChecked == false && CategoriesTab.IsChecked == false
            && AddonsList.Visibility == Visibility.Visible && SearchControls.Visibility == Visibility.Collapsed
            && DetailHost.Visibility == Visibility.Collapsed && DetailColumn.Width.Value == 0;
        await Task.Delay(350);
        var addonRowActions = VerifyRecordRowActions(AddonsList);
        await PresentationVerification.CaptureAsync(this, "native-menu-addons.png");
        OnTab(DrinksTab, new RoutedEventArgs());
        var option = CompactCategoryFilter.Items.OfType<CategoryOption>().FirstOrDefault(category => category.Id is not null);
        bool? filteringWorks = null;
        if (option is not null)
        {
            CompactCategoryFilter.SelectedItem = option;
            filteringWorks = DrinksList.Items.OfType<DrinkRow>().All(row => row.Item.CategoryId == option.Id);
        }
        await ResetFiltersAsync();
        var resetWorks = SearchBox.Text.Length == 0 && categoryFilterId is null && DrinksList.Items.Count == model.Items.Count;
        if (originalSelection is { } id && model.Items.Any(item => item.Id == id)) await model.SelectAsync(id);
        return new { itemListScrollsIndependently, categoriesWork, categoryRowActions, addonsWork, addonRowActions, filteringWorks, resetWorks };
    }

    private static bool VerifyRecordRowActions(ListView list)
    {
        var rows = PresentationVerification.Elements(list).OfType<ListViewItem>().Where(item => item.Content is CatalogRow).ToArray();
        return rows.Length > 0 && rows.All(item =>
        {
            var row = (CatalogRow)item.Content;
            var buttons = PresentationVerification.Elements(item).OfType<Button>().Where(button => ReferenceEquals(button.Tag, row.Record)).ToArray();
            return buttons.Length == 2 && buttons.Any(button => Equals(button.Content, "Edit"))
                && buttons.Any(button => Equals(button.Content, "Deactivate") && button.IsEnabled == row.IsActive);
        });
    }

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
            await Task.Delay(150);
            if (PresentationVerification.Elements(dialog).OfType<Border>().FirstOrDefault(border => border.Name == "BackgroundElement") is { } frame)
                await PresentationVerification.CaptureAsync(frame, "native-menu-editor.png");
            result = new { compactPreserved, expandedPreserved = inputs[0].Text == draft, savedRecordUnchanged = model.SelectedItem.Name == savedName, compactTargets, expandedTargets = PresentationVerification.ButtonTargets(dialog) };
            dialog.Hide();
        };
        try { await EditDrinkAsync(model.SelectedItem); }
        finally { editorVerification = null; }
        return result;
    }
    public sealed record CategoryOption(EntityId? Id, string Name);
    public sealed record CatalogRow(object Record, string Name, string StateDisplay)
    {
        public bool IsActive => Record switch { CategorySummary category => category.IsActive, AddonSummary addon => addon.IsActive, _ => false };
        public string PriceDisplay => Record is AddonSummary addon ? addon.Price.ToString("0.00") : "";
        public Visibility PriceVisibility => Record is AddonSummary ? Visibility.Visible : Visibility.Collapsed;
        public string EditLabel => $"Edit {Name}";
        public string DeactivateLabel => $"Deactivate {Name}";
    }
    public sealed record CatalogSizeRow(SizeVariantDetail Variant)
    {
        public string SizeName => Variant.SizeName + (Variant.IsActive ? "" : " · Inactive");
        public string PriceDisplay => Variant.Price.ToString("0.00");
        public bool IsActive => Variant.IsActive;
        public string ActionName => $"Actions for {Variant.SizeName}";
    }
    public sealed record DrinkRow(MenuItemSummary Item)
    {
        public string Name => Item.Name;
        public string CategoryName => $"{Item.CategoryName} · {Item.SizeVariants.Count(size => size.IsActive)} sizes";
        public string StateDisplay => Item.IsActive ? "Active" : "Inactive";
        public string PriceDisplay => Item.SizeVariants.Where(size => size.IsActive).ToArray() is { Length: > 0 } sizes
            ? $"from {sizes.Min(size => size.Price):0.00}" : "No active sizes";
        public BitmapImage Photo => new CounterMenuCard(Item).Photo;
    }

    public CatalogPage()
    {
        InitializeComponent();
        model = new CatalogWorkspace(App.Services.GetRequiredService<ICatalogWorkspaceSource>());
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
            if (Grid.GetRow(DetailScroll) == 1) DetailHeading.StartBringIntoView();
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
        card.BorderThickness = new Thickness(selected ? 2 : 1);
        card.Background = (Brush)ResourcesLookup(selected ? "ConsolePanelBrush" : "ConsoleRaisedGroundBrush");
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
            CatalogCount.Text = "MANAGE / MENU";
            CatalogHealth.Text = $"{model.Items.Count(item => item.IsActive)} active items\n{model.Items.Count(item => !item.IsActive)} inactive in results";
            DrinksTab.IsEnabled = CategoriesTab.IsEnabled = AddonsTab.IsEnabled = IncludeInactive.IsEnabled = !model.IsBusy;
            if (!ReferenceEquals(renderedCategories, model.Categories))
            {
                renderedCategories = model.Categories;
                var options = new[] { new CategoryOption(null, "All categories") }.Concat(model.Categories.Select(item => new CategoryOption(item.Id, item.Name))).ToArray();
                CategoryFilter.ItemsSource = options;
                CategoryFilter.SelectedItem = options.FirstOrDefault(item => item.Id == categoryFilterId) ?? options[0];
                CompactCategoryFilter.ItemsSource = options;
                CompactCategoryFilter.SelectedItem = CategoryFilter.SelectedItem;
                CategoriesList.ItemsSource = model.Categories.Select(item => new CatalogRow(item, item.Name, item.IsActive ? "Active" : "Inactive")).ToArray();
            }
            if (!ReferenceEquals(renderedItems, model.Items))
            {
                renderedItems = model.Items;
                ApplyDrinkFilter();
            }
            if (!ReferenceEquals(AddonsList.Tag, model.Addons))
            {
                AddonsList.Tag = model.Addons;
                AddonsList.ItemsSource = model.Addons.Select(item => new CatalogRow(item, item.Name, item.IsActive ? "Active" : "Inactive")).ToArray();
            }
            if (!model.IsBusy && DrinksList.ItemsSource is IEnumerable<DrinkRow> rows)
            {
                DrinksList.SelectedItem = rows.FirstOrDefault(row => row.Item.Id == model.SelectedItem?.Id);
                foreach (var row in rows) PaintDrinkSelection(DrinksList.ContainerFromItem(row) as GridViewItem, row.Item.Id == model.SelectedItem?.Id);
            }
            EmptyState.Visibility = activeTab == "drinks" && DrinksList.Items.Count == 0 && !model.IsBusy && !model.IsError ? Visibility.Visible : Visibility.Collapsed;
            CategoriesEmpty.Visibility = activeTab == "categories" && model.Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            AddonsEmpty.Visibility = activeTab == "addons" && model.Addons.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CategoryLimit.Visibility = activeTab == "categories" && model.Categories.Count == 200 ? Visibility.Visible : Visibility.Collapsed;
            UpdateResultsSummary();
            RenderDetail();
            ConfigureLayout();
        }
        finally { rendering = false; }
    }
    private void ApplyDrinkFilter()
    {
        var rows = model.Items.Where(item => categoryFilterId is null || item.CategoryId == categoryFilterId).Select(item => new DrinkRow(item)).ToArray();
        DrinksList.ItemsSource = rows;
        DrinksList.SelectedItem = rows.FirstOrDefault(row => row.Item.Id == model.SelectedItem?.Id);
        EmptyState.Visibility = activeTab == "drinks" && rows.Length == 0 && !model.IsBusy && !model.IsError ? Visibility.Visible : Visibility.Collapsed;
        UpdateResultsSummary();
    }

    private void UpdateResultsSummary() => ResultsSummary.Text = activeTab switch
    {
        "categories" => $"{model.Categories.Count} categories · Manage names and availability",
        "addons" => $"{model.Addons.Count} add-ons · Manage names, prices, and availability",
        _ => $"{DrinksList.Items.Count} items · {DrinksList.Items.OfType<DrinkRow>().Count(row => row.Item.IsActive)} active"
            + (model.Items.Count == 200 ? " · First 200 matches; narrow your search to find more" : "")
    };
    private void RenderDetail()
    {
        if (activeTab != "drinks") return;
        var detail = model.SelectedItem;
        if (detail is not null && categoryFilterId is not null && detail.CategoryId != categoryFilterId) detail = null;
        ItemActions.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        DetailHeading.Text = detail?.Name ?? $"Select {(activeTab == "drinks" ? "a drink" : activeTab == "categories" ? "a category" : "an add-on")}";
        DetailDescription.Text = detail is null ? "Details and available actions appear here." : $"{detail.CategoryName} · {(detail.IsActive ? "Active" : "Inactive")}\n{detail.Description}";
        DetailArt.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        DetailPhoto.Source = detail is not null && model.Items.FirstOrDefault(item => item.Id == detail.Id) is { } item
            ? new CounterMenuCard(item).Photo : null;
        DetailMeta.Text = detail is null ? "" : $"{detail.CategoryName} · {detail.SizeVariants.Count} sizes · {(detail.IsActive ? "Active" : "Inactive")}";
        if (detail is not null) DetailDescription.Text = detail.Description ?? "";
        VariantsList.ItemsSource = detail?.SizeVariants.Select(variant => new CatalogSizeRow(variant)).ToArray();
        var recipeVariant = detail?.SizeVariants.FirstOrDefault(size => size.IsActive) ?? detail?.SizeVariants.FirstOrDefault();
        RecipeHeading.Text = recipeVariant is null ? "Recipe" : $"Recipe · {recipeVariant.SizeName}";
        RecipeButton.Tag = recipeVariant;
        RecipeButton.IsEnabled = recipeVariant is not null;
        RecipeSummary.Text = recipeVariant is null || recipeVariant.Recipe.Count == 0 ? "No recipe lines. Add ingredients to deduct stock per cup."
            : string.Join("\n", recipeVariant.Recipe.Select(line => $"{line.Quantity:0.###} {line.Unit} · {line.IngredientName}"));
        DeactivateDrinkButton.IsEnabled = detail?.IsActive == true;
        CompatibilityText.Text = detail is null || detail.CompatibleAddons.Count == 0 ? "No compatible add-ons selected."
            : string.Join("\n", detail.CompatibleAddons.Select(addon => $"{addon.Name}  +{addon.Price:N2}{(addon.IsActive ? "" : " (inactive)")}"));
    }
    private void OnTab(object sender, RoutedEventArgs args)
    {
        activeTab = (string)((Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)sender).Tag;
        DrinksTab.IsChecked = activeTab == "drinks";
        CategoriesTab.IsChecked = activeTab == "categories";
        AddonsTab.IsChecked = activeTab == "addons";
        DrinksList.Visibility = SearchControls.Visibility = activeTab == "drinks" ? Visibility.Visible : Visibility.Collapsed;
        CategoriesList.Visibility = activeTab == "categories" ? Visibility.Visible : Visibility.Collapsed;
        AddonsList.Visibility = activeTab == "addons" ? Visibility.Visible : Visibility.Collapsed;
        NewButton.Content = activeTab == "drinks" ? "+ New item" : activeTab == "categories" ? "+ New category" : "+ New add-on";
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
        CategoryFilter.SelectedItem = CompactCategoryFilter.SelectedItem = CompactCategoryFilter.Items.OfType<CategoryOption>().First(option => option.Id == categoryFilterId);
        ApplyDrinkFilter();
        rendering = false;
        RenderDetail();
    }
    private void OnSearchKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter) { args.Handled = true; OnSearch(sender, new RoutedEventArgs()); }
    }
    private async void OnResetFilters(object sender, RoutedEventArgs args) => await ResetFiltersAsync();
    private async Task ResetFiltersAsync()
    {
        rendering = true;
        categoryFilterId = null;
        CategoryFilter.SelectedIndex = CompactCategoryFilter.SelectedIndex = 0;
        model.Search = SearchBox.Text = "";
        rendering = false;
        await model.LoadAsync();
    }
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => ConfigureLayout();
    private void ConfigureLayout()
    {
        if (DetailColumn is null) return;
        var wide = ActualWidth >= 1000 && ActualHeight >= 600;
        var hasDetails = activeTab == "drinks";
        var stackedDetails = hasDetails && !wide;
        var showCategoryRail = wide && activeTab == "drinks";
        CategoryRail.Visibility = showCategoryRail ? Visibility.Visible : Visibility.Collapsed;
        CompactCategoryFilter.Visibility = !wide && activeTab == "drinks" ? Visibility.Visible : Visibility.Collapsed;
        RailColumn.Width = new GridLength(showCategoryRail ? 155 : 0);
        DetailHost.Visibility = hasDetails ? Visibility.Visible : Visibility.Collapsed;
        DetailColumn.Width = new GridLength(wide && hasDetails ? 330 : 0);
        WorkspaceTabs.ColumnSpacing = wide && hasDetails ? 18 : 0;
        WorkspaceTabs.RowDefinitions[0].Height = new GridLength(stackedDetails ? 3 : 1, GridUnitType.Star);
        WorkspaceTabs.RowDefinitions[1].Height = stackedDetails ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        WorkspaceTabs.RowSpacing = stackedDetails ? 12 : 0;
        Grid.SetColumn(DetailHost, wide ? 2 : 1);
        Grid.SetRow(DetailHost, wide ? 0 : 1);
        var headerInline = ActualWidth >= 680;
        HeaderOrnament.Visibility = ActualWidth >= 1000 ? Visibility.Visible : Visibility.Collapsed;
        Header.ColumnDefinitions[1].Width = new GridLength(ActualWidth >= 1000 ? 116 : 0);
        Header.ColumnDefinitions[2].Width = headerInline ? GridLength.Auto : new GridLength(0);
        Header.ColumnSpacing = headerInline ? 18 : 0;
        Grid.SetColumn(HeaderActions, headerInline ? 2 : 0);
        Grid.SetColumnSpan(HeaderActions, headerInline ? 1 : 3);
        Grid.SetRow(HeaderActions, headerInline ? 0 : 1);
        MenuTitle.FontSize = headerInline ? 52 : 40;
        Grid.SetColumn(IncludeInactive, ActualWidth >= 520 ? 1 : 0);
        Grid.SetRow(IncludeInactive, ActualWidth >= 520 ? 0 : 1);
        var catalogWidth = Math.Max(240, ActualWidth - 48 - (wide ? 366 + (showCategoryRail ? 155 : 0) : 0));
        var height = WorkspaceContent.ActualHeight;
        CategoryOrnament.Visibility = CategoryPattern.Visibility = height >= 650 ? Visibility.Visible : Visibility.Collapsed;
        CategoryFilter.MaxHeight = Math.Max(88, height - 160);
        var columns = catalogWidth >= 560 ? 2 : 1;
        if (DrinksList.ItemsPanelRoot is not ItemsWrapGrid drinkWrap) return;
        drinkWrap.ItemWidth = Math.Max(240, (catalogWidth - 4) / columns);
        drinkWrap.MaximumRowsOrColumns = columns;
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
        (StackPanel Fields, Func<IReadOnlyCollection<RecipeLineInput>?> Read)? recipe =
            item is null ? RecipeEditor([], "Ingredients for the initial size (optional)") : null;
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
        description.Height = item is null ? 84 : 112;
        fields.Children.Add(description);
        if (item is null)
        {
            Grid.SetRow(size, 2);
            Grid.SetRow(price, 2);
            Grid.SetColumn(price, 1);
            fields.Children.Add(size);
            fields.Children.Add(price);
            fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(recipe!.Value.Fields, 3);
            Grid.SetColumnSpan(recipe.Value.Fields, 2);
            fields.Children.Add(recipe.Value.Fields);
        }

        return ShowEditorAsync(item is null ? "New drink" : $"Edit {item.Name}", fields, async () =>
        {
            if (category.SelectedItem is not CategorySummary selected)
            {
                model.ShowValidation("Select an active category. If none is available, create one in Categories first.");
                return false;
            }

            if (item is not null)
            {
                return await model.SaveAsync((commands, _) =>
                    commands.UpdateMenuItemAsync(new UpdateMenuItemCommand(item.Id, selected.Id, name.Text, description.Text)), "Drink saved.");
            }

            var ingredients = recipe!.Value.Read();
            if (ingredients is null) return false;
            return await model.SaveAsync((commands, _) =>
                commands.CreateMenuItemAsync(new CreateMenuItemCommand(selected.Id, name.Text, description.Text,
                    [new CreateSizeVariantInput(size.Text, CatalogNumberInput.Price(price.Text), ingredients)])),
                ingredients.Count == 0 ? "Drink saved." : "Drink and initial size recipe saved.");
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
        var (fields, read) = RecipeEditor(variant.Recipe, "Ingredients");
        await ShowEditorAsync($"{model.SelectedItem!.Name} / {variant.SizeName} recipe", fields, async () =>
        {
            var inputs = read();
            if (inputs is null) return false;
            return await model.SaveAsync((commands, _) => commands.ReplaceRecipeAsync(new ReplaceRecipeCommand(variant.Id, inputs)), "Recipe saved.");
        }, wide: true);
    }

    private (StackPanel Fields, Func<IReadOnlyCollection<RecipeLineInput>?> Read) RecipeEditor(
        IReadOnlyList<RecipeLineDetail> existingLines, string heading)
    {
        var fields = new StackPanel { Spacing = 8, Margin = new Thickness(0, 5, 0, 0) };
        fields.Children.Add(new TextBlock
        {
            Text = heading, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        fields.Children.Add(new TextBlock
        {
            Text = model.Ingredients.Count == 0 ? "Add ingredients in Stock room to use them in recipes."
                : "Enter the amount used for one serving. Stock is deducted when the order is confirmed.",
            TextWrapping = TextWrapping.Wrap
        });
        var rows = new StackPanel { Spacing = 10 };
        fields.Children.Add(rows);
        var lines = new List<(ComboBox Ingredient, TextBox Quantity, TextBox Unit)>();

        Grid AddLine(RecipeLineDetail? existing = null)
        {
            var options = model.Ingredients.ToList();
            if (existing is not null && options.All(option => option.Id != existing.IngredientId))
            {
                options.Add(new RecipeIngredientOption(existing.IngredientId, $"{existing.IngredientName} (unavailable)", existing.Unit));
            }

            var ingredient = new ComboBox
            {
                Header = "Ingredient", PlaceholderText = "Choose an ingredient", DisplayMemberPath = nameof(RecipeIngredientOption.Name),
                ItemsSource = options, MinHeight = 48, HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ingredient.SelectedItem = options.FirstOrDefault(option => option.Id == existing?.IngredientId);
            var quantity = TextField("Quantity", existing?.Quantity.ToString("0.###"));
            var unit = TextField("Unit", existing?.Unit);
            ingredient.SelectionChanged += (_, _) =>
            {
                if (ingredient.SelectedItem is RecipeIngredientOption option) unit.Text = option.BaseUnit;
            };
            var remove = new Button
            {
                Content = "×", MinWidth = 44, MinHeight = 48, VerticalAlignment = VerticalAlignment.Bottom,
                Style = (Style)Resources["MenuButton"]
            };
            AutomationProperties.SetName(remove, "Remove ingredient line");
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(quantity, 1);
            Grid.SetColumn(unit, 2);
            Grid.SetColumn(remove, 3);
            row.Children.Add(ingredient);
            row.Children.Add(quantity);
            row.Children.Add(unit);
            row.Children.Add(remove);

            var entry = (ingredient, quantity, unit);
            lines.Add(entry);
            remove.Click += (_, _) => { lines.Remove(entry); rows.Children.Remove(row); };
            rows.Children.Add(row);
            return row;
        }

        foreach (var line in existingLines) AddLine(line);

        var add = new Button
        {
            Content = "Add ingredient", MinHeight = 44, IsEnabled = model.Ingredients.Count > 0,
            Style = (Style)Resources["MenuButton"]
        };
        add.Click += (_, _) => AddLine().StartBringIntoView();
        fields.Children.Add(add);

        IReadOnlyCollection<RecipeLineInput>? Read()
        {
            var inputs = new List<RecipeLineInput>();
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line.Ingredient.SelectedItem is not RecipeIngredientOption option
                    || !CatalogNumberInput.TryParse(line.Quantity.Text, out var quantity) || quantity <= 0
                    || string.IsNullOrWhiteSpace(line.Unit.Text))
                {
                    model.ShowValidation($"Ingredient line {index + 1} needs an ingredient, a positive quantity, and a unit.");
                    return null;
                }

                inputs.Add(new RecipeLineInput(option.Id, quantity, line.Unit.Text));
            }
            return inputs;
        }

        return (fields, Read);
    }

    private Task ConfirmAsync(string title, string explanation, Func<ICatalogWorkspaceSource, ICatalogWorkspaceSource, Task<Result>> action) =>
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
            Style = (Style)Resources["MenuDialog"],
            PrimaryButtonText = button,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            FontFamily = (FontFamily)ResourcesLookup("ConsoleSansFont"),
            FontSize = 14,
            Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 }
        };
        if (wide)
        {
            dialog.Style = (Style)Resources["MenuDrinkDialog"];
        }
        dialog.Resources["AccentButtonStyle"] = Resources["MenuPrimary"];
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
                    error.StartBringIntoView();
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
        FontSize = 14,
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
