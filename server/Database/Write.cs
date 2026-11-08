// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;

namespace Revora.Database
{
    public enum Mode : byte
    {
        Open = 1,
        Create = 2
    }
    public class Write : IDisposable
    {
        private string location = "";
        private string[] Items;
        private int Count = 0;
        private StreamWriter SW;
        public Write(string loc)
        {
            location = loc;
        }
        public void ChangeLocation(string loc)
        {
            location = loc;
        }
        public void Dispose()
        {
            Items = null;
            Count = 0;
            location = string.Empty;
            SW = null;
        }
        public Write Add(string[] data, int count)
        {
            Count = count;

            Items = new string[count];

            for (int x = 0; x < count; x++)
                Items[x] = data[x];

            return this;
        }
        public Write Execute(Mode mod)
        {
            bool Exit = true;
#pragma warning disable
            string Messaj = "";
#pragma warning restore
            if (mod == Mode.Open)
            {
                if (Exit == File.Exists(location))
                {
                    File.WriteAllText(location, string.Empty);
                    ((new FileInfo(location)).Open(FileMode.Truncate))
                        .Close();
                    using (SW = File.AppendText(location))
                    {
                        SW.WriteLine("Count=" + Count);
                        for (int x = 0; x < Count; x++)
                        {
                            SW.WriteLine(Items[x]);
                        }
                        SW.Close();
                    }
                }
                else
                {
                    Console.WriteLine(Messaj = "Write new Reader " + location + " location");
                    using (SW = File.AppendText(location))
                    {
                        SW.WriteLine("Count=" + Count);
                        for (int x = 0; x < Count; x++)
                        {
                            SW.WriteLine(Items[x]);
                        }
                        SW.Close();
                    }
                }
            }
            else
            {
                using (SW = File.AppendText(location))
                {
                    SW.WriteLine("Count=" + Count);
                    for (int x = 0; x < Count; x++)
                    {
                        SW.WriteLine(Items[x]);
                    }
                    SW.Close();
                }
            }
            return this;
        }
    }
}