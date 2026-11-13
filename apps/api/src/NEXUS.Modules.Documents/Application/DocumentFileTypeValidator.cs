using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace NEXUS.Modules.Documents.Application;

/// <summary>Concrete file formats recognized for project document template validation.</summary>
public enum DocumentFileType
{
    /// <summary>Content could not be classified as one supported format.</summary>
    Unknown = 0,
    /// <summary>Portable Document Format.</summary>
    Pdf,
    /// <summary>Legacy binary Word document.</summary>
    Doc,
    /// <summary>Open XML Word document.</summary>
    Docx,
    /// <summary>Legacy binary Excel workbook.</summary>
    Xls,
    /// <summary>Open XML Excel workbook.</summary>
    Xlsx,
    /// <summary>Plain text content. This classification does not make TXT an allowed upload type.</summary>
    Txt
}

/// <summary>Invalid or ambiguous content is never represented as a concrete file type.</summary>
/// <param name="IsValid">Whether the content was recognized as a supported, unambiguous format.</param>
/// <param name="DetectedType">The detected format, or <see cref="DocumentFileType.Unknown"/>.</param>
public sealed record DocumentFileTypeDetectionResult(bool IsValid, DocumentFileType DetectedType);

/// <summary>
/// Detects the actual supported document format from its contents. Filename extensions are not
/// accepted by this API and are intentionally not consulted.
/// </summary>
public static class DocumentFileTypeValidator
{
    /// <summary>The existing configured Documents module upload limit.</summary>
    public static long MaximumFileBytes => DocumentUploadLimits.MaximumConfiguredBytes;

    private static readonly byte[] CfbSignature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
    private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-");
    private static readonly byte[] PdfStartXref = Encoding.ASCII.GetBytes("startxref");

    private const uint FreeSector = 0xFFFFFFFF;
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FatSector = 0xFFFFFFFD;
    private const uint NoStream = 0xFFFFFFFF;

    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string WordMainPart = "/word/document.xml";
    private const string ExcelMainPart = "/xl/workbook.xml";
    private const string WordMainContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
    private const string ExcelMainContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private const string WordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string WordStrictNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string ExcelNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string ExcelStrictNamespace = "http://purl.oclc.org/ooxml/spreadsheetml/main";

    /// <summary>
    /// Detects PDF, DOC, DOCX, XLS, XLSX, or plain-text content. The stream must be readable and
    /// seekable; its original position is restored whether detection succeeds or fails.
    /// </summary>
    public static DocumentFileTypeDetectionResult Detect(Stream content)
    {
        if (content is null || !content.CanRead || !content.CanSeek)
        {
            return Invalid();
        }

        long originalPosition;
        long length;
        try
        {
            originalPosition = content.Position;
            length = content.Length;
        }
        catch (Exception exception) when (IsInputException(exception))
        {
            return Invalid();
        }

        if (length <= 0 || length > MaximumFileBytes)
        {
            return Invalid();
        }

        var result = Invalid();
        var restored = false;
        try
        {
            content.Position = 0;
            result = DetectStream(content, length);
        }
        catch (Exception exception) when (IsInputException(exception) || IsFormatException(exception))
        {
            result = Invalid();
        }
        finally
        {
            try
            {
                content.Position = originalPosition;
                restored = true;
            }
            catch (Exception exception) when (IsInputException(exception))
            {
                restored = false;
            }
        }

        return restored ? result : Invalid();
    }

    /// <summary>Only the identical concrete format is compatible; Office generations are distinct.</summary>
    public static bool IsCompatible(DocumentFileType expected, DocumentFileTypeDetectionResult actual) =>
        actual is { IsValid: true } && expected != DocumentFileType.Unknown && actual.DetectedType == expected;

    /// <summary>Returns the user-facing English label for a detected format.</summary>
    public static string GetEnglishLabel(DocumentFileType type) => type switch
    {
        DocumentFileType.Pdf => "PDF",
        DocumentFileType.Doc => "Word 97–2003 (.doc)",
        DocumentFileType.Docx => "Word (.docx)",
        DocumentFileType.Xls => "Excel 97–2003 (.xls)",
        DocumentFileType.Xlsx => "Excel (.xlsx)",
        DocumentFileType.Txt => "Plain text (.txt)",
        _ => "Unknown or unsupported file type"
    };

