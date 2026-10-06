using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace JobRadar.Infrastructure.Services;

public class DocumentTextExtractionService : IDocumentTextExtractionService
{
    private readonly ILogger<DocumentTextExtractionService> _logger;
    private static readonly XNamespace WordprocessingMLNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public DocumentTextExtractionService(ILogger<DocumentTextExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

        // Ensure seekable memory stream
        MemoryStream memoryStream;
        bool createdNewStream = false;

        if (fileStream is MemoryStream ms && fileStream.CanSeek)
        {
            memoryStream = ms;
            memoryStream.Position = 0;
        }
        else
        {
            memoryStream = new MemoryStream();
            createdNewStream = true;
            await fileStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
        }

        try
        {
            return extension switch
            {
                ".pdf" => ExtractFromPdf(memoryStream),
                ".docx" => await ExtractFromDocxAsync(memoryStream, cancellationToken),
                _ => throw new NotSupportedException($"Unsupported file format '{extension}'. Only .pdf and .docx are supported for text extraction.")
            };
        }
        finally
        {
            if (createdNewStream)
            {
                await memoryStream.DisposeAsync();
            }
        }
    }

    private string ExtractFromPdf(Stream stream)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
            }

            return sb.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF document.");
            throw;
        }
    }

    private async Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null)
            {
                _logger.LogWarning("DOCX archive missing 'word/document.xml' entry.");
                return string.Empty;
            }

            await using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var xmlContent = await reader.ReadToEndAsync(cancellationToken);

            var doc = XDocument.Parse(xmlContent);
            var textElements = doc.Descendants(WordprocessingMLNamespace + "t")
                .Select(node => node.Value)
                .Where(val => !string.IsNullOrWhiteSpace(val));

            return string.Join(" ", textElements).Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from DOCX document.");
            throw;
        }
    }
}
