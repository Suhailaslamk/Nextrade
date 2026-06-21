using MediatR;
using TradingService.Application.Common.Models;

namespace TradingService.Application.Common.Interfaces
{
    /// <summary>
    /// Marker interface for commands. Returns a non-generic <see cref="Result"/>.
    /// </summary>
    public interface ICommand : IRequest<Result>
    {
    }

    /// <summary>
    /// Marker interface for commands that return a result. Returns a generic <see cref="Result{T}"/>.
    /// </summary>
    public interface ICommand<TResponse> : IRequest<Result<TResponse>>
    {
    }
}
