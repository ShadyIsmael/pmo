using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NEXUS.Modules.Documents.Application;

/// <summary>Stable identity for the exact reusable template version used for comparison.</summary>
public sealed record DocumentTemplateReference(Guid TemplateId, Guid VersionId);

/// <summary>A localized, structured finding from the advisory template comparison.</summary>
public sealed record DocumentTemplateDifference(
    string Code,
    string MessageEn,
    string MessageAr,
    string? Expected = null,
    string? Detected = null);

/// <summary>Non-blocking structural comparison result; it never controls upload acceptance.</summary>
public sealed record DocumentTemplateComparisonResult(
    string Status,
    string ValidatorVersion,
    IReadOnlyList<DocumentTemplateDifference> Differences);

/// <summary>
/// Compares only stable, explainable document structure. It intentionally ignores document body
/// text, spreadsheet cell values, formatting, and binary bytes so ordinary completion edits do
/// not prevent upload. Findings are advisory and must not be used to block a phase transition.
/// </summary>
public static class DocumentTemplateComparison
{
    public const string ValidatorVersion = "project-document-structure-v1";
    public const string Compliant = "Compliant";
    public const string Mismatch = "Mismatch";
    public const string NotVerified = "NotVerified";

    private const int MaximumZipEntries = 4096;
    private const int MaximumWorksheets = 256;
    private const int MaximumXmlBytes = 16 * 1024 * 1024;
    private const long MaximumXmlCharacters = 16 * 1024 * 1024;
    private const long MaximumTotalDecompressedBytes = 32 * 1024 * 1024;
    private const long MaximumTotalXmlCharactersAcrossComparison = 32 * 1024 * 1024;
    private const long MaximumTotalXmlWorkUnits = 400_000;
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string WordStrictNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string ExcelNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string ExcelStrictNamespace = "http://purl.oclc.org/ooxml/spreadsheetml/main";
    private const string OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string OfficeStrictRelationshipsNamespace = "http://purl.oclc.org/ooxml/officeDocument/relationships";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Compares DOCX and XLSX structures when the upload's content type is supported.</summary>
    public static DocumentTemplateComparisonResult Compare(Stream template, Stream projectDocument, DocumentFileType type) =>
        CompareCore(template, projectDocument, type, expectedTemplate: null, uploadedSourceTemplate: null);

    /// <summary>
    /// Compares the structures and, when supplied, the source-template identity captured for the
    /// project document. A confirmed identity difference is advisory and does not reject upload.
    /// </summary>
    public static DocumentTemplateComparisonResult Compare(
        Stream template,
        Stream projectDocument,
        DocumentFileType type,
        DocumentTemplateReference expectedTemplate,
        DocumentTemplateReference? uploadedSourceTemplate)
    {
        ArgumentNullException.ThrowIfNull(expectedTemplate);
        return CompareCore(template, projectDocument, type, expectedTemplate, uploadedSourceTemplate);
    }

