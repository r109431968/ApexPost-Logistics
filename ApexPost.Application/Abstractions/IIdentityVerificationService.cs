using ApexPost.Domain.Enums;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Decides the result of an identity check. The clerk only supplies the document; the system decides,
/// so a clerk cannot simply mark a check as passed. A real implementation would call an ID provider.
/// </summary>
public interface IIdentityVerificationService
{
    Task<VerificationResult> VerifyAsync(
        DocumentType documentType, string documentNumber, CancellationToken cancellationToken);
}