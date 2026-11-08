// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Revora.Game;
using System.Threading;
using Revora.Game.Features;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Network.GamePackets
{
    public unsafe class ClanWarPacket : Writer, Interfaces.IPacket
    {
        byte[] Packet;
        public ClanWarPacket()
        {
            Packet = new byte[148];
            WriteUInt16(140, 0, Packet);
            WriteUInt16(1313, 2, Packet);
        }
        public byte Action
        {
            get { return Packet[4]; }
            set { Packet[4] = value; }
        }
        public uint UID
        {
            get { return BitConverter.ToUInt32(Packet, 8); }
            set { WriteUInt32(value, 8, Packet); }
        }
        public uint NpcUid
        {
            get { return BitConverter.ToUInt32(Packet, 12); }
            set { WriteUInt32(value, 12, Packet); }
        }
        public byte Button
        {
            get { return Packet[16]; }
            set { Packet[16] = value; }
        }
        public string Name
        {
            get { return Encoding.ASCII.GetString(Packet, 20, 16); }
            set { WriteString(value, 20, Packet); }
        }
        public string DominationMap
        {
            get { return Encoding.ASCII.GetString(Packet, 56, 16); }
            set { WriteString(value, 56, Packet); }
        }
        public byte Enter
        {
            get { return Packet[92]; }
            set { Packet[92] = value; }
        }
        public byte Unknown93
        {
            get { return Packet[93]; }
            set { Packet[93] = value; }
        }
        public byte Unknown94
        {
            get { return Packet[94]; }
            set { Packet[94] = value; }
        }
        public uint OccupationDays
        {
            get { return BitConverter.ToUInt32(Packet, 96); }
            set { WriteUInt32(value, 96, Packet); }
        }
        public uint Reward
        {
            get { return BitConverter.ToUInt32(Packet, 100); }
            set { WriteUInt32(value, 100, Packet); }
        }
        public uint NextReward
        {
            get { return BitConverter.ToUInt32(Packet, 104); }
            set { WriteUInt32(value, 104, Packet); }
        }
        public uint Free
        {
            get { return BitConverter.ToUInt32(Packet, 120); }
            set { WriteUInt32(value, 120, Packet); }
        }
        public byte[] ToArray()
        {
            return Packet;
        }
        public void Deserialize(byte[] buffer)
        {
            Packet = buffer;
        }
        public void Send(Client.GameState client)
        {
            client.Send(Packet);
        }
    }
    public class ClanWar
    {
        public class ClientWar
        {
            public uint UID;
            public uint Reward;
            public uint NextReward;
            public uint Free;
            public uint OccupationDays;
            public string Name = "None";
            public string CurentMap = "";
            public string DominationMap = "";
            public ClientWar(Clan clan)
            {
                UID = clan.ID;
                Name = clan.Name;
                Free = clan.Fund;
            }
            public ClientWar()
            {

            }
            public override string ToString()
            {
                if (Name.Contains('#'))
                    Name = Name.Replace("#", "");
                if (Name.Contains('^'))
                    Name = Name.Replace("^", "");
                StringBuilder build = new StringBuilder();
                build.Append(UID + "#" + Reward + "#" + NextReward
                    + "#" + OccupationDays + "#" + Name + "#" + CurentMap + "#" + DominationMap + "#" + Free + "#");
                return build.ToString();
            }
            public void Load(string data)
            {
                if (data == null) return;
                if (!data.Contains('#')) return;
                string[] line = data.Split('#');
                UID = uint.Parse(line[0]);
                Reward = uint.Parse(line[1]);
                NextReward = uint.Parse(line[2]);
                OccupationDays = uint.Parse(line[3]);
                Name = line[4];
                CurentMap = line[5];
                DominationMap = line[6];
                Free = uint.Parse(line[7]);
            }
        }
        public enum ClanArena : ushort
        {
            TwinCityClan = 0,
            PhoenixCastleClan = 1,
            ApeCityClan = 2,
            DesertCityClan = 3,
            BirdCityClan = 4,
            WindPlainClan = 5,
            MapleForestClan = 6,
            LoveCanyonClan = 7,
            DesertClan = 8,
            BirdIslandClan = 9,
            Count = 10
        }
        public static uint GetItemReward(ClanArena typ)
        {
            switch (typ)
            {
                case ClanArena.TwinCityClan:
                    return 722454;
                case ClanArena.WindPlainClan:
                    return 722455;
                case ClanArena.DesertCityClan:
                    return 722459;
                case ClanArena.DesertClan:
                    return 722460;
                case ClanArena.BirdCityClan:
                    return 722464;
                case ClanArena.BirdIslandClan:
                    return 722465;
                case ClanArena.ApeCityClan:
                    return 722469;
                case ClanArena.LoveCanyonClan:
                    return 722470;
                case ClanArena.PhoenixCastleClan:
                    return 722474;
                case ClanArena.MapleForestClan:
                    return 722475;
            }
            return 0;
        }
        public static ushort GetMap(ClanArena typ)
        {
            switch (typ)
            {
                case ClanArena.TwinCityClan:
                    return 1505;
                case ClanArena.WindPlainClan:
                    return 1555;
                case ClanArena.PhoenixCastleClan:
                    return 1509;
                case ClanArena.MapleForestClan:
                    return 1590;
                case ClanArena.DesertCityClan:
                    return 1508;
                case ClanArena.DesertClan:
                    return 1580;
                case ClanArena.BirdCityClan:
                    return 1507;
                case ClanArena.BirdIslandClan:
                    return 1570;
                case ClanArena.ApeCityClan:
                    return 1506;
                case ClanArena.LoveCanyonClan:
                    return 1560;
            }
            return 0;
        }
        private static ClanArena GetTournament(uint NpcUID)
        {
            if (NpcUID == 101895)
                return ClanArena.TwinCityClan;
            if (NpcUID == 101897)
                return ClanArena.WindPlainClan;
            if (NpcUID == 101901)
                return ClanArena.PhoenixCastleClan;
            if (NpcUID == 101902)
                return ClanArena.MapleForestClan;
            if (NpcUID == 101905)
                return ClanArena.ApeCityClan;
            if (NpcUID == 101906)
                return ClanArena.LoveCanyonClan;
            if (NpcUID == 101913)
                return ClanArena.DesertCityClan;
            if (NpcUID == 101914)
                return ClanArena.DesertClan;
            if (NpcUID == 101909)
                return ClanArena.BirdCityClan;
            if (NpcUID == 101910)
                return ClanArena.BirdIslandClan;
            return ClanArena.Count;
        }
        public static ClanTournament GetNpcTournament(uint NpcUID)
        {
            try
            {
                return Tournaments[(byte)GetTournament(NpcUID)];
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.ToString());
                return null;
            }
        }
        public static ClanTournament[] Tournaments;
        public static void Create()
        {
            Tournaments = new ClanTournament[(byte)ClanArena.Count];
            for (byte x = 0; x < (byte)ClanArena.Count; x++)
                Tournaments[x] = new ClanTournament((ClanArena)x);
            Load();
            ClanWarResults.Initialize();
            ClanWarResults.CancelInterrupted();
            ClanWarResults.Load(Tournaments);
        }
        public static bool GetMyWar(uint CLAN_ID, out ClientWar war)
        {
            foreach (var client_War in Tournaments)
                if (client_War.Client.UID == CLAN_ID)
                {
                    war = client_War.Client;
                    return true;
                }
            war = null;
            return false;
        }
        public static void Start()
        {
            bool opened=false;
            foreach(var tournament in Tournaments){bool was=tournament.Open;tournament.Start();opened|=!was && tournament.Open;}
            if(opened)Entity.SendWorldMessage("Clan War has begun. Meet your city's Clan War envoy. The last clan standing after twenty minutes claims the territory.");
        }
        public static void Tick() {
            var now=ClanWarResults.Clock().ToLocalTime();if(now.Hour==20 && now.Minute==33)Start();
        }
        internal sealed class CombatLock:IDisposable {
            private readonly List<object> locks=new List<object>();internal bool Allowed;
            internal bool Own(object gate){if(gate==null || !Monitor.TryEnter(gate))return false;locks.Add(gate);return true;}
            public void Dispose(){for(int i=locks.Count-1;i>=0;i--)Monitor.Exit(locks[i]);locks.Clear();}
        }
        internal static CombatLock CombatLease(Entity a,Entity b) {
            if(Tournaments==null || !Tournaments.Any(t=>a!=null && a.MapID==GetMap(t.Map) || b!=null && b.MapID==GetMap(t.Map)))return null;
            var lease=new CombatLock();try{
                if(a==null || b==null || a.Owner==null || b.Owner==null)return lease;
                foreach(var p in new[]{a.Owner,b.Owner}.Distinct().OrderBy(p=>p.Entity==null?0:p.Entity.UID))if(!lease.Own(p.ItemSyncRoot))return lease;
                if(!lease.Own(TournamentCombat.Gate))return lease;lease.Allowed=!BlockAttack(a,b);return lease;
            }catch{lease.Dispose();throw;}
        }
        internal static bool BlockAttack(Entity a,Entity b) {
            if(Tournaments==null)return false;
            var tour=Tournaments.FirstOrDefault(t=>a!=null && a.MapID==GetMap(t.Map) || b!=null && b.MapID==GetMap(t.Map));
            if(tour==null)return false;
            lock(TournamentCombat.Gate)return !tour.Open || a==null || b==null || a.MapID!=b.MapID || !tour.Eligible(a.Owner) || !tour.Eligible(b.Owner) || !ReferenceEquals(a.Owner.Entity,a) || !ReferenceEquals(b.Owner.Entity,b) || a.ClanId==b.ClanId;
        }
        public static void Save()
        {
            using (Database.Write _wr = new Database.Write("database\\Slayer\\ClanWar.txt"))
            {
                string[] items = new string[(byte)ClanArena.Count];
                for (byte x = 0; x < Tournaments.Length; x++)
                    items[x] = "" + (byte)Tournaments[x].Map + "^" + Tournaments[x].Client.ToString();
                _wr.Add(items, items.Length).Execute(Database.Mode.Open);
            }
        }
        public static void Load()
        {
            using (Database.Read r = new Database.Read("database\\Slayer\\ClanWar.txt"))
            {
                if (r.Reader())
                {
                    int count = r.Count;
                    for (uint x = 0; x < count; x++)
                    {
                        string data = r.ReadString("");
                        if (data != null)
                        {
                            ClanArena tour = (ClanArena)byte.Parse(data.Split('^')[0]);
                            Tournaments[(byte)tour].Client.Load(data.Split('^')[1]);
                        }
                    }
                }
            }
        }
        public class ClanTournament
        {
            public const int MinuteTimes = 20;
            public ClientWar Client;
            internal DateTime StartTimer;
            private IDisposable Subscribe;
            public ClanArena Map;
            public bool Open = false;
            private Guid run;
            private readonly Dictionary<uint,uint> entrants=new Dictionary<uint,uint>();
            private int ticking;
            public static Client.GameState client;
            public ClanTournament(ClanArena map)
            {
                if (!Constants.PKFreeMaps.Contains(GetMap(map)))Constants.PKFreeMaps.Add(GetMap(map));
                Map=map;Client=new ClientWar();Client.DominationMap=Client.CurentMap=Map.ToString();Client.Reward=Client.NextReward=GetItemReward(Map);
            }
            internal bool Eligible(Client.GameState p) {
                uint clan;return Q.Player(p) && p.Entity.MapID==GetMap(Map) && entrants.TryGetValue(p.Entity.UID,out clan)
                    && clan==p.Entity.ClanId && p.Entity.GetClan!=null;
            }
            public void Teleport(Client.GameState p)
            {
                if(p==null || p.ItemSyncRoot==null)return;
                lock(p.ItemSyncRoot)lock(TournamentCombat.Gate)try{
                    if(!Open || ClanWarResults.Clock()>=StartTimer.AddMinutes(MinuteTimes) || !NearEnvoy(p,p.JoinToWar)
                        || GetTournament(p.JoinToWar)!=Map || p.Entity.GetClan==null)return;
                    var clan=p.Entity.GetClan;var destination=Kernel.Maps[GetMap(Map)];var xy=destination.RandomCoordinates();
                    if(!ClanWarResults.Register(run,p,clan))return;
                    p.Entity.Teleport(GetMap(Map),xy.Item1,xy.Item2);
                    if(!Q.Player(p) || p.Entity.MapID!=GetMap(Map) || !ReferenceEquals(p.Entity.GetClan,clan))throw new InvalidOperationException("Clan entry did not arrive.");
                    ClanWarResults.Admit(run,p,clan.ID);entrants[p.Entity.UID]=clan.ID;
                    p.Send("Hold the field for your clan. If rivals remain after thirty minutes, neither clan wins.");
                }catch(Exception ex){Q.Fail(p,"clan-war-entry",ex);}
            }
            public void Start() {
                var now=ClanWarResults.Clock();var local=now.ToLocalTime();Begin(now,local.Year*10000+local.Month*100+local.Day,true);
            }
            internal void Begin(DateTime now,int day,bool subscribe) {
                lock(TournamentCombat.Gate){if(Open)return;var key=ClanWarResults.Open(Map,now,day);if(key==Guid.Empty)return;
                    run=key;StartTimer=now;entrants.Clear();Open=true;if(subscribe)Subscribe=Thread.Subscribe(Work,5000);}
            }
            private void Finish() {Open=false;if(Subscribe!=null){Subscribe.Dispose();Subscribe=null;}}
            private void Work(int time)
            {
                if(!Open || Interlocked.Exchange(ref ticking,1)!=0)return;
                var locks=new List<object>();var leases=new List<uint>();bool combat=false,relations=false;Client.GameState[] winners=new Client.GameState[0];bool closed=false;
                try{
                    var now=ClanWarResults.Clock();if(now<StartTimer.AddMinutes(MinuteTimes))return;
                    var field=Kernel.GamePool.Values.Where(p=>Q.Player(p) && p.Entity.MapID==GetMap(Map)).OrderBy(p=>p.Entity.UID).ToArray();
                    foreach(var p in field){if(!SessionInstall.TryEnter(p.Entity.UID))return;leases.Add(p.Entity.UID);}
                    foreach(var p in field){if(!Monitor.TryEnter(p.ItemSyncRoot))return;locks.Add(p.ItemSyncRoot);}
                    if(!Monitor.TryEnter(TournamentCombat.Gate))return;combat=true;
                    if(!Monitor.TryEnter(Clan.RelationshipSync))return;relations=true;
                    if(!Open)return;
                    var fresh=Kernel.GamePool.Values.Where(p=>Q.Player(p) && p.Entity.MapID==GetMap(Map)).Select(p=>p.Entity.UID).OrderBy(x=>x).ToArray();
                    if(!fresh.SequenceEqual(field.Select(p=>p.Entity.UID)))return;
                    var present=field.Where(Eligible).ToArray();var clans=present.Select(p=>p.Entity.ClanId).Distinct().ToArray();
                    if(clans.Length>1 && now<StartTimer.AddMinutes(30))return;
                    if(clans.Length==1)winners=present;
                    var winner=winners.Length==0?null:winners[0].Entity.GetClan;
                    if(!ClanWarResults.Finish(run,Map,winner,winners))return;
                    Finish();closed=true;ClanWarResults.Load(Tournaments);
                }catch(Exception ex){System.Console.WriteLine("[clan-war] settlement error="+ex.GetType().Name);}
                finally{if(relations)Monitor.Exit(Clan.RelationshipSync);if(combat)Monitor.Exit(TournamentCombat.Gate);for(int i=locks.Count-1;i>=0;i--)Monitor.Exit(locks[i]);for(int i=leases.Count-1;i>=0;i--)SessionInstall.Exit(leases[i]);Volatile.Write(ref ticking,0);}
                if(closed){
                    foreach(var p in winners){lock(p.ItemSyncRoot){if(Q.Player(p,false))Q.Refresh(p);}GetClientReward(p,p.Entity.GetClan);}
                    foreach(var p in Kernel.GamePool.Values.Where(p=>Q.Player(p,false) && p.Entity.MapID==GetMap(Map)).ToArray())lock(p.ItemSyncRoot){if(Q.Player(p,false) && p.Entity.MapID==GetMap(Map))p.Entity.Teleport(1002,301,279);}
                }
            }
            private void GetClientReward(Client.GameState p,Clan winner) {
                if(winner!=null && Q.Player(p,false) && ReferenceEquals(p.Entity.GetClan,winner))ClanWarResults.Pay(p);
            }
        }
        internal static void RecoverLocation(Client.GameState p) {
            if(Tournaments==null || !Q.Player(p,false) || !Tournaments.Any(t=>p.Entity.MapID==GetMap(t.Map)))return;
            lock(p.ItemSyncRoot)lock(TournamentCombat.Gate){
                var tour=Tournaments.FirstOrDefault(t=>p.Entity.MapID==GetMap(t.Map));
                if(tour!=null && !tour.Open && Q.Player(p,false))p.Entity.Teleport(1002,301,279);
            }
        }
        private static bool NearEnvoy(Client.GameState p,uint npc) {
            Revora.Interfaces.INpc actor;
            return Q.Player(p) && GetTournament(npc)!=ClanArena.Count && p.Booth==null && p.Trade!=null && !p.Trade.InTrade
                && p.Map.Npcs.TryGetValue(npc,out actor) && Kernel.GetDistance(p.Entity.X,p.Entity.Y,actor.X,actor.Y)<=6;
        }
        public static void Process(byte[] packet,Client.GameState p)
        {
            if(packet==null || packet.Length!=148 || BitConverter.ToUInt16(packet,0)!=140 || BitConverter.ToUInt16(packet,2)!=1313
                || Encoding.ASCII.GetString(packet,140,8)!="TQClient" || !Q.Player(p))return;
            lock(p.ItemSyncRoot){
                if(packet[4]==6){
                    uint npc=BitConverter.ToUInt32(packet,12);if(GetTournament(npc)==ClanArena.Count || !NearEnvoy(p,npc))return;
                    var tournament=GetNpcTournament(npc);if(tournament==null)return;p.JoinToWar=npc;
                    lock(TournamentCombat.Gate){var view=tournament.Client;
                        var response=new ClanWarPacket{Action=6,NpcUid=npc,UID=view.UID,Name=view.Name,DominationMap=view.DominationMap,
                            Enter=(byte)(tournament.Open && ClanWarResults.Clock()<tournament.StartTimer.AddMinutes(20)?1:0),Unknown93=1,Unknown94=1,
                            OccupationDays=view.OccupationDays,Reward=view.Reward,NextReward=view.NextReward,Free=view.Free};p.Send(response);
                    }
                } else if(packet[4]==8 && GetTournament(p.JoinToWar)!=ClanArena.Count){var tournament=GetNpcTournament(p.JoinToWar);if(tournament!=null)tournament.Teleport(p);}
            }
        }
    }
}