    private static DocumentTemplateComparisonResult CompareCore(
        Stream template,
        Stream projectDocument,
        DocumentFileType type,
        DocumentTemplateReference? expectedTemplate,
        DocumentTemplateReference? uploadedSourceTemplate)
    {
        var differences = new List<DocumentTemplateDifference>();
        var hasConfirmedMismatch = false;
        var cannotVerify = false;
        var budget = new ComparisonBudget();

        if (expectedTemplate is not { } expected || !HasCompleteReference(expected))
        {
            AddNotVerified(differences, "TemplateIdentityNotVerified",
                "The expected template version identity is missing or incomplete, so compliance cannot be confirmed.",
                "بيانات هوية إصدار القالب المتوقع غير موجودة أو غير مكتملة، لذلك لا يمكن تأكيد الامتثال.");
            cannotVerify = true;
        }
        else if (uploadedSourceTemplate is not { } uploaded || !HasCompleteReference(uploaded))
        {
            AddNotVerified(differences, "TemplateIdentityNotVerified",
                "The uploaded document does not contain a complete source template and version reference, so compliance cannot be confirmed.",
                "لا يحتوي المستند المرفوع على مرجع كامل للقالب المصدري وإصداره، لذلك لا يمكن تأكيد الامتثال.",
                FormatReference(expected));
            cannotVerify = true;
        }
        else if (expected != uploaded)
        {
            differences.Add(new DocumentTemplateDifference(
                "TemplateIdentityMismatch",
                "The uploaded document records a different source template or template version.",
                "يسجل المستند المرفوع قالباً مصدرياً أو إصداراً مختلفاً.",
                FormatReference(expected),
                FormatReference(uploaded)));
            hasConfirmedMismatch = true;
        }

        if (type is not (DocumentFileType.Docx or DocumentFileType.Xlsx) ||
            !CanCompareStream(template) || !CanCompareStream(projectDocument))
        {
            AddNotVerified(differences, "StructureNotSupported",
                "This file format or stream cannot be structurally compared by the current validator.",
                "لا يستطيع المدقق الحالي مقارنة بنية هذا النوع من الملفات أو هذا التدفق.");
            return Result(differences, hasConfirmedMismatch, cannotVerify: true);
        }

        var templateType = DocumentFileTypeValidator.Detect(template);
        var projectType = DocumentFileTypeValidator.Detect(projectDocument);
        if (!DocumentFileTypeValidator.IsCompatible(type, templateType) ||
            !DocumentFileTypeValidator.IsCompatible(type, projectType))
        {
            AddNotVerified(differences, "ContentTypeNotVerified",
                "The content signatures do not confirm the requested comparison format.",
                "لا تؤكد بصمات المحتوى نوع الملف المطلوب للمقارنة.");
            return Result(differences, hasConfirmedMismatch, cannotVerify: true);
        }

        var templatePosition = template.Position;
        var projectPosition = projectDocument.Position;
        try
        {
            template.Position = 0;
            projectDocument.Position = 0;
            using var templateArchive = new ZipArchive(template, ZipArchiveMode.Read, leaveOpen: true);
            using var projectArchive = new ZipArchive(projectDocument, ZipArchiveMode.Read, leaveOpen: true);

            if (templateArchive.Entries.Count > MaximumZipEntries || projectArchive.Entries.Count > MaximumZipEntries)
            {
                AddNotVerified(differences, "PackageTooComplex",
                    "The document package exceeds the comparison validator's safe structure limits.",
                    "تتجاوز حزمة المستند الحدود الآمنة للبنية التي يسمح بها المدقق.");
                cannotVerify = true;
            }
            else if (type == DocumentFileType.Docx)
            {
                CompareWord(templateArchive, projectArchive, budget, differences, ref hasConfirmedMismatch, ref cannotVerify);
            }
            else
            {
                CompareWorkbook(templateArchive, projectArchive, budget, differences, ref hasConfirmedMismatch, ref cannotVerify);
            }
        }
        catch (ComparisonBudgetExceededException)
        {
            AddNotVerified(differences, "ComparisonBudgetExceeded",
                "The document packages exceeded the comparison validator's cumulative parsing limits, so the structure could not be verified.",
                "تجاوزت حزم المستندات حدود القراءة التراكمية للمدقق، لذلك تعذر التحقق من البنية.");
            cannotVerify = true;
        }
        catch (Exception exception) when (IsInputException(exception))
        {
            AddNotVerified(differences, "StructureNotVerified",
                "The template structure could not be read reliably, so no confirmed structural conclusion was made.",
                "تعذرت قراءة بنية القالب بشكل موثوق، لذلك لم يتم تأكيد وجود اختلاف بنيوي.");
            cannotVerify = true;
        }
        finally
        {
            RestorePosition(template, templatePosition);
            RestorePosition(projectDocument, projectPosition);
        }

        return Result(differences, hasConfirmedMismatch, cannotVerify);
    }

