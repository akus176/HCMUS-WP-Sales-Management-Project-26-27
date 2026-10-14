using CoffeeShop.Application.Customers;

namespace CoffeeShop.WinUI.ViewModels;

// Shared across navigation. The POS consumes Request through
// CustomerOrderSelectionHandler.CreateDraftAsync, which revalidates current data.
public sealed class CustomerOrderContext
{
    public CustomerOrderSelectionRequest Request { get; private set; } = new();
    public CustomerOrderSelection Selection { get; private set; } = new(null, null, null, null);

    public void Set(CustomerOrderSelectionRequest request, CustomerOrderSelection selection)
    {
        Request = request;
        Selection = selection;
    }

    public void Clear() => Set(new(), new(null, null, null, null));
}
