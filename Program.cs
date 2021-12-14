using System;
using System.Collections.Generic;
using System.IO;
namespace VoCards
{
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
        //creates three decks and fills them with cards if save is not detected
        //save will be located in the same folder with .exe file
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
