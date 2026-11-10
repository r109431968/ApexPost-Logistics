namespace ApexPost.Api.Composition;

/// <summary>
/// The Swagger UI page for /swagger (Development only). It shows the OpenAPI documents that the built-in generator makes
/// (/openapi/v1.json and /openapi/v2.json); it does not create them. The page only needs the documents, so there is no second generator.
/// The embedded Swagger UI version reads OpenAPI 3.0.0-3.0.3, so the page lowers the "openapi" field of 3.0.4 to 3.0.3 in memory
/// before showing it (the document served by the API is not changed).
/// </summary>
internal static class SwaggerPage
{
    public const string Html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>ApexPost Logistics API</title>
          <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.17.14/swagger-ui.min.css">
          <style>
            body { margin: 0; }
            #bar { padding: 12px 20px; background: #1b1b1b; color: #fff; font-family: sans-serif; display: flex; gap: 12px; align-items: center; }
            #bar button { padding: 6px 14px; border: 1px solid #89bf04; background: transparent; color: #fff; border-radius: 4px; cursor: pointer; }
            #bar button.on { background: #89bf04; color: #000; font-weight: bold; }
          </style>
        </head>
        <body>
          <div id="bar">
            <strong>ApexPost Logistics API</strong>
            <button id="b-v2" onclick="show('v2')">v2</button>
            <button id="b-v1" onclick="show('v1')">v1 (deprecated)</button>
          </div>
          <div id="ui"></div>
          <script src="https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.17.14/swagger-ui-bundle.min.js"></script>
          <script>
            const ui = SwaggerUIBundle({ dom_id: '#ui', docExpansion: 'list', deepLinking: false, persistAuthorization: true });
            async function show(version) {
              const response = await fetch('/openapi/' + version + '.json');
              const spec = await response.json();
              if (spec.openapi === '3.0.4') { spec.openapi = '3.0.3'; }
              ui.specActions.updateSpec(JSON.stringify(spec));
              document.title = 'ApexPost Logistics API ' + version;
              for (const v of ['v1', 'v2']) { document.getElementById('b-' + v).className = v === version ? 'on' : ''; }
            }
            window.onload = () => show('v2');
          </script>
        </body>
        </html>
        """;
}
