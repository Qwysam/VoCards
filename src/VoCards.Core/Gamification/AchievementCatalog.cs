using VoCards.Core.Models;

namespace VoCards.Core.Gamification;

/// <summary>
/// Every badge in the game. Kept as data rather than a switch statement so the trophy
/// page can render the locked ones too, with their progress rings.
/// </summary>
public static class AchievementCatalog
{
    private static readonly Achievement[] Catalog =
    [
        // ---------------------------------------------------------- milestones
        Make("first-steps", "First Steps", "Answer your very first card.", "👶",
            AchievementTier.Bronze, AchievementCategory.Milestones, 10,
            c => c.Profile.LifetimeReviews >= 1,
            c => c.Profile.LifetimeReviews / 1d),

        Make("century", "Century", "Answer 100 cards.", "💯",
            AchievementTier.Bronze, AchievementCategory.Milestones, 25,
            c => c.Profile.LifetimeReviews >= 100,
            c => c.Profile.LifetimeReviews / 100d),

        Make("thousand-club", "Thousand Club", "Answer 1,000 cards.", "🏛️",
            AchievementTier.Silver, AchievementCategory.Milestones, 75,
            c => c.Profile.LifetimeReviews >= 1_000,
            c => c.Profile.LifetimeReviews / 1_000d),

        Make("ten-thousand", "Ten Thousand Hours", "Answer 10,000 cards.", "🗿",
            AchievementTier.Platinum, AchievementCategory.Milestones, 500,
            c => c.Profile.LifetimeReviews >= 10_000,
            c => c.Profile.LifetimeReviews / 10_000d),

        Make("level-ten", "Double Digits", "Reach level 10.", "🔟",
            AchievementTier.Silver, AchievementCategory.Milestones, 60,
            c => c.Profile.Level >= 10,
            c => c.Profile.Level / 10d),

        Make("level-twentyfive", "Seasoned", "Reach level 25.", "🎖️",
            AchievementTier.Gold, AchievementCategory.Milestones, 150,
            c => c.Profile.Level >= 25,
            c => c.Profile.Level / 25d),

        // ---------------------------------------------------------- consistency
        Make("streak-3", "Getting Warm", "Study three days in a row.", "🔥",
            AchievementTier.Bronze, AchievementCategory.Consistency, 20,
            c => c.Profile.CurrentStreak >= 3,
            c => c.Profile.CurrentStreak / 3d),

        Make("streak-7", "Full Week", "Study seven days in a row.", "📅",
            AchievementTier.Silver, AchievementCategory.Consistency, 50,
            c => c.Profile.CurrentStreak >= 7,
            c => c.Profile.CurrentStreak / 7d),

        Make("streak-30", "Monthly Habit", "Study thirty days in a row.", "🌙",
            AchievementTier.Gold, AchievementCategory.Consistency, 150,
            c => c.Profile.CurrentStreak >= 30,
            c => c.Profile.CurrentStreak / 30d),

        Make("streak-100", "Unbreakable", "Study one hundred days in a row.", "💎",
            AchievementTier.Platinum, AchievementCategory.Consistency, 400,
            c => c.Profile.CurrentStreak >= 100,
            c => c.Profile.CurrentStreak / 100d),

        Make("early-bird", "Early Bird", "Finish a session before 7am.", "🌅",
            AchievementTier.Silver, AchievementCategory.Consistency, 40,
            c => c.Session is { Total: > 0 } && c.LocalHour < 7),

        Make("night-owl", "Night Owl", "Finish a session after 11pm.", "🦉",
            AchievementTier.Silver, AchievementCategory.Consistency, 40,
            c => c.Session is { Total: > 0 } && c.LocalHour >= 23),

        Make("marathon", "Marathon", "Answer 100 cards in a single session.", "🏃",
            AchievementTier.Gold, AchievementCategory.Consistency, 120,
            c => c.Session is { Total: >= 100 }),

        // ---------------------------------------------------------- accuracy
        Make("flawless", "Flawless", "Finish a session of 10+ cards with no mistakes.", "✨",
            AchievementTier.Silver, AchievementCategory.Accuracy, 60,
            c => c.Session is { Total: >= 10 } s && s.Incorrect == 0),

        Make("combo-20", "On Fire", "Hit a 20-answer correct streak in one session.", "🎯",
            AchievementTier.Gold, AchievementCategory.Accuracy, 100,
            c => c.Session is { Results.Count: > 0 } && LongestRun(c) >= 20),

        Make("sharpshooter", "Sharpshooter", "Hold 90% lifetime accuracy over 200+ reviews.", "🏹",
            AchievementTier.Gold, AchievementCategory.Accuracy, 120,
            c => c.Library.History.Count >= 200 && LifetimeAccuracy(c) >= 0.90d,
            c => c.Library.History.Count / 200d),

        Make("comeback", "Comeback", "Clear a card that had lapsed six times.", "🔁",
            AchievementTier.Gold, AchievementCategory.Accuracy, 110,
            c => c.Library.AllCards.Any(static card =>
                card.Lapses >= Card.LeechThreshold && card.IntervalDays >= 21d)),

        // ---------------------------------------------------------- collection
        Make("first-deck", "Curator", "Create your own deck.", "🗂️",
            AchievementTier.Bronze, AchievementCategory.Collection, 15,
            c => c.Library.DeckCount >= 1),

        Make("five-decks", "Collector", "Keep five decks going at once.", "📚",
            AchievementTier.Silver, AchievementCategory.Collection, 45,
            c => c.Library.DeckCount >= 5,
            c => c.Library.DeckCount / 5d),

        Make("hundred-cards", "Vocabulary Builder", "Have 100 cards in your library.", "🧱",
            AchievementTier.Silver, AchievementCategory.Collection, 50,
            c => c.Library.TotalCards >= 100,
            c => c.Library.TotalCards / 100d),

        Make("thousand-cards", "Lexicographer", "Have 1,000 cards in your library.", "📖",
            AchievementTier.Platinum, AchievementCategory.Collection, 300,
            c => c.Library.TotalCards >= 1_000,
            c => c.Library.TotalCards / 1_000d),

        Make("tagger", "Organised", "Use ten different tags.", "🏷️",
            AchievementTier.Bronze, AchievementCategory.Collection, 30,
            c => c.Library.AllTags.Count >= 10,
            c => c.Library.AllTags.Count / 10d),

        // ---------------------------------------------------------- mastery
        Make("first-mature", "It Stuck", "Push a card out to a three-week interval.", "🌱",
            AchievementTier.Bronze, AchievementCategory.Mastery, 25,
            c => c.Library.MaturedCards >= 1),

        Make("fifty-mature", "Rooted", "Mature fifty cards.", "🌳",
            AchievementTier.Silver, AchievementCategory.Mastery, 80,
            c => c.Library.MaturedCards >= 50,
            c => c.Library.MaturedCards / 50d),

        Make("deck-mastered", "Deck Mastered", "Get any deck to 90% mastery.", "👑",
            AchievementTier.Gold, AchievementCategory.Mastery, 160,
            c => c.Library.Decks.Any(static d => d.TotalCards >= 10 && d.Mastery >= 0.90d)),

        Make("polyglot", "Polyglot", "Study decks covering three different languages.", "🌍",
            AchievementTier.Gold, AchievementCategory.Mastery, 140,
            c => c.Library.Decks
                .Select(static d => d.BackLanguage)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() >= 3),

        // ---------------------------------------------------------- curiosity
        Make("mode-explorer", "Explorer", "Try every study mode at least once.", "🧭",
            AchievementTier.Gold, AchievementCategory.Curiosity, 120,
            c => Enum.GetValues<StudyMode>()
                .All(mode => c.Library.History.All.Any(r => r.Mode == mode))),

        Make("reverse-thinker", "Reverse Thinker", "Answer 50 cards back-to-front.", "🔄",
            AchievementTier.Silver, AchievementCategory.Curiosity, 60,
            c => c.Library.History.All.Count(static r => r.Direction == StudyDirection.BackToFront) >= 50,
            c => c.Library.History.All.Count(static r => r.Direction == StudyDirection.BackToFront) / 50d),

        Make("speed-demon", "Speed Demon", "Answer a card correctly in under two seconds.", "⚡",
            AchievementTier.Silver, AchievementCategory.Curiosity, 55,
            c => c.Library.AllCards.Any(static card =>
                card.BestResponseTime is { } best && best < TimeSpan.FromSeconds(2))),

        Make("typist", "Touch Typist", "Answer 100 cards in typing mode.", "⌨️",
            AchievementTier.Silver, AchievementCategory.Curiosity, 70,
            c => c.Library.History.All.Count(static r => r.Mode == StudyMode.Typing) >= 100,
            c => c.Library.History.All.Count(static r => r.Mode == StudyMode.Typing) / 100d),
    ];

