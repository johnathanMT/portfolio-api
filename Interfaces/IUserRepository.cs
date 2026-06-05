using PortfolioApi.Models;

namespace PortfolioApi.Interfaces;

public interface IUserRepository
{
    Task<User?>             GetByIdAsync(int id);
    Task<User?>             GetByEmailAsync(string email);
    Task<User?>             GetByUsernameAsync(string username);
    Task<bool>              EmailExistsAsync(string email);
    Task<bool>              UsernameExistsAsync(string username);
    Task<User>              CreateAsync(User user);
    Task<User>              UpdateAsync(User user);
    Task                    DeleteAsync(int id);
}
