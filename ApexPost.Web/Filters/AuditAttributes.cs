namespace ApexPost.Web.Filters;

/// <summary>Marks an action as an important business action that must be written to the audit log.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AuditAttribute(string operation) : Attribute
{
    public string Operation { get; } = operation;
}

/// <summary>
/// For POST actions that are one of several forms on a page: when the model is invalid, go back to the given
/// action with an error message (Post/Redirect/Get) instead of redisplaying a view.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnInvalidModelAttribute(string action, string? controller = null) : Attribute
{
    public string Action { get; } = action;
    public string? Controller { get; } = controller;
}

/// <summary>
/// Lets an action add safe details to its audit entry, for example the masked document type or the amount.
/// Only call this with values that are safe to store: no passwords, full document numbers, API keys or card data.
/// </summary>
public static class AuditNotes
{
    private const string DetailsKey = "ApexPost.Audit.Details";
    private const string ActorKey = "ApexPost.Audit.Actor";

    public static void Add(HttpContext context, string key, string value)
    {
        if (context.Items[DetailsKey] is not Dictionary<string, string> details)
        {
            details = new Dictionary<string, string>();
            context.Items[DetailsKey] = details;
        }

        details[key] = value;
    }

    public static IReadOnlyDictionary<string, string> Read(HttpContext context) =>
        context.Items[DetailsKey] as Dictionary<string, string> ?? new Dictionary<string, string>();

    /// <summary>Used by login, where the user is only known after the action ran.</summary>
    public static void SetActor(HttpContext context, Guid staffId, StaffRole role, string branchCode) =>
        context.Items[ActorKey] = (staffId, role, branchCode);

    public static (Guid StaffId, StaffRole Role, string BranchCode)? ReadActor(HttpContext context) =>
        context.Items[ActorKey] is ValueTuple<Guid, StaffRole, string> actor ? actor : null;
}
