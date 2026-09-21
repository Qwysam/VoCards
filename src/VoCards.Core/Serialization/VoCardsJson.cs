using System.Text.Json;
using System.Text.Json.Serialization;
using VoCards.Core.Common;
using VoCards.Core.Models;

namespace VoCards.Core.Serialization;

/// <summary>
/// Source-generated serialization context. Blazor WebAssembly trims aggressively and
/// reflection-based serialization is both slower and fragile under trimming, so every
/// persisted type is registered here and generated at compile time.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LibraryDto))]
[JsonSerializable(typeof(DeckDto))]
[JsonSerializable(typeof(CardDto))]
[JsonSerializable(typeof(DeckSettingsDto))]
[JsonSerializable(typeof(ProfileDto))]
[JsonSerializable(typeof(ReviewDto))]
[JsonSerializable(typeof(IReadOnlyList<DeckDto>))]
internal sealed partial class VoCardsJsonContext : JsonSerializerContext;

/// <summary>
/// The same contract, indented. A separate context rather than a reflection-based
/// options object, so that exporting a file stays trim-safe.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(LibraryDto))]
[JsonSerializable(typeof(DeckDto))]
internal sealed partial class VoCardsJsonIndentedContext : JsonSerializerContext;

/// <summary>
/// Reads and writes libraries and decks as JSON.
///
/// This is the direct replacement for the 2021 <c>WriteToBinaryFile</c> /
/// <c>ReadFromBinaryFile</c> pair. Those used <c>BinaryFormatter</c>, whose
/// <c>Deserialize</c> can construct arbitrary types from attacker-controlled input —
/// the reason it was obsoleted in .NET 5 and removed in .NET 9. JSON is inspectable,
/// diffable, portable between versions, and cannot instantiate arbitrary types.
/// </summary>
public static class VoCardsJson
{
    /// <summary>Serializes a whole library. Pretty-printed output is for file export.</summary>
    public static string Serialize(Library library, bool pretty = false)
    {
        ArgumentNullException.ThrowIfNull(library);

        LibraryDto dto = LibraryMapper.ToDto(library);

        return pretty
            ? JsonSerializer.Serialize(dto, VoCardsJsonIndentedContext.Default.LibraryDto)
            : JsonSerializer.Serialize(dto, VoCardsJsonContext.Default.LibraryDto);
    }

    /// <summary>
    /// Deserializes a library. Returns a failure rather than throwing, because this is
    /// fed by user-supplied files and by whatever is left in browser storage.
    /// </summary>
    public static Result<Library> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<Library>("The file is empty.");
        }

        try
        {
            LibraryDto? dto = JsonSerializer.Deserialize(json, VoCardsJsonContext.Default.LibraryDto);

            if (dto is null)
            {
                return Result.Failure<Library>("That file does not contain a VoCards library.");
            }

            if (dto.Version > Library.SchemaVersion)
            {
                return Result.Failure<Library>(
                    $"That file was written by a newer version of VoCards (format {dto.Version}). Please update.");
            }

            return Result.Success(LibraryMapper.FromDto(dto));
        }
        catch (JsonException ex)
        {
            return Result.Failure<Library>($"That file is not valid JSON: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return Result.Failure<Library>($"That file could not be read: {ex.Message}");
        }
    }

    /// <summary>Serializes a single deck, for sharing one deck rather than a whole library.</summary>
    public static string SerializeDeck(Deck deck, bool pretty = true)
    {
        ArgumentNullException.ThrowIfNull(deck);

        DeckDto dto = LibraryMapper.ToDto(deck);

        return pretty
            ? JsonSerializer.Serialize(dto, VoCardsJsonIndentedContext.Default.DeckDto)
            : JsonSerializer.Serialize(dto, VoCardsJsonContext.Default.DeckDto);
    }

    /// <summary>Reads a single shared deck.</summary>
    public static Result<Deck> DeserializeDeck(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result.Failure<Deck>("The file is empty.");
        }

        try
        {
            DeckDto? dto = JsonSerializer.Deserialize(json, VoCardsJsonContext.Default.DeckDto);

            return dto is null || string.IsNullOrWhiteSpace(dto.Name)
                ? Result.Failure<Deck>("That file does not contain a VoCards deck.")
                : Result.Success(LibraryMapper.FromDto(dto));
        }
        catch (JsonException ex)
        {
            return Result.Failure<Deck>($"That file is not valid JSON: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return Result.Failure<Deck>($"That file could not be read: {ex.Message}");
        }
    }
}