    private static void CompareWord(
        ZipArchive templateArchive,
        ZipArchive projectArchive,
        ComparisonBudget budget,
        List<DocumentTemplateDifference> differences,
        ref bool hasConfirmedMismatch,
        ref bool cannotVerify)
    {
        var templateDocument = ReadUniqueXml(templateArchive, "word/document.xml", budget, out var templateDocumentAmbiguous);
        var projectDocument = ReadUniqueXml(projectArchive, "word/document.xml", budget, out var projectDocumentAmbiguous);
        if (templateDocument is null || projectDocument is null || templateDocumentAmbiguous || projectDocumentAmbiguous)
        {
            AddNotVerified(differences, "WordStructureNotVerified",
                "The Word document body could not be read as a unique, valid XML part.",
                "تعذرت قراءة محتوى مستند Word كجزء XML صالح وفريد.");
            cannotVerify = true;
            return;
        }

        var templateStyles = ReadWordHeadingStyles(templateArchive, budget, out var templateStylesAmbiguous);
        var projectStyles = ReadWordHeadingStyles(projectArchive, budget, out var projectStylesAmbiguous);
        if (templateStylesAmbiguous || projectStylesAmbiguous)
        {
            AddNotVerified(differences, "WordStylesNotVerified",
                "The Word heading styles are ambiguous, so section comparison is incomplete.",
                "أنماط العناوين في مستند Word غير واضحة، لذلك فمقارنة الأقسام غير مكتملة.");
            cannotVerify = true;
            return;
        }

        var expectedSections = ExtractWordHeadings(templateDocument, templateStyles);
        var detectedSections = ExtractWordHeadings(projectDocument, projectStyles);
        if (expectedSections.Count == 0)
        {
            AddNotVerified(differences, "TemplateSectionsNotFound",
                "No reliably marked template sections were found in the Word template.",
                "لم يتم العثور على أقسام محددة بوضوح في قالب Word.");
            cannotVerify = true;
            return;
        }

        if (detectedSections.Count == 0)
        {
            AddNotVerified(differences, "ProjectSectionsNotFound",
                "No reliably marked sections were found in the uploaded Word document.",
                "لم يتم العثور على أقسام محددة بوضوح في مستند Word المرفوع.");
            cannotVerify = true;
            return;
        }

        CompareNamedStructures(
            expectedSections,
            detectedSections,
            "Section",
            "RequiredSectionMissing",
            "AdditionalSectionDetected",
            "A template section is missing from the uploaded document.",
            "أحد أقسام القالب غير موجود في المستند المرفوع.",
            "The uploaded document contains a section that is not in the template.",
            "يحتوي المستند المرفوع على قسم غير موجود في القالب.",
            differences,
            ref hasConfirmedMismatch);

        if (expectedSections.Count > 1 && detectedSections.Count == expectedSections.Count &&
            expectedSections.Select(NormalizeKey).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(detectedSections.Select(NormalizeKey).OrderBy(value => value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase) &&
            !expectedSections.Select(NormalizeKey).SequenceEqual(detectedSections.Select(NormalizeKey), StringComparer.OrdinalIgnoreCase))
        {
            differences.Add(new DocumentTemplateDifference(
                "SectionOrderChanged",
                "The section order differs from the template.",
                "يختلف ترتيب الأقسام عن القالب.",
                string.Join(", ", expectedSections),
                string.Join(", ", detectedSections)));
            hasConfirmedMismatch = true;
        }
    }

    private static HashSet<string> ReadWordHeadingStyles(ZipArchive archive, ComparisonBudget budget, out bool ambiguous)
    {
        ambiguous = false;
        var styles = ReadUniqueXml(archive, "word/styles.xml", budget, out ambiguous);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ambiguous)
        {
            return result;
        }

        if (styles is null)
        {
            return result;
        }

        foreach (var style in styles.Descendants().Where(element => IsWordElement(element, "style")))
        {
            var styleId = AttributeValue(style, "styleId");
            var nameElement = style.Elements().FirstOrDefault(element => IsWordElement(element, "name"));
            var name = AttributeValue(nameElement, "val");
            var hasOutline = style.Descendants().Any(element => IsWordElement(element, "outlineLvl"));
            if (!string.IsNullOrWhiteSpace(styleId) &&
                (hasOutline || IsHeadingStyleName(styleId) || IsHeadingStyleName(name)))
            {
                result.Add(styleId);
            }
        }

        return result;
    }

