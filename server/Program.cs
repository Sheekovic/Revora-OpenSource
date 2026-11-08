using System;
using System.IO;

namespace Revora
{
    public unsafe class Program
    {
        public static Counter ShadowCloneCounter = new Counter(0);
        static void Main(String[] args)
        {
            try
            {
                WriteHeader();
                Server.Run();
            }
            catch (Exception Exc)
            {
                WriteLine(Exc);
            }
            Console.ReadLine();
        }
        
        private static void WriteHeader()
        {
            
            Time32 Start = Time32.Now;
            Server.RandomSeed = Convert.ToInt32(DateTime.Now.Ticks.ToString().Remove(DateTime.Now.Ticks.ToString().Length / 2));
            Kernel.Random = new FastRandom(Server.RandomSeed);
            Server.StartDate = DateTime.Now;
            Console.Title = "Revora [Private Server]";
            //IntPtr hWnd = Server.FindWindow(null, Console.Title);
            Console.WriteLine(@"|                                            |", ConsoleColor.Red);
            Console.WriteLine(@"|                  Revora                    |", ConsoleColor.Red);
            Console.WriteLine(@"|                 Mr Sheekovic                |", ConsoleColor.Red);
            Console.WriteLine(@"|                version:6711                |", ConsoleColor.Red);
            Console.WriteLine(@"|                  Source 3D                 |", ConsoleColor.Red);
            Console.WriteLine(@"+--------------------------------------------+", ConsoleColor.Red);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Red;
            
        }
        public static void WriteLine(Object Object)
        {
            Console.WriteLine(Object);
        }
    }
}