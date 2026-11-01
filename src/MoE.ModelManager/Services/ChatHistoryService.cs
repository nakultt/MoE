using System.Text.Json;
using MoE.ModelManager.Models;

namespace MoE.ModelManager.Services;

/// <summary>
/// Saves each conversation as one JSON file under the user's local application data folder.
/// </summary>
internal sealed class ChatHistoryService : IChatHistoryService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public ChatHistoryService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MoEModelManager",
            "Chats"))
    {
    }

    public ChatHistoryService(string historyDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyDirectory);
        HistoryDirectory = historyDirectory;
    }

    public string HistoryDirectory { get; }

    /// <summary>
    /// Creates a new conversation identifier.
    /// </summary>
    /// <returns>A 32-character lowercase hexadecimal identifier.</returns>
    public static string CreateId() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Determines whether an identifier is safe to use as a history file name.
    /// </summary>
    /// <param name="id">The identifier to check.</param>
    /// <returns><see langword="true"/> when the identifier is a 32-character hexadecimal string.</returns>
    public static bool IsValidId(string? id)
    {
        return id is { Length: 32 } && id.All(char.IsAsciiHexDigit);
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    public async Task<IReadOnlyList<ChatConversationSummary>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(HistoryDirectory))
        {
            return [];
        }

        List<ChatConversationSummary> summaries = [];
        foreach (string path in Directory.EnumerateFiles(HistoryDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChatConversation? conversation = await ReadAsync(path, cancellationToken);
            if (conversation is null || !IsValidId(conversation.Id))
            {
                continue;
            }

            summaries.Add(new ChatConversationSummary
            {
                Id = conversation.Id,
                Title = conversation.Title,
                ModelName = conversation.ModelName,
                UpdatedAt = conversation.UpdatedAt,
            });
        }

        return summaries.OrderByDescending(summary => summary.UpdatedAt).ToList();
    }

    public async Task<ChatConversation?> LoadAsync(string id, CancellationToken cancellationToken)
    {
        string path = GetPath(id);
        return File.Exists(path) ? await ReadAsync(path, cancellationToken) : null;
    }

    public async Task SaveAsync(ChatConversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        string path = GetPath(conversation.Id);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(HistoryDirectory);
            string temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, conversation, SerializerOptions, cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        string path = GetPath(id);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(HistoryDirectory))
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (string path in Directory.EnumerateFiles(HistoryDirectory, "*.json"))
            {
                if (IsValidId(Path.GetFileNameWithoutExtension(path)))
                {
                    File.Delete(path);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<ChatConversation?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<ChatConversation>(stream, SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged or locked file must not hide the rest of the history.
            return null;
        }
    }

    private string GetPath(string id)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException("The conversation identifier is not valid.", nameof(id));
        }

        return Path.Combine(HistoryDirectory, $"{id.ToLowerInvariant()}.json");
    }
}
