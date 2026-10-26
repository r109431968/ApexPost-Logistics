using ApexPost.Application.Validation;
using ApexPost.Domain.Enums;

namespace ApexPost.Application.DTOs;

public sealed class MoneyOrderRequest
{
    [NotBlank, TextLength(80)]
    public string Sender { get; init; } = string.Empty;

    [NotBlank, TextLength(80)]
    public string Recipient { get; init; } = string.Empty;

    [PositiveNumber]
    public decimal Amount { get; init; }
}

public sealed class StaffRequest
{
    [NotBlank, TextLength(100)]
    public string Name { get; init; } = string.Empty;

    [NotBlank, TextLength(150), Pattern(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Email is not valid.")]
    public string Email { get; init; } = string.Empty;

    public StaffRole Role { get; init; }

    [NotBlank, Pattern(@"^[Bb][Rr]\d{3}$", ErrorMessage = "Branch code must look like BR001.")]
    public string HomeBranch { get; init; } = string.Empty;
}
