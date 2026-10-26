namespace ApexPost.Domain.Enums;

/// <summary>Values start at 1 so the default value 0 is never a valid choice.</summary>
public enum StaffRole
{
    /// <summary>Opens and processes counter transactions at the home branch only.</summary>
    CounterClerk = 1,
    /// <summary>Everything a clerk can do, plus voids and refunds (with a reason).</summary>
    BranchManager = 2,
    /// <summary>Read-only access to reports, money orders and identity verifications.</summary>
    Auditor = 3,
    /// <summary>Manages staff and branches. Does not process sales.</summary>
    Administrator = 4
}

public enum ServiceLevel { Standard = 1, Express = 2, Registered = 3 }

public enum ShipmentType { Parcel = 1, Letter = 2 }

public enum TransactionStatus { Open = 1, Paid = 2, Voided = 3, Refunded = 4 }

public enum PaymentMethod { Cash = 1, Card = 2, Voucher = 3 }

public enum TaxCategory { Standard = 1, Reduced = 2, Exempt = 3 }

public enum ProductCategory { Packaging = 1, Stationery = 2, OfficeSupplies = 3 }

public enum DocumentType { Passport = 1, NationalId = 2, DrivingLicence = 3 }

public enum VerificationResult { Pending = 1, Passed = 2, Failed = 3 }