    private static List<string> ExtractWordHeadings(XDocument document, HashSet<string> headingStyles)
    {
        var result = new List<string>();
        foreach (var paragraph in document.Descendants().Where(element => IsWordElement(element, "p")))
        {
            var properties = paragraph.Elements().FirstOrDefault(element => IsWordElement(element, "pPr"));
            var style = properties?.Elements().FirstOrDefault(element => IsWordElement(element, "pStyle"));
            var styleId = AttributeValue(style, "val");
            var directOutline = properties?.Elements().Any(element => IsWordElement(element, "outlineLvl")) == true;
            if (!directOutline && (styleId is null || !headingStyles.Contains(styleId) && !IsHeadingStyleName(styleId)))
            {
                continue;
            }

            var text = Normalize(string.Concat(paragraph.Descendants().Where(element =>
                    IsWordElement(element, "t") || IsWordElement(element, "tab") || IsWordElement(element, "br"))
                .Select(element => IsWordElement(element, "t") ? element.Value : " ")));
            if (text.Length > 0)
            {
                result.Add(text);
            }
        }

        return result;
    }

    private static void CompareWorkbook(
        ZipArchive templateArchive,
        ZipArchive projectArchive,
        ComparisonBudget budget,
        List<DocumentTemplateDifference> differences,
        ref bool hasConfirmedMismatch,
        ref bool cannotVerify)
    {
        var expectedSheets = ReadWorkbook(templateArchive, budget, out var expectedStatus);
        var detectedSheets = ReadWorkbook(projectArchive, budget, out var detectedStatus);
        if (expectedSheets is null || detectedSheets is null)
        {
            AddNotVerified(differences, "WorkbookStructureNotVerified",
                expectedStatus ?? detectedStatus ?? "The workbook structure could not be read reliably.",
                "تعذرت قراءة بنية المصنف بشكل موثوق.");
            cannotVerify = true;
            return;
        }

        if (expectedSheets.Count == 0)
        {
            AddNotVerified(differences, "TemplateWorksheetsNotFound",
                "No worksheets were found in the Excel template.",
                "لم يتم العثور على أوراق عمل في قالب Excel.");
            cannotVerify = true;
            return;
        }

        CompareNamedStructures(
            expectedSheets.Select(sheet => sheet.Name).ToList(),
            detectedSheets.Select(sheet => sheet.Name).ToList(),
            "Worksheet",
            "RequiredWorksheetMissing",
            "AdditionalWorksheetDetected",
            "A template worksheet is missing from the uploaded workbook.",
            "إحدى أوراق عمل القالب غير موجودة في المصنف المرفوع.",
            "The uploaded workbook contains a worksheet that is not in the template.",
            "يحتوي المصنف المرفوع على ورقة عمل غير موجودة في القالب.",
            differences,
            ref hasConfirmedMismatch);

        var detectedByName = detectedSheets.ToDictionary(sheet => NormalizeKey(sheet.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var expected in expectedSheets)
        {
            var key = NormalizeKey(expected.Name);
            if (!detectedByName.TryGetValue(key, out var detected))
            {
                continue;
            }

            if (expected.Headers is null)
            {
                AddNotVerified(differences, "TemplateColumnsNotVerified",
                    $"Column headers could not be identified reliably on template worksheet '{expected.Name}'.",
                    $"تعذر تحديد عناوين الأعمدة بوضوح في ورقة القالب '{expected.Name}'.",
                    expected.Name);
                cannotVerify = true;
                continue;
            }

            if (detected.Headers is null)
            {
                AddNotVerified(differences, "ProjectColumnsNotVerified",
                    $"Column headers could not be identified reliably on uploaded worksheet '{detected.Name}'.",
                    $"تعذر تحديد عناوين الأعمدة بوضوح في ورقة العمل المرفوعة '{detected.Name}'.",
                    expected.Name,
                    detected.Name);
                cannotVerify = true;
                continue;
            }

            CompareNamedStructures(
                expected.Headers,
                detected.Headers,
                "Column",
                "RequiredColumnMissing",
                "AdditionalColumnDetected",
                $"A required column is missing from worksheet '{expected.Name}'.",
                $"أحد الأعمدة المطلوبة غير موجود في ورقة العمل '{expected.Name}'.",
                $"Worksheet '{expected.Name}' contains a column that is not in the template.",
                $"تحتوي ورقة العمل '{expected.Name}' على عمود غير موجود في القالب.",
                differences,
                ref hasConfirmedMismatch);

            if (expected.Headers.Count > 1 && detected.Headers.Count > 1 &&
                !expected.Headers.Select(NormalizeKey).SequenceEqual(detected.Headers.Select(NormalizeKey), StringComparer.OrdinalIgnoreCase) &&
                expected.Headers.Select(NormalizeKey).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(detected.Headers.Select(NormalizeKey).OrderBy(value => value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
            {
                differences.Add(new DocumentTemplateDifference(
                    "ColumnOrderChanged",
                    $"The column order on worksheet '{expected.Name}' differs from the template.",
                    $"يختلف ترتيب الأعمدة في ورقة العمل '{expected.Name}' عن القالب.",
                    string.Join(", ", expected.Headers),
                    string.Join(", ", detected.Headers)));
                hasConfirmedMismatch = true;
            }
        }

        var expectedOrder = expectedSheets.Select(sheet => NormalizeKey(sheet.Name)).ToList();
        var detectedOrder = detectedSheets.Select(sheet => NormalizeKey(sheet.Name)).ToList();
        if (expectedOrder.Count > 1 && detectedOrder.Count == expectedOrder.Count &&
            expectedOrder.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(detectedOrder.OrderBy(value => value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase) &&
            !expectedOrder.SequenceEqual(detectedOrder, StringComparer.OrdinalIgnoreCase))
        {
            differences.Add(new DocumentTemplateDifference(
                "WorksheetOrderChanged",
                "The worksheet order differs from the template.",
                "يختلف ترتيب أوراق العمل عن القالب.",
                string.Join(", ", expectedSheets.Select(sheet => sheet.Name)),
                string.Join(", ", detectedSheets.Select(sheet => sheet.Name))));
            hasConfirmedMismatch = true;
        }
    }

    private static List<WorksheetStructure>? ReadWorkbook(ZipArchive archive, ComparisonBudget budget, out string? failure)
    {
        failure = null;
        if (archive.Entries.Count > MaximumZipEntries)
        {
            failure = "The workbook package exceeds the comparison validator's safe structure limits.";
            return null;
        }

        var workbook = ReadUniqueXml(archive, "xl/workbook.xml", budget, out var workbookAmbiguous);
        var relationships = ReadUniqueXml(archive, "xl/_rels/workbook.xml.rels", budget, out var relationshipsAmbiguous);
        if (workbook is null || relationships is null || workbookAmbiguous || relationshipsAmbiguous)
        {
            failure = "A unique workbook definition or relationship part could not be read.";
            return null;
        }

        var relationshipTargets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var relationship in relationships.Descendants().Where(element => element.Name.LocalName == "Relationship" && element.Name.NamespaceName == PackageRelationshipsNamespace))
        {
            var id = (string?)relationship.Attribute("Id");
            var target = (string?)relationship.Attribute("Target");
            var targetMode = (string?)relationship.Attribute("TargetMode");
            var relationshipType = (string?)relationship.Attribute("Type");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(target) ||
                !string.IsNullOrEmpty(targetMode) && string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase) ||
                relationshipType is null || !relationshipType.EndsWith("/worksheet", StringComparison.Ordinal))
            {
                continue;
            }

            if (!relationshipTargets.TryAdd(id, target))
            {
                failure = "The workbook contains duplicate worksheet relationship identifiers.";
                return null;
            }
        }

        var sheetElements = workbook.Descendants().Where(element =>
            (element.Name.NamespaceName == ExcelNamespace || element.Name.NamespaceName == ExcelStrictNamespace) &&
            element.Name.LocalName == "sheet").ToList();
        if (sheetElements.Count > MaximumWorksheets)
        {
            failure = "The workbook exceeds the comparison validator's worksheet limit.";
            return null;
        }

        var sharedStrings = ReadSharedStrings(archive, budget, out var sharedFailure);
        if (sharedFailure is not null)
        {
            failure = sharedFailure;
            return null;
        }

        var result = new List<WorksheetStructure>(sheetElements.Count);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in sheetElements)
        {
            var name = (string?)sheet.Attribute("name");
            var relationshipId = (string?)sheet.Attribute(XName.Get("id", OfficeRelationshipsNamespace)) ??
                (string?)sheet.Attribute(XName.Get("id", OfficeStrictRelationshipsNamespace));
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relationshipId) ||
                !seenNames.Add(NormalizeKey(name)) || !relationshipTargets.TryGetValue(relationshipId, out var target))
            {
                failure = "A worksheet name or relationship could not be resolved uniquely.";
                return null;
            }

            var partName = ResolveWorksheetPart(target);
            if (partName is null)
            {
                failure = "A worksheet relationship points outside the workbook package or uses an unsupported target.";
                return null;
            }

            var worksheet = ReadUniqueXml(archive, partName, budget, out var worksheetAmbiguous);
            if (worksheet is null || worksheetAmbiguous)
            {
                failure = $"Worksheet '{name}' could not be read as a unique, valid XML part.";
                return null;
            }

            var headers = ReadHeaderRow(worksheet, sharedStrings, out var headerFailure);
            if (headerFailure is not null)
            {
                failure = headerFailure;
                return null;
            }

            result.Add(new WorksheetStructure(name, headers));
        }

