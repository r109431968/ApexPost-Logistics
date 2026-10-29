using ApexPost.Application.Services;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.UnitTests.Services;

public class StockGateTests
{
    [Fact]
    public async Task One_branch_shares_one_lock_and_other_branches_are_not_blocked_by_it()
    {
        var first = new BranchCode("BR001");
        var second = new BranchCode("BR002");

        Assert.Same(StockGate.For(first), StockGate.For(new BranchCode("BR001")));
        Assert.NotSame(StockGate.For(first), StockGate.For(second));

        await StockGate.For(first).WaitAsync();
        try
        {
            // BR002 is free while BR001 is held.
            var other = StockGate.For(second);
            Assert.True(await other.WaitAsync(TimeSpan.FromMilliseconds(300)));
            other.Release();

            // BR001 itself stays locked.
            Assert.False(await StockGate.For(first).WaitAsync(TimeSpan.FromMilliseconds(100)));
        }
        finally
        {
            StockGate.For(first).Release();
        }
    }
}