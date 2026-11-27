namespace CoffeeShop.WinUI.ViewModels;

/// <summary>Guards Stock room mutations against permission, busy state, and stale list rows.</summary>
public static class StockRoomActionPolicy
{
    public static bool CanManage(bool hasPermission, bool isBusy) => hasPermission && !isBusy;

    public static bool IsStale(bool loadedState, bool currentState) => loadedState != currentState;

    public static bool IsTransitionNeeded(bool currentState, bool requestedState) => currentState != requestedState;
}
