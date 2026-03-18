using MomCare.Data;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly MomCareContext _context;
    public IGenericRepository<ApplicationUser> Users { get; private set; }
    public IGenericRepository<ApplicationRole> Roles { get; private set; }
    public IGenericRepository<ApplicationUserRole> UserRoles { get; private set; }
    public IGenericRepository<RefreshToken> RefreshTokens { get; private set; }
    public IGenericRepository<NurseProfile> NurseProfiles { get; private set; }
    public IGenericRepository<Document> Documents { get; private set; }
    
    public UnitOfWork(MomCareContext context)
    {
        _context = context;
        Users = new GenericRepository<ApplicationUser>(_context);
        Roles = new GenericRepository<ApplicationRole>(_context);
        UserRoles = new GenericRepository<ApplicationUserRole>(_context);
        RefreshTokens = new GenericRepository<RefreshToken>(_context);
        NurseProfiles = new GenericRepository<NurseProfile>(_context);
        Documents = new GenericRepository<Document>(_context);
    }

    public async Task<int> CompleteAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
