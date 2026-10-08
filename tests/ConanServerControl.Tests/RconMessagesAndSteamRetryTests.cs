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
        new object[] { RconMessages.RestartWarningMinutes(5) },
        new object[] { RconMessages.RestartWarningSeconds(30) }
    };

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Broadcasts_are_short_vietnamese_without_double_quotes(string message)
    {
        Assert.DoesNotContain('"', message);
        Assert.True(message.Length < 200);
        Assert.StartsWith("[SERVER] Server", message);
    }

    [Fact]
    public void Countdowns_carry_the_number()
    {
        Assert.Equal("[SERVER] Server sẽ tắt sau 10 phút để cập nhật. Hãy về nơi an toàn và thoát game.", RconMessages.UpdateWarning(10));
        Assert.Contains("30 giây", RconMessages.RestartWarningSeconds(30));
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
