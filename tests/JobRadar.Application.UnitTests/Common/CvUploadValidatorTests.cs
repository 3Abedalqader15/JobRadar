using System.Text;
using JobRadar.Application.Common.Validators;
using Xunit;

namespace JobRadar.Application.UnitTests.Common;

public class CvUploadValidatorTests
{
    private readonly CvUploadValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_ValidPdfFile_ReturnsSuccess()
    {
        // Arrange: Header %PDF (0x25, 0x50, 0x44, 0x46)
        byte[] content = Encoding.ASCII.GetBytes("%PDF-1.7 sample pdf content");
        using var stream = new MemoryStream(content);
        var input = new CvUploadInput(stream, "resume.pdf", "application/pdf", content.Length);

        // Act
        var result = await _validator.ValidateAsync(input);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_ValidDocxFile_ReturnsSuccess()
    {
        // Arrange: Header PK\x03\x04 (0x50, 0x4B, 0x03, 0x04)
        byte[] content = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 };
        using var stream = new MemoryStream(content);
        var input = new CvUploadInput(stream, "my_cv.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", content.Length);

        // Act
        var result = await _validator.ValidateAsync(input);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_FileExceeds5MB_ReturnsFailure()
    {
        // Arrange
        byte[] content = Encoding.ASCII.GetBytes("%PDF-1.7 dummy sample");
        using var stream = new MemoryStream(content);
        long oversized = (5 * 1024 * 1024) + 1; // 5MB + 1 byte
        var input = new CvUploadInput(stream, "large.pdf", "application/pdf", oversized);

        // Act
        var result = await _validator.ValidateAsync(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("exceeds maximum allowed limit of 5 MB", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_DisallowedExtension_ReturnsFailure()
    {
        // Arrange
        byte[] content = Encoding.ASCII.GetBytes("binary executable content");
        using var stream = new MemoryStream(content);
        var input = new CvUploadInput(stream, "hacker.exe", "application/octet-stream", content.Length);

        // Act
        var result = await _validator.ValidateAsync(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Only PDF (.pdf) and Word documents (.docx) are supported", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_SpoofedMagicBytesHeader_ReturnsSecurityFailure()
    {
        // Arrange: Name is "cv.pdf" but magic bytes are NOT %PDF (e.g., text or MZ header)
        byte[] content = Encoding.ASCII.GetBytes("NOT A REAL PDF FILE HEADER");
        using var stream = new MemoryStream(content);
        var input = new CvUploadInput(stream, "cv.pdf", "application/pdf", content.Length);

        // Act
        var result = await _validator.ValidateAsync(input);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Security validation failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_StreamPositionIsPreservedAfterValidation()
    {
        // Arrange
        byte[] content = Encoding.ASCII.GetBytes("%PDF-1.7 sample pdf content");
        using var stream = new MemoryStream(content);
        var input = new CvUploadInput(stream, "resume.pdf", "application/pdf", content.Length);

        // Act
        await _validator.ValidateAsync(input);

        // Assert: Stream position must be 0 after validation
        Assert.Equal(0, stream.Position);
    }
}
