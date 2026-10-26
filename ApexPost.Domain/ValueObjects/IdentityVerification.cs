using ApexPost.Domain.Enums;

namespace ApexPost.Domain.ValueObjects;

/// <summary>The full document number is never kept: only "****" plus the last four characters.</summary>
public sealed record IdentityVerification
{
    public DocumentType DocumentType { get; }
    public string MaskedNumber { get; }
    public VerificationResult Result { get; }
    public DateTimeOffset VerifiedAt { get; }

    private IdentityVerification(
        DocumentType documentType, string maskedNumber, VerificationResult result, DateTimeOffset verifiedAt)
    {
        DocumentType = documentType;
        MaskedNumber = maskedNumber;
        Result = result;
        VerifiedAt = verifiedAt;
    }

    public static IdentityVerification Create(
        DocumentType documentType, string documentNumber, VerificationResult result, DateTimeOffset verifiedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);
        var trimmed = documentNumber.Trim();
        if (trimmed.Length < 4)
            throw new ArgumentException("Document number is too short.", nameof(documentNumber));

        return new IdentityVerification(documentType, "****" + trimmed[^4..], result, verifiedAt);
    }
}
