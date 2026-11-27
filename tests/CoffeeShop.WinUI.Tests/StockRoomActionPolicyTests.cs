using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class StockRoomActionPolicyTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void CanManage_RequiresPermissionAndIdleState(bool busy, bool permitted, bool expected) =>
        Assert.Equal(expected, StockRoomActionPolicy.CanManage(permitted, busy));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void TargetState_MustDifferFromAuthoritativeState(bool current, bool requested) =>
        Assert.True(StockRoomActionPolicy.IsTransitionNeeded(current, requested));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void RepeatedStateChange_IsNotNeeded(bool current, bool requested) =>
        Assert.False(StockRoomActionPolicy.IsTransitionNeeded(current, requested));

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    public void ChangedServerState_RejectsStaleSelection(bool loaded, bool current, bool stale) =>
        Assert.Equal(stale, StockRoomActionPolicy.IsStale(loaded, current));
}
