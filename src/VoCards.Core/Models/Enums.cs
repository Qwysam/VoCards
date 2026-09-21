namespace VoCards.Core.Models;

/// <summary>
/// How well the learner recalled a card. These are the four Anki-style grades, and
/// they replace the original app's single boolean <c>memorized</c> flag: a card is no
/// longer "learnt forever" the first time it is seen.
/// </summary>
public enum Rating
{
    /// <summary>Complete blank. The card lapses and goes back into relearning.</summary>
    Again = 1,

    /// <summary>Recalled, but with real effort. Interval grows slowly.</summary>
    Hard = 2,

    /// <summary>Recalled correctly. The expected outcome.</summary>
    Good = 3,

    /// <summary>Instant, effortless recall. Interval jumps.</summary>
    Easy = 4,
}

/// <summary>Where a card sits in its learning lifecycle.</summary>
public enum CardState
{
    /// <summary>Never studied.</summary>
    New = 0,

    /// <summary>Being learnt for the first time, still on sub-day steps.</summary>
    Learning = 1,

    /// <summary>Graduated; on a multi-day interval.</summary>
    Review = 2,

    /// <summary>Was in review, then lapsed, and is being re-learnt.</summary>
    Relearning = 3,

    /// <summary>Deliberately taken out of rotation.</summary>
    Suspended = 4,
}

/// <summary>The available ways to drill a deck.</summary>
public enum StudyMode
{
    /// <summary>Classic flashcard: see the prompt, flip, grade yourself.</summary>
    Flip = 0,

    /// <summary>Pick the right answer from several options.</summary>
    MultipleChoice = 1,

    /// <summary>Type the answer; it is graded with typo tolerance.</summary>
    Typing = 2,

    /// <summary>Hear the prompt spoken, then answer.</summary>
    Listening = 3,

    /// <summary>Match prompts to answers in a grid.</summary>
    Match = 4,

    /// <summary>Multiple choice against a countdown.</summary>
    SpeedRound = 5,
}

/// <summary>Which face of the card is shown as the prompt.</summary>
public enum StudyDirection
{
    /// <summary>Prompt with the front (the term), answer with the back (the meaning).</summary>
    FrontToBack = 0,

    /// <summary>Prompt with the back, answer with the front. Harder: recall, not recognition.</summary>
    BackToFront = 1,

    /// <summary>Randomised per card.</summary>
    Mixed = 2,
}

/// <summary>Which spaced-repetition algorithm drives scheduling.</summary>
public enum SchedulerKind
{
    /// <summary>SuperMemo 2. Simple, predictable, well understood.</summary>
    Sm2 = 0,

    /// <summary>Free Spaced Repetition Scheduler: memory-stability based, adapts faster.</summary>
    Fsrs = 1,
}

/// <summary>Ordering applied when a study queue is built.</summary>
public enum QueueOrder
{
    /// <summary>Most overdue first.</summary>
    DueFirst = 0,

    /// <summary>Shuffled.</summary>
    Random = 1,

    /// <summary>The order cards were added.</summary>
    Added = 2,

    /// <summary>Lowest accuracy first — drill the weak spots.</summary>
    HardestFirst = 3,
}

/// <summary>Result of grading a typed or chosen answer.</summary>
public enum AnswerVerdict
{
    /// <summary>Matched an accepted answer exactly (after normalisation).</summary>
    Correct = 0,

    /// <summary>Within the typo tolerance — counted as correct, but flagged.</summary>
    AlmostCorrect = 1,

    /// <summary>Did not match.</summary>
    Incorrect = 2,

    /// <summary>The learner skipped without answering.</summary>
    Skipped = 3,
}
