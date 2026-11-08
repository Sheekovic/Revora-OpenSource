using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora
{
    public static class Console
    {
        private static DateTime NOW = DateTime.Now;
        private static Time32 NOW32 = Time32.Now;
        public static string Title
        {
            get
            {
                return System.Console.Title;
            }
            set
            {
                System.Console.Title = value;
            }
        }

        public static int WindowWidth
        {
            get
            {
                return System.Console.WindowWidth;
            }
            set
            {
                System.Console.WindowWidth = value; ;
            }
        }

        public static int WindowHeight
        {
            get
            {
                return System.Console.WindowHeight;
            }
            set
            {
                System.Console.WindowHeight = value; ;
            }
        }


        public static void WriteLine(object line, ConsoleColor color = ConsoleColor.DarkCyan)
        {

            if (line.ToString() == "" || line.ToString() == " ")
                System.Console.WriteLine();
            else
            {
                ForegroundColor = ConsoleColor.White;
                System.Console.Write(TimeStamp() + " ");
                ForegroundColor = color;
                System.Console.Write(line + "\n");
            }
        }
        #region Color de COnsola (Letras y Lineas)
        public static void WriteLine2(object line, ConsoleColor color = ConsoleColor.Magenta)
        {

            if (line.ToString() == "" || line.ToString() == " ")
                System.Console.WriteLine();
            else
            {
                ForegroundColor = ConsoleColor.White;
                System.Console.Write(TimeStamp() + " ");
                ForegroundColor = color;
                System.Console.Write(line + "\n");
            }
        }
        public static void WriteLine3(object line, ConsoleColor color = ConsoleColor.Green)
        {

            if (line.ToString() == "" || line.ToString() == " ")
                System.Console.WriteLine();
            else
            {
                ForegroundColor = ConsoleColor.White;
                System.Console.Write(TimeStamp() + " ");
                ForegroundColor = color;
                System.Console.Write(line + "\n");
            }
        }
        public static void WriteLine4(object line, ConsoleColor color = ConsoleColor.Red)
        {

            if (line.ToString() == "" || line.ToString() == " ")
                System.Console.WriteLine();
            else
            {
                ForegroundColor = ConsoleColor.White;
                System.Console.Write(TimeStamp() + " ");
                ForegroundColor = color;
                System.Console.Write(line + "\n");
            }
        }
        #endregion

        public static void WriteLine()
        {
            System.Console.WriteLine();
        }
        
        public static string ReadLine()
        {
            return System.Console.ReadLine();
        }

        public static ConsoleColor BackgroundColor
        {
            get
            {
                return System.Console.BackgroundColor;
            }
            set
            {
                System.Console.BackgroundColor = value;
            }
        }

        public static void Clear()
        {
            System.Console.Clear();
        }

        public static ConsoleColor ForegroundColor
        {
            get
            {
                return System.Console.ForegroundColor;
            }
            set
            {
                System.Console.ForegroundColor = value;
            }
        }

        public static string TimeStamp()
        {
            return "Revora[Built by Sheekovic] ";
        }
    }
}
