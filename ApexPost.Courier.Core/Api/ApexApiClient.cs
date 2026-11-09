using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ApexPost.Courier.Core.Resilience;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace ApexPost.Courier.Core.Api;

/// <summary>
/// Typed client for ApexPost.Api (version 2). The HttpClient it receives already has the token handler and the resilience
/// pipeline (retry, circuit breaker, timeouts) in front of it, see <see cref="ApexApiRegistration"/>. This class only turns
/// answers into <see cref="ApiResult{T}"/>: the expected problems (offline, 400, 401, 404, 409, 5xx) are values, not exceptions.
/// </summary>
public sealed class ApexApiClient(HttpClient http, ConnectionStatus connection, ILogger<ApexApiClient> logger)
{
    public Task<ApiResult<SignInResponseDto>> SignInAsync(string userName, string password, CancellationToken cancellationToken = default) =>
        SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "api/v2/auth/token")
            {
                Content = JsonContent.Create(new SignInRequestDto(userName, password), ApexJsonContext.Default.SignInRequestDto)
            },
            ApexJsonContext.Default.SignInResponseDto,
            cancellationToken);

    /// <summary>
    /// Uploads scans of ONE shipment. Safe to repeat: the scan id is the key, so a scan that was already stored is counted
    /// as a duplicate. That is why the retry policy may send it again after a timeout.
    /// </summary>
    public Task<ApiResult<ScanUploadResponseDto>> UploadScansAsync(
        string trackingNumber, string courierId, IReadOnlyList<ScanItemDto> scans, CancellationToken cancellationToken = default) =>
        SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"api/v2/shipments/{Uri.EscapeDataString(trackingNumber)}/scans")
            {
                Content = JsonContent.Create(new ScanUploadRequestDto(courierId, scans), ApexJsonContext.Default.ScanUploadRequestDto)
            },
            ApexJsonContext.Default.ScanUploadResponseDto,
            cancellationToken);

    private async Task<ApiResult<T>> SendAsync<T>(Func<HttpRequestMessage> createRequest, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        try
        {
            using var request = createRequest();
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                connection.RequestSucceeded();
                var value = await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false);
                return value is null
                    ? ApiResult<T>.Failure(new ApiError(ApiFailureKind.Unexpected, (int)response.StatusCode, "The server sent an empty answer."))
                    : ApiResult<T>.Success(value);
            }

            var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            if (error.Kind == ApiFailureKind.Server)
            {
                connection.RequestFailed(error.Title);   // the server answered, but it is broken
            }
            else
            {
                connection.RequestSucceeded();           // 4xx: the server is there, it just said no
            }

            return ApiResult<T>.Failure(error);
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning("The circuit breaker is open; the call was not sent.");
            return Fail<T>(ApiFailureKind.CircuitOpen, "The server is not answering, so calls are paused for a moment.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            return Fail<T>(ApiFailureKind.Timeout, "The server took too long to answer.", ex);
        }
        catch (HttpRequestException ex)
        {
            return Fail<T>(ApiFailureKind.Unreachable, "The server cannot be reached.", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail<T>(ApiFailureKind.Timeout, "The server took too long to answer.", ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "The server sent an answer that could not be read.");
            return ApiResult<T>.Failure(new ApiError(ApiFailureKind.Unexpected, null, "The server sent an answer that could not be read."));
        }
    }

    private ApiResult<T> Fail<T>(ApiFailureKind kind, string title, Exception exception)
    {
        connection.RequestFailed(title);
        logger.LogWarning("{Kind}: {Reason}", kind, exception.GetType().Name);
        return ApiResult<T>.Failure(new ApiError(kind, null, title));
    }

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        ProblemDto? problem = null;
        try
        {
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            {
                problem = await response.Content.ReadFromJsonAsync(ApexJsonContext.Default.ProblemDto, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            // Not a problem document. The status code still says what happened.
        }

        var title = problem?.Title ?? response.ReasonPhrase ?? $"HTTP {status}";
        var kind = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => ApiFailureKind.Validation,
            HttpStatusCode.Unauthorized => ApiFailureKind.Unauthorized,
            HttpStatusCode.Forbidden => ApiFailureKind.Forbidden,
            HttpStatusCode.NotFound => ApiFailureKind.NotFound,
            HttpStatusCode.Conflict => ApiFailureKind.Conflict,
            HttpStatusCode.TooManyRequests => ApiFailureKind.Server,
            HttpStatusCode.RequestTimeout => ApiFailureKind.Timeout,
            >= HttpStatusCode.InternalServerError => ApiFailureKind.Server,
            _ => ApiFailureKind.Unexpected
        };

        return new ApiError(kind, status, title, problem?.Detail, problem?.Errors);
    }
}
