using System;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    public static class MarketErrandSystem
    {
        public const byte StartOption=20, TurnOption=21;
        private const string StageKey="market-errand", DayKey="market-errand-day";
        internal const uint RuneType=4030101;
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        private static int Stamp(){var now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Errand clock requires UTC.");return now.Year*1000+now.DayOfYear;}
        private static bool OnErrand(GameState p){return p!=null && p.Variables!=null && (p[StageKey]==1 || p[StageKey]==3);}
        private static bool DoneToday(GameState p){return p!=null && p.Variables!=null && p[StageKey]==2 && p[DayKey]>=Stamp();}
        private static bool Near(GameState p){return Q.Near(p,CpShopClerkSystem.NpcId,1002) && p.Variables!=null;}
        public static void Offer(GameState p,Npcs dialog){if(!DoneToday(p))dialog.Option(OnErrand(p)?"I visited the Market":"City errand",OnErrand(p)?TurnOption:StartOption);}
        public static bool Handle(GameState p,Npcs dialog,byte option){
            if(option!=StartOption && option!=TurnOption)return false;
            dialog.Text(Act(p,option==StartOption));dialog.Option("Farewell.",255);dialog.Send();return true;
        }
        private static byte[] Advance(Npgsql.NpgsqlConnection c,Npgsql.NpgsqlTransaction t,GameState p,int stage,int day)
        {
            byte[] before;
            using(var q=Q.Command(c,t,"SELECT data FROM variablevault WHERE entityid=@uid FOR UPDATE","uid",(long)p.Entity.UID))before=q.ExecuteScalar() as byte[];
            if(before==null)throw new InvalidOperationException("Errand variables missing.");
            var saved=new VariableVault();saved.Deserialize(before);
            if((int)saved[StageKey]!=(int)p[StageKey] || (int)saved[DayKey]!=(int)p[DayKey])throw new InvalidOperationException("Errand state changed.");
            byte[] merged;
            if(!p.Variables.TryMerge(before,new[]{StageKey,DayKey},new DynamicVariable[]{stage,day},out merged)
                || Q.Execute(c,t,"UPDATE variablevault SET data=@data WHERE entityid=@uid AND data=@before",
                    "data",merged,"uid",(long)p.Entity.UID,"before",before)!=1)throw new InvalidOperationException("Errand state was not saved.");
            return merged;
        }
        // Called only from the server's accepted-arrival path. An old PreviousMapID is not proof.
        internal static void Arrived(GameState p,Map map,ushort x,ushort y)
        {
            if(!Q.Player(p) || p.Variables==null || p[StageKey]!=1 || map==null || map.ID!=1036 || map.BaseID!=1036
                || p.Entity.MapID!=1036 || p.Entity.X!=x || p.Entity.Y!=y || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot)lock(p.Variables.SyncRoot){
                Map current;
                if(!Q.Player(p) || p[StageKey]!=1 || !Kernel.Maps.TryGetValue(1036,out current) || !ReferenceEquals(map,current)
                    || map.Floor==null || !map.Floor[x,y,MapObjectType.Player,null])return;
                byte[] merged;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    merged=Advance(c,t,p,3,(int)p[DayKey]);
                    if(!Q.Player(p))throw new InvalidOperationException("Errand owner changed.");t.Commit();
                }}
                p.Variables.ReplaceContents(merged);
            }}catch(Exception ex){Q.Fail(p,"market-visit",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
        private static string Act(GameState p,bool start)
        {
            if(!Near(p) || !SessionInstall.TryEnter(p.Entity.UID))return "Come speak with me when you are ready.";
            try{lock(p.ItemSyncRoot)lock(p.Variables.SyncRoot){
                if(!Near(p))return "Come closer.";
                int day=Stamp();
                if((int)p[DayKey]>day)return "The ledger is dated ahead. Return when the calendar catches up.";
                if(DoneToday(p))return "The ledger is filed. Come back tomorrow.";
                if(start && OnErrand(p))return "Finish your walk through the Market first.";
                if(!start && !OnErrand(p))return "I have not sent you yet.";
                if(!start && p[StageKey]!=3)return "Enter the Market through the portal at 312, 344, then return to me.";
                long cp=(long)p.Entity.ConquerPoints+Revora.Config.MarketErrandCp;
                long rp=(long)p.Entity.RevoraPoint+Revora.Config.MarketErrandRevoraPoints;
                decimal gold=(decimal)p.Entity.Money+Revora.Config.MarketErrandGold;
                ConquerItem rune=null;
                if(!start){
                    if(cp>uint.MaxValue || rp>uint.MaxValue || gold>Entity.MaximumSilver)return "Make room in your purse first.";
                    if(p.Inventory.Count>=40)return "Leave one space for your rune.";
                    if(!ConquerItemInformation.BaseInformations.ContainsKey(RuneType) || ConquerItemInformation.BaseInformations[RuneType].Class!=0)
                        throw new InvalidOperationException("Errand rune catalog unavailable.");
                    rune=Q.New(RuneType,true);
                }
                byte[] merged;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    // Keep the same entity-before-vault order as purse claims.
                    SilverBalance.AssertSaved(c,t,p);
                    if(!start){
                        using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                            if((long)q.ExecuteScalar()>=40)return "Leave one space for your rune.";
                        long? oldCp,oldRp;PurseBalance.CopySaved(p,out oldCp,out oldRp);
                        if(Q.Execute(c,t,@"UPDATE entities SET conquerpoints=@cp,revorapoint=@rp,money=@gold WHERE uid=@uid
AND (CASE WHEN @cpnull THEN conquerpoints IS NULL ELSE conquerpoints=@oldcp END)
AND (CASE WHEN @rpnull THEN revorapoint IS NULL ELSE revorapoint=@oldrp END)",
                            "cp",cp,"rp",rp,"gold",gold,"uid",(long)p.Entity.UID,"cpnull",!oldCp.HasValue,"oldcp",oldCp.GetValueOrDefault(),
                            "rpnull",!oldRp.HasValue,"oldrp",oldRp.GetValueOrDefault())!=1)throw new InvalidOperationException("Errand purse changed.");
                        Q.Insert(c,t,p.Entity.UID,rune);
                    }
                    merged=Advance(c,t,p,start?1:2,day);
                    if(!Near(p))throw new InvalidOperationException("Errand context changed.");t.Commit();
                }}
                p.Variables.ReplaceContents(merged);
                if(start)return "Walk through the Market portal at 312, 344, then bring word back to me.";
                p.Entity.ApplyCommittedConquerPoints((uint)cp);p.Entity.ApplyCommittedRevoraPoints((uint)rp);SilverBalance.ApplyCommitted(p,(ulong)gold);
                Q.Install(p,new uint[0],new[]{rune});
                return "The ledger is filed. Take this purse and rune for your trouble. Return tomorrow.";
            }}catch(Exception ex){Q.Fail(p,"market-errand",ex);return "Reconnect before we settle the errand.";}
            finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}
