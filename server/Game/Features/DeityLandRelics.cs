using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
 internal static class DeityLandRelics {
  internal const ushort MapId=10250;
  internal const uint Guide=900000171,Keeper=900000172,Family=990250;
  private sealed class Death {internal Time32 Stamp;internal bool Seen;}
  private static readonly ConditionalWeakTable<Entity,Death> Deaths=new ConditionalWeakTable<Entity,Death>();
  internal static Func<int,int> Roll=n=>Kernel.Random.Next(n);
  private static bool enabled;
  internal static void Initialize(){
   if(!MapsTable.MapInformations.ContainsKey(MapId)) {Console.WriteLine("[deityland] awaiting map provisioning");return;}
   using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_deity_relic_rewards(
    id uuid PRIMARY KEY,uid bigint NOT NULL,item_type integer NOT NULL CHECK(item_type BETWEEN 4100001 AND 4100005),
    attr1 bigint NOT NULL,attr2 bigint NOT NULL,attr3 bigint NOT NULL,earned_at timestamptz NOT NULL DEFAULT now(),
    item_uid bigint UNIQUE,claimed_at timestamptz);");}
   Map map;if(!Kernel.Maps.TryGetValue(MapId,out map))map=new Map(MapId,"");
   foreach(uint id in new uint[]{4100001,4100002,4100003,4100004,4100005})
    if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Relic catalog missing.");
   Revora.Interfaces.INpc guide,keeper;
   if(!Kernel.Maps[1002].Npcs.TryGetValue(Guide,out guide) || !map.Npcs.TryGetValue(Keeper,out keeper))
    throw new InvalidOperationException("DeityLand NPC provisioning missing.");
   guide.Name="DeityGuide";keeper.Name="SaintTaiyi";
   int spawned=map.Entities.Values.Count(e=>e.MonsterInfo!=null && e.MonsterInfo.ID==Family);
   if(spawned!=40)throw new InvalidOperationException("DeityLand requires 40 spawned Banshees; found "+spawned+". Check spawn terrain.");
   enabled=true;Console.WriteLine("[deityland] relic hunting ready; monsters="+spawned+" chance=5% attributes=3 level=120");
  }
  internal static bool TryDrop(Entity mob,Entity killer){
   if(mob==null || mob.MapID!=MapId || mob.MonsterInfo==null || mob.MonsterInfo.ID!=Family)return false;
   var p=killer==null?null:killer.Owner;
   if(!enabled || !Q.Player(p) || !ReferenceEquals(p.Entity,killer) || killer.MapID!=MapId
     || p.Entity.Level<120 || !ReferenceEquals(mob.MonsterInfo.Owner,mob) || mob.EntityFlag!=EntityFlag.Monster || mob.Companion || !mob.Dead || Kernel.GetDistance(killer.X,killer.Y,mob.X,mob.Y)>32)return true;
   Entity current;
   if(p.Map==null || p.Map.BaseID!=MapId || !p.Map.Entities.TryGetValue(mob.UID,out current) || !ReferenceEquals(current,mob))return true;
   var death=Deaths.GetValue(mob,_=>new Death());
   lock(death){
    if(death.Seen && death.Stamp.Equals(mob.DeathStamp))return true;
    death.Stamp=mob.DeathStamp;death.Seen=true;
    int chance=Roll(100);if(chance<0 || chance>=100)throw new InvalidOperationException("Invalid relic chance.");
    if(chance>=5)return true;
    lock(p.ItemSyncRoot)try{
     if(!Q.Player(p))return true;
     var item=Relics.CreateHuntRelic((uint)(4100001+Roll(5)),Roll);
     using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
      SkillTable.LockOwner(c,t,p);
      Q.Execute(c,t,@"INSERT INTO revora_deity_relic_rewards(id,uid,item_type,attr1,attr2,attr3)
       VALUES(@id,@uid,@type,@a,@b,@d)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"type",(int)item.ID,
       "a",(long)item.RelicAttrib1,"b",(long)item.RelicAttrib2,"d",(long)item.RelicAttrib3);t.Commit();
     }}
     p.Send("You found an ancient Relic. Saint Taiyi will identify it for you.");
    }catch(Exception ex){Q.Fail(p,"deity-relic-earn",ex);}
   }return true;
  }
  private static void Claim(GameState p){
   lock(p.ItemSyncRoot)try{
    if(!Q.Near(p,Keeper,MapId))return;
    if(p.Inventory.Count>=40){p.Send(Constants.FullInventory);return;}
    ConquerItem item;Guid claim;
    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
     SkillTable.LockOwner(c,t,p);
     using(var q=Q.Command(c,t,"SELECT id,item_type,attr1,attr2,attr3 FROM revora_deity_relic_rewards WHERE uid=@uid AND claimed_at IS NULL ORDER BY earned_at,id LIMIT 1 FOR UPDATE","uid",(long)p.Entity.UID))
     using(var r=q.ExecuteReader()){
      if(!r.Read()){p.Send("You have no unidentified Relics. Hunt the spirits beyond the camp.");return;}
      claim=r.GetGuid(0);item=new ConquerItem(true){ID=Convert.ToUInt32(r[1]),UID=ConquerItem.ItemUID.Next,StackSize=1,
       Durability=10000,MaximDurability=10000,RelicAttrib1=Convert.ToUInt32(r[2]),RelicAttrib2=Convert.ToUInt32(r[3]),RelicAttrib3=Convert.ToUInt32(r[4])};
     }
     var saved=new System.Collections.Generic.List<uint>();
     using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0 ORDER BY uid","uid",(long)p.Entity.UID))
     using(var r=q.ExecuteReader())while(r.Read())saved.Add(Convert.ToUInt32(r[0]));
     if(saved.Count>=40 || !saved.SequenceEqual(p.Inventory.Objects.Select(i=>i.UID).OrderBy(i=>i)))throw new InvalidOperationException("Relic inventory requires reload.");
     if(Q.Execute(c,t,@"INSERT INTO items(uid,id,entityid,position,warehouse,stacksize,bound,durability,maximdurability,relicattrib1,relicattrib2,relicattrib3)
      VALUES(@item,@type,@uid,0,0,1,0,10000,10000,@a,@b,@d)","item",(long)item.UID,"type",(int)item.ID,"uid",(long)p.Entity.UID,
      "a",(long)item.RelicAttrib1,"b",(long)item.RelicAttrib2,"d",(long)item.RelicAttrib3)!=1)throw new InvalidOperationException("Relic insert suppressed.");
     if(Q.Execute(c,t,"UPDATE revora_deity_relic_rewards SET claimed_at=now(),item_uid=@item WHERE id=@id AND uid=@uid AND claimed_at IS NULL",
      "item",(long)item.UID,"id",claim,"uid",(long)p.Entity.UID)!=1)throw new InvalidOperationException("Relic claim changed.");
     if(!Q.Near(p,Keeper,MapId))throw new InvalidOperationException("Relic claim session changed.");t.Commit();
    }}
    Q.Install(p,new uint[0],new[]{item});p.Send("Your Relic is ready. Equip it from your inventory.");
   }catch(Exception ex){Q.Fail(p,"deity-relic-claim",ex);}
  }
  private static void Repair(GameState p){
   lock(p.ItemSyncRoot)try{
    if(!Q.Near(p,Keeper,MapId))return;
    var item=p.Equipment.TryGetItem(ConquerItem.Relics);
    if(item==null || item.ID<4100001 || item.ID>4100005 || item.Warehouse!=0 || item.InWardrobe || item.StackSize>1)return;
    if(item.Durability>=item.MaximDurability){p.Send("Your Relic needs no repair.");return;}
    if(p.Entity.ConquerPoints<1000){p.Send("Repairs cost 1,000 CPs.");return;}
    uint next=p.Entity.ConquerPoints-1000;
    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
     SkillTable.LockOwner(c,t,p);
     if(Q.Execute(c,t,@"UPDATE items SET durability=@max WHERE uid=@item AND entityid=@uid AND id=@type
      AND position=13 AND warehouse=0 AND durability=@old AND maximdurability=@max",
      "max",(int)item.MaximDurability,"item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"old",(int)item.Durability)!=1)
      throw new InvalidOperationException("Relic repair item changed.");
     if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@next WHERE uid=@uid AND conquerpoints=@old",
      "next",(long)next,"uid",(long)p.Entity.UID,"old",(long)p.Entity.ConquerPoints)!=1)throw new InvalidOperationException("Relic repair purse changed.");
     if(!Q.Near(p,Keeper,MapId))throw new InvalidOperationException("Relic repair session changed.");t.Commit();
    }}
    item.Durability=item.MaximDurability;item.Mode=Enums.ItemMode.Update;item.Send(p);
    p.Entity.ApplyCommittedConquerPoints(next);p.LoadItemStats();p.Send("Your Relic is restored.");
   }catch(Exception ex){Q.Fail(p,"deity-relic-repair",ex);}
  }
  internal static bool HandleNpc(GameState p,NpcRequest request){
   if(p==null || (p.ActiveNpc!=Guide && p.ActiveNpc!=Keeper))return false;
   ushort map=p.ActiveNpc==Guide?(ushort)1002:MapId;
   if(!enabled || request==null || !Q.Near(p,p.ActiveNpc,map))return true;
   if(request.OptionID==1){
    if(map==1002 && p.Entity.Level<120){p.Send("Reach level 120 before entering DeityLand.");return true;}
    ushort dest=map==1002?MapId:(ushort)1002,x=map==1002?(ushort)1022:(ushort)396,y=map==1002?(ushort)1283:(ushort)341;
    Map target;if(Kernel.Maps.TryGetValue(dest,out target) && PotencyGuide.FindLanding(target,ref x,ref y))p.Entity.Teleport(dest,x,y);
   }else if(request.OptionID==2 && map==MapId)Claim(p);
   else if(request.OptionID==3 && map==MapId)Repair(p);
   else if(request.OptionID==0){
    var d=new Npcs(p);d.Text(map==1002?"The spirits of DeityLand guard ancient Relics. I can guide seasoned hunters there.":"Hunt the spirits beyond camp. Bring your discoveries to me, and I will reveal their power.");
    d.Option(map==1002?"Enter DeityLand (level 120).":"Return to Twin City.",1);
    if(map==MapId){d.Option("Identify my Relic.",2);d.Option("Repair equipped Relic (1,000 CPs).",3);}d.Option("Farewell.",255);d.Send();
   }return true;
  }
 }
}
