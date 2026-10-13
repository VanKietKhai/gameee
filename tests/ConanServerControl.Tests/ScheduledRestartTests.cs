using ConanServerControl.Infrastructure.ProcessManagement;
using static ConanServerControl.Infrastructure.ProcessManagement.ScheduledRestartService;

namespace ConanServerControl.Tests;

public class ScheduledRestartTests
{
    [Theory]
    [InlineData(5, 4.4, 2, RestartDecision.None)]
    [InlineData(5, 4.5, 2, RestartDecision.Countdown)]
    [InlineData(5, 4.9, 0, RestartDecision.None)]
    [InlineData(5, 5.0, 0, RestartDecision.Now)]
    [InlineData(5, 7.0, 3, RestartDecision.Countdown)]
    [InlineData(0, 50, 0, RestartDecision.None)]
    public void Restarts_at_the_mark_with_a_countdown_only_when_players_are_online(int hours, double uptimeHours, int players, RestartDecision expected)
    {
        Assert.Equal(expected, Decide(hours, TimeSpan.FromHours(uptimeHours), players));
    }
}
