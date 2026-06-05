using PortfolioApi.Common;
using PortfolioApi.DTOs.Auth;

namespace PortfolioApi.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResponseDto>> RegisterAsync(RegisterDto dto);
    Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto);
}
