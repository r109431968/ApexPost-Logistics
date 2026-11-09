using System.Text.Json.Serialization;

namespace ApexPost.Courier.Core.Api;

// The wire shapes of the parts of ApexPost.Api that the courier app uses (version 2). They are written by hand on purpose:
// the app needs only five types, and a small set of records works with the source-generated JSON context (no reflection).

public sealed record SignInRequestDto(string UserName, string Password);

public sealed record SignInResponseDto(string AccessToken, string TokenType, int ExpiresInSeconds);

/// <summary>ScanType is the text the API expects: Pickup, InTransit, Delivered or Failed.</summary>
public sealed record ScanItemDto(
    string ScanId,
    string ScanType,
    string Location,
    DateTimeOffset ScannedAtUtc,
    string? Notes,
    string? RecipientSignatureName);

public sealed record ScanUploadRequestDto(string CourierId, IReadOnlyList<ScanItemDto> Scans);

public sealed record ScanRejectionDto(string ScanId, string Reason);

public sealed record ScanUploadResponseDto(int Accepted, int DuplicatesSkipped, IReadOnlyList<ScanRejectionDto> Rejected);

/// <summary>RFC 9457 problem details. For a validation error (400), Errors lists the messages per field.</summary>
public sealed record ProblemDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    Dictionary<string, string[]>? Errors);

/// <summary>
/// Source-generated JSON: the serializer code is made at compile time, so there is no reflection at run time.
/// That is faster to start on a phone and safe for trimming and AOT.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SignInRequestDto))]
[JsonSerializable(typeof(SignInResponseDto))]
[JsonSerializable(typeof(ScanUploadRequestDto))]
[JsonSerializable(typeof(ScanUploadResponseDto))]
[JsonSerializable(typeof(ProblemDto))]
public partial class ApexJsonContext : JsonSerializerContext;
