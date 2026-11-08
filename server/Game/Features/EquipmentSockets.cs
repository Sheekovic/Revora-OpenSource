using System;
using System.Linq;
using Revora.Client;
using Revora.Database;
using Revora.Network;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    internal static class EquipmentSockets {
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static Func<int,int> Roll=max=>Kernel.Random.Next(max);
        internal static Action BeforeProjection;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_equipment_sockets(
id uuid PRIMARY KEY,uid bigint NOT NULL,target_uid bigint NOT NULL,item_type integer NOT NULL,
materials bigint[] NOT NULL,material_type integer NOT NULL,stacks integer[] NOT NULL,bound boolean NOT NULL,
before_one integer NOT NULL,before_two integer NOT NULL,after_one integer NOT NULL,after_two integer NOT NULL,
success boolean NOT NULL,refund_uid bigint UNIQUE,created_at timestamptz NOT NULL,
CHECK(cardinality(materials)=cardinality(stacks)),CHECK(cardinality(materials) IN(1,5,7,12)));
CREATE INDEX IF NOT EXISTS revora_socket_recent ON revora_equipment_sockets(uid,target_uid,created_at DESC);
CREATE TABLE IF NOT EXISTS revora_gem_embeddings(
id uuid PRIMARY KEY,uid bigint NOT NULL,target_uid bigint NOT NULL,gem_uid bigint NOT NULL,gem_type integer NOT NULL,
slot integer NOT NULL CHECK(slot IN(1,2)),before_stack integer NOT NULL CHECK(before_stack>0),
after_stack integer NOT NULL CHECK(after_stack=before_stack-1),created_at timestamptz NOT NULL,
UNIQUE(uid,target_uid,gem_uid,slot));");}
        }
        private static bool Ready(GameState p){return Q.Player(p) && p.Trade!=null && !p.Trade.InTrade && p.Booth==null && p.Entity.PokerTable==0;}
        private static bool Usable(ConquerItem i){return ArtifactStabilization.Usable(i) && i.Lock==0 && i.TimeLeftInMinutes==0;}
        private static ConquerItem Find(GameState p,uint uid){ConquerItem i;return p.Inventory.TryGetItem(uid,out i)?i:p.Equipment==null?null:p.Equipment.TryGetItem(uid);}
        private static bool GemCode(int value) {
            return value>0 && value<255 && Enum.IsDefined(typeof(Enums.Gem),(byte)value);
        }
        private static bool EmbedTarget(GameState p,ConquerItem item) {
            if(!Usable(item) || item.StackSize!=1 || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID) || ItemHandler.IsArrow(item.ID))return false;
            int kind=(int)ItemHandler.GetPositionFromID(item.ID),slot=item.Position>20?item.Position-20:item.Position;
            if(!(kind>=1 && kind<=6 || kind==8 || kind==10 || kind==11 || kind==19))return false;
            if(item.Position!=0 && slot!=kind && !(slot==5 && kind==4 && !ItemHandler.IsTwoHand(item.ID) && item.ID/1000!=421))return false;
            return ReferenceEquals(Find(p,item.UID),item) && Enum.IsDefined(typeof(Enums.Gem),(byte)item.SocketOne)
                && Enum.IsDefined(typeof(Enums.Gem),(byte)item.SocketTwo) && (item.SocketOne!=0 || item.SocketTwo==0);
        }
        internal static void Embed(GameState p,EmbedSocket request) {
            if(!Ready(p) || request==null)return;
            byte[] frame=request.ToArray();
            // Existing 1027 fields end at byte23; preserve bounded extension bytes.
            if(frame==null || frame.Length<32 || frame.Length>64 || BitConverter.ToUInt16(frame,0)!=frame.Length-8
                || BitConverter.ToUInt16(frame,2)!=1027 || BitConverter.ToUInt64(frame,frame.Length-8)!=PacketHandler.ClientSeal)return;
            if(request.Slot!=1 && request.Slot!=2 || request.Mode>1)return;
            lock(p.ItemSyncRoot)try {
                var item=Find(p,request.ItemUID);if(!Ready(p) || !EmbedTarget(p,item))return;
                int slot=request.Slot,one=(byte)item.SocketOne,two=(byte)item.SocketTwo,before=slot==1?one:two;
                int after=255,stack=0;ConquerItem gem=null;
                if(request.Mode==EmbedSocket.Add) {
                    if(before!=255 || !p.Inventory.TryGetItem(request.GemUID,out gem) || gem.UID==item.UID || !Usable(gem)
                        || gem.Position!=0 || gem.ID<700001 || gem.ID>700123 || !ConquerItemInformation.BaseInformations.ContainsKey(gem.ID)
                        || !GemCode((int)(gem.ID%1000)) || gem.Bound && !item.Bound)return;
                    after=(int)(gem.ID%1000);stack=gem.StackSize;
                    int kind=(int)ItemHandler.GetPositionFromID(item.ID),family=after/10;
                    if(kind==10?family!=10:kind==11?family!=12:kind==19?family!=10 && family!=12:family>7)return;
                } else if(!GemCode(before))return;
                uint type=item.ID;byte position=item.Position;bool bound=item.Bound,gemBound=gem!=null && gem.Bound;
                uint gemType=gem==null?0:gem.ID;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    SkillTable.LockOwner(c,t,p);
                    foreach(var owned in (gem==null?new[]{item}:new[]{item,gem}).OrderBy(i=>i.UID))ArtifactStabilization.Own(c,t,p,owned);
                    if(gem!=null) {
                        using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_gem_embeddings WHERE uid=@uid AND target_uid=@target AND gem_uid=@gem AND slot=@slot)",
                            "uid",(long)p.Entity.UID,"target",(long)item.UID,"gem",(long)gem.UID,"slot",slot))if(Equals(q.ExecuteScalar(),true))return;
                        string consume=stack==1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,consume+" WHERE uid=@uid AND entityid=@owner AND stacksize=@stack","uid",(long)gem.UID,"owner",(long)p.Entity.UID,"stack",stack)!=1)
                            throw new InvalidOperationException("Gem payment changed.");
                        if(Q.Execute(c,t,@"INSERT INTO revora_gem_embeddings(id,uid,target_uid,gem_uid,gem_type,slot,before_stack,after_stack,created_at)
VALUES(@id,@uid,@target,@gem,@type,@slot,@before,@after,@now)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"target",(long)item.UID,
                            "gem",(long)gem.UID,"type",(int)gem.ID,"slot",slot,"before",stack,"after",stack-1,"now",Clock())!=1)
                            throw new InvalidOperationException("Gem receipt was not saved.");
                    }
                    if(Q.Execute(c,t,@"UPDATE items SET socketone=@newOne,sockettwo=@newTwo WHERE uid=@uid AND entityid=@owner AND socketone=@one AND sockettwo=@two",
                        "newOne",slot==1?after:one,"newTwo",slot==2?after:two,"uid",(long)item.UID,"owner",(long)p.Entity.UID,"one",one,"two",two)!=1)
                        throw new InvalidOperationException("Gem socket preimage changed.");
                    EquipmentAchievements.Record(c,t,p.Entity.UID,Clock());
                    if(!Ready(p) || !EmbedTarget(p,item) || item.ID!=type || item.Position!=position || item.Bound!=bound
                        || (byte)item.SocketOne!=one || (byte)item.SocketTwo!=two
                        || gem!=null && (!Usable(gem) || gem.Position!=0 || gem.StackSize!=stack || gem.ID!=gemType || gem.Bound!=gemBound || !ReferenceEquals(Find(p,gem.UID),gem)))
                        throw new InvalidOperationException("Gem context changed.");
                    t.Commit();
                }}
                if(BeforeProjection!=null)BeforeProjection();
                if(gem!=null) {
                    if(stack==1)Q.Install(p,new[]{gem.UID},new ConquerItem[0]);
                    else {gem.StackSize--;gem.Mode=Enums.ItemMode.Update;gem.Send(p);}
                }
                if(slot==1)item.SocketOne=(Enums.Gem)after;else item.SocketTwo=(Enums.Gem)after;
                item.Mode=Enums.ItemMode.Update;item.Send(p);Q.Refresh(p);
                if(position!=0){p.LoadItemStats();p.Equipment.UpdateEntityPacket();}
                p.Send(request);
            }catch(Exception ex){Q.Fail(p,"gem-embedding",ex);}
        }
        internal static bool Parse(byte[] frame,out uint target,out uint[] selected) {
            target=0;selected=null;
            if(frame==null || frame.Length<107 || frame.Length>151 || BitConverter.ToUInt16(frame,2)!=1009 || BitConverter.ToUInt16(frame,20)!=43)return false;
            uint n=BitConverter.ToUInt32(frame,26);if(n!=1 && n!=5 && n!=7 && n!=12)return false;
            int length=95+4*(int)n;
            if(BitConverter.ToUInt16(frame,0)!=length || frame.Length!=length+8 || BitConverter.ToUInt64(frame,length)!=PacketHandler.ClientSeal)return false;
            target=BitConverter.ToUInt32(frame,8);if(target==0)return false;selected=new uint[n];
            for(int i=0;i<selected.Length;i++){uint uid=BitConverter.ToUInt32(frame,91+4*i);if(uid==0 || uid==target || selected.Take(i).Contains(uid))return false;selected[i]=uid;}
            return true;
        }
        private static bool Target(GameState p,ConquerItem item,out bool weapon) {
            weapon=false;if(!Usable(item) || item.StackSize!=1 || !ConquerItemInformation.BaseInformations.ContainsKey(item.ID) || ItemHandler.IsArrow(item.ID))return false;
            int slot=(int)ItemHandler.GetPositionFromID(item.ID);if(!(slot>=1 && slot<=6 || slot==8))return false;
            weapon=slot==4 || slot==5 && !ItemHandler.IsShield(item.ID);
            if(!Enum.IsDefined(typeof(Enums.Gem),(byte)item.SocketOne) || !Enum.IsDefined(typeof(Enums.Gem),(byte)item.SocketTwo)
                || item.SocketOne==0 && item.SocketTwo!=0 || item.SocketTwo!=0)return false;
            if(item.Position==0)return p.Inventory.ContainsUID(item.UID);
            int actual=item.Position>20?item.Position-20:item.Position;
            return actual==slot || actual==5 && slot==4 && !ItemHandler.IsTwoHand(item.ID) && item.ID/1000!=421;
        }
        internal static void Handle(GameState p,byte[] frame) {
            uint uid;uint[] selected;if(!Ready(p) || !Parse(frame,out uid,out selected))return;
            lock(p.ItemSyncRoot)try {
                var item=Find(p,uid);bool weapon;if(!Ready(p) || !Target(p,item,out weapon))return;
                byte one=(byte)item.SocketOne,two=(byte)item.SocketTwo,position=item.Position;uint type=item.ID;bool bound=item.Bound;
                int required=weapon?(one==0?1:5):one==0?12:selected.Length==1?1:7;
                uint materialType=weapon || one==0?1088000u:required==1?1200005u:1200006u;
                if(selected.Length!=required)return;
                var materials=new ConquerItem[selected.Length];
                for(int n=0;n<selected.Length;n++)if(!p.Inventory.TryGetItem(selected[n],out materials[n]) || !Usable(materials[n]) || materials[n].Position!=0
                    || materials[n].ID!=materialType || materials[n].Bound && !bound)return;
                var stacks=materials.Select(m=>(int)m.StackSize).ToArray();var bindings=materials.Select(m=>m.Bound).ToArray();
                if(materialType==1200005 && stacks[0]>1 && p.Inventory.Count>=40)return;
                DateTime now=Clock();if(now.Kind!=DateTimeKind.Utc)throw new InvalidOperationException("Socket clock must be UTC.");
                bool success=true;byte afterOne=one,afterTwo=two;ConquerItem refund=null;Guid receipt=Guid.NewGuid();
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_equipment_sockets WHERE uid=@uid AND target_uid=@item AND created_at>@now-interval '1 second')","uid",(long)p.Entity.UID,"item",(long)uid,"now",now))
                        if(Equals(q.ExecuteScalar(),true))return;
                    foreach(var owned in materials.Concat(new[]{item}).OrderBy(i=>i.UID))ArtifactStabilization.Own(c,t,p,owned);
                    using(var q=Q.Command(c,t,"SELECT uid FROM items WHERE uid=@item AND entityid=@uid AND socketone=@one AND sockettwo=@two","item",(long)uid,"uid",(long)p.Entity.UID,"one",(int)one,"two",(int)two))
                        if(q.ExecuteScalar()==null)throw new InvalidOperationException("Socket preimage changed.");
                    if(materialType==1200005){int draw=Roll(699);if(draw<0 || draw>=699)throw new InvalidOperationException("Drill roll outside range.");success=draw>=320;}
                    if(success){if(one==0)afterOne=(byte)Enums.Gem.EmptySocket;else afterTwo=(byte)Enums.Gem.EmptySocket;}
                    else {refund=Q.New(1200006,bindings[0]);using(var q=Q.Command(c,t,"SELECT count(*) FROM items WHERE entityid=@uid AND position=0 AND warehouse=0 AND COALESCE(inwardrobe,0)=0","uid",(long)p.Entity.UID))
                        if(Convert.ToInt64(q.ExecuteScalar())!=p.Inventory.Count)throw new InvalidOperationException("Drill refund bag preimage changed.");}
                    for(int n=0;n<materials.Length;n++) {
                        string sql=stacks[n]==1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
                        if(Q.Execute(c,t,sql+" WHERE uid=@item AND entityid=@uid AND stacksize=@stack","item",(long)selected[n],"uid",(long)p.Entity.UID,"stack",stacks[n])!=1)throw new InvalidOperationException("Socket payment was not consumed.");
                    }
                    if(Q.Execute(c,t,"UPDATE items SET socketone=@one,sockettwo=@two WHERE uid=@item AND entityid=@uid","one",(int)afterOne,"two",(int)afterTwo,"item",(long)uid,"uid",(long)p.Entity.UID)!=1)
                        throw new InvalidOperationException("Socket result was not saved.");
                    if(refund!=null)Q.Insert(c,t,p.Entity.UID,refund);
                    if(Q.Execute(c,t,@"INSERT INTO revora_equipment_sockets(id,uid,target_uid,item_type,materials,material_type,stacks,bound,before_one,before_two,after_one,after_two,success,refund_uid,created_at)
VALUES(@id,@uid,@item,@type,@materials,@material,@stacks,@bound,@bo,@bt,@ao,@at,@success,@refund,@now)","id",receipt,"uid",(long)p.Entity.UID,"item",(long)uid,"type",(int)type,
                        "materials",selected.Select(x=>(long)x).ToArray(),"material",(int)materialType,"stacks",stacks,"bound",bound,"bo",(int)one,"bt",(int)two,"ao",(int)afterOne,"at",(int)afterTwo,
                        "success",success,"refund",refund==null?(object)DBNull.Value:(long)refund.UID,"now",now)!=1)throw new InvalidOperationException("Socket receipt was not saved.");
                    if(success){if(one==0)ProgressionEvidence.Complete(c,t,p.Entity.UID,10425,"equipment-socket",receipt.ToString("N"),1);
                        else ProgressionEvidence.Complete(c,t,p.Entity.UID,10426,"equipment-socket",receipt.ToString("N"),1);}
                    if(!Ready(p) || !Target(p,item,out weapon) || item.ID!=type || item.Position!=position || item.Bound!=bound || (byte)item.SocketOne!=one || (byte)item.SocketTwo!=two
                        || !ReferenceEquals(Find(p,uid),item))throw new InvalidOperationException("Socket target context changed.");
                    for(int n=0;n<materials.Length;n++)if(!Usable(materials[n]) || materials[n].Position!=0 || materials[n].ID!=materialType || materials[n].StackSize!=stacks[n]
                        || materials[n].Bound!=bindings[n] || !ReferenceEquals(Find(p,selected[n]),materials[n]))throw new InvalidOperationException("Socket payment context changed.");
                    t.Commit();
                }}
                if(BeforeProjection!=null)BeforeProjection();
                Q.Install(p,materials.Where((m,n)=>stacks[n]==1).Select(m=>m.UID),refund==null?new ConquerItem[0]:new[]{refund});
                for(int n=0;n<materials.Length;n++)if(stacks[n]>1){materials[n].StackSize--;materials[n].Mode=Enums.ItemMode.Update;materials[n].Send(p);}
                item.SocketOne=(Enums.Gem)afterOne;item.SocketTwo=(Enums.Gem)afterTwo;item.Mode=Enums.ItemMode.Update;item.Send(p);Q.Refresh(p);
                if(position!=0){p.LoadItemStats();p.Equipment.UpdateEntityPacket();}
                var response=(byte[])frame.Clone();Writer.WriteUInt32(success?1u:0u,12,response);p.Send(response);
            }catch(Exception ex){Q.Fail(p,"equipment-socket",ex);}
        }
    }
}
