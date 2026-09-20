using VoCards.Core.Models;

namespace VoCards.Core.Serialization;

/// <summary>
/// Converts between the live object graph and the persisted DTOs. One place, both
/// directions, so a round-trip can be asserted in a single test.
/// </summary>
public static class LibraryMapper
{
    // ---------------------------------------------------------------- to DTO

    public static LibraryDto ToDto(Library library)
    {
        ArgumentNullException.ThrowIfNull(library);

        return new LibraryDto
        {
            Version = Library.SchemaVersion,
            CreatedAt = library.CreatedAt,
            ExportedAt = DateTimeOffset.Now,
            Profile = ToDto(library.Profile),
            Decks = library.Decks.Select(ToDto).ToArray(),
            Reviews = library.History.All.Select(ToDto).ToArray(),
        };
    }

    public static DeckDto ToDto(Deck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        return new DeckDto
        {
            Id = deck.Id,
            Name = deck.Name,
            Description = deck.Description,
            Emoji = deck.Emoji,
            ColorToken = deck.ColorToken,
            FrontLanguage = deck.FrontLanguage,
            BackLanguage = deck.BackLanguage,
            IsPinned = deck.IsPinned,
            CreatedAt = deck.CreatedAt,
            ModifiedAt = deck.ModifiedAt,
            Settings = ToDto(deck.Settings),
            Cards = deck.Cards.Select(ToDto).ToArray(),
        };
    }

    public static CardDto ToDto(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return new CardDto
        {
            Id = card.Id,
            Front = card.Front,
            Back = card.Back,
            Example = card.Example,
            Notes = card.Notes,
            Pronunciation = card.Pronunciation,
            Tags = [.. card.Tags],
            IsStarred = card.IsStarred,
            CreatedAt = card.CreatedAt,
            ModifiedAt = card.ModifiedAt,
            State = card.State,
            StateBeforeSuspension = card.StateBeforeSuspension,
            DueAt = card.DueAt,
            IntervalDays = card.IntervalDays,
            EaseFactor = card.EaseFactor,
            Repetitions = card.Repetitions,
            Lapses = card.Lapses,
            LearningStep = card.LearningStep,
            Stability = card.Stability,
            Difficulty = card.Difficulty,
            LastReviewedAt = card.LastReviewedAt,
            ReviewCount = card.ReviewCount,
            CorrectCount = card.CorrectCount,
            BestResponseTicks = card.BestResponseTime?.Ticks,
        };
    }

    public static DeckSettingsDto ToDto(DeckSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new DeckSettingsDto
        {
            Scheduler = settings.Scheduler,
            Direction = settings.Direction,
            DefaultMode = settings.DefaultMode,
            NewCardsPerDay = settings.NewCardsPerDay,
            MaxReviewsPerDay = settings.MaxReviewsPerDay,
            Order = settings.Order,
            LearningStepsMinutes = [.. settings.LearningStepsMinutes],
            RelearningStepsMinutes = [.. settings.RelearningStepsMinutes],
            GraduatingIntervalDays = settings.GraduatingIntervalDays,
            EasyIntervalDays = settings.EasyIntervalDays,
            MaximumIntervalDays = settings.MaximumIntervalDays,
            IntervalModifier = settings.IntervalModifier,
            DesiredRetention = settings.DesiredRetention,
            AutoSpeak = settings.AutoSpeak,
            IgnoreAccents = settings.IgnoreAccents,
            IgnoreCase = settings.IgnoreCase,
            TypoTolerance = settings.TypoTolerance,
        };
    }

