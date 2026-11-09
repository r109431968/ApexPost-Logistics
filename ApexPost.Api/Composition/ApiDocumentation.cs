using Asp.Versioning;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ApexPost.Api.Composition;

/// <summary>
/// Versioning and documentation set-up.
///
/// * Asp.Versioning decides which controller answers /api/v1/... and /api/v2/..., and its API Explorer names the groups "v1" and "v2".
/// * The built-in .NET generator (AddOpenApi) builds one OpenAPI document per group: /openapi/v1.json and /openapi/v2.json.
/// * Swagger UI only displays those documents. There is no second generator (no Swashbuckle.AspNetCore generation).
/// </summary>
public static class ApiDocumentation
{
    public const string TeamEmail = "api-team@apexpost.example";   // fictional address

    public static IServiceCollection AddApexPostVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                // The version is part of the URL (/api/v2/shipments), so it is always explicit.
                options.DefaultApiVersion = new ApiVersion(2, 0);
                options.AssumeDefaultVersionWhenUnspecified = false;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();

                // Each response reports its own version's state: api-supported-versions for v2, api-deprecated-versions for the deprecated v1.
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";          // "v1", "v2"
                options.SubstituteApiVersionInUrl = true;    // the document shows /api/v2/shipments, not /api/v{version}/shipments
            });

        return services;
    }

    public static IServiceCollection AddApexPostOpenApi(this IServiceCollection services)
    {
        // The document name must equal the API Explorer group name; that is how each document gets only its own version's endpoints.
        services.AddOpenApi("v1", options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;   // NSwag reads 3.0 nullable enums correctly
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = CreateInfo("1.0", deprecated: true);
                UnwrapNullableReferences(document);
                return Task.CompletedTask;
            });

            // Every operation of v1 is marked deprecated in the document, so generated clients and Swagger UI show it.
            options.AddOperationTransformer((operation, _, _) =>
            {
                operation.Deprecated = true;
                return Task.CompletedTask;
            });
        });

        services.AddOpenApi("v2", options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;   // NSwag reads 3.0 nullable enums correctly
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = CreateInfo("2.0", deprecated: false);
                UnwrapNullableReferences(document);
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// The generator writes a nullable enum property as "oneOf: [null, $ref]". Generated clients (NSwag) turn that into an empty class.
    /// The properties are validated as required anyway, so the wrapper is removed and the property points straight at the enum.
    /// </summary>
    private static void UnwrapNullableReferences(OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
            return;

        foreach (var schema in document.Components.Schemas.Values.OfType<OpenApiSchema>())
        {
            if (schema.Properties is null)
                continue;

            foreach (var (name, property) in schema.Properties.ToList())
            {
                if (property is not OpenApiSchema { OneOf: { Count: 2 } oneOf })
                    continue;

                var reference = oneOf.OfType<OpenApiSchemaReference>().SingleOrDefault();
                if (reference is not null)
                    schema.Properties[name] = reference;
            }
        }
    }

    private static OpenApiInfo CreateInfo(string version, bool deprecated) => new()
    {
        Title = "ApexPost Logistics API",
        Version = version,
        Description =
            "Track shipments, record courier scans and upload scans that were saved offline.\n\n" +
            (deprecated
                ? "**Version 1 is deprecated.** It stays available while clients move to version 2. See VERSIONING.md for the sunset plan.\n\n"
                : "Version 2 groups the recipient fields, adds the latest scan and spells the status as text.\n\n") +
            "All errors are returned as RFC 9457 problem details (application/problem+json). All data in this API is fictional.",
        Contact = new OpenApiContact
        {
            Name = "ApexPost Platform Team",
            Email = TeamEmail
        }
    };
}
