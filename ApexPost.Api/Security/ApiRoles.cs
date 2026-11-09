namespace ApexPost.Api.Security;

/// <summary>The roles of the API and the claim that ties a courier token to one courier.</summary>
public static class ApiRoles
{
    public const string Courier = "Courier";
    public const string Admin = "Admin";

    /// <summary>The role of a partner that signs in with an API key.</summary>
    public const string Partner = "Partner";

    /// <summary>For [Authorize(Roles = ...)]: either role may call the endpoint.</summary>
    public const string CourierOrAdmin = Courier + "," + Admin;
}

public static class ApiClaims
{
    /// <summary>The courier a Courier token belongs to, for example courier-007. Admin tokens do not carry it.</summary>
    public const string CourierId = "courierId";
}
