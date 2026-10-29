using ApexPost.Application.Abstractions;
using ApexPost.Domain.Enums;

namespace ApexPost.Infrastructure.Verification;

/// <summary>
/// Pretends to be an external ID-check provider. Demo rules: a document number starting with FAIL is rejected,
/// one starting with PEND stays pending, anything else passes. The number is never logged or stored here.
/// </summary>
public sealed class SimulatedIdentityVerificationService(int latencyMilliseconds) : IIdentityVerificationService
{
    public SimulatedIdentityVerificationService() : this(100)
    {
    }

    public async Task<VerificationResult> VerifyAsync(
        DocumentType documentType, string documentNumber, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

        if (latencyMilliseconds > 0)
            await Task.Delay(latencyMilliseconds, cancellationToken);

        var number = documentNumber.Trim().ToUpperInvariant();

        if (number.Length < 4 || number.StartsWith("FAIL", StringComparison.Ordinal))
            return VerificationResult.Failed;

        return number.StartsWith("PEND", StringComparison.Ordinal)
            ? VerificationResult.Pending
            : VerificationResult.Passed;
    }
}