using MomCare.Data;
using MomCare.Interfaces;
using MomCare.Models;

namespace MomCare.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly MomCareContext _context;
    public IGenericRepository<User> Users { get; private set; }
    public IGenericRepository<Role> Roles { get; private set; }
    public IGenericRepository<UserRole> UserRoles { get; private set; }
    public IGenericRepository<RefreshToken> RefreshTokens { get; private set; }

    public UnitOfWork(MomCareContext context)
    {
        _context = context;
        Users = new GenericRepository<User>(_context);
        Roles = new GenericRepository<Role>(_context);
        UserRoles = new GenericRepository<UserRole>(_context);
        RefreshTokens = new GenericRepository<RefreshToken>(_context);
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
