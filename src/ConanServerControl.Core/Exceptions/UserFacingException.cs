namespace ConanServerControl.Core.Exceptions;

/// <summary>
/// An error that can be shown directly to a non-expert administrator.
/// </summary>
public sealed class UserFacingException : Exception
{
    public UserFacingException(string title, string message, string? guidance = null, Exception? inner = null)
        : base(message, inner)
    {
        Title = title;
        Guidance = guidance;
    }

    public string Title { get; }

    public string? Guidance { get; }

    public string FormatForDisplay()
    {
        if (string.IsNullOrWhiteSpace(Guidance))
        {
            return $"{Title}{Environment.NewLine}{Environment.NewLine}{Message}";
        }

        return $"{Title}{Environment.NewLine}{Environment.NewLine}{Message}{Environment.NewLine}{Environment.NewLine}{Guidance}";
    }
}
