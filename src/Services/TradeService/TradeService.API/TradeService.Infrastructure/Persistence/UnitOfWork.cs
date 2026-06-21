using Microsoft.EntityFrameworkCore.Storage;
using TradingService.Application.Common.Interfaces;

namespace TradingService.Infrastructure.Persistence;

/// <summary>
/// Wraps a <see cref="TradingDbContext"/> database transaction so that
/// command handlers can explicitly demarcate "Order insert + Outbox
/// insert must commit or roll back together" without depending on EF
/// Core directly.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork, IAsyncDisposable
{
    private readonly TradingDbContext _dbContext;
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(TradingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            return;
        }

        _currentTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Wraps <paramref name="operation"/> inside a retryable EF Core execution
    /// strategy + explicit transaction. This is required when
    /// SqlServerRetryingExecutionStrategy is configured — EF Core does not allow
    /// user-initiated transactions outside of an execution strategy scope.
    /// </summary>
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        // Execute within the strategy, providing a dummy verification delegate (null) to satisfy the overload.
        await strategy.ExecuteAsync<object, int>(
            null,
            async (dbCtx, state, ct) =>
            {
                await using var transaction = await dbCtx.Database.BeginTransactionAsync(ct);
                try
                {
                    await operation(ct);
                    await dbCtx.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
                return 0; // dummy result
            },
            null,
            cancellationToken);



    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.SaveChangesAsync(cancellationToken);

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            return;
        }

        try
        {
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
        }
    }
}