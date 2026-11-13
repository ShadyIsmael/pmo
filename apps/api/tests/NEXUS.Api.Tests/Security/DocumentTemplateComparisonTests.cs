using System.IO.Compression;
using System.Security;
using System.Text;
using NEXUS.Modules.Documents.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class DocumentTemplateComparisonTests
{
    [Fact]
    public void DocxComparisonIgnoresCompletedBodyTextWhenTemplateSectionsMatch()
    {
        using var template = StreamFor(CreateDocx(["Overview", "Risk Register"], "Fill in this text."));
        using var projectDocument = StreamFor(CreateDocx(["Overview", "Risk Register"], "Completed project-specific content."));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Docx);

        Assert.Equal(DocumentTemplateComparison.Compliant, result.Status);
        Assert.Equal(DocumentTemplateComparison.ValidatorVersion, result.ValidatorVersion);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void DocxComparisonRecordsMissingSectionsAsLocalizedAdvisoryFindings()
    {
        using var template = StreamFor(CreateDocx(["Overview", "Risk Register"], "Template content."));
        using var projectDocument = StreamFor(CreateDocx(["Overview"], "Project content."));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Docx);

        Assert.Equal(DocumentTemplateComparison.Mismatch, result.Status);
        var difference = Assert.Single(result.Differences);
        Assert.Equal("RequiredSectionMissing", difference.Code);
        Assert.Equal("Risk Register", difference.Expected);
        Assert.Contains("section", difference.MessageEn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("أقسام", difference.MessageAr);
    }

    [Fact]
    public void XlsxComparisonAcceptsCellEditsWhenWorksheetAndColumnsMatch()
    {
        using var template = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task", "Owner"], ["1", "Example", "Template Owner"])]));
        using var projectDocument = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task", "Owner"], ["99", "Build the service", "Project Owner"])]));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Xlsx);

        Assert.Equal(DocumentTemplateComparison.Compliant, result.Status);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void XlsxComparisonUsesFirstHeaderRowInsteadOfWiderCompletedDataRows()
    {
        using var template = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task"], ["1", "Example", "Template notes"])]));
        using var projectDocument = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task"], ["99", "Build the service", "Completed project-specific notes"])]));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Xlsx);

        Assert.Equal(DocumentTemplateComparison.Compliant, result.Status);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void XlsxComparisonReturnsNotVerifiedWhenTheFirstRowCannotBeIdentifiedAsHeaders()
    {
        var ambiguousRows = new IReadOnlyList<string>[]
        {
            ["Project Plan"],
            ["ID", "Task", "Owner"],
            ["1", "Completed work", "Project Owner"]
        };
        using var template = StreamFor(CreateXlsx([new Sheet("Tasks", [], [], Rows: ambiguousRows)]));
        using var projectDocument = StreamFor(CreateXlsx([new Sheet("Tasks", [], [], Rows: ambiguousRows)]));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Xlsx);

        Assert.Equal(DocumentTemplateComparison.NotVerified, result.Status);
        Assert.Contains(result.Differences, item => item.Code == "TemplateColumnsNotVerified");
    }

    [Fact]
    public void XlsxComparisonReadsSharedStringsForWorkbookHeaders()
    {
        using var template = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task", "Owner"], ["1", "Example", "Template Owner"])], useSharedStrings: true));
        using var projectDocument = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task", "Owner"], ["99", "Completed work", "Project Owner"])], useSharedStrings: true));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Xlsx);

        Assert.Equal(DocumentTemplateComparison.Compliant, result.Status);
        Assert.Empty(result.Differences);
    }

    [Fact]
    public void XlsxComparisonReturnsNotVerifiedWhenCumulativeParsingBudgetIsExceeded()
    {
        using var template = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task"], ["1", "Example"])]));
        using var projectDocument = StreamFor(CreateXlsx([new Sheet("Tasks", ["ID", "Task"], ["99", "Completed work"], EmptyRows: 140_000)]));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Xlsx);

        Assert.Equal(DocumentTemplateComparison.NotVerified, result.Status);
        Assert.Contains(result.Differences, item => item.Code == "ComparisonBudgetExceeded");
    }

    [Fact]
    public void XlsxComparisonRecordsMissingWorksheetsAndColumnsWithoutBlockingUpload()
    {
        using var template = StreamFor(CreateXlsx(
        [
            new Sheet("Tasks", ["ID", "Task", "Owner"], ["1", "Example", "Owner"]),
            new Sheet("Risks", ["Risk", "Mitigation"], ["Example", "Plan"])
        ]));
        using var projectDocument = StreamFor(CreateXlsx(
        [
            new Sheet("Tasks", ["ID", "Task"], ["2", "Project work"]),
            new Sheet("Notes", ["Note"], ["Completed offline"])
        ]));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Xlsx);

        Assert.Equal(DocumentTemplateComparison.Mismatch, result.Status);
        Assert.Contains(result.Differences, item => item.Code == "RequiredWorksheetMissing" && item.Expected == "Risks");
        Assert.Contains(result.Differences, item => item.Code == "RequiredColumnMissing" && item.Expected == "Owner");
        Assert.Contains(result.Differences, item => item.Code == "AdditionalWorksheetDetected" && item.Detected == "Notes");
    }

    [Fact]
    public void ComparisonReturnsNotVerifiedWhenContentCannotBeParsedReliably()
    {
        using var template = StreamFor(CreateDocx(["Overview"], "Template content."));
        using var malformedProject = StreamFor(Encoding.UTF8.GetBytes("not a Word package"));

        var result = CompareWithMatchingTemplateIdentity(template, malformedProject, DocumentFileType.Docx);

        Assert.Equal(DocumentTemplateComparison.NotVerified, result.Status);
        Assert.Contains(result.Differences, item => item.Code == "ContentTypeNotVerified");
    }

    [Fact]
    public void ComparisonReportsKnownTemplateIdentityMismatchWithExpectedAndDetectedReferences()
    {
        var expected = new DocumentTemplateReference(Guid.NewGuid(), Guid.NewGuid());
        var uploadedSource = new DocumentTemplateReference(Guid.NewGuid(), Guid.NewGuid());
        using var template = StreamFor(CreateDocx(["Overview"], "Template content."));
        using var projectDocument = StreamFor(CreateDocx(["Overview"], "Project content."));

        var result = DocumentTemplateComparison.Compare(template, projectDocument, DocumentFileType.Docx, expected, uploadedSource);

        Assert.Equal(DocumentTemplateComparison.Mismatch, result.Status);
        var difference = Assert.Single(result.Differences);
        Assert.Equal("TemplateIdentityMismatch", difference.Code);
        Assert.Equal($"{expected.TemplateId:D}/{expected.VersionId:D}", difference.Expected);
        Assert.Equal($"{uploadedSource.TemplateId:D}/{uploadedSource.VersionId:D}", difference.Detected);
    }

    [Fact]
    public void LegacyComparisonReturnsNotVerifiedWhenTemplateIdentityEvidenceIsAbsent()
    {
        using var template = StreamFor(CreateDocx(["Overview"], "Template content."));
        using var projectDocument = StreamFor(CreateDocx(["Overview"], "Completed project content."));

        var result = DocumentTemplateComparison.Compare(template, projectDocument, DocumentFileType.Docx);

        Assert.Equal(DocumentTemplateComparison.NotVerified, result.Status);
        var finding = Assert.Single(result.Differences);
        Assert.Equal("TemplateIdentityNotVerified", finding.Code);
        Assert.Contains("identity", finding.MessageEn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("هوية", finding.MessageAr);
    }

    [Fact]
    public void IdentityAwareComparisonReturnsNotVerifiedWhenUploadedSourceTemplateIsAbsent()
    {
        var expected = new DocumentTemplateReference(Guid.NewGuid(), Guid.NewGuid());
        using var template = StreamFor(CreateDocx(["Overview"], "Template content."));
        using var projectDocument = StreamFor(CreateDocx(["Overview"], "Completed project content."));

        var result = DocumentTemplateComparison.Compare(
            template, projectDocument, DocumentFileType.Docx, expected, uploadedSourceTemplate: null);

        Assert.Equal(DocumentTemplateComparison.NotVerified, result.Status);
        var finding = Assert.Single(result.Differences);
        Assert.Equal("TemplateIdentityNotVerified", finding.Code);
        Assert.Equal($"{expected.TemplateId:D}/{expected.VersionId:D}", finding.Expected);
        Assert.Contains("source template and version", finding.MessageEn, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("للقالب المصدري", finding.MessageAr);
    }

    [Fact]
    public void LegacyOfficeFormatsAreNotClaimedAsStructurallyVerified()
    {
        using var template = StreamFor(CreateDocx(["Overview"], "Template content."));
        using var projectDocument = StreamFor(CreateDocx(["Overview"], "Project content."));

        var result = CompareWithMatchingTemplateIdentity(template, projectDocument, DocumentFileType.Doc);

        Assert.Equal(DocumentTemplateComparison.NotVerified, result.Status);
        Assert.Contains(result.Differences, item => item.Code == "StructureNotSupported");
    }

    private static DocumentTemplateComparisonResult CompareWithMatchingTemplateIdentity(
        Stream template,
        Stream projectDocument,
        DocumentFileType type)
    {
        var identity = new DocumentTemplateReference(Guid.NewGuid(), Guid.NewGuid());
        return DocumentTemplateComparison.Compare(template, projectDocument, type, identity, identity);
    }

    private static byte[] CreateDocx(IReadOnlyList<string> headings, string bodyText)
    {
        var paragraphs = string.Concat(headings.Select(heading =>
            "<w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:t>" + Escape(heading) + "</w:t></w:r></w:p>")) +
            "<w:p><w:r><w:t>" + Escape(bodyText) + "</w:t></w:r></w:p>";
        var document = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" + paragraphs + "</w:body></w:document>";
        var contentTypes = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
            "</Types>";
        return CreatePackage(("[Content_Types].xml", contentTypes), ("word/document.xml", document));
    }

    private static byte[] CreateXlsx(IReadOnlyList<Sheet> sheets, bool useSharedStrings = false)
    {
        var sheetDefinitions = new StringBuilder();
        var relationships = new StringBuilder();
        var entries = new List<(string Name, string Content)>();
        var contentTypes = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        contentTypes.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        var rowsBySheet = sheets.Select(sheet => sheet.Rows ?? new[] { sheet.Headers, sheet.Data }).ToList();
        List<string> sharedStringValues = useSharedStrings
            ? rowsBySheet.SelectMany(rows => rows).SelectMany(row => row).Distinct(StringComparer.Ordinal).ToList()
            : [];
        var sharedStringIndices = sharedStringValues.Select((value, index) => (value, index))
            .ToDictionary(item => item.value, item => item.index, StringComparer.Ordinal);

        for (var index = 0; index < sheets.Count; index++)
        {
            var sheet = sheets[index];
            var sheetRows = rowsBySheet[index];
            var relationshipId = "rId" + (index + 1);
            var partName = $"xl/worksheets/sheet{index + 1}.xml";
            sheetDefinitions.Append("<sheet name=\"").Append(Escape(sheet.Name)).Append("\" sheetId=\"")
                .Append(index + 1).Append("\" r:id=\"").Append(relationshipId).Append("\"/>");
            relationships.Append("<Relationship Id=\"").Append(relationshipId)
                .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                .Append(index + 1).Append(".xml\"/>");
            contentTypes.Append("<Override PartName=\"/").Append(partName)
                .Append("\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");

            var worksheet = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
                string.Concat(sheetRows.Select((row, rowIndex) => "<row r=\"" + (rowIndex + 1) + "\">" +
                    BuildCells(row, rowIndex + 1, useSharedStrings ? sharedStringIndices : null) + "</row>")) +
                string.Concat(Enumerable.Range(sheetRows.Count + 1, sheet.EmptyRows).Select(rowIndex => "<row r=\"" + rowIndex + "\"/>")) +
                "</sheetData></worksheet>";
            entries.Add((partName, worksheet));
        }

        if (useSharedStrings)
        {
            var sharedStringXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"" +
                sharedStringValues.Count + "\" uniqueCount=\"" + sharedStringValues.Count + "\">" +
                string.Concat(sharedStringValues.Select(value => "<si><t>" + Escape(value) + "</t></si>")) + "</sst>";
            relationships.Append("<Relationship Id=\"rId").Append(sheets.Count + 1)
                .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
            contentTypes.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
            entries.Add(("xl/sharedStrings.xml", sharedStringXml));
        }

        contentTypes.Append("</Types>");
        var workbook = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
            sheetDefinitions + "</sheets></workbook>";
        var workbookRelationships = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + relationships + "</Relationships>";

        entries.Insert(0, ("[Content_Types].xml", contentTypes.ToString()));
        entries.Insert(1, ("xl/workbook.xml", workbook));
        entries.Insert(2, ("xl/_rels/workbook.xml.rels", workbookRelationships));
        return CreatePackage(entries.ToArray());
    }

    private static string BuildCells(IReadOnlyList<string> values, int row, IReadOnlyDictionary<string, int>? sharedStringIndices = null)
    {
        var result = new StringBuilder();
        for (var index = 0; index < values.Count; index++)
        {
            result.Append("<c r=\"").Append(ColumnName(index + 1)).Append(row).Append("\"");
            if (sharedStringIndices is not null)
            {
                result.Append(" t=\"s\"><v>").Append(sharedStringIndices[values[index]]).Append("</v></c>");
            }
            else
            {
                result.Append(" t=\"inlineStr\"><is><t>").Append(Escape(values[index])).Append("</t></is></c>");
            }
        }

        return result.ToString();
    }

    private static string ColumnName(int column)
    {
        var result = new StringBuilder();
        while (column > 0)
        {
            column--;
            result.Insert(0, (char)('A' + (column % 26)));
            column /= 26;
        }

        return result.ToString();
    }

    private static byte[] CreatePackage(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static MemoryStream StreamFor(byte[] content) => new(content, writable: false);

    private static string Escape(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private sealed record Sheet(
        string Name,
        IReadOnlyList<string> Headers,
        IReadOnlyList<string> Data,
        IReadOnlyList<IReadOnlyList<string>>? Rows = null,
        int EmptyRows = 0);
}
