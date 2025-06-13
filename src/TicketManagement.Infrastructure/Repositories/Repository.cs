using Microsoft.EntityFrameworkCore;
using TicketManagement.Contracts.Repositories;
using TicketManagement.Infrastructure.Data;

namespace TicketManagement.Infrastructure.Repositories;

public class Repository<TEntity, TKey> : IRepository<TEntity, TKey> where TEntity : class
{
    protected readonly TicketDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public Repository(TicketDbContext context)
    {
        _context = context;
        _dbSet = context.Set<TEntity>();
    }

    public virtual async Task<TEntity?> GetByIdAsync(TKey id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity)
    {
        var entry = await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entry.Entity;
    }

    public virtual async Task<TEntity> UpdateAsync(TEntity entity)
    {
        // Detach any existing tracked entities with the same key
        var existingEntry = _context.ChangeTracker.Entries<TEntity>()
            .FirstOrDefault(e => e.Entity == entity);
        
        if (existingEntry != null)
        {
            // Entity is already being tracked, just save changes
            await _context.SaveChangesAsync();
        }
        else
        {
            // Entity is not tracked, update it
            _dbSet.Update(entity);
            await _context.SaveChangesAsync();
        }
        
        return entity;
    }

    public virtual async Task DeleteAsync(TKey id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public virtual async Task<bool> ExistsAsync(TKey id)
    {
        var entity = await _dbSet.FindAsync(id);
        return entity != null;
    }
}