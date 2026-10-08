using ConanServerControl.Core.Models;
using ConanServerControl.Core.Notifications;
using ConanServerControl.Infrastructure.Steam;

namespace ConanServerControl.Tests;

public class RconMessagesTests
{
    public static IEnumerable<object[]> AllMessages() => new[]
    {
        new object[] { RconMessages.ShuttingDown },
        new object[] { RconMessages.UpdateWarning(10) },
        new object[] { RconMessages.RestartWarning(5) },
        new object[] { RconMessages.StopWarning(30) },
        new object[] { RconMessages.CountdownCancelled }
    };

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Broadcasts_are_short_vietnamese_without_double_quotes(string message)
    {
        Assert.DoesNotContain('"', message);
        Assert.True(message.Length < 200);
        Assert.StartsWith("[SERVER] ", message);
    }

    [Fact]
    public void Countdowns_carry_the_number()
    {
        Assert.Equal("[SERVER] Server sẽ tắt sau 10 phút để cập nhật. Hãy về nơi an toàn và thoát game.", RconMessages.UpdateWarning(10));
        Assert.Equal("[SERVER] Server sẽ tắt sau 30 phút. Hãy về nơi an toàn và thoát game.", RconMessages.StopWarning(30));
    }

    [Theory]
    [InlineData(30, new[] { 30, 10, 5, 1 })]
    [InlineData(60, new[] { 60, 30, 10, 5, 1 })]
    [InlineData(15, new[] { 15, 10, 5, 1 })]
    [InlineData(10, new[] { 10, 5, 1 })]
    [InlineData(1, new[] { 1 })]
    [InlineData(0, new int[0])]
    public void Countdown_warns_at_30_10_5_1_minutes(int total, int[] expected)
    {
        Assert.Equal(expected, RconMessages.MarksFor(total));
    }
}

public class SteamCmdRetryTests
{
    [Theory]
    [InlineData(8, "", true)]
    [InlineData(1, "ERROR! Failed to install app '443030' (Missing configuration)", true)]
    [InlineData(1, "ERROR! Failed to install app '443030' (No subscription)", false)]
    [InlineData(0, "Success! App '443030' fully installed.", false)]
    public void Missing_configuration_and_self_update_exit_are_retried(int exitCode, string output, bool retry)
    {
        var result = new ProcessExecutionResult { ExitCode = exitCode, StandardOutput = output };

        Assert.Equal(retry, SteamCmdService.IsTransientAppUpdateFailure(result));
    }
}
