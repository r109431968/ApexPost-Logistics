using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonStorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>Folder that holds the JSON data files. Relative paths are resolved by the web project.</summary>
    public string DataDirectory { get; set; } = "data";
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>Lets other parts of Infrastructure reuse the same serializer settings.</summary>
public static class JsonDefaultsAccessor
{
    public static JsonSerializerOptions Options => JsonDefaults.Options;
}

internal static class JsonFile
{
    public static async Task<List<T>> ReadListAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return [];

        try
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

            var list = await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonDefaults.Options, cancellationToken);
            return list ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"The data file '{Path.GetFileName(path)}' is damaged or has an unexpected format.", ex);
        }
    }

    /// <summary>
    /// Writes to a temporary file first and then swaps it in, so a crash or cancellation
    /// in the middle never leaves the real file half written.
    /// </summary>
    public static async Task WriteListAsync<T>(string path, List<T> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = path + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, items, JsonDefaults.Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            await MoveWithRetryAsync(temporaryPath, path, cancellationToken);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static async Task MoveWithRetryAsync(
    string source, string destination, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < 6 &&
                (ex is UnauthorizedAccessException || ex is IOException))
            {
                await Task.Delay(50 * attempt, cancellationToken);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Nothing more can be done; the original error is the one that matters.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
