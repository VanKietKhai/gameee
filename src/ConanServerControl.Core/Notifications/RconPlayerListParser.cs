using ConanServerControl.Core.Models;

namespace ConanServerControl.Core.Notifications;

/// <summary>
/// Parses the Conan Exiles RCON <c>listplayers</c> reply:
/// <c>Idx | Char name | Player name | User ID | Platform ID | Platform Name</c>, one row per player.
/// </summary>
public static class RconPlayerListParser
{
    public static IReadOnlyList<PlayerInfo> Parse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)
            || response.Contains("no players", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<PlayerInfo>();
        }

        var lines = response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        var charColumn = 1;
        var playerColumn = 2;
        var platformIdColumn = 4;
        var players = new List<PlayerInfo>();
        foreach (var line in lines)
        {
            var separator = line.Contains('|') ? '|' : ',';
            var cells = line.Split(separator).Select(c => c.Trim().Trim('"')).ToArray();
            if (cells.Length > 0 && cells[0].StartsWith("idx", StringComparison.OrdinalIgnoreCase))
            {
                charColumn = Find(cells, "char name", charColumn);
                playerColumn = Find(cells, "player name", playerColumn);
                platformIdColumn = Find(cells, "platform id", platformIdColumn);
                continue;
            }

            if (cells.Length < 2)
            {
                continue;
            }

            var character = Cell(cells, charColumn);
            var player = Cell(cells, playerColumn);
            var name = !string.IsNullOrEmpty(character) && !string.IsNullOrEmpty(player) && character != player
                ? $"{character} ({player})"
                : !string.IsNullOrEmpty(character) ? character : player;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var platformId = Cell(cells, platformIdColumn);
            players.Add(new PlayerInfo
            {
                Name = name,
                SteamId = string.IsNullOrEmpty(platformId) ? null : platformId
            });
        }

        return players;
    }

    private static int Find(string[] header, string name, int fallback)
    {
        var index = Array.FindIndex(header, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : fallback;
    }

    private static string? Cell(string[] cells, int index) =>
        index >= 0 && index < cells.Length && cells[index].Length > 0 ? cells[index] : null;
}
