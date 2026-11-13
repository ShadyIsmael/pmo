using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using NEXUS.Modules.Documents.Application;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class DocumentFileTypeValidatorTests
{
    [Fact]
    public void DetectsPdfFromContentAndRestoresStreamPosition()
    {
        var bytes = CreatePdf();
        using var stream = new MemoryStream(bytes);
        stream.Position = 3;

        var result = DocumentFileTypeValidator.Detect(stream);

        Assert.True(result.IsValid);
        Assert.Equal(DocumentFileType.Pdf, result.DetectedType);
        Assert.Equal(3, stream.Position);
    }

    [Theory]
    [InlineData(true, false, DocumentFileType.Docx)]
    [InlineData(false, false, DocumentFileType.Xlsx)]
    [InlineData(true, true, DocumentFileType.Docx)]
    [InlineData(false, true, DocumentFileType.Xlsx)]
    public void DetectsOpenXmlPackageTypeFromParts(bool word, bool strict, DocumentFileType expected)
    {
        var result = Detect(CreateOpenXmlPackage(word, strict));

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.DetectedType);
    }

    [Theory]
    [InlineData(true, DocumentFileType.Doc)]
    [InlineData(false, DocumentFileType.Xls)]
    public void DetectsCompoundOfficeTypeFromRootStream(bool word, DocumentFileType expected)
    {
        var result = Detect(CreateCompoundOfficeFile(word ? "WordDocument" : "Workbook"));

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.DetectedType);
    }

    [Theory]
    [InlineData("WordDocument")]
    [InlineData("Workbook")]
    public void RejectsCompoundOfficeNamedStreamWithoutPlausibleOfficePayload(string streamName)
    {
        var result = Detect(CreateCompoundOfficeFile(streamName, validOfficePayload: false));

        Assert.False(result.IsValid);
        Assert.Equal(DocumentFileType.Unknown, result.DetectedType);
    }

    [Fact]
    public void RejectsMismatchedOrSpoofedPackageContentWithoutUsingFilename()
    {
        // A Word package remains DOCX even when its caller supplies an .xlsx filename.
        var result = Detect(CreateOpenXmlPackage(word: true));

        Assert.Equal(DocumentFileType.Docx, result.DetectedType);
        Assert.False(DocumentFileTypeValidator.IsCompatible(DocumentFileType.Xlsx, result));
    }

    [Fact]
    public void DetectsPlainTextAsTxtAndRejectsItForAWordTemplate()
    {
        var result = Detect(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes("Project charter\nCompleted by the project team."));

        Assert.True(result.IsValid);
        Assert.Equal(DocumentFileType.Txt, result.DetectedType);
        Assert.False(DocumentFileTypeValidator.IsCompatible(DocumentFileType.Docx, result));
        Assert.False(DocumentFileTypeValidator.IsCompatible(DocumentFileType.Doc, result));
    }

    [Fact]
    public void DetectsBomMarkedUtf16TextAsTxt()
    {
        var encoded = Encoding.Unicode.GetBytes("Project charter");
        var bytes = new byte[encoded.Length + 2];
        bytes[0] = 0xFF;
        bytes[1] = 0xFE;
        Buffer.BlockCopy(encoded, 0, bytes, 2, encoded.Length);

        var result = Detect(bytes);

        Assert.True(result.IsValid);
        Assert.Equal(DocumentFileType.Txt, result.DetectedType);
    }

    [Theory]
    [InlineData(DocumentFileType.Pdf, DocumentFileType.Pdf, true)]
    [InlineData(DocumentFileType.Doc, DocumentFileType.Doc, true)]
    [InlineData(DocumentFileType.Docx, DocumentFileType.Docx, true)]
    [InlineData(DocumentFileType.Xls, DocumentFileType.Xls, true)]
    [InlineData(DocumentFileType.Xlsx, DocumentFileType.Xlsx, true)]
    [InlineData(DocumentFileType.Doc, DocumentFileType.Docx, false)]
    [InlineData(DocumentFileType.Docx, DocumentFileType.Doc, false)]
    [InlineData(DocumentFileType.Xls, DocumentFileType.Xlsx, false)]
    [InlineData(DocumentFileType.Xlsx, DocumentFileType.Xls, false)]
    [InlineData(DocumentFileType.Unknown, DocumentFileType.Pdf, false)]
    public void CompatibilityRequiresTheSameConcreteFormat(DocumentFileType expected, DocumentFileType detected, bool compatible)
    {
        Assert.Equal(compatible, DocumentFileTypeValidator.IsCompatible(expected, new DocumentFileTypeDetectionResult(true, detected)));
    }

    [Fact]
    public void InvalidDetectionNeverComparesAsCompatible()
    {
        Assert.False(DocumentFileTypeValidator.IsCompatible(
            DocumentFileType.Pdf,
            new DocumentFileTypeDetectionResult(false, DocumentFileType.Pdf)));
    }

    [Fact]
    public void RejectsMalformedAndAmbiguousContent()
    {
        AssertInvalid(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x01, 0x02 });
        AssertInvalid(Encoding.ASCII.GetBytes("%PDF-1.7 malformed"));
        AssertInvalid(new byte[] { 0xC3, 0x28 });
        AssertInvalid(new byte[] { 0x41, 0x00, 0x42 });
        AssertInvalid(CreateCompoundOfficeFile("WordDocument", includeSecondWorkbook: true));
    }

    [Fact]
    public void RejectsUnreadableOrNonSeekableStreams()
    {
        using var nonSeekable = new NonSeekableReadStream(CreatePdf());

        var result = DocumentFileTypeValidator.Detect(nonSeekable);

        Assert.False(result.IsValid);
        Assert.Equal(DocumentFileType.Unknown, result.DetectedType);
    }

    [Fact]
    public void RejectsOversizedInputBeforeReadingIt()
    {
        using var oversized = new LengthOnlyStream(DocumentFileTypeValidator.MaximumFileBytes + 1);

        var result = DocumentFileTypeValidator.Detect(oversized);

        Assert.False(result.IsValid);
        Assert.Equal(DocumentFileType.Unknown, result.DetectedType);
        Assert.Equal(0, oversized.ReadCalls);
    }

    [Fact]
    public void ProvidesEnglishAndArabicLabelsForUnsupportedContent()
    {
        Assert.Equal("Unknown or unsupported file type", DocumentFileTypeValidator.GetEnglishLabel(DocumentFileType.Unknown));
        Assert.Equal("نوع ملف غير معروف أو غير مدعوم", DocumentFileTypeValidator.GetArabicLabel(DocumentFileType.Unknown));
        Assert.Equal("Plain text (.txt)", DocumentFileTypeValidator.GetEnglishLabel(DocumentFileType.Txt));
        Assert.Equal("نص عادي (.txt)", DocumentFileTypeValidator.GetArabicLabel(DocumentFileType.Txt));
    }

    private static DocumentFileTypeDetectionResult Detect(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return DocumentFileTypeValidator.Detect(stream);
    }

    private static void AssertInvalid(byte[] bytes)
    {
        var result = Detect(bytes);
        Assert.False(result.IsValid);
        Assert.Equal(DocumentFileType.Unknown, result.DetectedType);
    }

    private static byte[] CreatePdf()
    {
        const string prefix = "%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n";
        var xrefOffset = Encoding.ASCII.GetByteCount(prefix);
        var pdf = prefix + "xref\n0 2\n0000000000 65535 f \n0000000009 00000 n \n" +
                  "trailer\n<< /Root 1 0 R /Size 2 >>\nstartxref\n" + xrefOffset + "\n%%EOF\n";
        return Encoding.ASCII.GetBytes(pdf);
    }

    private static byte[] CreateOpenXmlPackage(bool word, bool strict = false)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var partName = word ? "word/document.xml" : "xl/workbook.xml";
            var contentType = word
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
            var rootName = word ? "document" : "workbook";
            var rootNamespace = (word, strict) switch
            {
                (true, false) => "http://schemas.openxmlformats.org/wordprocessingml/2006/main",
                (true, true) => "http://purl.oclc.org/ooxml/wordprocessingml/main",
                (false, false) => "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
                _ => "http://purl.oclc.org/ooxml/spreadsheetml/main"
            };

            WriteZipEntry(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                "<Override PartName=\"/" + partName + "\" ContentType=\"" + contentType + "\"/>" +
                "</Types>");
            WriteZipEntry(archive, partName, "<" + rootName + " xmlns=\"" + rootNamespace + "\"/>");
        }

        return stream.ToArray();
    }

    private static byte[] CreateCompoundOfficeFile(
        string streamName,
        bool includeSecondWorkbook = false,
        bool validOfficePayload = true)
    {
        const int sectorSize = 512;
        const int dataSectorStart = 2;
        const int dataSectorCount = 8;
        const int sectorCount = dataSectorStart + dataSectorCount;
        var bytes = new byte[sectorSize * (sectorCount + 1)];
        var header = bytes.AsSpan(0, sectorSize);
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.AsSpan().CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(26, 2), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(28, 2), 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(30, 2), 9);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(32, 2), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(40, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(44, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(48, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(56, 4), 4096);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(60, 4), 0xFFFFFFFE);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(64, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(68, 4), 0xFFFFFFFE);
        BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(72, 4), 0);
        for (var index = 0; index < 109; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.Slice(76 + index * 4, 4), index == 0 ? 1u : 0xFFFFFFFFu);
        }

        var directory = bytes.AsSpan(sectorSize, sectorSize);
        WriteDirectoryEntry(directory.Slice(0, 128), "Root Entry", 5, 0xFFFFFFFE, 0, child: 1);
        WriteDirectoryEntry(directory.Slice(128, 128), streamName, 2, dataSectorStart, dataSectorCount * sectorSize, child: 0xFFFFFFFF);
        if (includeSecondWorkbook)
        {
            // Make the second root entry a sibling of the first, which the directory tree walker sees.
            BinaryPrimitives.WriteUInt32LittleEndian(directory.Slice(128 + 72, 4), 2);
            WriteDirectoryEntry(directory.Slice(256, 128), "Workbook", 2, dataSectorStart, dataSectorCount * sectorSize, child: 0xFFFFFFFF);
        }

        var fat = bytes.AsSpan(sectorSize * 2, sectorSize);
        BinaryPrimitives.WriteUInt32LittleEndian(fat.Slice(0, 4), 0xFFFFFFFE);
        BinaryPrimitives.WriteUInt32LittleEndian(fat.Slice(4, 4), 0xFFFFFFFD);
        for (var index = 0; index < dataSectorCount; index++)
        {
            var sector = (uint)(dataSectorStart + index);
            BinaryPrimitives.WriteUInt32LittleEndian(fat.Slice((int)sector * 4, 4), index == dataSectorCount - 1 ? 0xFFFFFFFEu : sector + 1);
        }

        if (validOfficePayload)
        {
            var payload = bytes.AsSpan(sectorSize + dataSectorStart * sectorSize, 32);
            if (string.Equals(streamName, "WordDocument", StringComparison.OrdinalIgnoreCase))
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload, 0xA5EC);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(2), 0x00C1);
            }
            else if (string.Equals(streamName, "Workbook", StringComparison.OrdinalIgnoreCase)
                || string.Equals(streamName, "Book", StringComparison.OrdinalIgnoreCase))
            {
                BinaryPrimitives.WriteUInt16LittleEndian(payload, 0x0809);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(2), 4);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(4), 0x0600);
                BinaryPrimitives.WriteUInt16LittleEndian(payload.Slice(6), 0x0005);
            }
        }

        return bytes;
    }

    private static void WriteDirectoryEntry(Span<byte> entry, string name, byte type, uint startSector, ulong size, uint child)
    {
        var encodedName = Encoding.Unicode.GetBytes(name + "\0");
        encodedName.CopyTo(entry);
        BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(64, 2), (ushort)encodedName.Length);
        entry[66] = type;
        entry[67] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(68, 4), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(72, 4), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(76, 4), child);
        BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(116, 4), startSector);
        BinaryPrimitives.WriteUInt64LittleEndian(entry.Slice(120, 8), size);
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private sealed class NonSeekableReadStream : MemoryStream
    {
        public NonSeekableReadStream(byte[] content) : base(content, writable: false) { }
        public override bool CanSeek => false;
    }

    private sealed class LengthOnlyStream : Stream
    {
        private readonly long _length;

        public LengthOnlyStream(long length) => _length = length;
        public int ReadCalls { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get; set; }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
