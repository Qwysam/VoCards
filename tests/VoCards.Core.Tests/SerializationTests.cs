using VoCards.Core.Common;
using VoCards.Core.Models;
using VoCards.Core.Scheduling;
using VoCards.Core.Serialization;

namespace VoCards.Core.Tests;

public class JsonRoundTripTests
{
    private static Library Populated()
    {
        Library library = Seed.SeedLibrary.Create();
        Deck deck = library[0];

        // Give it some genuine study history so scheduling state is non-trivial.
        DateTimeOffset now = TestData.Epoch;
        for (int i = 0; i < 5; i++)
        {
            library.History.Add(CardReviewer.Apply(
                deck[i], deck, i % 3 == 0 ? Rating.Again : Rating.Good, now.AddMinutes(i)));
            library.Profile.RecordReview();
        }

        deck[0].AddTag("tricky");
        deck[1].Star();
        deck[2].Suspend();
        deck[3].Edit(example: "The cat sat.", notes: "мужской род", pronunciation: "/kæt/");

        library.Profile.AwardXp(1234);
        library.Profile.RegisterStudyDay(new DateOnly(2026, 3, 14));
        library.Profile.Unlock("first-steps", TestData.Epoch);
        library.Profile.DisplayName = "Vsevolod";
        library.Profile.Accent = "aurora";
        library.Profile.DailyGoal = 42;

        return library;
    }

    [Fact]
    public void Library_SurvivesARoundTrip()
    {
        Library original = Populated();

        string json = VoCardsJson.Serialize(original);
        Result<Library> restored = VoCardsJson.Deserialize(json);

        Assert.True(restored.IsSuccess, restored.Error);
        Library copy = restored.Value!;

        Assert.Equal(original.DeckCount, copy.DeckCount);
        Assert.Equal(original.TotalCards, copy.TotalCards);
        Assert.Equal(original.History.Count, copy.History.Count);
        Assert.Equal(original.AllTags.Count, copy.AllTags.Count);
    }

    [Fact]
    public void CardSchedulingState_SurvivesARoundTrip()
    {
        Library original = Populated();
        Card source = original[0][0];

        Library copy = VoCardsJson.Deserialize(VoCardsJson.Serialize(original)).Value!;
        Card restored = copy.FindDeck(original[0].Id)!.FindCard(source.Id)!;

        Assert.Equal(source.State, restored.State);
        Assert.Equal(source.DueAt, restored.DueAt);
        Assert.Equal(source.IntervalDays, restored.IntervalDays, 9);
        Assert.Equal(source.EaseFactor, restored.EaseFactor, 9);
        Assert.Equal(source.Repetitions, restored.Repetitions);
        Assert.Equal(source.Lapses, restored.Lapses);
        Assert.Equal(source.Stability, restored.Stability, 9);
        Assert.Equal(source.Difficulty, restored.Difficulty, 9);
        Assert.Equal(source.ReviewCount, restored.ReviewCount);
        Assert.Equal(source.CorrectCount, restored.CorrectCount);
        Assert.Equal(source.Tags, restored.Tags);
    }

    [Fact]
    public void CardContent_SurvivesARoundTrip()
    {
        Library original = Populated();
        Card source = original[0][3];

        Library copy = VoCardsJson.Deserialize(VoCardsJson.Serialize(original)).Value!;
        Card restored = copy.FindDeck(original[0].Id)!.FindCard(source.Id)!;

        Assert.Equal("The cat sat.", restored.Example);
        Assert.Equal("мужской род", restored.Notes);
        Assert.Equal("/kæt/", restored.Pronunciation);
    }

    [Fact]
    public void SuspendedState_SurvivesARoundTrip()
    {
        Library original = Populated();
        Card source = original[0][2];

        // This card was studied into Learning before being suspended, so the state to
        // come back to is Learning — not New. That is the bit worth round-tripping.
        Assert.Equal(CardState.Suspended, source.State);

        Library copy = VoCardsJson.Deserialize(VoCardsJson.Serialize(original)).Value!;
        Card restored = copy.FindDeck(original[0].Id)!.FindCard(source.Id)!;

        Assert.Equal(CardState.Suspended, restored.State);

        restored.Unsuspend();
        Assert.Equal(CardState.Learning, restored.State);
    }

