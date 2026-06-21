using Microsoft.EntityFrameworkCore;
using TradingService.Application.Common.Interfaces;
using TradingService.Domain.Entities;

namespace TradingService.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly TradingDbContext _dbContext;

    public OutboxRepository(TradingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OrderOutbox outboxRecord, CancellationToken cancellationToken = default) =>
        await _dbContext.OrderOutboxes.AddAsync(outboxRecord, cancellationToken);

    public async Task<IReadOnlyList<OrderOutbox>> GetUnprocessedBatchAsync(
        int batchSize, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.OrderOutboxes
            .Where(o => o.ProcessedAt == null)
            .OrderBy(o => o.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return records;
    }

    public async Task MarkProcessedAsync(Guid outboxId, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.OrderOutboxes.FirstOrDefaultAsync(o => o.Id == outboxId, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.MarkProcessed();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}