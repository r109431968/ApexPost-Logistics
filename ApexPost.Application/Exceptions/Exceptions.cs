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

/// <summary>
/// Another user changed the same data first. The Infrastructure project raises it when the database reports that a
/// row version no longer matches (optimistic concurrency), so the Web layer needs no knowledge of Entity Framework.
/// The web layer answers 409 Conflict.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("Someone else changed this data at the same time. Reload the page and try again.")
    {
    }

    public ConcurrencyConflictException(Exception innerException)
        : base("Someone else changed this data at the same time. Reload the page and try again.", innerException)
    {
    }
}

/// <summary>A unique index in the database rejected the change, for example a staff email that already exists. Answered with 409.</summary>
public sealed class DuplicateValueException : Exception
{
    public DuplicateValueException(Exception innerException)
        : base("A record with the same unique value already exists.", innerException)
    {
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
