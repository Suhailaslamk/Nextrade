using System;
using TradingService.Application.Common.Interfaces;

namespace TradingService.Application.Features.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, Guid UserId) : ICommand;