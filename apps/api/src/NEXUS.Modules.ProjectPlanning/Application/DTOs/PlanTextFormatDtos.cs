using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

/// <summary>
/// Structured display formatting for a Planning activity or WBS node.
/// Colors and sizes are semantic tokens; clients never send HTML or CSS.
/// </summary>
public class PlanTextFormatDto
{
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public PlanTextColorToken? TextColorToken { get; set; }
    public PlanHighlightColorToken? HighlightColorToken { get; set; }
    public PlanFontSizeToken? FontSizeToken { get; set; }
}
