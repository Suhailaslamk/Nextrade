using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using TradingService.Application.Common.Models;

namespace TradingService.Application.Common.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (_validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);
                var failures = _validators
                    .Select(v => v.Validate(context))
                    .SelectMany(r => r.Errors)
                    .Where(f => f != null)
                    .ToList();

                if (failures.Count != 0)
                {
                    var error = Error.Validation("validation", string.Join(", ", failures.Select(f => f!.ErrorMessage)));
                    if (typeof(TResponse).IsGenericType && typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                    {
                        var failureResult = typeof(Result<>).MakeGenericType(typeof(TResponse).GetGenericArguments()[0])
                                                         .GetMethod("Failure")!
                                                         .Invoke(null, new object[] { error });
                        return (TResponse)failureResult!;
                    }
                    if (typeof(TResponse) == typeof(Result))
                        return (TResponse)(object)Result.Failure(error);
                }
            }
            return await next();
        }
    }
}
