namespace JobRadar.Application.Abstractions;

public interface IDocumentTextExtractionService
{
    /// <summary>
    /// Extracts plain text from an uploaded document stream (supports PDF and DOCX).
    /// </summary>
    Task<string> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
