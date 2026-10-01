using ConanServerControl.Core.Abstractions;

namespace ConanServerControl.Core.Mods;

public static class WorkshopUpdateComparer
{
    /// <summary>
    /// Steam <c>time_updated</c> is compared to the last successful local install timestamp.
    /// A missing local install is treated as an update (download required).
    /// </summary>
    public static bool IsUpdateAvailable(DateTimeOffset? installedTimestamp, DateTimeOffset remoteUpdatedUtc)
    {
        if (installedTimestamp is null)
        {
            return true;
        }

        return remoteUpdatedUtc > installedTimestamp.Value.AddSeconds(2);
    }

    public static DateTimeOffset FromUnixSeconds(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return DateTimeOffset.UnixEpoch;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
    }
}