        return result;
    }

    private static List<string>? ReadHeaderRow(XDocument worksheet, IReadOnlyList<string>? sharedStrings, out string? failure)
    {
        failure = null;

        var rows = worksheet.Descendants().Where(element =>
                (element.Name.NamespaceName == ExcelNamespace || element.Name.NamespaceName == ExcelStrictNamespace) &&
                element.Name.LocalName == "row")
            .Select(row => new HeaderRow(
                (string?)row.Attribute("r"),
                row.Elements().Where(element => element.Name.LocalName == "c").ToList()))
            .Where(row => row.Cells.Count > 0)
            .Take(20)
            .ToList();
        var firstPopulatedRow = rows.FirstOrDefault();
        // Do not guess from the row with the most text: completed data can be wider than headers.
        // Only a unique, textual first row at worksheet row 1 is treated as an identifiable header.
        if (firstPopulatedRow is null ||
            !string.Equals(firstPopulatedRow.Number, "1", StringComparison.Ordinal) ||
            firstPopulatedRow.Cells.Count < 2)
        {
            return null;
        }

        var headerCells = firstPopulatedRow.Cells
            .Select(cell => new HeaderCell(
                ReadColumnIndex((string?)cell.Attribute("r")),
                Normalize(ReadCellText(cell, sharedStrings)),
                (string?)cell.Attribute("t")))
            .Where(cell => cell.Name.Length > 0)
            .ToList();
        if (headerCells.Count < 2 || headerCells.Count != firstPopulatedRow.Cells.Count ||
            headerCells.Any(cell => cell.ColumnIndex == int.MaxValue || cell.Type is not ("s" or "inlineStr")) ||
            headerCells.Select(cell => NormalizeKey(cell.Name)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != headerCells.Count ||
            headerCells.Select(cell => cell.ColumnIndex).Distinct().Count() != headerCells.Count)
        {
            return null;
        }

        var headers = headerCells
            .OrderBy(cell => cell.ColumnIndex)
            .Select(cell => cell.Name)
            .ToList();
        return headers;
    }

    private static List<string>? ReadSharedStrings(ZipArchive archive, ComparisonBudget budget, out string? failure)
    {
        failure = null;
        var entry = FindUniqueEntry(archive, "xl/sharedStrings.xml", out var ambiguous);
        if (ambiguous)
        {
            failure = "The shared-string part is ambiguous.";
            return null;
        }

        if (entry is null)
        {
            return [];
        }

        var document = ReadXml(entry, budget);
        if (document is null)
        {
            failure = "The shared-string part could not be read reliably.";
            return null;
        }

        return document.Descendants().Where(element =>
                (element.Name.NamespaceName == ExcelNamespace || element.Name.NamespaceName == ExcelStrictNamespace) &&
                element.Name.LocalName == "si")
            .Select(item => string.Concat(item.Descendants().Where(text => text.Name.LocalName == "t").Select(text => text.Value)))
            .ToList();
    }

    private static string ReadCellText(XElement cell, IReadOnlyList<string>? sharedStrings)
    {
        var value = cell.Elements().FirstOrDefault(element => element.Name.LocalName == "v")?.Value ?? string.Empty;
        var type = (string?)cell.Attribute("t");
        if (string.Equals(type, "s", StringComparison.Ordinal))
        {
            return int.TryParse(value, out var index) && sharedStrings is not null && index >= 0 && index < sharedStrings.Count
                ? sharedStrings[index]
                : string.Empty;
        }

        if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
        {
            return string.Concat(cell.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value));
        }

        return value;
    }

    private static int ReadColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return int.MaxValue;
        }

        var column = 0;
        foreach (var character in cellReference)
        {
            if (!((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z')))
            {
                break;
            }

            var upper = char.ToUpperInvariant(character);
            column = checked((column * 26) + (upper - 'A' + 1));
        }

        return column;
    }

    private static string? ResolveWorksheetPart(string target)
    {
        if (target.Contains(':') || target.Contains('\\') || target.Contains('%'))
        {
            return null;
        }

        var segments = new List<string>();
        var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        foreach (var segment in path.Split('/'))
        {
            if (string.IsNullOrEmpty(segment) || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count > 0 && segments[0] == "xl" ? string.Join('/', segments) : null;
    }

    private static XDocument? ReadUniqueXml(ZipArchive archive, string partName, ComparisonBudget budget, out bool ambiguous)
    {
        var entry = FindUniqueEntry(archive, partName, out ambiguous);
        return ambiguous || entry is null ? null : ReadXml(entry, budget);
    }

    private static ZipArchiveEntry? FindUniqueEntry(ZipArchive archive, string partName, out bool ambiguous)
    {
        ambiguous = false;
        ZipArchiveEntry? found = null;
        foreach (var entry in archive.Entries)
        {
            if (!string.Equals(entry.FullName, partName, StringComparison.Ordinal))
            {
                continue;
            }

            if (found is not null)
            {
                ambiguous = true;
                return null;
            }

            found = entry;
        }

        return found;
    }

    private static XDocument? ReadXml(ZipArchiveEntry entry, ComparisonBudget budget)
    {
        if (entry.Length <= 0 || entry.Length > MaximumXmlBytes)
        {
            return null;
        }

        using var entryStream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        var chunk = new byte[81920];
        while (true)
        {
            var read = entryStream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                break;
            }

            budget.AddDecompressedBytes(read);

            if (buffer.Length + read > MaximumXmlBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaximumXmlCharacters,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };
        buffer.Position = 0;
        long workUnits = 0;
        using (var preflightReader = XmlReader.Create(buffer, settings))
        {
            while (preflightReader.Read())
            {
                workUnits++;
                budget.AddXmlCharacters(preflightReader.Value.Length);
                if (preflightReader.HasAttributes)
                {
                    while (preflightReader.MoveToNextAttribute())
                    {
                        workUnits++;
                        budget.AddXmlCharacters(preflightReader.Value.Length);
                    }

                    preflightReader.MoveToElement();
                }

                budget.AddWorkUnits(1);
            }
        }

        // XDocument performs a second parse. Charge a matching amount before allocating its tree.
        budget.AddWorkUnits(workUnits);
        buffer.Position = 0;
        using var reader = XmlReader.Create(buffer, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static void CompareNamedStructures(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> detected,
        string structureName,
        string missingCode,
        string additionalCode,
        string missingEn,
        string missingAr,
        string additionalEn,
        string additionalAr,
        List<DocumentTemplateDifference> differences,
        ref bool hasConfirmedMismatch)
    {
        var unmatchedDetected = detected.ToList();
        foreach (var item in expected)
        {
            var match = unmatchedDetected.FindIndex(candidate => string.Equals(NormalizeKey(candidate), NormalizeKey(item), StringComparison.OrdinalIgnoreCase));
            if (match >= 0)
            {
                unmatchedDetected.RemoveAt(match);
                continue;
            }

            differences.Add(new DocumentTemplateDifference(
                missingCode,
                $"{missingEn} Expected {structureName.ToLowerInvariant()}: '{item}'.",
                $"{missingAr} القيمة المتوقعة: '{item}'.",
                item,
                null));
            hasConfirmedMismatch = true;
        }

        var unmatchedExpected = expected.ToList();
        foreach (var item in detected)
        {
            var match = unmatchedExpected.FindIndex(candidate => string.Equals(NormalizeKey(candidate), NormalizeKey(item), StringComparison.OrdinalIgnoreCase));
            if (match >= 0)
            {
                unmatchedExpected.RemoveAt(match);
                continue;
            }

            differences.Add(new DocumentTemplateDifference(
                additionalCode,
                $"{additionalEn} Detected {structureName.ToLowerInvariant()}: '{item}'.",
                $"{additionalAr} القيمة المكتشفة: '{item}'.",
                null,
                item));
            hasConfirmedMismatch = true;
        }
    }

    private static void AddNotVerified(
        List<DocumentTemplateDifference> differences,
        string code,
        string messageEn,
        string messageAr,
        string? expected = null,
        string? detected = null) =>
        differences.Add(new DocumentTemplateDifference(code, messageEn, messageAr, expected, detected));

    private static DocumentTemplateComparisonResult Result(
        IReadOnlyList<DocumentTemplateDifference> differences,
        bool hasConfirmedMismatch,
        bool cannotVerify) =>
        new(hasConfirmedMismatch ? Mismatch : cannotVerify ? NotVerified : Compliant, ValidatorVersion, differences);

    private static string FormatReference(DocumentTemplateReference reference) =>
        $"{reference.TemplateId:D}/{reference.VersionId:D}";

    private static bool HasCompleteReference(DocumentTemplateReference reference) =>
        reference.TemplateId != Guid.Empty && reference.VersionId != Guid.Empty;

    private static bool CanCompareStream(Stream stream) => stream is not null && stream.CanRead && stream.CanSeek;

    private static void RestorePosition(Stream stream, long position)
    {
        try
        {
            stream.Position = position;
        }
        catch (Exception exception) when (IsInputException(exception))
        {
            // Comparison is advisory; a hostile or concurrently disposed stream cannot be restored.
        }
    }

    private static bool IsWordElement(XElement element, string localName) =>
        element.Name.LocalName == localName && (element.Name.NamespaceName == WordNamespace || element.Name.NamespaceName == WordStrictNamespace);

    private static string? AttributeValue(XElement? element, string localName) =>
        element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static bool IsHeadingStyleName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = Normalize(value).Replace(" ", string.Empty, StringComparison.Ordinal);
        return normalized.StartsWith("heading", StringComparison.OrdinalIgnoreCase) &&
               normalized.Length > "heading".Length && char.IsDigit(normalized["heading".Length]);
    }

    private static string Normalize(string value) =>
        Whitespace.Replace(value.Normalize(NormalizationForm.FormKC).Trim(), " ");

    private static string NormalizeKey(string value) => Normalize(value);

    private static bool IsInputException(Exception exception) =>
        exception is IOException or InvalidDataException or XmlException or InvalidOperationException or
            ArgumentException or NotSupportedException or OverflowException or FormatException;

    private sealed record WorksheetStructure(string Name, List<string>? Headers);
    private sealed record HeaderRow(string? Number, List<XElement> Cells);
    private sealed record HeaderCell(int ColumnIndex, string Name, string? Type);

    private sealed class ComparisonBudget
    {
        private long _decompressedBytes;
        private long _xmlCharacters;
        private long _workUnits;

        public void AddDecompressedBytes(long amount)
        {
            if (amount < 0 || amount > MaximumTotalDecompressedBytes - _decompressedBytes)
            {
                throw new ComparisonBudgetExceededException();
            }

            _decompressedBytes += amount;
        }

        public void AddXmlCharacters(long amount)
        {
            if (amount < 0 || amount > MaximumTotalXmlCharactersAcrossComparison - _xmlCharacters)
            {
                throw new ComparisonBudgetExceededException();
            }

            _xmlCharacters += amount;
        }

        public void AddWorkUnits(long amount)
        {
            if (amount < 0 || amount > MaximumTotalXmlWorkUnits - _workUnits)
            {
                throw new ComparisonBudgetExceededException();
            }

            _workUnits += amount;
        }
    }

    private sealed class ComparisonBudgetExceededException : Exception { }
}
