using FluentValidation;
using MediatR;
using HostelSystem.Application.Common;

namespace HostelSystem.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .GroupBy(f => f.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.ErrorMessage).ToArray());

        if (failures.Count != 0)
        {
            var resultType = typeof(TResponse);
            var failMethod = resultType == typeof(Result)
                ? typeof(Result).GetMethod("Fail", new[] { typeof(string) })
                : resultType.GetMethod("Fail", new[] { typeof(string) });

            var errorMessage = string.Join("; ", failures.SelectMany(f => f.Value));

            if (failMethod != null)
                return (TResponse)failMethod.Invoke(null, new object[] { errorMessage })!;
        }

        return await next();
    }
}