    [Fact]
    public void Profile_SurvivesARoundTrip()
    {
        Library original = Populated();

        Library copy = VoCardsJson.Deserialize(VoCardsJson.Serialize(original)).Value!;

        Assert.Equal("Vsevolod", copy.Profile.DisplayName);
        Assert.Equal("aurora", copy.Profile.Accent);
        Assert.Equal(42, copy.Profile.DailyGoal);
        Assert.Equal(original.Profile.Xp, copy.Profile.Xp);
        Assert.Equal(original.Profile.Level, copy.Profile.Level);
        Assert.Equal(original.Profile.CurrentStreak, copy.Profile.CurrentStreak);
        Assert.Equal(original.Profile.LastStudyDay, copy.Profile.LastStudyDay);
        Assert.Equal(original.Profile.LifetimeReviews, copy.Profile.LifetimeReviews);
        Assert.True(copy.Profile.HasUnlocked("first-steps"));
    }

    [Fact]
    public void DeckSettings_SurviveARoundTrip()
    {
        Library original = Populated();
        original[0].Settings.Scheduler = SchedulerKind.Fsrs;
        original[0].Settings.LearningStepsMinutes = [2d, 15d, 60d];
        original[0].Settings.DesiredRetention = 0.94d;
        original[0].Settings.NewCardsPerDay = 13;

        Library copy = VoCardsJson.Deserialize(VoCardsJson.Serialize(original)).Value!;
        DeckSettings restored = copy.FindDeck(original[0].Id)!.Settings;

        Assert.Equal(SchedulerKind.Fsrs, restored.Scheduler);
        Assert.Equal([2d, 15d, 60d], restored.LearningStepsMinutes);
        Assert.Equal(0.94d, restored.DesiredRetention, 6);
        Assert.Equal(13, restored.NewCardsPerDay);
    }

    [Fact]
    public void RoundTrip_IsStable_WhenRepeated()
    {
        Library original = Populated();

        string once = VoCardsJson.Serialize(original);
        string twice = VoCardsJson.Serialize(VoCardsJson.Deserialize(once).Value!);

        // ExportedAt is stamped at write time, so compare everything else.
        Assert.Equal(Strip(once), Strip(twice));

        static string Strip(string json) =>
            System.Text.RegularExpressions.Regex.Replace(json, "\"exportedAt\":\"[^\"]*\",?", string.Empty);
    }