    /// <summary>Returns the user-facing Arabic label for a detected format.</summary>
    public static string GetArabicLabel(DocumentFileType type) => type switch
    {
        DocumentFileType.Pdf => "PDF",
        DocumentFileType.Doc => "مستند Word 97–2003 (.doc)",
        DocumentFileType.Docx => "مستند Word (.docx)",
        DocumentFileType.Xls => "جدول Excel 97–2003 (.xls)",
        DocumentFileType.Xlsx => "جدول Excel (.xlsx)",
        DocumentFileType.Txt => "نص عادي (.txt)",
        _ => "نوع ملف غير معروف أو غير مدعوم"
    };

    private static DocumentFileTypeDetectionResult DetectStream(Stream content, long length)
    {
        var signature = new byte[8];
        var signatureLength = (int)Math.Min(signature.Length, length);
        if (!TryReadAt(content, 0, signature, 0, signatureLength))
        {
            return Invalid();
        }

        if (signature.AsSpan(0, PdfHeader.Length).SequenceEqual(PdfHeader))
        {
            return IsPdf(content, length, signature) ? Valid(DocumentFileType.Pdf) : Invalid();
        }

        if (signature.AsSpan(0, CfbSignature.Length).SequenceEqual(CfbSignature))
        {
            return DetectCompoundBinary(content, length);
        }

        if (signature[0] == 0x50 && signature[1] == 0x4B && signature[2] == 0x03 && signature[3] == 0x04)
        {
            return DetectOpenXml(content);
        }

        return DetectPlainText(content, length);
    }

    private static DocumentFileTypeDetectionResult DetectPlainText(Stream content, long length)
    {
        var prefix = new byte[4];
        var prefixLength = (int)Math.Min(prefix.Length, length);
        if (!TryReadAt(content, 0, prefix, 0, prefixLength))
        {
            return Invalid();
        }

        if (prefixLength >= 4 &&
            (prefix.AsSpan(0, 4).SequenceEqual(new byte[] { 0x00, 0x00, 0xFE, 0xFF }) ||
             prefix.AsSpan(0, 4).SequenceEqual(new byte[] { 0xFF, 0xFE, 0x00, 0x00 })))
        {
            return Invalid();
        }

        var offset = 0;
        Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        if (prefixLength >= 3 && prefix.AsSpan(0, 3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }))
        {
            offset = 3;
        }
        else if (prefixLength >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE)
        {
            offset = 2;
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
        }
        else if (prefixLength >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF)
        {
            offset = 2;
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
        }

        if (offset >= length)
        {
            return Invalid();
        }

        content.Position = offset;
        using var reader = new StreamReader(content, encoding, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
        var characters = new char[8192];
        var sawCharacter = false;
        int charactersRead;
        while ((charactersRead = reader.Read(characters, 0, characters.Length)) > 0)
        {
            for (var index = 0; index < charactersRead; index++)
            {
                var character = characters[index];
                if (char.IsControl(character) && character is not '\t' and not '\r' and not '\n')
                {
                    return Invalid();
                }
            }

            sawCharacter = true;
        }

        return sawCharacter ? Valid(DocumentFileType.Txt) : Invalid();
    }

    private static bool IsPdf(Stream content, long length, byte[] header)
    {
        if (length < 16 || !header.AsSpan(0, PdfHeader.Length).SequenceEqual(PdfHeader) ||
            (header[5] != (byte)'1' && header[5] != (byte)'2') ||
            header[6] != (byte)'.' || header[7] < (byte)'0' || header[7] > (byte)'9')
        {
            return false;
        }

        var tailLength = (int)Math.Min(4096, length);
        var tail = new byte[tailLength];
        if (!TryReadAt(content, length - tailLength, tail, 0, tail.Length))
        {
            return false;
        }

        var eof = LastIndexOf(tail, Encoding.ASCII.GetBytes("%%EOF"));
        if (eof < 0 || tail.Length - eof > 1024)
        {
            return false;
        }

        for (var index = eof + 5; index < tail.Length; index++)
        {
            if (!IsPdfWhitespace(tail[index]))
            {
                return false;
            }
        }

        var startXref = LastIndexOf(tail, PdfStartXref);
        if (startXref < 0 || startXref >= eof)
        {
            return false;
        }

        var cursor = startXref + PdfStartXref.Length;
        SkipPdfWhitespace(tail, ref cursor);
        var numberStart = cursor;
        while (cursor < tail.Length && tail[cursor] >= (byte)'0' && tail[cursor] <= (byte)'9')
        {
            cursor++;
        }

        var absoluteStartXref = length - tailLength + startXref;
        if (numberStart == cursor || !ulong.TryParse(Encoding.ASCII.GetString(tail, numberStart, cursor - numberStart), out var xrefOffset) || xrefOffset >= (ulong)absoluteStartXref || xrefOffset >= (ulong)length)
        {
            return false;
        }

        var xrefLength = (int)Math.Min(64, length - (long)xrefOffset);
        var xrefHeader = new byte[xrefLength];
        if (!TryReadAt(content, (long)xrefOffset, xrefHeader, 0, xrefHeader.Length))
        {
            return false;
        }

        cursor = 0;
        SkipPdfWhitespace(xrefHeader, ref cursor);
        return StartsWithAscii(xrefHeader, cursor, "xref") || LooksLikeIndirectObjectHeader(xrefHeader, cursor);
    }

