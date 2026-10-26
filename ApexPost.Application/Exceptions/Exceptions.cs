using ApexPost.Application.Validation;

namespace ApexPost.Application.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.")
    {
    }
}

public sealed class ValidationFailedException : Exception
{
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public ValidationFailedException(IReadOnlyList<ValidationFailure> failures)
        : base(string.Join(" ", failures.Select(failure => failure.Message)))
    {
        Failures = failures;
    }
}

public class ExternalServiceException : Exception
{
    public ExternalServiceException(string message) : base(message)
    {
    }

    public ExternalServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class ExternalServiceTimeoutException : ExternalServiceException
{
    public ExternalServiceTimeoutException(string serviceName, TimeSpan timeout)
        : base($"{serviceName} did not answer within {timeout.TotalMilliseconds:0} ms.")
    {
    }
}
