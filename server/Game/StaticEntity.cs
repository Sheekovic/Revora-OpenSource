using Revora.Client;
using Revora.Interfaces;
using Revora.Network;
using Revora.Network.GamePackets;
using System;
using System.Linq;

namespace Revora.Game
{
    public class StaticEntity : Writer, IMapObject
    {
        public const uint FlagMesh = 8910;

        public object SyncRoot = new object();
        public NpcSpawn NpsEffect;
        private uint uid;
        private ushort x, y;

        public bool Viable { get; set; }
        public Time32 NotViableStamp;
        public uint SetBy = 0;

        public StaticEntity(uint uid, ushort x, ushort y, ushort map)
        {
            NpsEffect = new NpcSpawn();
            this.uid = uid;
            this.x = x;
            this.y = y;
            this.MapID = map;

            NpsEffect.X = x;
            NpsEffect.Type = Enums.NpcType.Flag;
            NpsEffect.Y = y;
            NpsEffect.MapID = map;
            NpsEffect.UID = uid;
            NpsEffect.NpcEffect = 152018;

        }
        public ushort X { get { return x; } set { x = value; } }
        public ushort Y { get { return y; } set { y = value; } }
        public ushort MapID { get; set; }
        public uint UID { get { return uid; } set { uid = value; } }
        public GameState Owner { get { return null; } }
        public MapObjectType MapObjType { get { return MapObjectType.StaticEntity; } }

        public void SendSpawn(Client.GameState client)
        {
            SendSpawn(client, true);
        }

        public void SendSpawn(Client.GameState client, bool checkScreen)
        {
            client.Send(NpsEffect);
        }
        public bool QuestionMark { get { return NpsEffect.Mesh == 767; } }
        public Enums.RaceItemType Type
        {
            get
            {
                switch (NpsEffect.Mesh)
                {
                    case 760: return Enums.RaceItemType.GuardPotion;
                    case 761: return Enums.RaceItemType.FrozenTrap;
                    case 762: return Enums.RaceItemType.SluggishPotion;
                    case 763: return Enums.RaceItemType.DizzyHammer;
                    case 764: return Enums.RaceItemType.RestorePotion;
                    case 765: return Enums.RaceItemType.ScreamBomb;
                    case 766: return Enums.RaceItemType.TransformItem;
                    case 768: return Enums.RaceItemType.SpiritPotion;
                    case 769: return Enums.RaceItemType.ExcitementPotion;
                    case 770: return Enums.RaceItemType.ChaosBomb;
                    default:
                        {

                            int val = (int)Enums.RaceItemType.TransformItem;
                            while (val == (int)Enums.RaceItemType.TransformItem || val == (int)Enums.RaceItemType.FrozenTrap)
                                val = Kernel.Random.Next((int)Enums.RaceItemType.ChaosBomb, (int)Enums.RaceItemType.SuperExcitementPotion);
                            return (Enums.RaceItemType)val;
                        }
                }
            }
        }

        public uint Hitpoints;
      
        public int Level { get; internal set; }

        private static Tuple<uint, string, ushort>[] raceItems = new[]
            {
                new  Tuple<uint, string, ushort>(760,"GuardPotion",1),
//                new  Tuple<uint, string, ushort>(761,"FrozenTrap",1),
                new  Tuple<uint, string, ushort>(762,"SluggishPotion",3),
                new  Tuple<uint, string, ushort>(763,"DizzyHammer",1),
                new  Tuple<uint, string, ushort>(764,"RestorePotion",3 ),
                new  Tuple<uint, string, ushort>(765,"ScreamBomb",1 ),
                new  Tuple<uint, string, ushort>(766,"SuperExclamationMark",1),
                new  Tuple<uint, string, ushort>(767,"SuperQuestionMark",1),
                new  Tuple<uint, string, ushort>(768,"SpiritPotion",1),
                new  Tuple<uint, string, ushort>(769,"ExcitementPotion",1),
                new  Tuple<uint, string, ushort>(770,"ChaosBomb",1)
            };


        public void Pick()
        {
            Viable = true;

            var itemData = raceItems[Kernel.Random.Next(raceItems.Length)];
            this.NpsEffect.Mesh = (ushort)itemData.Item1;
        }
        public void DoFlag()
        {
            Viable = true;
            this.NpsEffect.Mesh = (ushort)8910;
        }
        public void DoFrozenTrap(uint setter)
        {
            SetBy = setter;
            Viable = true;
            this.NpsEffect.Mesh = 761;
        }
        public void SendScreen(IPacket packet)
        {
            var Values = Kernel.GamePool.Values.ToArray();
            foreach (var pClient in Values)
            {
                if (pClient == null) continue;
                if (!pClient.Socket.Alive) continue;
                if (pClient.Entity.MapID != MapID) continue;
                if (Kernel.GetDistance(pClient.Entity.X, pClient.Entity.Y, X, Y) > Constants.pScreenDistance) continue;
                if (packet == null)
                {
                    NpsEffect.X = 900;
                    pClient.Send(NpsEffect);
                }
                else
                {
                    pClient.Send(packet);

                }
            }
        }
    }
}