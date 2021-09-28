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
                Console.Write($"{deck.Topic} ");
        }
        public bool HasTopic(string topic)
        {
            bool res = false;
            foreach (Deck deck in progress_list)
                if (topic == deck.Topic)
                    res = true;
            return res;
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
            FillDecks(progress);
            int index = -1;
            Console.WriteLine("Welcome to my flashcard app!");
            for(; ; )
            {
                Console.WriteLine("Existing decks by topic: ");
                progress.PrintAllTopics();
                Console.WriteLine("Choose one of existing decks by typing it's topic or type 'New' to create a new one");
                string input = Console.ReadLine();
                if (input == "New" || progress.HasTopic(input))
                    break;
            }
        }
    }
}
