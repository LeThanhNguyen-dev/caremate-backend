using MomCare.Models;

namespace MomCare.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<ApplicationUser> Users { get; }
    IGenericRepository<ApplicationRole> Roles { get; }
    IGenericRepository<ApplicationUserRole> UserRoles { get; }
    IGenericRepository<RefreshToken> RefreshTokens { get; }
    IGenericRepository<NurseProfile> NurseProfiles { get; }
    IGenericRepository<Document> Documents { get; }
    Task<int> CompleteAsync();
}
