using EasySave.Core;
using Xunit;

namespace EasySave.Tests;

// Unit tests for the computed BackupState.Progression property.
public class BackupStateTests
{
    [Fact]
    public void Progression_NoFilesProcessed_IsZero()
    {
        var state = new BackupState { TotalFiles = 10, RemainingFiles = 10 };
        Assert.Equal(0.0, state.Progression);
    }

    [Fact]
    public void Progression_AllFilesProcessed_Is100()
    {
        var state = new BackupState { TotalFiles = 10, RemainingFiles = 0 };
        Assert.Equal(100.0, state.Progression);
    }

    [Fact]
    public void Progression_TotalIsZero_IsZero_NoDivisionByZero()
    {
        var state = new BackupState { TotalFiles = 0, RemainingFiles = 0 };
        Assert.Equal(0.0, state.Progression);
    }

    [Theory]
    [InlineData(10, 5,  50.0)]
    [InlineData(10, 7,  30.0)]
    [InlineData(10, 3,  70.0)]
    [InlineData( 3, 2,  33.3)]
    [InlineData( 3, 1,  66.7)]
    [InlineData( 1, 0, 100.0)]
    public void Progression_RoundsToOneDecimalPlace(int total, int remaining, double expected)
    {
        var state = new BackupState { TotalFiles = total, RemainingFiles = remaining };
        Assert.Equal(expected, state.Progression);
    }
}
