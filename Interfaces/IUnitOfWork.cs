using MomCare.Models;

namespace MomCare.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<User> Users { get; }
    IGenericRepository<Role> Roles { get; }
    IGenericRepository<UserRole> UserRoles { get; }
    IGenericRepository<RefreshToken> RefreshTokens { get; }
    IGenericRepository<OAuthProvider> OAuthProviders { get; }
    IGenericRepository<NurseProfile> NurseProfiles { get; }
    Task<int> CompleteAsync();
}
