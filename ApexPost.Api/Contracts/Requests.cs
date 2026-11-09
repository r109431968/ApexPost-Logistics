using System.ComponentModel.DataAnnotations;
using ApexPost.Domain.Parcels;

namespace ApexPost.Api.Contracts;

/// <summary>Shared patterns, so the rules are written once.</summary>
internal static class Patterns
{
    public const string TrackingNumber = @"^APX-\d{8}$";

    // Optional +, then digits with spaces, dashes or brackets between them: 7 to 20 characters in total.
    public const string Phone = @"^\+?[0-9][0-9 ()\-]{5,18}[0-9]$";

    public const string ScanId = @"^scan::[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$";
}

/// <summary>Body of POST /api/shipments.</summary>
public sealed class CreateShipmentRequest
{
    /// <summary>Public tracking number, for example <c>APX-12345678</c>.</summary>
    /// <example>APX-12345678</example>
    [Required, RegularExpression(Patterns.TrackingNumber, ErrorMessage = "The tracking number must look like APX-12345678 (APX- and 8 digits).")]
    public string TrackingNumber { get; init; } = "";

    /// <summary>Weight in kilograms, from 0.1 to 70.</summary>
    /// <example>2.5</example>
    [Range(0.1, 70.0, ErrorMessage = "The weight must be between 0.1 and 70 kg.")]
    public decimal WeightKg { get; init; }

    /// <summary>Full name of the person who receives the parcel.</summary>
    /// <example>Asha Verma</example>
    [Required, StringLength(100)]
    public string RecipientName { get; init; } = "";

    /// <summary>Phone number of the recipient.</summary>
    /// <example>+44 20 7946 0000</example>
    [Required, RegularExpression(Patterns.Phone, ErrorMessage = "The phone number is not valid.")]
    public string RecipientPhone { get; init; } = "";

    /// <summary>Street address of the recipient.</summary>
    /// <example>14 Harbour Lane</example>
    [Required, StringLength(200)]
    public string RecipientAddress { get; init; } = "";

    /// <summary>City of the recipient.</summary>
    /// <example>Brightport</example>
    [Required, StringLength(80)]
    public string RecipientCity { get; init; } = "";
}

/// <summary>Body of PUT /api/shipments/{trackingNumber}. The tracking number itself cannot change.</summary>
public sealed class UpdateShipmentRequest
{
    /// <summary>Weight in kilograms, from 0.1 to 70.</summary>
    /// <example>3.2</example>
    [Range(0.1, 70.0, ErrorMessage = "The weight must be between 0.1 and 70 kg.")]
    public decimal WeightKg { get; init; }

    /// <summary>Full name of the person who receives the parcel.</summary>
    /// <example>Asha Verma</example>
    [Required, StringLength(100)]
    public string RecipientName { get; init; } = "";

    /// <summary>Phone number of the recipient.</summary>
    /// <example>+44 20 7946 0000</example>
    [Required, RegularExpression(Patterns.Phone, ErrorMessage = "The phone number is not valid.")]
    public string RecipientPhone { get; init; } = "";

    /// <summary>Street address of the recipient.</summary>
    /// <example>14 Harbour Lane</example>
    [Required, StringLength(200)]
    public string RecipientAddress { get; init; } = "";

    /// <summary>City of the recipient.</summary>
    /// <example>Brightport</example>
    [Required, StringLength(80)]
    public string RecipientCity { get; init; } = "";
}

/// <summary>Body of PATCH /api/shipments/{trackingNumber}/status.</summary>
public sealed class UpdateStatusRequest
{
    /// <summary>The new status, by name (<c>PickedUp</c>) or by number (<c>1</c>).</summary>
    /// <example>PickedUp</example>
    [Required, EnumDataType(typeof(ParcelStatus))]
    public ParcelStatus? Status { get; init; }
}

/// <summary>Body of POST /api/shipments/{trackingNumber}/scans: a batch of scans recorded offline by one courier.</summary>
public sealed class ScanUploadRequest
{
    /// <summary>The courier who recorded the scans.</summary>
    /// <example>courier-007</example>
    [Required, StringLength(40, MinimumLength = 1)]
    public string CourierId { get; init; } = "";

    /// <summary>1 to 100 scans.</summary>
    [Required, MinLength(1, ErrorMessage = "Send at least one scan."), MaxLength(100, ErrorMessage = "Send at most 100 scans per request.")]
    public List<ScanItemRequest> Scans { get; init; } = [];
}

/// <summary>
/// One scan from the courier's offline store (Part A). Custom rule: a Delivered scan must name the person who signed.
/// </summary>
public sealed class ScanItemRequest : IValidatableObject
{
    /// <summary>The id the scan got on the phone, <c>scan::{guid}</c>. Sending the same id again never creates a duplicate.</summary>
    /// <example>scan::3f2504e0-4f89-41d3-9a0c-0305e82c3301</example>
    [Required, RegularExpression(Patterns.ScanId, ErrorMessage = "The scan id must look like scan::{guid}.")]
    public string ScanId { get; init; } = "";

    /// <summary>Pickup, InTransit, Delivered or Failed.</summary>
    /// <example>Pickup</example>
    [Required, EnumDataType(typeof(ParcelScanType))]
    public ParcelScanType? ScanType { get; init; }

    /// <summary>Where the scan happened.</summary>
    /// <example>Brightport depot</example>
    [Required, StringLength(120)]
    public string Location { get; init; } = "";

    /// <summary>When the scan happened (UTC).</summary>
    /// <example>2026-11-10T09:30:00Z</example>
    [Required]
    public DateTimeOffset? ScannedAtUtc { get; init; }

    /// <summary>Free text.</summary>
    [StringLength(500)]
    public string? Notes { get; init; }

    /// <summary>Name of the person who signed. Required for a Delivered scan.</summary>
    /// <example>Asha Verma</example>
    [StringLength(100)]
    public string? RecipientSignatureName { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ScanType == ParcelScanType.Delivered && string.IsNullOrWhiteSpace(RecipientSignatureName))
        {
            yield return new ValidationResult(
                "A Delivered scan needs the name of the person who signed.",
                [nameof(RecipientSignatureName)]);
        }
    }
}

/// <summary>Query string of GET /api/shipments.</summary>
public sealed class ListShipmentsQuery
{
    /// <summary>Page number, starting at 1.</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue, ErrorMessage = "The page must be 1 or more.")]
    public int Page { get; init; } = 1;

    /// <summary>Items per page: 1 to 50 (default 20).</summary>
    /// <example>20</example>
    [Range(1, 50, ErrorMessage = "The page size must be between 1 and 50.")]
    public int PageSize { get; init; } = 20;

    /// <summary>Only shipments in this status.</summary>
    public ParcelStatus? Status { get; init; }

    /// <summary>Only shipments for recipients in this city.</summary>
    [StringLength(80)]
    public string? City { get; init; }
}
