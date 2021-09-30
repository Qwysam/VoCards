using System;
using System.Collections.Generic;
using System.IO;
namespace VoCards
{
    //top entity that stores all other classes
    [Serializable]
    class Progress
    {
        private List<Deck> progress_list;
        private int words_total, words_learnt;
        public int Words_Total {
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
            for(int i = 0;i<progress_list.Count;i++)
                if (topic == progress_list[i].Topic)
                    res = i;
            return res;

        }
        public void PrintProgress()
        {
            Console.WriteLine($"{Words_Learnt} words learnt out of {Words_Total}");
        }
    }
    [Serializable]
    class Deck
    {
        private List<Card> inner_deck;
        public string Topic { get; set; }
        int words_learnt_deck, words_total_deck;
        public int Words_Total_Deck{ get { return words_total_deck; } }
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
        public void GoThroughCards()
        {

            int cards_count,available_cards = Words_Total_Deck - Words_Learnt_Deck;
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
                            if(input == "Flip")
                            {
                                Console.WriteLine("\nTranslation : " + inner_deck[i].Back+"\n");
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
        public string Back{get;set;}
        public bool memorized;
        public Card(string front, string back)
        {
            Front = front;
            Back = back;
            memorized = false;
        }
    }

    class Program
    {
        public static void AddUserCard(Progress progress,int deck_index)
        {
            Console.WriteLine("Input card front text:");
            string front = Console.ReadLine();
            Console.WriteLine("Input card back text:");
            string back = Console.ReadLine();
            progress[deck_index].CreateAndAdd(front, back);
        }
        public static void CreateUserDeck(Progress progress)
        {
            Console.WriteLine("Input deck topic:");
            progress.AddDeck(Console.ReadLine());
            Console.WriteLine("Add at least one card");
            AddUserCard(progress, progress.NumberOfDecks-1);
            Console.WriteLine("Deck created successfully");
        }
        //methods to save progress in binary for easier access to private fields
        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false)
        {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }
        public static void FillDecks(Progress progress)
        {
            progress.AddDeck("Animals");
            progress.AddDeck("Jobs");
            progress.AddDeck("Food");
            List<Card> tmp_deck = new List<Card> { new Card("Cat", "Кот"), new Card("Horse", "Лошадь"), new Card("Dog", "Собака") };
            foreach (Card card in tmp_deck)
                progress[0].AddCard(card);
            tmp_deck = new List<Card> { new Card("Firefighter", "Пожарный"), new Card("Lawyer", "Адвокат"), new Card("Teacher", "Учитель") };
            foreach (Card card in tmp_deck)
                progress[1].AddCard(card);
            tmp_deck = new List<Card> { new Card("Pizza", "Пицца"), new Card("Soup", "Суп"), new Card("Bread", "Хлеб") };
            foreach(Card card in tmp_deck)
                progress[2].AddCard(card);
        }
        static void Main(string[] args)
        {
            Progress progress = new Progress();
            if (File.Exists("save.bin"))
            {
                progress = ReadFromBinaryFile<Progress>("save.bin");
                Console.WriteLine("Save loaded.");
            }
            else
                FillDecks(progress);
            int index = -1;
            bool go = true;
            string input = "";
            Console.WriteLine("Welcome to my flashcard app!");
            while(go)
            {
                for (; ; )
                {
                    Console.WriteLine("Existing decks by topic: ");
                    progress.PrintAllTopics();
                    Console.WriteLine("Choose one of existing decks by typing it's topic or type 'New' to create a new one, type 'Progress' to view statistics\n" +
                        "(you can type 'Exit' now to save data and exit the app):");
                    input = Console.ReadLine();
                    if (input == "New" || input == "Progress"|| input == "Exit" || progress.HasTopic(input))
                        break;
                }
                switch (input)
                {
                    case "Exit":
                        WriteToBinaryFile<Progress>("save.bin", progress);
                        go = false;
                        break;
                    case "New":
                        CreateUserDeck(progress);
                        break;
                    case "Progress":
                        progress.PrintProgress();
                        break;
                    default:
                        index = progress.FindByTopic(input);
                        for (; ; )
                        {
                            Console.WriteLine("Input 'Add' to add new card to the deck or 'Learn' to go through cards:");
                            input = Console.ReadLine();
                            if (input == "Add")
                            {
                                AddUserCard(progress, index);
                                break;
                            }
                            else
                            {
                                progress[index].GoThroughCards();
                                break;
                            }
                        }
                        break;
                }
                Console.WriteLine();
            }
        }
    }
}
