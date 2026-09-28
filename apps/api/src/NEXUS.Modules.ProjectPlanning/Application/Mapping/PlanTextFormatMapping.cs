using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.Mapping;

public static class PlanTextFormatMapping
{
    public static PlanTextFormat? ToEntity(this PlanTextFormatDto? dto) => dto is null
        ? null
        : new PlanTextFormat
        {
            IsBold = dto.IsBold,
            IsItalic = dto.IsItalic,
            IsUnderline = dto.IsUnderline,
            TextColorToken = dto.TextColorToken,
            HighlightColorToken = dto.HighlightColorToken,
            FontSizeToken = dto.FontSizeToken
        };

    public static PlanTextFormatDto? ToDto(this PlanTextFormat? format) => format is null
        ? null
        : new PlanTextFormatDto
        {
            IsBold = format.IsBold,
            IsItalic = format.IsItalic,
            IsUnderline = format.IsUnderline,
            TextColorToken = format.TextColorToken,
            HighlightColorToken = format.HighlightColorToken,
            FontSizeToken = format.FontSizeToken
        };
}
