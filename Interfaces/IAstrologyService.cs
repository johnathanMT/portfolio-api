using PortfolioApi.Common;
using PortfolioApi.DTOs.Astrology;

namespace PortfolioApi.Interfaces;

public interface IAstrologyService
{
    /// <summary>Compute a sidereal Rasi (D1) birth chart from birth details.</summary>
    ApiResponse<BirthChartData> ComputeRasiChart(BirthChartRequest req);
}
