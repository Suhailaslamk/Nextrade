using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using TradingService.Application.Common.Models;

namespace TradingService.Application.Common.Behaviors
{
    public class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;
        public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger) => _logger = logger;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for request {RequestType}", typeof(TRequest).Name);
                if (typeof(TResponse) == typeof(Result))
                    return (TResponse)(object)Result.Failure(Error.Failure("unhandled", ex.Message));
                if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var error = Error.Failure("unhandled", ex.Message);
                    var failure = typeof(Result<>).MakeGenericType(typeof(TResponse).GetGenericArguments()[0])
                                         .GetMethod("Failure")!
                                         .Invoke(null, new object[] { error });
                    return (TResponse)failure!;
                }
                throw;
            }
        }
    }
}
