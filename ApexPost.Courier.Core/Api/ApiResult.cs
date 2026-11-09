namespace ApexPost.Courier.Core.Api;

public enum ApiFailureKind
{
    /// <summary>No network, or the server did not answer (connection refused, DNS, TLS).</summary>
    Unreachable,

    /// <summary>The circuit breaker is open: recent calls failed, so this call was not even tried.</summary>
    CircuitOpen,

    /// <summary>The attempt or the whole call took too long.</summary>
    Timeout,

    /// <summary>The server is overloaded or broken (5xx) or asks to slow down (429).</summary>
    Server,

    /// <summary>401: no token, or the token expired or is wrong.</summary>
    Unauthorized,

    /// <summary>403: signed in, but not allowed (for example another courier's id).</summary>
    Forbidden,

    /// <summary>400: the request is wrong. Sending it again will not help.</summary>
    Validation,

    /// <summary>404: the shipment does not exist.</summary>
    NotFound,

    /// <summary>409: the request conflicts with the state of the shipment.</summary>
    Conflict,

    /// <summary>Anything else.</summary>
    Unexpected
}

/// <summary>What went wrong, in a form the app can act on and show.</summary>
public sealed record ApiError(
    ApiFailureKind Kind,
    int? StatusCode,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    /// <summary>True when trying again later can work: offline, slow, or a broken server. False when the answer is a decision.</summary>
    public bool IsTransient => Kind is ApiFailureKind.Unreachable or ApiFailureKind.CircuitOpen or ApiFailureKind.Timeout or ApiFailureKind.Server;

    /// <summary>One line for the screen: the title, the detail and the first validation messages.</summary>
    public string Message
    {
        get
        {
            var parts = new List<string> { Title };
            if (!string.IsNullOrWhiteSpace(Detail) && !string.Equals(Detail, Title, StringComparison.Ordinal))
            {
                parts.Add(Detail);
            }

            if (ValidationErrors is { Count: > 0 })
            {
                parts.AddRange(ValidationErrors.Values.SelectMany(messages => messages).Take(3));
            }

            return string.Join(" ", parts);
        }
    }
}

/// <summary>The value, or the error. The client returns this instead of throwing for expected problems such as being offline.</summary>
public sealed class ApiResult<T>
{
    private ApiResult(T? value, ApiError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }
    public ApiError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ApiResult<T> Success(T value) => new(value, null);

    public static ApiResult<T> Failure(ApiError error) => new(default, error);
}