    public static ProfileDto ToDto(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ProfileDto
        {
            DisplayName = profile.DisplayName,
            Theme = profile.Theme,
            Accent = profile.Accent,
            ReduceMotion = profile.ReduceMotion,
            CardFlipAnimation = profile.CardFlipAnimation,
            SoundEffects = profile.SoundEffects,
            DailyGoal = profile.DailyGoal,
            PreferredVoice = profile.PreferredVoice,
            SpeechRate = profile.SpeechRate,
            SpeechPitch = profile.SpeechPitch,
            ShowAlternatives = profile.ShowAlternatives,
            HasOnboarded = profile.HasOnboarded,
            Xp = profile.Xp,
            CurrentStreak = profile.CurrentStreak,
            LongestStreak = profile.LongestStreak,
            LastStudyDay = profile.LastStudyDay,
            StreakFreezes = profile.StreakFreezes,
            LifetimeReviews = profile.LifetimeReviews,
            Achievements = profile.AchievementDates.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal),
        };
    }

    public static ReviewDto ToDto(Review review)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new ReviewDto
        {
            CardId = review.CardId,
            DeckId = review.DeckId,
            ReviewedAt = review.ReviewedAt,
            Rating = review.Rating,
            Mode = review.Mode,
            Direction = review.Direction,
            Verdict = review.Verdict,
            ElapsedTicks = review.Elapsed.Ticks,
            PreviousIntervalDays = review.PreviousIntervalDays,
            NewIntervalDays = review.NewIntervalDays,
            PreviousState = review.PreviousState,
            NewState = review.NewState,
        };
    }

    // ---------------------------------------------------------------- from DTO

    public static Library FromDto(LibraryDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var library = new Library { CreatedAt = dto.CreatedAt };

        foreach (DeckDto deckDto in dto.Decks)
        {
            library.AddDeck(FromDto(deckDto));
        }

        ApplyTo(library.Profile, dto.Profile);

        // Drop history pointing at cards that did not survive the import.
        var liveCards = library.AllCards.Select(static c => c.Id).ToHashSet();
        library.History.AddRange(dto.Reviews
            .Where(r => liveCards.Contains(r.CardId))
            .Select(FromDto));

        return library;
    }

    public static Deck FromDto(DeckDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var deck = new Deck(string.IsNullOrWhiteSpace(dto.Name) ? "Untitled" : dto.Name)
        {
            Id = dto.Id,
            CreatedAt = dto.CreatedAt,
            Settings = FromDto(dto.Settings),
        };

        deck.Describe(dto.Description);
        deck.SetAppearance(dto.Emoji, dto.ColorToken);
        deck.SetLanguages(dto.FrontLanguage, dto.BackLanguage);
        deck.Pin(dto.IsPinned);
        deck.AddCards(dto.Cards.Where(static c => !string.IsNullOrWhiteSpace(c.Front)).Select(FromDto));
        deck.RestoreModifiedAt(dto.ModifiedAt);

        return deck;
    }

    public static Card FromDto(CardDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var card = new Card(dto.Front, dto.Back)
        {
            Id = dto.Id,
            CreatedAt = dto.CreatedAt,
            State = dto.State,
            StateBeforeSuspension = dto.StateBeforeSuspension,
            DueAt = dto.DueAt,
            IntervalDays = Math.Max(0d, dto.IntervalDays),
            EaseFactor = Math.Clamp(dto.EaseFactor <= 0d ? 2.5d : dto.EaseFactor, 1.3d, 3.0d),
            Repetitions = Math.Max(0, dto.Repetitions),
            Lapses = Math.Max(0, dto.Lapses),
            LearningStep = Math.Max(0, dto.LearningStep),
            Stability = Math.Max(0d, dto.Stability),
            Difficulty = Math.Clamp(dto.Difficulty, 0d, 10d),
            LastReviewedAt = dto.LastReviewedAt,
            ReviewCount = Math.Max(0, dto.ReviewCount),
            CorrectCount = Math.Max(0, dto.CorrectCount),
            BestResponseTime = dto.BestResponseTicks is { } ticks and > 0
                ? TimeSpan.FromTicks(ticks)
                : null,
        };

        // CorrectCount can never exceed ReviewCount, whatever a hand-edited file claims.
        if (card.CorrectCount > card.ReviewCount)
        {
            card.CorrectCount = card.ReviewCount;
        }

        card.RestoreContent(
            dto.Example, dto.Notes, dto.Pronunciation, dto.IsStarred, dto.ModifiedAt, dto.Tags);

        return card;
    }

    public static DeckSettings FromDto(DeckSettingsDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new DeckSettings
        {
            Scheduler = dto.Scheduler,
            Direction = dto.Direction,
            DefaultMode = dto.DefaultMode,
            NewCardsPerDay = Math.Max(0, dto.NewCardsPerDay),
            MaxReviewsPerDay = Math.Max(0, dto.MaxReviewsPerDay),
            Order = dto.Order,
            LearningStepsMinutes = dto.LearningStepsMinutes.Count > 0 ? [.. dto.LearningStepsMinutes] : [1d, 10d],
            RelearningStepsMinutes = dto.RelearningStepsMinutes.Count > 0 ? [.. dto.RelearningStepsMinutes] : [10d],
            GraduatingIntervalDays = Math.Max(1d / 1440d, dto.GraduatingIntervalDays),
            EasyIntervalDays = Math.Max(1d / 1440d, dto.EasyIntervalDays),
            MaximumIntervalDays = Math.Max(1d, dto.MaximumIntervalDays),
            IntervalModifier = Math.Clamp(dto.IntervalModifier, 0.1d, 5d),
            DesiredRetention = Math.Clamp(dto.DesiredRetention, 0.70d, 0.99d),
            AutoSpeak = dto.AutoSpeak,
            IgnoreAccents = dto.IgnoreAccents,
            IgnoreCase = dto.IgnoreCase,
            TypoTolerance = Math.Clamp(dto.TypoTolerance, 0.5d, 1d),
        };
    }

    public static Review FromDto(ReviewDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new Review
        {
            CardId = dto.CardId,
            DeckId = dto.DeckId,
            ReviewedAt = dto.ReviewedAt,
            Rating = dto.Rating,
            Mode = dto.Mode,
            Direction = dto.Direction,
            Verdict = dto.Verdict,
            Elapsed = TimeSpan.FromTicks(Math.Max(0L, dto.ElapsedTicks)),
            PreviousIntervalDays = dto.PreviousIntervalDays,
            NewIntervalDays = dto.NewIntervalDays,
            PreviousState = dto.PreviousState,
            NewState = dto.NewState,
        };
    }

    private static void ApplyTo(UserProfile profile, ProfileDto dto)
    {
        profile.DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? "Learner" : dto.DisplayName;
        profile.Theme = dto.Theme;
        profile.Accent = dto.Accent;
        profile.ReduceMotion = dto.ReduceMotion;
        profile.CardFlipAnimation = dto.CardFlipAnimation;
        profile.SoundEffects = dto.SoundEffects;
        profile.DailyGoal = Math.Clamp(dto.DailyGoal, 1, 1000);
        profile.PreferredVoice = dto.PreferredVoice;
        profile.SpeechRate = Math.Clamp(dto.SpeechRate, 0.5d, 2d);
        profile.SpeechPitch = Math.Clamp(dto.SpeechPitch, 0d, 2d);
        profile.ShowAlternatives = dto.ShowAlternatives;
        profile.HasOnboarded = dto.HasOnboarded;

        profile.Restore(
            dto.Xp,
            dto.CurrentStreak,
            dto.LongestStreak,
            dto.LastStudyDay,
            dto.StreakFreezes,
            dto.LifetimeReviews,
            dto.Achievements);
    }
}
