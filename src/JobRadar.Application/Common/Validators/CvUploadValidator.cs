namespace JobRadar.Application.Common.Validators;

public record CvUploadInput(
    Stream Stream,
    string FileName,
    string ContentType,
    long Length
);

public class CvUploadValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private CvUploadValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static CvUploadValidationResult Success() => new(true, null);
    public static CvUploadValidationResult Failure(string errorMessage) => new(false, errorMessage);
}

public interface ICvUploadValidator
{
    Task<CvUploadValidationResult> ValidateAsync(CvUploadInput input, CancellationToken cancellationToken = default);
}

public class CvUploadValidator : ICvUploadValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx" };

    // Magic Bytes signatures
    private static readonly byte[] PdfMagicBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
    private static readonly byte[] DocxMagicBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // "PK\x03\x04" (Zip OpenXML)

    public async Task<CvUploadValidationResult> ValidateAsync(CvUploadInput input, CancellationToken cancellationToken = default)
    {
        if (input.Stream == null || input.Length <= 0)
        {
            return CvUploadValidationResult.Failure("Uploaded file is empty.");
        }

        // 1. File size check (Max 5MB)
        if (input.Length > MaxFileSizeBytes)
        {
            return CvUploadValidationResult.Failure($"File size exceeds maximum allowed limit of 5 MB ({input.Length} bytes provided).");
        }

        // 2. Extension check
        var extension = Path.GetExtension(input.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return CvUploadValidationResult.Failure("Invalid file type. Only PDF (.pdf) and Word documents (.docx) are supported.");
        }

        // 3. MIME type check
        var normalizedContentType = input.ContentType?.Trim().ToLowerInvariant();
        var isValidMime = extension switch
        {
            ".pdf" => normalizedContentType is "application/pdf" or "application/octet-stream",
            ".docx" => normalizedContentType is "application/vnd.openxmlformats-officedocument.wordprocessingml.document" or "application/x-zip-compressed" or "application/octet-stream",
            _ => false
        };

        if (!isValidMime)
        {
            return CvUploadValidationResult.Failure($"Invalid Content-Type header '{input.ContentType}' for extension '{extension}'.");
        }

        // 4. Magic-byte signature header validation (Hard requirement)
        if (!input.Stream.CanRead)
        {
            return CvUploadValidationResult.Failure("File stream is unreadable.");
        }

        long originalPosition = input.Stream.CanSeek ? input.Stream.Position : 0;
        try
        {
            if (input.Stream.CanSeek)
            {
                input.Stream.Seek(0, SeekOrigin.Begin);
            }

            byte[] header = new byte[4];
            int bytesRead = 0;
            while (bytesRead < 4)
            {
                int read = await input.Stream.ReadAsync(header.AsMemory(bytesRead, 4 - bytesRead), cancellationToken);
                if (read == 0) break;
                bytesRead += read;
            }

            if (bytesRead < 4)
            {
                return CvUploadValidationResult.Failure("File is corrupted or too small to verify file header.");
            }

            bool matchesPdf = extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && HeaderMatches(header, PdfMagicBytes);
            bool matchesDocx = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) && HeaderMatches(header, DocxMagicBytes);

            if (!matchesPdf && !matchesDocx)
            {
                return CvUploadValidationResult.Failure("Security validation failed: File content does not match the valid binary magic bytes signature for PDF or DOCX format.");
            }
        }
        finally
        {
            if (input.Stream.CanSeek)
            {
                input.Stream.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        return CvUploadValidationResult.Success();
    }

    private static bool HeaderMatches(byte[] actual, byte[] expected)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i] != expected[i]) return false;
        }
        return true;
    }
}
