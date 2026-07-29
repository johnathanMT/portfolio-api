using PortfolioApi.Common;
using PortfolioApi.DTOs.Astrology;

namespace PortfolioApi.Interfaces;

/// <summary>Generates a personalised Vedic reading from a summarised chart, via
/// an OpenAI-compatible chat-completions provider.</summary>
public interface IAiReadingService
{
    Task<ApiResponse<AiReadingResponseDto>> GenerateAsync(AiReadingRequestDto req, CancellationToken ct = default);
}
