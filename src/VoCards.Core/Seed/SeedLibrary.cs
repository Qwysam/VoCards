using VoCards.Core.Models;

namespace VoCards.Core.Seed;

/// <summary>
/// The starter library a first-time learner is handed.
///
/// The 2021 app seeded three decks of three English→Russian cards each — nine words in
/// total, hard-coded in <c>FillDecks</c>. Those three decks are still here, kept as the
/// project's origin story, but grown to a usable size and joined by decks in five more
/// languages so that the study modes have something to work with on day one.
/// </summary>
public static class SeedLibrary
{
    /// <summary>Builds a fresh starter library.</summary>
    public static Library Create()
    {
        var library = new Library();

        foreach (SeedDeck seed in Decks)
        {
            var deck = new Deck(seed.Name);
            deck.Describe(seed.Description);
            deck.SetAppearance(seed.Emoji, seed.Color);
            deck.SetLanguages(seed.FrontLanguage, seed.BackLanguage);

            foreach ((string front, string back, string tags) in seed.Cards)
            {
                var card = new Card(front, back);

                if (tags.Length > 0)
                {
                    card.SetTags(tags.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }

                deck.AddCard(card);
            }

            library.AddDeck(deck);
        }

        // The first three decks are the originals, so they lead the list.
        library.Decks[0].Pin();

        return library;
    }

    /// <summary>The decks available to add from the deck gallery, without seeding everything.</summary>
    public static IReadOnlyList<SeedDeck> Available => Decks;

    /// <summary>Builds one named starter deck, for the "add a sample deck" action.</summary>
    public static Deck? Build(string name)
    {
        SeedDeck? seed = Decks.FirstOrDefault(d =>
            string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));

        if (seed is null)
        {
            return null;
        }

        var deck = new Deck(seed.Name);
        deck.Describe(seed.Description);
        deck.SetAppearance(seed.Emoji, seed.Color);
        deck.SetLanguages(seed.FrontLanguage, seed.BackLanguage);

        foreach ((string front, string back, string tags) in seed.Cards)
        {
            var card = new Card(front, back);

            if (tags.Length > 0)
            {
                card.SetTags(tags.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }

            deck.AddCard(card);
        }

        return deck;
    }

    private static readonly SeedDeck[] Decks =
    [
        new("Animals", "The very first VoCards deck, grown from three words to twenty.",
            "🐾", "amber", "en-US", "ru-RU",
        [
            ("Cat", "Кот", "original pets"),
            ("Horse", "Лошадь", "original farm"),
            ("Dog", "Собака", "original pets"),
            ("Bird", "Птица", ""),
            ("Fish", "Рыба", ""),
            ("Cow", "Корова", "farm"),
            ("Sheep", "Овца", "farm"),
            ("Pig", "Свинья", "farm"),
            ("Chicken", "Курица", "farm"),
            ("Mouse", "Мышь", ""),
            ("Bear", "Медведь", "wild"),
            ("Wolf", "Волк", "wild"),
            ("Fox", "Лиса", "wild"),
            ("Rabbit", "Кролик", ""),
            ("Snake", "Змея", "wild"),
            ("Frog", "Лягушка", ""),
            ("Butterfly", "Бабочка", "insects"),
            ("Bee", "Пчела", "insects"),
            ("Spider", "Паук", "insects"),
            ("Elephant", "Слон", "wild"),
        ]),

        new("Jobs", "Professions and workplaces. The second of the original three decks.",
            "💼", "sky", "en-US", "ru-RU",
        [
            ("Firefighter", "Пожарный", "original"),
            ("Lawyer", "Адвокат", "original"),
            ("Teacher", "Учитель", "original"),
            ("Doctor", "Врач", "medical"),
            ("Nurse", "Медсестра", "medical"),
            ("Engineer", "Инженер", ""),
            ("Driver", "Водитель", ""),
            ("Cook", "Повар", "food"),
            ("Waiter", "Официант", "food"),
            ("Writer", "Писатель", "creative"),
            ("Artist", "Художник", "creative"),
            ("Musician", "Музыкант", "creative"),
            ("Farmer", "Фермер", ""),
            ("Scientist", "Учёный", ""),
            ("Programmer", "Программист", ""),
            ("Accountant", "Бухгалтер", ""),
            ("Journalist", "Журналист", "creative"),
            ("Police officer", "Полицейский", ""),
            ("Pilot", "Пилот", ""),
            ("Translator", "Переводчик", ""),
        ]),

        new("Food", "Meals, ingredients and drinks. The third of the original three decks.",
            "🍲", "rose", "en-US", "ru-RU",
        [
            ("Pizza", "Пицца", "original"),
            ("Soup", "Суп", "original"),
            ("Bread", "Хлеб", "original staples"),
            ("Water", "Вода", "drinks"),
            ("Tea", "Чай", "drinks"),
            ("Coffee", "Кофе", "drinks"),
            ("Milk", "Молоко", "drinks"),
            ("Cheese", "Сыр", ""),
            ("Butter", "Масло", ""),
            ("Egg", "Яйцо", ""),
            ("Meat", "Мясо", ""),
            ("Rice", "Рис", "staples"),
            ("Potato", "Картофель", "vegetables"),
            ("Tomato", "Помидор", "vegetables"),
            ("Onion", "Лук", "vegetables"),
            ("Apple", "Яблоко", "fruit"),
            ("Orange", "Апельсин", "fruit"),
            ("Salt", "Соль", ""),
            ("Sugar", "Сахар", ""),
            ("Breakfast", "Завтрак", "meals"),
        ]),

        new("Travel Russian", "The phrases you actually need at a station, a hotel or a counter.",
            "🧳", "teal", "en-US", "ru-RU",
        [
            ("Hello", "Здравствуйте", "greetings"),
            ("Goodbye", "До свидания", "greetings"),
            ("Please", "Пожалуйста", "polite"),
            ("Thank you", "Спасибо", "polite"),
            ("Excuse me", "Извините", "polite"),
            ("Yes", "Да", "basics"),
            ("No", "Нет", "basics"),
            ("How much does it cost?", "Сколько это стоит?", "shopping"),
            ("Where is the station?", "Где вокзал?", "directions"),
            ("I don't understand", "Я не понимаю", "basics"),
            ("Do you speak English?", "Вы говорите по-английски?", "basics"),
            ("Help!", "Помогите!", "emergency"),
            ("The bill, please", "Счёт, пожалуйста", "restaurant"),
            ("One ticket", "Один билет", "transport"),
            ("Left", "Налево", "directions"),
            ("Right", "Направо", "directions"),
            ("Straight ahead", "Прямо", "directions"),
            ("Today", "Сегодня", "time"),
            ("Tomorrow", "Завтра", "time"),
            ("My name is…", "Меня зовут…", "greetings"),
        ]),

        new("Spanish Starter", "First hundred words of Spanish, beginning with the useful ones.",
            "🇪🇸", "amber", "en-US", "es-ES",
        [
            ("Hello", "Hola", "greetings"),
            ("Good morning", "Buenos días", "greetings"),
            ("Good night", "Buenas noches", "greetings"),
            ("Please", "Por favor", "polite"),
            ("Thank you", "Gracias", "polite"),
            ("You're welcome", "De nada", "polite"),
            ("Water", "El agua", "food"),
            ("Bread", "El pan", "food"),
            ("House", "La casa", "nouns"),
            ("Book", "El libro", "nouns"),
            ("Friend", "El amigo, la amiga", "people"),
            ("To eat", "Comer", "verbs"),
            ("To drink", "Beber", "verbs"),
            ("To speak", "Hablar", "verbs"),
            ("To go", "Ir", "verbs irregular"),
            ("To be (permanent)", "Ser", "verbs irregular"),
            ("To be (temporary)", "Estar", "verbs irregular"),
            ("Big", "Grande", "adjectives"),
            ("Small", "Pequeño", "adjectives"),
            ("Beautiful", "Bonito, hermoso", "adjectives"),
            ("Today", "Hoy", "time"),
            ("Tomorrow", "Mañana", "time"),
            ("Where?", "¿Dónde?", "questions"),
            ("Why?", "¿Por qué?", "questions"),
            ("How much?", "¿Cuánto?", "questions"),
        ]),

        new("French Essentials", "Everyday French, with the accents that change the meaning.",
            "🇫🇷", "indigo", "en-US", "fr-FR",
        [
            ("Hello", "Bonjour", "greetings"),
            ("Good evening", "Bonsoir", "greetings"),
            ("Please", "S'il vous plaît", "polite"),
            ("Thank you", "Merci", "polite"),
            ("Excuse me", "Excusez-moi", "polite"),
            ("Yes", "Oui", "basics"),
            ("No", "Non", "basics"),
            ("Water", "L'eau", "food"),
            ("Bread", "Le pain", "food"),
            ("Cheese", "Le fromage", "food"),
            ("House", "La maison", "nouns"),
            ("Street", "La rue", "nouns"),
            ("To be", "Être", "verbs irregular"),
            ("To have", "Avoir", "verbs irregular"),
            ("To do", "Faire", "verbs irregular"),
            ("To want", "Vouloir", "verbs"),
            ("Beautiful", "Beau, belle", "adjectives"),
            ("Tired", "Fatigué", "adjectives"),
            ("Where is…?", "Où est… ?", "questions"),
            ("I would like…", "Je voudrais…", "phrases"),
            ("I don't understand", "Je ne comprends pas", "phrases"),
            ("How are you?", "Comment allez-vous ?", "phrases"),
        ]),

        new("German Basics", "Nouns with their articles, because the article is half the word.",
            "🇩🇪", "slate", "en-US", "de-DE",
        [
            ("Hello", "Hallo", "greetings"),
            ("Good day", "Guten Tag", "greetings"),
            ("Please", "Bitte", "polite"),
            ("Thank you", "Danke", "polite"),
            ("The house", "Das Haus", "nouns neuter"),
            ("The man", "Der Mann", "nouns masculine"),
            ("The woman", "Die Frau", "nouns feminine"),
            ("The child", "Das Kind", "nouns neuter"),
            ("The water", "Das Wasser", "nouns neuter"),
            ("The bread", "Das Brot", "nouns neuter"),
            ("The city", "Die Stadt", "nouns feminine"),
            ("The book", "Das Buch", "nouns neuter"),
            ("To be", "Sein", "verbs irregular"),
            ("To have", "Haben", "verbs irregular"),
            ("To go", "Gehen", "verbs"),
            ("To speak", "Sprechen", "verbs"),
            ("Big", "Groß", "adjectives"),
            ("Small", "Klein", "adjectives"),
            ("Fast", "Schnell", "adjectives"),
            ("I don't understand", "Ich verstehe nicht", "phrases"),
        ]),

        new("Japanese Basics", "Survival Japanese in romaji, with kana alongside.",
            "🇯🇵", "rose", "en-US", "ja-JP",
        [
            ("Hello", "こんにちは (konnichiwa)", "greetings"),
            ("Good morning", "おはよう (ohayou)", "greetings"),
            ("Thank you", "ありがとう (arigatou)", "polite"),
            ("Excuse me / sorry", "すみません (sumimasen)", "polite"),
            ("Yes", "はい (hai)", "basics"),
            ("No", "いいえ (iie)", "basics"),
            ("Water", "みず (mizu)", "food"),
            ("Rice / meal", "ごはん (gohan)", "food"),
            ("Tea", "おちゃ (ocha)", "food"),
            ("Person", "ひと (hito)", "nouns"),
            ("Book", "ほん (hon)", "nouns"),
            ("School", "がっこう (gakkou)", "nouns"),
            ("To eat", "たべる (taberu)", "verbs"),
            ("To drink", "のむ (nomu)", "verbs"),
            ("To go", "いく (iku)", "verbs"),
            ("To see", "みる (miru)", "verbs"),
            ("Big", "おおきい (ookii)", "adjectives"),
            ("Small", "ちいさい (chiisai)", "adjectives"),
            ("How much is it?", "いくらですか (ikura desu ka)", "phrases"),
            ("I don't understand", "わかりません (wakarimasen)", "phrases"),
        ]),

        new("Academic English", "The words that carry an argument, defined rather than translated.",
            "🎓", "emerald", "en-US", "en-US",
        [
            ("Ubiquitous", "Present everywhere at once.", "adjectives"),
            ("Ephemeral", "Lasting a very short time.", "adjectives"),
            ("Salient", "Most noticeable or important.", "adjectives"),
            ("Tenuous", "Very weak or slight.", "adjectives"),
            ("Cogent", "Clear, logical and convincing.", "adjectives argument"),
            ("Specious", "Superficially plausible, but actually wrong.", "adjectives argument"),
            ("Corroborate", "To confirm with supporting evidence.", "verbs argument"),
            ("Refute", "To prove a statement false.", "verbs argument"),
            ("Posit", "To put forward as a basis for argument.", "verbs argument"),
            ("Delineate", "To describe or portray precisely.", "verbs"),
            ("Extrapolate", "To extend a conclusion beyond the observed data.", "verbs"),
            ("Juxtapose", "To place side by side for contrast.", "verbs"),
            ("Caveat", "A warning or qualification.", "nouns"),
            ("Corollary", "A proposition that follows from one already proved.", "nouns"),
            ("Dichotomy", "A division into two mutually exclusive groups.", "nouns"),
            ("Paradigm", "A typical example or model of something.", "nouns"),
            ("Nuance", "A subtle difference in meaning or tone.", "nouns"),
            ("Premise", "A statement an argument takes as given.", "nouns argument"),
            ("Empirical", "Based on observation rather than theory.", "adjectives"),
            ("Anomalous", "Deviating from what is standard or expected.", "adjectives"),
        ]),
    ];
}

/// <summary>One deck's worth of starter content.</summary>
/// <param name="Name">Deck name.</param>
/// <param name="Description">One-line description shown on the deck card.</param>
/// <param name="Emoji">Deck glyph.</param>
/// <param name="Color">Accent token.</param>
/// <param name="FrontLanguage">BCP-47 tag for the front face.</param>
/// <param name="BackLanguage">BCP-47 tag for the back face.</param>
/// <param name="Cards">Front, back and space-separated tags for each card.</param>
public sealed record SeedDeck(
    string Name,
    string Description,
    string Emoji,
    string Color,
    string FrontLanguage,
    string BackLanguage,
    IReadOnlyList<(string Front, string Back, string Tags)> Cards);
