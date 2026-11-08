// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace Revora.Database
{
    public unsafe class KoBoard
    {
        public struct Info
        {
            public string Name;
            public int KillCount;
            public uint KOID;
            public void WriteThis(System.IO.BinaryWriter BW)
            {
                if (Name == null)
                    Name = "";
                BW.Write(Name.Length);
                BW.Write(Encoding.ASCII.GetBytes(Name));
                BW.Write(KillCount);
                BW.Write(KOID);
            }
            public void ReadThis(System.IO.BinaryReader BR)
            {
                try
                {
                    Name = Encoding.ASCII.GetString(BR.ReadBytes(BR.ReadInt32()));
                    KillCount = BR.ReadInt32();
                    KOID = BR.ReadUInt32();
                }
                catch
                {
                    Name = "";
                    KillCount = 0;
                    KOID = 0;
                }
            }
        }
        public static Info[] KOBoard = new Info[500];
        public static Random Rnd = new Random();
        public static void New(string Name, int KO)
        {
            try
            {
                if (KO > 0)
                {
                    int MyPlace = 10;
                    for (int i = 9; i >= 0; i--)
                    {
                        if (KO >= KOBoard[i].KillCount)
                            MyPlace--;
                    }
                    if (MyPlace < 10)
                    {
                        for (int i = 8; i >= MyPlace; i--)
                            KOBoard[i + 1] = KOBoard[i];
                        Info K = new Info();
                        K.Name = Name;
                        K.KillCount = KO;
                        K.KOID = (uint)Rnd.Next(10000000);
                        KOBoard[MyPlace] = K;
                        Save();
                        Kernel.SendWorldMessage(new Network.GamePackets.Message(Name + " is Now Having a Kill Score on " + KO + " His Rank is Now " + (MyPlace + 1) + " on Top Ko Board.", Network.GamePackets.Message.Center), Server.GamePool);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        public static void Save()
        {
            FileStream FS = new FileStream(@"database\Slayer\KOBoard.dat", FileMode.OpenOrCreate);
            BinaryWriter BW = new BinaryWriter(FS);
            for (int i = 0; i < KOBoard.Length; i++)
                KOBoard[i].WriteThis(BW);
            BW.Close();
            FS.Close();
        }
        public static void Load()
        {
            Console.WriteLine("Loading KoBoard information!");
            if (System.IO.File.Exists(@"database\Slayer\KoBoard.dat"))
            {
                //System.Console.Write("\b{0}", Cargando.NextChar());
                FileStream FS = new FileStream(@"database\Slayer\KOBoard.dat", FileMode.Open);
                BinaryReader BR = new BinaryReader(FS);
                for (int i = 0; i < KOBoard.Length; i++)
                    KOBoard[i].ReadThis(BR);
                BR.Close();
                FS.Close();
                //Console.WriteLine("Done!");
            }
        }
    }
}