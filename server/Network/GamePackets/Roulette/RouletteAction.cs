// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using Revora.Client;
using Revora.Network;

namespace Revora.Roulette
{
    public unsafe struct MsgRouletteAction
    {
        public enum Action : byte
        {
            Join = 3,
            RemovePlayer = 2,
            ShowSpectators = 1,
            Record = 0
        }
        public Action Typ;
        public static void Poroces(GameState user, byte[] stream)
        {
            MsgRouletteAction Info = new MsgRouletteAction();
            Info.Typ = (Action)stream[4];
            switch (Info.Typ)
            {
                case Action.Join:
                    {
                        Database.Roulettes.RouletteTable table;
                        if (user.WatchRoulette != 0)
                        {
                            if (Database.Roulettes.RoulettesPoll.TryGetValue(user.WatchRoulette, out table))
                            {
                                table.RemoveWatch(user.Entity.UID);
                                table.AddPlayer(user);
                            }
                        }
                        break;
                    }
                case Action.Record:
                    {

                        Database.Roulettes.RouletteTable table;
                        if (Database.Roulettes.RoulettesPoll.TryGetValue(user.PlayRouletteUID, out table))
                        {
                            var Winner = table.RegistredPlayers.Values.Where(p => p.Winning > 0).ToArray();
                            var Ranks = Winner.OrderByDescending(p => p.Winning).ToArray();

                            MsgRouletteRecord RecordGui = new MsgRouletteRecord();
                            RecordGui.ApplayUser(Ranks, (byte)table.LuckyNumber);
                            RecordGui.SendInfo(user);
                        }
                        if (user.WatchRoulette != 0)
                        {
                            if (Database.Roulettes.RoulettesPoll.TryGetValue(user.WatchRoulette, out table))
                            {
                                var Winner = table.RegistredPlayers.Values.Where(p => p.Winning > 0).ToArray();
                                var Ranks = Winner.OrderByDescending(p => p.Winning).ToArray();

                                MsgRouletteRecord RecordGui = new MsgRouletteRecord();
                                RecordGui.ApplayUser(Ranks, (byte)table.LuckyNumber);
                                RecordGui.SendInfo(user);
                            }
                        }
                        break;
                    }
                case Action.RemovePlayer:
                    {
                        if (user.OnDisconnect != null)
                            user.OnDisconnect = null;
                        Database.Roulettes.RouletteTable table;
                        if (user.PlayRouletteUID != 0)
                        {
                            if (Database.Roulettes.RoulettesPoll.TryGetValue(user.PlayRouletteUID, out table))
                            {
                                table.RemovePlayer(user);
                            }
                        }
                        else if (user.WatchRoulette != 0)
                        {
                            if (Database.Roulettes.RoulettesPoll.TryGetValue(user.WatchRoulette, out table))
                            {
                                table.RemoveWatch(user.Entity.UID);
                            }
                        }
                        break;
                    }
                case Action.ShowSpectators://not sure
                    {
                        Database.Roulettes.RouletteTable table;
                        if (user.PlayRouletteUID != 0)
                        {
                            if (Database.Roulettes.RoulettesPoll.TryGetValue(user.PlayRouletteUID, out table))
                            {
                                foreach (var client in table.ClientsWatch.Values)
                                {
                                    MsgRouletteScreen ScreenPacket = MsgRouletteScreen.Create();
                                    ScreenPacket.UID = client.Entity.UID;
                                    user.Send(ScreenPacket);
                                }

                            }
                        }
                        break;
                    }
            }
        }
    }
}