    private static DocumentFileTypeDetectionResult DetectOpenXml(Stream content)
    {
        try
        {
            content.Position = 0;
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            if (ContainsMacroPart(archive))
            {
                return Invalid();
            }

            var contentTypes = FindUniqueEntry(archive, "[Content_Types].xml");
            if (contentTypes is null || contentTypes.Length <= 0 || contentTypes.Length > 1024 * 1024)
            {
                return Invalid();
            }

            var declaredTypes = ReadMainContentTypes(contentTypes);
            var wordEntry = FindUniqueEntry(archive, "word/document.xml");
            var excelEntry = FindUniqueEntry(archive, "xl/workbook.xml");
            if (wordEntry is not null && excelEntry is not null)
            {
                return Invalid();
            }

            if (wordEntry is not null && wordEntry.Length > 0 &&
                declaredTypes.TryGetValue(WordMainPart, out var wordContentType) &&
                wordContentType == WordMainContentType &&
                HasExpectedXmlRoot(wordEntry, "document", WordNamespace, WordStrictNamespace))
            {
                return Valid(DocumentFileType.Docx);
            }

            if (excelEntry is not null && excelEntry.Length > 0 &&
                declaredTypes.TryGetValue(ExcelMainPart, out var excelContentType) &&
                excelContentType == ExcelMainContentType &&
                HasExpectedXmlRoot(excelEntry, "workbook", ExcelNamespace, ExcelStrictNamespace))
            {
                return Valid(DocumentFileType.Xlsx);
            }
        }
        catch (Exception exception) when (IsFormatException(exception))
        {
            return Invalid();
        }

        return Invalid();
    }

