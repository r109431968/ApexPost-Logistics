using System.Collections.Concurrent;
using System.Reflection;

namespace ApexPost.Application.Validation;

public sealed record ValidationFailure(string Member, string Message);

public interface IModelValidator
{
    IReadOnlyList<ValidationFailure> Validate(object model);
}

public sealed class AttributeModelValidator : IModelValidator
{
    private sealed record PropertyRules(PropertyInfo Property, ValidationRuleAttribute[] Rules);

    // Reflection is slow, so each type is inspected once and the result is reused for every later call.
    private static readonly ConcurrentDictionary<Type, PropertyRules[]> Cache = new();

    public static int CachedTypeCount => Cache.Count;

    public IReadOnlyList<ValidationFailure> Validate(object model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var failures = new List<ValidationFailure>();

        foreach (var entry in Cache.GetOrAdd(model.GetType(), Discover))
        {
            var value = entry.Property.GetValue(model);

            foreach (var rule in entry.Rules)
            {
                if (!rule.IsValid(value))
                    failures.Add(new ValidationFailure(entry.Property.Name, rule.MessageFor(entry.Property.Name)));
            }
        }

        return failures;
    }

    private static PropertyRules[] Discover(Type type) => type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => new PropertyRules(
            property,
            property.GetCustomAttributes<ValidationRuleAttribute>(inherit: true).ToArray()))
        .Where(entry => entry.Rules.Length > 0)
        .ToArray();
}
