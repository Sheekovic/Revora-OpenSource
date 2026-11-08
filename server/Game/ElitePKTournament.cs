using System;
using System.Linq;
using System.Text;
using Revora.Client;
using System.Collections.Generic;
using Revora.Network.GamePackets;

namespace Revora.Game.Features.Tournaments
{
    public unsafe class ElitePKTournament
    {
        public const byte
            EPK_Lvl100Minus = 0,
            EPK_Lvl100To119 = 1,
            EPK_Lvl120To129 = 2,
            EPK_Lvl130Plus = 3;

        public static ElitePK[] Tournaments;
        public const ushort WaitingAreaID = 2068;
        public static bool TimersRegistered;
        public static void Create()
        {
            Tournaments = new ElitePK[0];
            Revora.Game.Features.ElitePkEvent.Create();
            Revora.Game.Features.LocalPkWar.Create();
            Revora.Game.Features.CouplePkEvent.Create();
        }
        public static void GiveClientReward(Client.GameState client)
        {
            TitleRequirements.Reconcile(client, false);
        }
        public static bool  SignUp(GameState client)
        {
            return Revora.Game.Features.ElitePkEvent.Current != null && Revora.Game.Features.ElitePkEvent.Current.Enroll(client);
        }
        public static void RegisterTimers()
        {
            TimersRegistered = false; // Superseded by the single owned event tick.
        } 
    }
}