    /// <summary>Every achievement, in catalogue order.</summary>
    public static IReadOnlyList<Achievement> All => Catalog;

    /// <summary>Looks one up by id.</summary>
    public static Achievement? Find(string id) =>
        Catalog.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.Ordinal));

    /// <summary>Grouped for the trophy page.</summary>
    public static IEnumerable<IGrouping<AchievementCategory, Achievement>> ByCategory() =>
        Catalog.GroupBy(static a => a.Category);

    private static Achievement Make(
        string id, string name, string description, string emoji,
        AchievementTier tier, AchievementCategory category, int xp,
        Func<AchievementContext, bool> satisfied,
        Func<AchievementContext, double>? progress = null) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Emoji = emoji,
            Tier = tier,
            Category = category,
            XpReward = xp,
            IsSatisfied = satisfied,
            Progress = progress,
        };

    /// <summary>Longest run of consecutive correct answers in the session just finished.</summary>
    private static int LongestRun(AchievementContext context)
    {
        if (context.Session is not { } session)
        {
            return 0;
        }

        int best = 0;
        int run = 0;

        foreach (var result in session.Results)
        {
            run = result.WasCorrect ? run + 1 : 0;
            best = Math.Max(best, run);
        }

        return best;
    }

    private static double LifetimeAccuracy(AchievementContext context)
    {
        var all = context.Library.History.All;
        return all.Count == 0 ? 0d : (double)all.Count(static r => r.WasCorrect) / all.Count;
    }
}
