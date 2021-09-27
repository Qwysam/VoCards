using System;
using System.Collections.Generic;

namespace VoCards
{
    //top entity that stores all other classes
    class Progress
    {
        private List<Deck> progress_list;
        int words_total, words_learnt;
        public int Words_Total {
            get { words_total = CountTotalWords(progress_list); return words_total; }
        }
        public int Words_Learnt
        {
            get { words_learnt = CountLearntWords(progress_list); return words_learnt; }
        }
        Progress()
        {
            words_total = 0;
            words_learnt = 0;
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
        //Todo
        public void SaveProgress()
        {

        }
    }
    class Deck
    {
        private List<Card> inner_deck;
        string topic;
        int words_learnt_deck, words_total_deck;
        public int Words_Total_Deck{ get { return words_total_deck; } }
        public int Words_Learnt_Deck { get { return words_learnt_deck; } }
        public Deck(string topic)
        {
            this.topic = topic;
            words_learnt_deck = 0;
            words_total_deck = 0;
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
    }
    class Card
    {
        private string front,back;
        public bool memorized;
        public Card(string front, string back)
        {
            this.front = front;
            this.back = back;
            memorized = false;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {

        }
    }
}
