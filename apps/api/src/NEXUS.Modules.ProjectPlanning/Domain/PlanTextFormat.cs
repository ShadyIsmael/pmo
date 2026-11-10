namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// Structured, optional display metadata for a Planning activity or WBS node.
/// This deliberately contains no HTML or arbitrary CSS values.
/// </summary>
public sealed class PlanTextFormat
{
    /// <summary>
    /// True when this value carries an explicitly supplied format. False is the
    /// persisted sentinel for the API's absent (null) optional format.
    /// </summary>
    public bool IsSpecified { get; set; } = true;

    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public PlanTextColorToken? TextColorToken { get; set; }
    public PlanHighlightColorToken? HighlightColorToken { get; set; }
    public PlanFontSizeToken? FontSizeToken { get; set; }

    public static PlanTextFormat Unspecified() => new() { IsSpecified = false };

    public PlanTextFormat Clone() => new()
    {
        IsSpecified = IsSpecified,
        IsBold = IsBold,
        IsItalic = IsItalic,
        IsUnderline = IsUnderline,
        TextColorToken = TextColorToken,
        HighlightColorToken = HighlightColorToken,
        FontSizeToken = FontSizeToken
    };
}
