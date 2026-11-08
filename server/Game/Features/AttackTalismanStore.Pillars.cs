using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using Scenes=Revora.Game.Features.AttackTalismanScenes;
namespace Revora.Game.Features
{
    internal static partial class AttackTalismanStore
    {
        internal const uint PillarStart=9910410,Platform=9910414;
        internal static readonly ushort[,] PillarPoints={{680,720},{694,720},{680,734},{694,734}};
        internal static readonly uint[] PillarGems={710216,710217,710218,710219};
        internal static void InstallPillars(){
            using(var sha=System.Security.Cryptography.SHA256.Create())using(var file=System.IO.File.OpenRead(System.IO.Path.Combine(Constants.DMapsPath,"maps","1015.map")))
                if(System.BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!="e47df70501200965040755a424873395172c03e7dad920d483414f85c0359ba7")throw new InvalidOperationException("Fan pillar terrain changed.");
            string[] names={"SparrowPillar","DragonPillar","TigerPillar","LeopardPillar"};
            for(int i=0;i<4;i++)CityGuardSystem.SpawnTalker(PillarStart+(uint)i,1015,PillarPoints[i,0],PillarPoints[i,1],3500,names[i],0);
            CityGuardSystem.SpawnTalker(Platform,1015,686,723,3540,"GardenPlatform",0);
        }
        private static bool AtPillarObject(GameState p,uint uid,ushort x,ushort y){
            Revora.Interfaces.INpc npc;return p.Entity.MapID==1015 && p.Map!=null && p.Map.ID==1015 && p.Map.BaseID==1015 && p.ActiveNpc==uid && p.Map.Npcs.TryGetValue(uid,out npc)
                && npc.UID==uid && npc.MapID==1015 && npc.X==x && npc.Y==y && Kernel.GetDistance(p.Entity.X,p.Entity.Y,x,y)<=3;
        }
        internal static bool OfferPillar(GameState p,int pillar,Func<bool> context){
            Ready(p,context);if(pillar<0 || pillar>3 || !AtPillarObject(p,PillarStart+(uint)pillar,PillarPoints[pillar,0],PillarPoints[pillar,1]))return false;uint consumed;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);int bit=1<<pillar;if(run.Stage!=9 || (run.Pillars&bit)!=0)return false;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_fan_courtesan_kills WHERE uid=@uid)","uid",(long)p.Entity.UID))if(!Equals(q.ExecuteScalar(),true))return false;
                consumed=Materials(c,t,p,PillarGems[pillar]).FirstOrDefault(id=>Material(c,t,p,id,PillarGems[pillar]));if(consumed==0)return false;
                Consume(c,t,p,consumed,PillarGems[pillar]);
                if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET pillars=@after WHERE uid=@uid AND stage=9 AND pillars=@before","uid",(long)p.Entity.UID,"before",run.Pillars,"after",run.Pillars|bit)!=1)throw new InvalidOperationException("Fan pillar progress changed.");
                Ready(p,()=>context() && AtPillarObject(p,PillarStart+(uint)pillar,PillarPoints[pillar,0],PillarPoints[pillar,1]));t.Commit();
            }}Q.Install(p,new[]{consumed},new ConquerItem[0]);return true;
        }
        private static bool BeginGardenVisit(GameState p,Scenes.Scene scene,Func<bool> context){
            if(!Scenes.Registered(scene,p) || Clock()>=scene.Until || !AtPillarObject(p,Platform,686,723))return false;ulong silver=p.Entity.Money;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Owner(c,t,p);var run=Read(c,t,p);if((run.Stage!=9 && run.Stage!=10) || run.Pillars!=15 || run.BookGiven)return false;
                if(Q.Execute(c,t,"INSERT INTO revora_fan_visits(uid,visit,map,active,until_at,base_map) VALUES(@uid,@visit,@map,true,@until,3937) ON CONFLICT DO NOTHING","uid",(long)p.Entity.UID,"visit",scene.Visit,"map",(int)scene.Map.ID,"until",scene.Until)!=1)return false;
                if(run.BoxWinner<0){int winner=GardenRoll(3);if(winner<0 || winner>2)throw new InvalidOperationException("Fan garden roll invalid.");
                    if(Q.Execute(c,t,"UPDATE revora_attack_talisman SET box_winner=@box WHERE uid=@uid AND box_winner=-1","box",winner,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Fan treasure selection changed.");}
                if(run.Stage==9){SilverBalance.AssertSaved(c,t,p);silver=checked(p.Entity.Money+100);if(silver>Entity.MaximumSilver)return false;SilverBalance.Persist(c,t,p,silver);Save(c,t,p,9,10);}
                Ready(p,()=>context() && Scenes.Registered(scene,p) && Clock()<scene.Until && AtPillarObject(p,Platform,686,723));t.Commit();
            }}SilverBalance.ApplyCommitted(p,silver);return true;
        }
    }
}