    [Fact]
    public void EnumsAreWrittenAsNames_SoTheFileStaysReadable()
    {
        Library library = Populated();

        string json = VoCardsJson.Serialize(library);

        Assert.Contains("\"state\":\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"state\":0", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PrettyOutput_IsIndented()
    {
        Library library = Populated();

        Assert.Contains("\n", VoCardsJson.Serialize(library, pretty: true), StringComparison.Ordinal);
        Assert.DoesNotContain("\n", VoCardsJson.Serialize(library, pretty: false), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("{ broken")]
    [InlineData("[1,2,3]")]
    public void BadInput_FailsGracefully(string input)
    {
        Result<Library> result = VoCardsJson.Deserialize(input);

        Assert.True(result.IsFailure);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void AFutureSchemaVersion_IsRejectedWithAClearMessage()
    {
        string json = VoCardsJson.Serialize(new Library())
            .Replace($"\"version\":{Library.SchemaVersion}", "\"version\":999", StringComparison.Ordinal);

        Result<Library> result = VoCardsJson.Deserialize(json);

        Assert.True(result.IsFailure);
        Assert.Contains("newer version", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoryForDeletedCards_IsDroppedOnLoad()
    {
        (Library library, Deck deck) = TestData.Library(cards: 2);
        library.History.Add(new Review
        {
            CardId = Guid.NewGuid(), // no such card
            DeckId = deck.Id,
            ReviewedAt = TestData.Epoch,
            Rating = Rating.Good,
        });

        Library copy = VoCardsJson.Deserialize(VoCardsJson.Serialize(library)).Value!;

        Assert.Equal(0, copy.History.Count);
    }

    [Fact]
    public void CorruptCounters_AreClampedOnLoad()
    {
        (Library library, Deck deck) = TestData.Library(cards: 1);
        string json = VoCardsJson.Serialize(library)
            .Replace("\"reviewCount\":0", "\"reviewCount\":3", StringComparison.Ordinal)
            .Replace("\"correctCount\":0", "\"correctCount\":99", StringComparison.Ordinal);

        Card restored = VoCardsJson.Deserialize(json).Value![0][0];

        Assert.True(restored.CorrectCount <= restored.ReviewCount);
        Assert.InRange(restored.Accuracy, 0d, 1d);
    }

    [Fact]
    public void SingleDeck_RoundTripsOnItsOwn()
    {
        Deck deck = Seed.SeedLibrary.Create()[0];

        string json = VoCardsJson.SerializeDeck(deck);
        Result<Deck> restored = VoCardsJson.DeserializeDeck(json);

        Assert.True(restored.IsSuccess, restored.Error);
        Assert.Equal(deck.Name, restored.Value!.Name);
        Assert.Equal(deck.TotalCards, restored.Value.TotalCards);
        Assert.Equal(deck.Emoji, restored.Value.Emoji);
        Assert.Equal(deck.BackLanguage, restored.Value.BackLanguage);
    }

    [Fact]
    public void DeckDeserialize_RejectsRubbish()
    {
        Assert.True(VoCardsJson.DeserializeDeck("nope").IsFailure);
        Assert.True(VoCardsJson.DeserializeDeck("").IsFailure);
    }
}

public class DelimitedPorterTests
{
    [Fact]
    public void Export_WritesAHeaderAndEveryCard()
    {
        var deck = new Deck("Test");
        deck.AddCard("house", "la casa");
        deck.AddCard("to eat", "comer");

        string csv = DelimitedPorter.Export(deck);
        string[] lines = csv.Trim().Split('\n');

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("Front,Back", lines[0], StringComparison.Ordinal);
        Assert.Contains("house", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ReadsASimpleTwoColumnFile()
    {
        const string csv = "house,la casa\nto eat,comer\nwater,el agua";

        var result = DelimitedPorter.Import(csv, "Spanish");

        Assert.True(result.IsSuccess, result.Error);
        (Deck deck, ImportReport report) = result.Value!;

        Assert.Equal("Spanish", deck.Name);
        Assert.Equal(3, deck.TotalCards);
        Assert.Equal(3, report.Imported);
        Assert.Equal(0, report.Skipped);
        Assert.False(report.HasProblems);
    }

    [Fact]
    public void Import_DetectsTabSeparatedFiles()
    {
        const string tsv = "house\tla casa\nto eat\tcomer";

        var result = DelimitedPorter.Import(tsv, "Spanish");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Deck.TotalCards);
        Assert.Equal("la casa", result.Value.Deck[0].Back);
    }

    [Fact]
    public void Import_SkipsTheHeaderWhenItRecognisesOne()
    {
        const string csv = "Front,Back\nhouse,la casa";

        var result = DelimitedPorter.Import(csv, "Spanish");

        Assert.Equal(1, result.Value!.Deck.TotalCards);
        Assert.Equal("house", result.Value.Deck[0].Front);
    }

    [Fact]
    public void Import_KeepsAFirstRowThatIsRealData()
    {
        const string csv = "house,la casa\nto eat,comer";

        var result = DelimitedPorter.Import(csv, "Spanish");

        Assert.Equal(2, result.Value!.Deck.TotalCards);
    }

    [Fact]
    public void Import_ReportsUnusableRows_WithoutAbandoningTheFile()
    {
        const string csv = "house,la casa\nbroken\nwater,el agua\n,missing front";

        var result = DelimitedPorter.Import(csv, "Spanish");

        Assert.True(result.IsSuccess);
        (Deck deck, ImportReport report) = result.Value!;

        Assert.Equal(2, deck.TotalCards);
        Assert.Equal(2, report.Imported);
        Assert.Equal(2, report.Skipped);
        Assert.True(report.HasProblems);
    }

    [Fact]
    public void Import_FailsWhenNothingIsUsable()
    {
        var result = DelimitedPorter.Import("just\nsome\nwords", "Deck");

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Import_ReadsEveryOptionalColumn()
    {
        const string csv = "Front,Back,Example,Notes,Pronunciation,Tags,Starred\n"
            + "house,la casa,Mi casa es su casa,feminine,/ˈkasa/,nouns home,yes";

        var result = DelimitedPorter.Import(csv, "Spanish");
        Card card = result.Value!.Deck[0];

        Assert.Equal("Mi casa es su casa", card.Example);
        Assert.Equal("feminine", card.Notes);
        Assert.Equal("/ˈkasa/", card.Pronunciation);
        Assert.Equal(2, card.Tags.Count);
        Assert.True(card.IsStarred);
    }

    [Fact]
    public void QuotedFields_SurviveTheDelimiter()
    {
        const string csv = "\"hello, world\",\"saludo\"";

        string[] fields = DelimitedPorter.ParseLine(csv, ',');

        Assert.Equal(2, fields.Length);
        Assert.Equal("hello, world", fields[0]);
    }

    [Fact]
    public void DoubledQuotes_BecomeALiteralQuote()
    {
        string[] fields = DelimitedPorter.ParseLine("\"say \"\"hi\"\"\",greeting", ',');

        Assert.Equal("say \"hi\"", fields[0]);
    }

    [Fact]
    public void ExportThenImport_PreservesContent()
    {
        var deck = new Deck("Round trip");
        deck.AddCard("hello, world", "привет, мир");
        deck.AddCard("say \"hi\"", "скажи «привет»");
        deck[0].AddTag("greetings");
        deck[0].Star();

        string csv = DelimitedPorter.Export(deck);
        var result = DelimitedPorter.Import(csv, "Round trip");

        Assert.True(result.IsSuccess, result.Error);
        Deck restored = result.Value!.Deck;

        Assert.Equal(2, restored.TotalCards);
        Assert.Equal("hello, world", restored[0].Front);
        Assert.Equal("привет, мир", restored[0].Back);
        Assert.Equal("say \"hi\"", restored[1].Front);
        Assert.Contains("greetings", restored[0].Tags);
        Assert.True(restored[0].IsStarred);
    }

    [Fact]
    public void ExportThenImport_RoundTripsEverySeedDeck()
    {
        foreach (Deck deck in Seed.SeedLibrary.Create().Decks)
        {
            string csv = DelimitedPorter.Export(deck, '\t');
            var result = DelimitedPorter.Import(csv, deck.Name, '\t');

            Assert.True(result.IsSuccess, $"{deck.Name}: {result.Error}");
            Assert.Equal(deck.TotalCards, result.Value!.Deck.TotalCards);
            Assert.Equal(deck[0].Back, result.Value.Deck[0].Back);
        }
    }

    [Fact]
    public void WindowsLineEndings_AreHandled()
    {
        var result = DelimitedPorter.Import("house,casa\r\nwater,agua\r\n", "Deck");

        Assert.Equal(2, result.Value!.Deck.TotalCards);
    }

    [Fact]
    public void Import_OfAnEmptyFile_Fails()
    {
        Assert.True(DelimitedPorter.Import("", "Deck").IsFailure);
        Assert.True(DelimitedPorter.Import("   \n  \n", "Deck").IsFailure);
    }
}

public class SeedLibraryTests
{
    [Fact]
    public void Seed_ProducesASubstantialLibrary()
    {
        Library library = Seed.SeedLibrary.Create();

        Assert.True(library.DeckCount >= 8, $"expected at least 8 decks, got {library.DeckCount}");
        Assert.True(library.TotalCards >= 150, $"expected at least 150 cards, got {library.TotalCards}");
        Assert.All(library.Decks, d => Assert.True(d.TotalCards >= 10, $"{d.Name} is too small"));
    }

    [Fact]
    public void Seed_CoversEnoughLanguagesForThePolyglotBadge()
    {
        Library library = Seed.SeedLibrary.Create();

        int languages = library.Decks
            .Select(d => d.BackLanguage)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.True(languages >= 3, $"expected 3+ languages, got {languages}");
    }

    [Fact]
    public void Seed_HasNoDuplicateFrontsWithinADeck() =>
        Assert.All(Seed.SeedLibrary.Create().Decks, deck =>
        {
            var fronts = deck.Cards.Select(c => c.Front).ToArray();
            Assert.Equal(fronts.Length, fronts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        });

    [Fact]
    public void Seed_HasNoEmptyFaces() =>
        Assert.All(Seed.SeedLibrary.Create().AllCards, card =>
        {
            Assert.False(string.IsNullOrWhiteSpace(card.Front));
            Assert.False(string.IsNullOrWhiteSpace(card.Back));
        });

    [Fact]
    public void BuildByName_ReturnsASingleDeck()
    {
        Deck? deck = Seed.SeedLibrary.Build("Animals");

        Assert.NotNull(deck);
        Assert.Equal("Animals", deck!.Name);
        Assert.Null(Seed.SeedLibrary.Build("No Such Deck"));
    }

    [Fact]
    public void EverySeedDeck_HasAnEmojiAndAColour() =>
        Assert.All(Seed.SeedLibrary.Available, seed =>
        {
            Assert.False(string.IsNullOrWhiteSpace(seed.Emoji));
            Assert.False(string.IsNullOrWhiteSpace(seed.Color));
            Assert.False(string.IsNullOrWhiteSpace(seed.Description));
        });
}
