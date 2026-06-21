using System;
using TradingService.Domain.Enums;

namespace TradingService.Application.Common.Models;

public sealed record OrderFilter(Guid UserId, string? Symbol, OrderStatus? Status, int Page, int PageSize);
