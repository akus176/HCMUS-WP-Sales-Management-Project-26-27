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
    private readonly CatalogWorkspace model;

    public CatalogPage()
    {
        InitializeComponent();
        model = new CatalogWorkspace(App.Services.GetRequiredService<IServiceScopeFactory>());
        model.PropertyChanged += (_, _) => Render();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args) => await model.LoadAsync();

    private async void OnReload(object sender, RoutedEventArgs args) => await model.LoadAsync();

    private async void OnSearch(object sender, RoutedEventArgs args)
    {
        model.Search = SearchBox.Text;
        model.IncludeInactive = IncludeInactive.IsChecked == true;
        await model.LoadAsync();
    }

    private async void OnDrinkSelected(object sender, SelectionChangedEventArgs args)
    {
        if (DrinksList.SelectedItem is MenuItemSummary item)
        {
            await model.SelectAsync(item.Id);
        }
    }

    private void Render()
    {
        LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceTabs.IsEnabled = !model.IsBusy;
        ReloadButton.IsEnabled = !model.IsBusy;
        Status.Severity = model.IsError ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
        Status.Message = model.Message;
        EmptyMessage.Visibility = model.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        // Do not reassign ItemsSource during a selection load: it would clear selection.
        if (!ReferenceEquals(DrinksList.ItemsSource, model.Items))
        {
            DrinksList.ItemsSource = model.Items;
        }

        CategoriesList.ItemsSource = model.Categories;
        AddonsList.ItemsSource = model.Addons;
        CategoriesEmpty.Visibility = model.Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AddonsEmpty.Visibility = model.Addons.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CategoryLimit.Visibility = model.Categories.Count == 200 ? Visibility.Visible : Visibility.Collapsed;
        var detail = model.SelectedItem;
        ItemActions.Visibility = detail is null ? Visibility.Collapsed : Visibility.Visible;
        DetailHeading.Text = detail is null ? "Select a drink to open its notebook." : $"{detail.Name} ({(detail.IsActive ? "active" : "inactive")})";
        DetailDescription.Text = detail is null ? "" : $"{detail.CategoryName}\n{detail.Description}";
        VariantsList.ItemsSource = detail?.SizeVariants;
        CompatibilityText.Text = detail is null || detail.CompatibleAddons.Count == 0
            ? "No compatible add-ons selected."
            : string.Join(", ", detail.CompatibleAddons.Select(addon => $"{addon.Name}{(addon.IsActive ? "" : " (inactive)")}"));
    }

    private async void OnNewCategory(object sender, RoutedEventArgs args) => await EditCategoryAsync(null);

    private async void OnEditCategory(object sender, RoutedEventArgs args) => await EditCategoryAsync((CategorySummary)((Button)sender).Tag);

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

    private async void OnNewDrink(object sender, RoutedEventArgs args) => await EditDrinkAsync(null);

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
            ItemsSource = model.Categories.Where(candidate => candidate.IsActive).ToArray()
        };
        category.SelectedItem = model.Categories.FirstOrDefault(candidate => candidate.Id == item?.CategoryId && candidate.IsActive);
        var size = TextField("Initial size name", "Regular");
        var price = TextField("Initial size price");
        var fields = Form(name, description, category);
        if (item is null)
        {
            fields.Children.Add(size);
            fields.Children.Add(price);
        }

        return ShowEditorAsync(item is null ? "New drink" : "Edit drink", fields, async () =>
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
        });
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

    private async void OnEditVariant(object sender, RoutedEventArgs args) => await EditVariantAsync((SizeVariantDetail)((Button)sender).Tag);

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
        var variant = (SizeVariantDetail)((Button)sender).Tag;
        await ConfirmAsync($"Deactivate {variant.SizeName}?", "This size becomes unavailable; its recipe and historical references remain.",
            (commands, _) => commands.DeactivateSizeVariantAsync(new DeactivateSizeVariantCommand(variant.Id)));
    }

    private async void OnNewAddon(object sender, RoutedEventArgs args) => await EditAddonAsync(null);

    private async void OnEditAddon(object sender, RoutedEventArgs args) => await EditAddonAsync((AddonSummary)((Button)sender).Tag);

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
        var variant = (SizeVariantDetail)((Button)sender).Tag;
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

    private async Task ShowEditorAsync(string title, StackPanel fields, Func<Task<bool>> save, string button = "Save")
    {
        var error = new InfoBar { IsOpen = false, Severity = InfoBarSeverity.Error, IsClosable = false };
        var progress = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };
        var editor = new ContentControl { Content = fields, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var content = Form(error, progress, editor);
        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = button,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            FontFamily = (FontFamily)ResourcesLookup("SketchBodyFontFamily"),
            Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 }
        };
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
        BorderThickness = new Thickness(2),
        CornerRadius = (CornerRadius)ResourcesLookup("SketchCornerRadiusWobbly"),
        BorderBrush = (Brush)ResourcesLookup("SketchBorderBrush")
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
