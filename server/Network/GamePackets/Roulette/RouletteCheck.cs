// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Revora.Roulette
{
    public unsafe struct MsgRouletteCheck
    {
        public struct Item
        {
            public byte Number;
            public uint BetPrice;
        }
        public static void Poroces(Client.GameState user, byte[] stream)
        {
            byte Count = stream[4];
            if (stream.Length >= Count * 5 && user.PlayRouletteUID != 0)
            {
                Database.Roulettes.RouletteTable Table;
                if (Database.Roulettes.RoulettesPoll.TryGetValue(user.PlayRouletteUID, out Table))
                {
                    Database.Roulettes.RouletteTable.Member player;
                    if (Table.RegistredPlayers.TryGetValue(user.Entity.UID, out player))
                    {
                        if (player.MyLuckNumber.Count != 0) return;
                        if (player.MyLuckExtra.Count != 0) return;
                        for (int x = 0; x < Count; x++)
                        {
                            var element = new Item();
                            element.Number = stream[5 + (x * 5)];
                            element.BetPrice = BitConverter.ReadUint(stream, 5 + (x * 5) + 1);
                            if (element.Number >= 0 && element.Number <= 37)
                            {
                                if (!player.MyLuckNumber.ContainsKey(element.Number))
                                {
                                    player.MyLuckNumber.TryAdd(element.Number, element);
                                }
                            }
                            else
                            {
                                if (!player.MyLuckExtra.ContainsKey(element.Number))
                                {
                                    player.MyLuckExtra.TryAdd(element.Number, element);
                                }
                            }
                        }
                        player.ShareBetting(Table);
                    }
                }
            }
        }
    }
}