    private static Dictionary<string, string> ReadMainContentTypes(ZipArchiveEntry entry)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var settings = SafeXmlSettings();
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, settings);
        var rootSeen = false;
        while (reader.Read())
        {
            if (!rootSeen && reader.NodeType == XmlNodeType.Element)
            {
                rootSeen = true;
                if (reader.LocalName != "Types" || reader.NamespaceURI != ContentTypesNamespace)
                {
                    throw new XmlException("Invalid Open XML content types root.");
                }
            }

            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Override" && reader.NamespaceURI == ContentTypesNamespace)
            {
                var partName = reader.GetAttribute("PartName");
                var contentType = reader.GetAttribute("ContentType");
                if (partName is not null && contentType is not null)
                {
                    if (result.ContainsKey(partName))
                    {
                        throw new XmlException("Duplicate Open XML content type declaration.");
                    }

                    result.Add(partName, contentType);
                }
            }
        }

        if (!rootSeen)
        {
            throw new XmlException("Missing Open XML content types root.");
        }

        return result;
    }

    private static bool HasExpectedXmlRoot(ZipArchiveEntry entry, string expectedName, params string[] expectedNamespaces)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, SafeXmlSettings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            return reader.LocalName == expectedName && expectedNamespaces.Contains(reader.NamespaceURI, StringComparer.Ordinal);
        }

        return false;
    }

    private static XmlReaderSettings SafeXmlSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        MaxCharactersFromEntities = 0
    };

    private static ZipArchiveEntry? FindUniqueEntry(ZipArchive archive, string fullName)
    {
        ZipArchiveEntry? found = null;
        foreach (var entry in archive.Entries)
        {
            if (!string.Equals(entry.FullName, fullName, StringComparison.Ordinal))
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidDataException("Duplicate Open XML package part.");
            }

            found = entry;
        }

        return found;
    }

    private static bool ContainsMacroPart(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            var fullName = entry.FullName;
            if (string.IsNullOrEmpty(fullName))
            {
                continue;
            }

            var lastSeparator = fullName.LastIndexOf('/');
            var fileName = lastSeparator >= 0 ? fullName[(lastSeparator + 1)..] : fullName;
            if (string.Equals(fileName, "vbaProject.bin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DocumentFileTypeDetectionResult DetectCompoundBinary(Stream content, long length)
    {
        try
        {
            var header = new byte[512];
            if (length < header.Length || !TryReadAt(content, 0, header, 0, header.Length) ||
                BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28, 2)) != 0xFFFE)
            {
                return Invalid();
            }

            var majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26, 2));
            var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30, 2));
            var miniSectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(32, 2));
            if (majorVersion is not (3 or 4) || sectorShift != (majorVersion == 3 ? 9 : 12) || miniSectorShift != 6)
            {
                return Invalid();
            }

            var sectorSize = 1 << sectorShift;
            if (length % sectorSize != 0)
            {
                return Invalid();
            }

            var sectorCount = checked((int)(length / sectorSize) - 1);
            var fatSectorCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(44, 4)));
            var firstDirectorySector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(48, 4));
            var directorySectorCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(40, 4)));
            var firstDifatSector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(68, 4));
            var difatSectorCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(72, 4)));
            var fatEntriesPerSector = sectorSize / 4;
            var maximumReasonableFatSectors = (sectorCount + fatEntriesPerSector - 1) / fatEntriesPerSector + 1;
            if (sectorCount <= 0 || fatSectorCount <= 0 || fatSectorCount > sectorCount || fatSectorCount > maximumReasonableFatSectors ||
                directorySectorCount < 0 || directorySectorCount > sectorCount || difatSectorCount < 0 || difatSectorCount > sectorCount)
            {
                return Invalid();
            }

            var fatSectorIds = ReadDifat(content, header, sectorSize, sectorCount, fatSectorCount, firstDifatSector, difatSectorCount);
            if (fatSectorIds is null || fatSectorIds.Distinct().Count() != fatSectorIds.Count)
            {
                return Invalid();
            }

            var fat = ReadFat(content, sectorSize, fatSectorIds);
            var directoryChain = ReadRegularChain(firstDirectorySector, fat, sectorCount, null);
            if (directoryChain is null || directoryChain.Count == 0 || (majorVersion == 4 && directoryChain.Count != directorySectorCount))
            {
                return Invalid();
            }

            var directory = new CompoundDirectoryReader(content, directoryChain, sectorSize);
            CompoundDirectoryEntry? root = null;
            for (var index = 0; index < directory.EntryCount; index++)
            {
                if (!directory.TryRead(index, out var entry))
                {
                    return Invalid();
                }

                if (entry.Type == 5)
                {
                    if (root is not null)
                    {
                        return Invalid();
                    }

                    root = entry;
                }
            }

            if (root is null)
            {
                return Invalid();
            }

            var visited = new HashSet<uint>();
            var pending = new Stack<uint>();
            if (root.Value.Child != NoStream)
            {
                pending.Push(root.Value.Child);
            }

            var matchingStreams = new List<CompoundDirectoryEntry>();
            while (pending.Count > 0)
            {
                var index = pending.Pop();
                if (index >= directory.EntryCount || !visited.Add(index) || !directory.TryRead((int)index, out var entry) || entry.Type is not (1 or 2))
                {
                    return Invalid();
                }

                if (entry.Left != NoStream)
                {
                    pending.Push(entry.Left);
                }

                if (entry.Right != NoStream)
                {
                    pending.Push(entry.Right);
                }

                if (entry.Type == 2 &&
                    (string.Equals(entry.Name, "WordDocument", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(entry.Name, "Workbook", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(entry.Name, "Book", StringComparison.OrdinalIgnoreCase)))
                {
                    matchingStreams.Add(entry);
                }
            }

            if (matchingStreams.Count != 1)
            {
                return Invalid();
            }

            var target = matchingStreams[0];
            if (target.Size == 0 || target.Size > (ulong)MaximumFileBytes)
            {
                return Invalid();
            }

            var miniStreamCutoff = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(56, 4));
            var streamIsValid = target.Size < miniStreamCutoff
                ? ValidateMiniStream(content, header, sectorSize, sectorCount, fat, root.Value, target.StartSector, target.Size)
                : ValidateRegularStream(target.StartSector, target.Size, sectorSize, fat, sectorCount);
            var isWordDocument = string.Equals(target.Name, "WordDocument", StringComparison.OrdinalIgnoreCase);
            var officePrefix = new byte[isWordDocument ? 32 : 8];
            if (!streamIsValid
                || !TryReadCompoundStreamPrefix(
                    content, header, sectorSize, sectorCount, fat, root.Value, target, miniStreamCutoff, officePrefix))
            {
                return Invalid();
            }

            if (isWordDocument)
            {
                return HasPlausibleWordFib(officePrefix)
                    ? Valid(DocumentFileType.Doc)
                    : Invalid();
            }

            return HasPlausibleExcelBof(officePrefix, target.Size)
                ? Valid(DocumentFileType.Xls)
                : Invalid();
        }
        catch (Exception exception) when (IsFormatException(exception))
        {
            return Invalid();
        }
    }

    private static List<uint>? ReadDifat(Stream content, byte[] header, int sectorSize, int sectorCount, int fatSectorCount, uint firstDifatSector, int difatSectorCount)
    {
        var result = new List<uint>(fatSectorCount);
        for (var offset = 76; offset < 512; offset += 4)
        {
            var sector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(offset, 4));
            if (sector == FreeSector)
            {
                continue;
            }

            if (sector >= sectorCount)
            {
                return null;
            }

            result.Add(sector);
        }

        var visited = new HashSet<uint>();
        var current = firstDifatSector;
        var entriesPerSector = sectorSize / 4;
        var difatBuffer = new byte[sectorSize];
        for (var sectorIndex = 0; sectorIndex < difatSectorCount; sectorIndex++)
        {
            if (current >= sectorCount || !visited.Add(current) || !TryReadAt(content, GetSectorOffset(current, sectorSize), difatBuffer, 0, difatBuffer.Length))
            {
                return null;
            }

            for (var entryIndex = 0; entryIndex < entriesPerSector - 1; entryIndex++)
            {
                var sector = BinaryPrimitives.ReadUInt32LittleEndian(difatBuffer.AsSpan(entryIndex * 4, 4));
                if (sector == FreeSector)
                {
                    continue;
                }

                if (sector >= sectorCount)
                {
                    return null;
                }

                result.Add(sector);
            }

            current = BinaryPrimitives.ReadUInt32LittleEndian(difatBuffer.AsSpan((entriesPerSector - 1) * 4, 4));
        }

        if ((difatSectorCount == 0 && firstDifatSector != EndOfChain) || (difatSectorCount > 0 && current != EndOfChain) || result.Count < fatSectorCount)
        {
            return null;
        }

        return result.Take(fatSectorCount).ToList();
    }

    private static uint[] ReadFat(Stream content, int sectorSize, IReadOnlyList<uint> fatSectorIds)
    {
        var valuesPerSector = sectorSize / 4;
        var fat = new uint[checked(fatSectorIds.Count * valuesPerSector)];
        var sectorBuffer = new byte[sectorSize];
        for (var sectorIndex = 0; sectorIndex < fatSectorIds.Count; sectorIndex++)
        {
            if (!TryReadAt(content, GetSectorOffset(fatSectorIds[sectorIndex], sectorSize), sectorBuffer, 0, sectorBuffer.Length))
            {
                throw new InvalidDataException("Truncated compound file allocation table.");
            }

            for (var entryIndex = 0; entryIndex < valuesPerSector; entryIndex++)
            {
                fat[(sectorIndex * valuesPerSector) + entryIndex] = BinaryPrimitives.ReadUInt32LittleEndian(sectorBuffer.AsSpan(entryIndex * 4, 4));
            }
        }

        return fat;
    }

    private static List<uint>? ReadRegularChain(uint firstSector, uint[] fat, int sectorCount, int? requiredCount)
    {
        if (firstSector >= sectorCount)
        {
            return null;
        }

        var chain = new List<uint>();
        var visited = new HashSet<uint>();
        var current = firstSector;
        while (current != EndOfChain)
        {
            if (current >= sectorCount || current >= fat.Length || !visited.Add(current))
            {
                return null;
            }

            chain.Add(current);
            if (requiredCount.HasValue && chain.Count > requiredCount.Value)
            {
                return null;
            }

            current = fat[current];
            if (chain.Count > sectorCount)
            {
                return null;
            }
        }

        return requiredCount.HasValue && chain.Count != requiredCount.Value ? null : chain;
    }

    private static bool ValidateRegularStream(uint firstSector, ulong streamSize, int sectorSize, uint[] fat, int sectorCount)
    {
        var requiredCount = checked((int)((streamSize + (ulong)sectorSize - 1) / (ulong)sectorSize));
        return ReadRegularChain(firstSector, fat, sectorCount, requiredCount) is not null;
    }

    private static bool ValidateMiniStream(Stream content, byte[] header, int sectorSize, int sectorCount, uint[] fat, CompoundDirectoryEntry root, uint firstMiniSector, ulong streamSize)
    {
        if (root.Size == 0 || root.Size > (ulong)MaximumFileBytes)
        {
            return false;
        }

        var rootSectorCount = checked((int)((root.Size + (ulong)sectorSize - 1) / (ulong)sectorSize));
        if (ReadRegularChain(root.StartSector, fat, sectorCount, rootSectorCount) is null)
        {
            return false;
        }

        var miniFatSectorCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(64, 4)));
        var firstMiniFatSector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(60, 4));
        if (miniFatSectorCount <= 0)
        {
            return false;
        }

        var miniFatChain = ReadRegularChain(firstMiniFatSector, fat, sectorCount, miniFatSectorCount);
        if (miniFatChain is null)
        {
            return false;
        }

        var miniFat = new MiniFatReader(content, miniFatChain, sectorSize);
        const int miniSectorSize = 64;
        var requiredMiniSectors = checked((int)((streamSize + miniSectorSize - 1) / miniSectorSize));
        var visited = new HashSet<uint>();
        var current = firstMiniSector;
        var miniFatEntryCount = checked(miniFatChain.Count * sectorSize / 4);
        for (var index = 0; index < requiredMiniSectors; index++)
        {
            if (current >= miniFatEntryCount || !visited.Add(current) || ((ulong)current + 1) * miniSectorSize > root.Size)
            {
                return false;
            }

            current = miniFat.Read(current);
        }

        return current == EndOfChain;
    }

    private static bool TryReadCompoundStreamPrefix(
        Stream content,
        byte[] header,
        int sectorSize,
        int sectorCount,
        uint[] fat,
        CompoundDirectoryEntry root,
        CompoundDirectoryEntry target,
        uint miniStreamCutoff,
        byte[] prefix)
    {
        if (target.Size < (ulong)prefix.Length)
        {
            return false;
        }

        if (target.Size >= miniStreamCutoff)
        {
            var requiredSectorCount = checked((int)((target.Size + (ulong)sectorSize - 1) / (ulong)sectorSize));
            var chain = ReadRegularChain(target.StartSector, fat, sectorCount, requiredSectorCount);
            if (chain is null)
            {
                return false;
            }

            var totalRead = 0;
            foreach (var sector in chain)
            {
                var bytesToRead = Math.Min(sectorSize, prefix.Length - totalRead);
                if (bytesToRead <= 0)
                {
                    break;
                }

                if (!TryReadAt(content, GetSectorOffset(sector, sectorSize), prefix, totalRead, bytesToRead))
                {
                    return false;
                }

                totalRead += bytesToRead;
            }

            return totalRead == prefix.Length;
        }

        if (root.Size == 0 || root.Size > (ulong)MaximumFileBytes)
        {
            return false;
        }

        var rootSectorCount = checked((int)((root.Size + (ulong)sectorSize - 1) / (ulong)sectorSize));
        var rootChain = ReadRegularChain(root.StartSector, fat, sectorCount, rootSectorCount);
        var miniFatSectorCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(64, 4)));
        var firstMiniFatSector = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(60, 4));
        if (rootChain is null || miniFatSectorCount <= 0)
        {
            return false;
        }

        var miniFatChain = ReadRegularChain(firstMiniFatSector, fat, sectorCount, miniFatSectorCount);
        if (miniFatChain is null)
        {
            return false;
        }

        var miniFat = new MiniFatReader(content, miniFatChain, sectorSize);
        const int miniSectorSize = 64;
        var miniFatEntryCount = checked(miniFatChain.Count * sectorSize / sizeof(uint));
        var visited = new HashSet<uint>();
        var current = target.StartSector;
        var totalMiniStreamBytes = 0;
        while (totalMiniStreamBytes < prefix.Length)
        {
            var miniSectorOffset = (ulong)current * miniSectorSize;
            var rootSectorIndex = checked((int)(miniSectorOffset / (ulong)sectorSize));
            var offsetWithinSector = checked((int)(miniSectorOffset % (ulong)sectorSize));
            if (current >= miniFatEntryCount
                || !visited.Add(current)
                || miniSectorOffset + miniSectorSize > root.Size
                || rootSectorIndex >= rootChain.Count)
            {
                return false;
            }

            var bytesToRead = Math.Min(miniSectorSize, prefix.Length - totalMiniStreamBytes);
            var physicalOffset = GetSectorOffset(rootChain[rootSectorIndex], sectorSize) + offsetWithinSector;
            if (!TryReadAt(content, physicalOffset, prefix, totalMiniStreamBytes, bytesToRead))
            {
                return false;
            }

            totalMiniStreamBytes += bytesToRead;
            current = miniFat.Read(current);
        }

        return true;
    }

    private static bool HasPlausibleWordFib(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length < 32 || BinaryPrimitives.ReadUInt16LittleEndian(prefix[..2]) != 0xA5EC)
        {
            return false;
        }

        var nFib = BinaryPrimitives.ReadUInt16LittleEndian(prefix.Slice(2, 2));
        return nFib is >= 0x00C1 and <= 0x011C;
    }

    private static bool HasPlausibleExcelBof(ReadOnlySpan<byte> prefix, ulong streamSize)
    {
        if (prefix.Length < 8 || streamSize < 8)
        {
            return false;
        }

        var recordType = BinaryPrimitives.ReadUInt16LittleEndian(prefix[..2]);
        var recordLength = BinaryPrimitives.ReadUInt16LittleEndian(prefix.Slice(2, 2));
        var version = BinaryPrimitives.ReadUInt16LittleEndian(prefix.Slice(4, 2));
        var substreamType = BinaryPrimitives.ReadUInt16LittleEndian(prefix.Slice(6, 2));
        return recordType == 0x0809
            && recordLength >= 4
            && (ulong)recordLength + 4 <= streamSize
            && version is 0x0500 or 0x0600
            && substreamType == 0x0005;
    }

    private static bool TryReadAt(Stream content, long offset, byte[] buffer, int bufferOffset, int count)
    {
        if (offset < 0 || bufferOffset < 0 || count < 0 || bufferOffset + count > buffer.Length)
        {
            return false;
        }

        content.Position = offset;
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = content.Read(buffer, bufferOffset + totalRead, count - totalRead);
            if (read <= 0)
            {
                return false;
            }

            totalRead += read;
        }

        return true;
    }

    private static int GetSectorOffset(uint sectorId, int sectorSize) => checked(sectorSize + (int)sectorId * sectorSize);

    private static int LastIndexOf(byte[] haystack, byte[] needle)
    {
        for (var index = haystack.Length - needle.Length; index >= 0; index--)
        {
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle))
            {
                return index;
            }
        }

        return -1;
    }

    private static void SkipPdfWhitespace(byte[] bytes, ref int index)
    {
        while (index < bytes.Length && IsPdfWhitespace(bytes[index]))
        {
            index++;
        }
    }

    private static bool IsPdfWhitespace(byte value) => value is 0 or 9 or 10 or 12 or 13 or 32;

    private static bool StartsWithAscii(byte[] bytes, int offset, string value)
    {
        var encoded = Encoding.ASCII.GetBytes(value);
        if (offset < 0 || offset + encoded.Length > bytes.Length || !bytes.AsSpan(offset, encoded.Length).SequenceEqual(encoded))
        {
            return false;
        }

        return offset + encoded.Length == bytes.Length || IsPdfWhitespace(bytes[offset + encoded.Length]);
    }

    private static bool LooksLikeIndirectObjectHeader(byte[] bytes, int offset)
    {
        var cursor = offset;
        if (!ConsumePdfDigits(bytes, ref cursor))
        {
            return false;
        }

        SkipPdfWhitespace(bytes, ref cursor);
        if (!ConsumePdfDigits(bytes, ref cursor))
        {
            return false;
        }

        SkipPdfWhitespace(bytes, ref cursor);
        return StartsWithAscii(bytes, cursor, "obj");
    }

    private static bool ConsumePdfDigits(byte[] bytes, ref int cursor)
    {
        var start = cursor;
        while (cursor < bytes.Length && bytes[cursor] >= (byte)'0' && bytes[cursor] <= (byte)'9')
        {
            cursor++;
        }

        return cursor > start;
    }

    private static DocumentFileTypeDetectionResult Valid(DocumentFileType type) => new(true, type);
    private static DocumentFileTypeDetectionResult Invalid() => new(false, DocumentFileType.Unknown);

    private static bool IsInputException(Exception exception) =>
        exception is IOException or NotSupportedException or ObjectDisposedException or UnauthorizedAccessException or ArgumentException;

    private static bool IsFormatException(Exception exception) =>
        exception is IOException or InvalidDataException or XmlException or ArgumentException or OverflowException or IndexOutOfRangeException;

    private readonly record struct CompoundDirectoryEntry(
        byte Type,
        string Name,
        uint Left,
        uint Right,
        uint Child,
        uint StartSector,
        ulong Size);

    private sealed class CompoundDirectoryReader
    {
        private readonly Stream _content;
        private readonly IReadOnlyList<uint> _chain;
        private readonly int _sectorSize;
        private readonly int _entriesPerSector;
        private readonly byte[] _sectorBuffer;
        private int _cachedSectorIndex = -1;

        public CompoundDirectoryReader(Stream content, IReadOnlyList<uint> chain, int sectorSize)
        {
            _content = content;
            _chain = chain;
            _sectorSize = sectorSize;
            _entriesPerSector = sectorSize / 128;
            _sectorBuffer = new byte[sectorSize];
            EntryCount = checked(chain.Count * _entriesPerSector);
        }

        public int EntryCount { get; }

        public bool TryRead(int index, out CompoundDirectoryEntry entry)
        {
            entry = default;
            if (index < 0 || index >= EntryCount)
            {
                return false;
            }

            var sectorIndex = index / _entriesPerSector;
            if (_cachedSectorIndex != sectorIndex)
            {
                if (!TryReadAt(_content, GetSectorOffset(_chain[sectorIndex], _sectorSize), _sectorBuffer, 0, _sectorBuffer.Length))
                {
                    return false;
                }

                _cachedSectorIndex = sectorIndex;
            }

            var entryOffset = (index % _entriesPerSector) * 128;
            var entryBytes = _sectorBuffer.AsSpan(entryOffset, 128);
            var type = entryBytes[66];
            if (type is not (1 or 2 or 5))
            {
                entry = new CompoundDirectoryEntry(type, string.Empty, NoStream, NoStream, NoStream, EndOfChain, 0);
                return true;
            }

            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(entryBytes.Slice(64, 2));
            if (nameLength < 2 || nameLength > 64 || (nameLength & 1) != 0)
            {
                return false;
            }

            entry = new CompoundDirectoryEntry(
                type,
                Encoding.Unicode.GetString(entryBytes.Slice(0, nameLength - 2)),
                BinaryPrimitives.ReadUInt32LittleEndian(entryBytes.Slice(68, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(entryBytes.Slice(72, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(entryBytes.Slice(76, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(entryBytes.Slice(116, 4)),
                BinaryPrimitives.ReadUInt64LittleEndian(entryBytes.Slice(120, 8)));
            return true;
        }
    }

    private sealed class MiniFatReader
    {
        private readonly Stream _content;
        private readonly IReadOnlyList<uint> _chain;
        private readonly int _sectorSize;
        private readonly byte[] _sectorBuffer;
        private int _cachedSectorIndex = -1;

        public MiniFatReader(Stream content, IReadOnlyList<uint> chain, int sectorSize)
        {
            _content = content;
            _chain = chain;
            _sectorSize = sectorSize;
            _sectorBuffer = new byte[sectorSize];
        }

        public uint Read(uint index)
        {
            var byteOffset = checked((int)index * 4);
            var sectorIndex = byteOffset / _sectorSize;
            if (sectorIndex >= _chain.Count)
            {
                throw new InvalidDataException("Compound file mini-FAT index is out of range.");
            }

            if (_cachedSectorIndex != sectorIndex)
            {
                if (!TryReadAt(_content, GetSectorOffset(_chain[sectorIndex], _sectorSize), _sectorBuffer, 0, _sectorBuffer.Length))
                {
                    throw new InvalidDataException("Truncated compound file mini-FAT.");
                }

                _cachedSectorIndex = sectorIndex;
            }

            return BinaryPrimitives.ReadUInt32LittleEndian(_sectorBuffer.AsSpan(byteOffset % _sectorSize, 4));
        }
    }
}
