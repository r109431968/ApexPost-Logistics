using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Client;

// The generated client (Generated/ApexPostClient.g.cs) is never edited. NSwag calls this hook when it builds its JSON settings.
// The API writes enums as text ("Created", "Pickup"), so the client must read and write them as text as well.
public partial class ApexPostClient
{
    static partial void UpdateJsonSerializerSettings(JsonSerializerOptions settings)
    {
        settings.Converters.Add(new JsonStringEnumConverter());
    }
}
