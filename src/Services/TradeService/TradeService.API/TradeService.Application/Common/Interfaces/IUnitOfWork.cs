namespace TradingService.Application.Common.Interfaces;

/// <summary>
/// Coordinates a single database transaction across one or more
/// repository writes within a command handler, so that (e.g.) an
/// Order insert and its OrderOutbox record commit or roll back
/// together atomically.
/// </summary>
public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes <paramref name="operation"/> inside a retryable transaction,
    /// satisfying SqlServerRetryingExecutionStrategy's requirement that
    /// user-initiated transactions be wrapped in an execution strategy.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}
