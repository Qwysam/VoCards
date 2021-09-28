using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Text.Json;

namespace VoCards
{
    //top entity that stores all other classes
    [Serializable]
    class Progress
    {
        private List<Deck> progress_list;
        public List<Deck> Progress_List { get { return progress_list; } }
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
        ////Todo
        //public void SaveToXML(Progress progress)
        //{
        //    XmlSerializer formatter = new XmlSerializer(typeof(Progress));
        //    using (FileStream fs = new FileStream("save.xml", FileMode.Create))
        //    {
        //        formatter.Serialize(fs, progress);
        //    }
        //}
        ////Todo
        //public void ReadFromXML(string path)
        //{

        //}

        //public void SaveToJSON(Progress progress)
        //{
        //    string jsonString = JsonSerializer.Serialize(progress);
        //    File.WriteAllText("save.json", jsonString);
        //}

        //public Progress ReadFrommJson(string path)
        //{
        //    string text = File.ReadAllText(path);
        //    return JsonSerializer.Deserialize<Progress>(text);
        //}

        public void AddDeck(string topic)
        {
            progress_list.Add(new Deck(topic));
        }

        public void AddDeck(Deck deck)
        {
            progress_list.Add(deck);
        }
    }
    [Serializable]
    class Deck
    {
        private List<Card> inner_deck;
        public List<Card> Inner_Deck { get { return inner_deck; } }
        public string Topic { get; set; }
        int words_learnt_deck, words_total_deck;
        public int Words_Total_Deck{ get { return words_total_deck; } }
        public int Words_Learnt_Deck { get { return words_learnt_deck; } }
        public Deck(string topic)
        {
            Topic = topic;
            words_learnt_deck = 0;
            words_total_deck = 0;
            inner_deck = new List<Card>();
        }
        public void AddCard(Card card)
        {
            Inner_Deck.Add(card);
            words_total_deck++;
        }
        public void CreateAndAdd(string front, string back)
        {
            Inner_Deck.Add(new Card(front, back));
            words_total_deck++;
        }
        public void RemoveCard(int index)
        {
            if (index < Inner_Deck.Count)
            {
                words_total_deck--;
                if (Inner_Deck[index].memorized)
                    words_learnt_deck--;
                Inner_Deck.RemoveAt(index);
            }
            else
                throw new ArgumentOutOfRangeException();
        }
        public void CardMemorized(int index)
        {
            if (index < Inner_Deck.Count)
            {
                Inner_Deck[index].memorized = true;
                words_learnt_deck++;
            }
            else
                throw new ArgumentOutOfRangeException();

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

        static void Main(string[] args)
        {

        }
    }
}
