using System;
using System.Collections.Generic;
namespace VoCards
{
    /*top entity that stores all other custom classes
    only one object of this class should be used at a time unless
    multiple user support is required*/
    [Serializable]
    class Progress
    {
        //stores all decks of a user
        private List<Deck> progress_list;
        //stores stats
        private int words_total, words_learnt;
        //updates stats when invoked
        public int Words_Total
        {
            get { words_total = CountTotalWords(progress_list); return words_total; }
        }
        public int Words_Learnt
        {
            get { words_learnt = CountLearntWords(progress_list); return words_learnt; }
        }
        public int NumberOfDecks { get { return progress_list.Count; } }
        public Progress()
        {
            words_total = 0;
            words_learnt = 0;
            //list initialization
            progress_list = new List<Deck>();
        }

        private int CountTotalWords(List<Deck> list)
        {
            int sum = 0;
            foreach (Deck d in list)
                sum += d.Words_Total_Deck;
            return sum;
        }
        private int CountLearntWords(List<Deck> list)
        {
            int sum = 0;
            foreach (Deck d in list)
                sum += d.Words_Learnt_Deck;
            return sum;
        }
        public void AddDeck(string topic)
        {
            progress_list.Add(new Deck(topic));
        }

        public void AddDeck(Deck deck)
        {
            progress_list.Add(deck);
        }

        //indexer to reduce nesting
        public Deck this[int i]
        {
            get { return progress_list[i]; }
            set { progress_list[i] = value; }
        }
        //used to decrease amount of code in main
        public void PrintAllTopics()
        {
            foreach (Deck deck in progress_list)
                Console.Write($"{deck.Topic} \n");
        }

        public bool HasTopic(string topic)
        {
            bool res = false;
            foreach (Deck deck in progress_list)
                if (topic == deck.Topic)
                    res = true;
            return res;
        }
        //find deck index by topic
        public int FindByTopic(string topic)
        {
            int res = -1;
            for (int i = 0; i < progress_list.Count; i++)
                if (topic == progress_list[i].Topic)
                    res = i;
            return res;

        }
        //stats output
        public void PrintProgress()
        {
            Console.WriteLine($"{Words_Learnt} words learnt out of {Words_Total}");
        }
    }

    [Serializable]
    class Deck
    {
        //stores cards in a deck
        private List<Card> inner_deck;
        public string Topic { get; set; }
        //deck stats
        int words_learnt_deck, words_total_deck;
        public int Words_Total_Deck { get { return words_total_deck; } }
        public int Words_Learnt_Deck { get { return words_learnt_deck; } }
        public Deck(string topic)
        {
            Topic = topic;
            words_learnt_deck = 0;
            words_total_deck = 0;
            //list initialization
            inner_deck = new List<Card>();
        }
        public void AddCard(Card card)
        {
            inner_deck.Add(card);
            words_total_deck++;
        }
        public void CreateAndAdd(string front, string back)
        {
            inner_deck.Add(new Card(front, back));
            words_total_deck++;
        }
        public void RemoveCard(int index)
        {
            if (index < inner_deck.Count)
            {
                words_total_deck--;
                if (inner_deck[index].memorized)
                    words_learnt_deck--;
                inner_deck.RemoveAt(index);
            }
            else
                throw new ArgumentOutOfRangeException();
        }
        //should be used insted of changing memorized variable in Card class directly
        public void CardMemorized(int index)
        {
            if (index < inner_deck.Count)
            {
                inner_deck[index].memorized = true;
                words_learnt_deck++;
            }
            else
                throw new ArgumentOutOfRangeException();

        }
        //indexer to reduce nesting
        public Card this[int i]
        {
            get { return inner_deck[i]; }
            set { inner_deck[i] = value; }
        }
        //used to reduce amount of code in main
        public void GoThroughCards()
        {

            int cards_count, available_cards = Words_Total_Deck - Words_Learnt_Deck;
            if (available_cards == 0)
                Console.WriteLine("You have learned all cards. Good Job!!!!");
            else
            {
                for (; ; )
                {
                    Console.WriteLine($"Input number of cards to go through(it should be between 1 and {available_cards}):");
                    int.TryParse(Console.ReadLine(), out cards_count);
                    if (cards_count > 0 && cards_count <= available_cards)
                        break;
                }
                int i = 0;
                for (; i < Words_Total_Deck; i++)
                {
                    if (!inner_deck[i].memorized)
                    {
                        Console.WriteLine("Card text: " + inner_deck[i].Front);
                        string input;
                        for (; ; )
                        {
                            Console.WriteLine("Type 'Remember' to mark card as memorized or 'Flip' to see the translation");
                            input = Console.ReadLine();
                            if (input == "Remember")
                            {
                                Console.WriteLine("Card marked as memorized.");
                                CardMemorized(i);
                                break;
                            }
                            if (input == "Flip")
                            {
                                Console.WriteLine("\nTranslation : " + inner_deck[i].Back + "\n");
                                break;
                            }
                        }
                        cards_count -= 1;
                        if (cards_count == 0)
                            break;
                    }
                }
            }
        }
    }
    [Serializable]
    public class Card
    {
        public string Front { get; set; }
        public string Back { get; set; }
        public bool memorized;
        public Card(string front, string back)
        {
            Front = front;
            Back = back;
            memorized = false;
        }
    }
}