namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>
/// Structured, optional display metadata for a Planning activity or WBS node.
/// This deliberately contains no HTML or arbitrary CSS values.
/// </summary>
public sealed class PlanTextFormat
{
    /// <summary>
    /// Required by EF for an optional owned dependent so a formatted row can be
    /// distinguished from an absent format when all user-facing values are null.
    /// </summary>
    public bool IsSpecified { get; set; } = true;

    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public PlanTextColorToken? TextColorToken { get; set; }
    public PlanHighlightColorToken? HighlightColorToken { get; set; }
    public PlanFontSizeToken? FontSizeToken { get; set; }

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
