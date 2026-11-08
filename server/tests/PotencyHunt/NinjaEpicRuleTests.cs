using System;
using BitConverter = System.BitConverter;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Revora;
using Revora.Database;
using Revora.Game;

internal static partial class PotencyHuntTests
{
    static readonly Type NinjaEpic=typeof(Entity).Assembly.GetType("Revora.Game.Features.NinjaEpicRules");
    static object NinjaResult(int stage,int roll){return Call(NinjaEpic,null,"Resolve",Enum.ToObject(NinjaEpic.GetNestedType("Trial",System.Reflection.BindingFlags.NonPublic),stage),roll);}
    static T NinjaResultField<T>(object value,string field){return (T)value.GetType().GetField(field,Instance).GetValue(value);}
    static void VerifyNinjaEpicRules(string runtime) {
        int[][] admission={new[]{50,2,70,1},new[]{53,2,90,1},new[]{55,2,70,1},new[]{55,2,69,0},
            new[]{55,1,140,0},new[]{55,0,140,0},new[]{55,3,140,0},new[]{49,2,70,0},new[]{56,2,70,0},
            new[]{45,2,140,0},new[]{165,2,140,0}};
        foreach(var row in admission)Check((bool)Call(NinjaEpic,null,"Eligible",(byte)row[0],(byte)row[1],(byte)row[2])==(row[3]==1),
            "reviewed Ninja Epic admission example: "+row[0]+"/"+row[1]+"/"+row[2]);
        Check(!(bool)Call(NinjaEpic,null,"BulkEligible",(byte)55,(byte)2,(byte)70,(byte)2)
            && (bool)Call(NinjaEpic,null,"BulkEligible",(byte)55,(byte)2,(byte)70,(byte)3)
            && !(bool)Call(NinjaEpic,null,"BulkEligible",(byte)45,(byte)2,(byte)140,(byte)6),"bulk trial requires both quest eligibility and VIP 3");
        uint[] wins={3004462,3004463,3004464};int[] losses={0,1,5};
        for(int stage=1;stage<=3;stage++) {
            int successes=0;
            for(int roll=0;roll<100;roll++) {
                var outcome=NinjaResult(stage,roll);bool success=NinjaResultField<bool>(outcome,"Success");if(success)successes++;
                uint item=NinjaResultField<uint>(outcome,"Item");int count=NinjaResultField<int>(outcome,"Count");ushort back=NinjaResultField<ushort>(outcome,"ReturnMap");
                if(success ? item!=wins[stage-1] || count!=1 || back!=0 : item!=(stage==1?0u:3004465u) || count!=losses[stage-1] || back!=(stage==1?0:3849))
                    throw new Exception("Trial payout/return differs from reviewed graph.");
            }
            Check(successes==10,"all 100 possible draws produce precisely ten successes and preserve success/failure rewards in trial "+stage);
        }
        Check((int)Call(NinjaEpic,null,"ResolveBulk",(object)new[]{0,9,10,99,15,9,90,0,10,99})==4
            && (int)Call(NinjaEpic,null,"ResolveBulk",(object)new int[10])==10
            && (int)Call(NinjaEpic,null,"ResolveBulk",(object)Enumerable.Repeat(99,10).ToArray())==0,"bulk payment buys ten independent attempts with zero through ten possible seals");
        foreach(var bad in new[]{new[]{0,0},new[]{4,0},new[]{1,-1},new[]{3,100}}) {
            bool denied=false;try{NinjaResult(bad[0],bad[1]);}catch(ArgumentException){denied=true;}Check(denied,"unknown trial or out-of-range draw is rejected");
        }
        foreach(int size in new[]{0,1,9,11}) {
            bool denied=false;try{Call(NinjaEpic,null,"ResolveBulk",(object)new int[size]);}catch(ArgumentException){denied=true;}Check(denied,"bulk trial rejects a partial or oversized draw batch: "+size);
        }
        for(byte option=1;option<=3;option++)Check((uint)Call(NinjaEpic,null,"Scroll",option)==3004458u+option,"Stardust exchange names the installed scroll: "+option);
        Func<uint,bool> contains=id=>ConquerItemInformation.BaseInformations.ContainsKey(id);
        int pairs=0;
        foreach(var katana in ConquerItemInformation.BaseInformations.Values.Where(i=>i.ID/1000==601)) {
            uint epic=(uint)Call(NinjaEpic,null,"EpicKatana",katana.ID,contains);
            if(epic==0)continue;var target=ConquerItemInformation.BaseInformations[epic];
            if(epic/1000!=616 || epic%1000!=katana.ID%1000 || target.Level!=katana.Level)throw new Exception("Katana conversion changes its catalog level or quality.");pairs++;
        }
        Check(pairs>20,"every supported installed katana/epic pair preserves level and quality; pairs="+pairs);
        foreach(uint id in new uint[]{410339,500439,616439,617439,601999})Check((uint)Call(NinjaEpic,null,"EpicKatana",id,contains)==0,"Life's Eye rejects non-katana, already-epic or absent catalog input: "+id);
        Check((uint)Call(NinjaEpic,null,"EpicKatana",601439u,(Func<uint,bool>)(id=>id==601439))==0,"conversion fails closed when its matching epic destination is missing");
        VerifyNinjaTerrain(runtime);
    }
    static void VerifyNinjaTerrain(string runtime) {
        string[] hashes={"28717B072EF67478060DDC4F3A5ACB1547FA9DB67DDACB09C1173F985DEB7CD6","88AE0BB59A5F64D1594366C7268DEA566EDF28B48E05EADC0D66147E9F7C3D19","DBBC55C34419080392174E2601585F42040442D828E0FBEC94690A94C757C636"};
        for(ushort map=3849;map<=3851;map++) {
            byte[] raw=File.ReadAllBytes(Path.Combine(runtime,"database/maps/"+map+".map"));
            using(var sha=SHA256.Create())Check(BitConverter.ToString(sha.ComputeHash(raw)).Replace("-","")==hashes[map-3849],"Ninja trial terrain matches reviewed hash: "+map);
            int width=BitConverter.ToInt32(raw,0),height=BitConverter.ToInt32(raw,4);var landing=(ushort[])Call(NinjaEpic,null,"Landing",map);
            var world=World(runtime,map);var actors=new List<uint[]>();
            using(var c=DataHolder.MySqlConnection){c.Open();using(var q=new Npgsql.NpgsqlCommand("SELECT id,cellx,celly,lookface,type FROM public.npcs WHERE mapid=@map",c)){
                q.Parameters.AddWithValue("map",(int)map);using(var r=q.ExecuteReader())while(r.Read()) {
                    var actor=new Revora.Network.GamePackets.NpcSpawn{UID=Convert.ToUInt32(r[0]),MapID=map,X=Convert.ToUInt16(r[1]),Y=Convert.ToUInt16(r[2]),Mesh=Convert.ToUInt16(r[3]),Type=(Enums.NpcType)Convert.ToByte(r[4])};
                    actors.Add(new[]{actor.UID,(uint)actor.X,(uint)actor.Y});world.AddNpc(actor);
                }
            }}
            Func<int,int,bool> valid=(x,y)=>x>=0 && y>=0 && x<width && y<height && world.Floor[x,y,MapObjectType.Player];
            Check(valid(landing[0],landing[1]),"reviewed trial landing is walkable: "+map);
            var reached=new HashSet<int>{landing[1]*width+landing[0]};var queue=new Queue<int>(reached);
            while(queue.Count>0){int cell=queue.Dequeue(),x=cell%width,y=cell/width;
                foreach(var p in new[]{new[]{x-1,y},new[]{x+1,y},new[]{x,y-1},new[]{x,y+1}})
                    if(valid(p[0],p[1]) && reached.Add(p[1]*width+p[0]))queue.Enqueue(p[1]*width+p[0]);}
            foreach(var actor in actors) {
                uint id=actor[0],x=actor[1],y=actor[2];
                Check((ushort)Call(NinjaEpic,null,"MapForNpc",id)==map,"installed trial actor belongs to its reviewed map: "+id);
                Check(!valid((int)x,(int)y),"actual NPC collision prevents standing on its occupied tile: "+id);
                Check(reached.Any(cell=>Kernel.GetDistance((ushort)(cell%width),(ushort)(cell/width),(ushort)x,(ushort)y)<=6),"actual runtime floor has an ordinary walking route to the actor: "+id);
            }
        }
    }
}
