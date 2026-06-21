using MediatR;
using TradingService.Application.Common.Models;

namespace TradingService.Application.Common.Interfaces
{
    /// <summary>
    /// Marker interface for read-only queries. Inherits from MediatR's <see cref="IRequest{TResponse}"/>.
    /// </summary>
    public interface IQuery<TResponse> : IRequest<Result<TResponse>>
    {
    }
}
