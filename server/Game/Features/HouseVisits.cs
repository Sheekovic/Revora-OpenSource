using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q = Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class HouseVisits
    {
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;
        private static bool enabled;
        private sealed class Invite { internal Guid Id; internal GameState Host; internal Entity HostEntity, GuestEntity; internal DateTime Until; internal int Generation; }
        private sealed class State {
            internal GameState[] Menu=new GameState[0]; internal int Page, Generation;
            internal DateTime MenuUntil, LastInvite; internal Invite Pending; internal Guid Reviewed;
        }
        private sealed class Entry { internal Entity Entity; internal ushort Map; internal uint Host; internal bool Entering; }
        private static readonly ConditionalWeakTable<GameState,State> states=new ConditionalWeakTable<GameState,State>();
        private static readonly ConditionalWeakTable<GameState,Entry> entries=new ConditionalWeakTable<GameState,Entry>();
        private static State For(GameState p){return states.GetValue(p,_=>new State());}
        internal static bool Frame(byte[] frame)
        {
            if(frame==null || frame.Length<28 || frame.Length>128)return false;
            int length=System.BitConverter.ToUInt16(frame,0);
            return length>=20 && length<=120 && length+8==frame.Length && System.BitConverter.ToUInt16(frame,2)==2032
                && frame[17]<=length-18 && System.BitConverter.ToUInt64(frame,frame.Length-8)==Revora.Network.PacketHandler.ClientSeal;
        }
        internal static void Initialize()
        {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_house_visits(
id uuid PRIMARY KEY,host bigint NOT NULL REFERENCES entities(uid),guest bigint NOT NULL REFERENCES entities(uid),
map integer NOT NULL CHECK(map BETWEEN 60000 AND 65534),level integer NOT NULL CHECK(level BETWEEN 1 AND 5),
accepted_at timestamptz NOT NULL,arrived_at timestamptz,state integer NOT NULL DEFAULT 0 CHECK(state IN(0,1,2)),
CHECK(host<>guest),CHECK((state=1)=(arrived_at IS NOT NULL)));
CREATE UNIQUE INDEX IF NOT EXISTS revora_house_visit_pending ON revora_house_visits(guest) WHERE state=0;
CREATE INDEX IF NOT EXISTS revora_house_visit_host ON revora_house_visits(host) WHERE state=1;
CREATE INDEX IF NOT EXISTS revora_house_visit_guest ON revora_house_visits(guest) WHERE state=1;
CREATE OR REPLACE FUNCTION revora_house_visit_arrived() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
DECLARE changed integer;
BEGIN
 IF OLD.mapid=1002 AND EXISTS(SELECT 1 FROM revora_house_visits WHERE guest=NEW.uid AND map=NEW.mapid AND state=0) THEN
  UPDATE revora_house_visits v SET state=1,arrived_at=now() WHERE v.guest=NEW.uid AND v.map=NEW.mapid AND v.state=0
  AND EXISTS(SELECT 1 FROM revora_house_maps m JOIN house h ON h.id=m.uid AND h.owner=m.uid WHERE m.uid=v.host AND m.map_id=v.map AND h.houselevel=v.level)
  AND (SELECT count(*) FROM friends WHERE entityid=v.host AND friendid=v.guest)=1
  AND (SELECT count(*) FROM friends WHERE entityid=v.guest AND friendid=v.host)=1;
  GET DIAGNOSTICS changed=ROW_COUNT;
  IF changed<>1 THEN RAISE EXCEPTION 'House visit arrival was not saved'; END IF;
 END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_house_visit_arrived') THEN
CREATE TRIGGER revora_house_visit_arrived AFTER UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION revora_house_visit_arrived();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_house_visit_arrived' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_house_visit_arrived()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='mapid' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_house_visit_arrived';
END IF; END $revora_trigger$;");
                t.Commit();
            }}enabled=true;
        }
        private static bool Near(GameState p)
        {
            return enabled && Housing.Enabled && Q.Near(p,30156,1002) && p.Map.BaseID==1002 && p.Entity.PokerTable==0
                && p.Account!=null && p.Account.State==AccountTable.AccountState.Player && p.Friends!=null;
        }
        private static bool Friends(GameState a,GameState b)
        {
            return a!=null && b!=null && a!=b && Near(a) && Near(b)
                && a.Friends.ContainsKey(b.Entity.UID) && b.Friends.ContainsKey(a.Entity.UID);
        }
        private static bool SavedFriends(NpgsqlConnection c,NpgsqlTransaction t,GameState a,GameState b)
        {
            using(var q=Q.Command(c,t,@"SELECT
(SELECT count(*) FROM friends WHERE entityid=@a AND friendid=@b)=1 AND
(SELECT count(*) FROM friends WHERE entityid=@b AND friendid=@a)=1","a",(long)a.Entity.UID,"b",(long)b.Entity.UID))return Equals(q.ExecuteScalar(),true);
        }
        private static void Pair(GameState a,GameState b,Action action)
        {
            if(b==null || b.Entity==null || b.ItemSyncRoot==null || a==b)return;
            var first=a.Entity.UID<b.Entity.UID?a:b;var second=ReferenceEquals(first,a)?b:a;
            lock(first.ItemSyncRoot)lock(second.ItemSyncRoot)try{action();}
            catch(Exception ex){Q.Fail(a,"house-visit",ex);Q.Fail(b,"house-visit",ex);}
        }
        private static void LockPair(NpgsqlConnection c,NpgsqlTransaction t,GameState a,GameState b)
        {
            SkillTable.LockOwner(c,t,a.Entity.UID<b.Entity.UID?a:b);
            SkillTable.LockOwner(c,t,a.Entity.UID<b.Entity.UID?b:a);
        }
        private static void InviteFriend(GameState host,GameState guest)
        {
            Pair(host,guest,()=>{
                if(!Friends(host,guest))return;var source=For(host);var target=For(guest);DateTime now=Clock();
                if(source.LastInvite>now.AddSeconds(-1))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    LockPair(c,t,host,guest);if(Housing.Level(c,t,host.Entity.UID)<1){host.Send("Build your own home before inviting guests.");return;}if(!SavedFriends(c,t,host,guest))return;t.Commit();
                }}
                // One pending invitation per recipient; never replace an unexpired invitation from another host.
                if(Current(guest,target.Pending))return;
                source.LastInvite=now;target.Reviewed=Guid.Empty;
                target.Pending=new Invite{Id=Guid.NewGuid(),Host=host,HostEntity=host.Entity,GuestEntity=guest.Entity,Until=now.AddMinutes(2),Generation=source.Generation};
                host.Send("Your invitation is ready. Ask your friend to speak to House Admin.");
                guest.Send(host.Entity.Name+" invites you home. Speak to House Admin to accept.");
            });
        }
        private static bool Current(GameState guest,Invite invite)
        {
            return invite!=null && Clock()<invite.Until && Friends(invite.Host,guest)
                && ReferenceEquals(invite.Host.Entity,invite.HostEntity) && ReferenceEquals(guest.Entity,invite.GuestEntity)
                && For(invite.Host).Generation==invite.Generation;
        }
        private static void Accept(GameState guest,Invite invite)
        {
            if(invite==null)return;
            Pair(guest,invite.Host,()=>{
                var state=For(guest);var host=invite.Host;
                if(!ReferenceEquals(state.Pending,invite) || state.Reviewed!=invite.Id || !Current(guest,invite))return;
                state.Pending=null;state.Reviewed=Guid.Empty;
                var map=Housing.Load(host);if(map==null)return;
                ushort x=map.BaseID==1098?(ushort)32:map.BaseID==3024?(ushort)90:(ushort)50;
                ushort y=map.BaseID==1098?(ushort)31:map.BaseID==3024?(ushort)70:(ushort)50;
                if(!PotencyGuide.FindLanding(map,ref x,ref y))return;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    LockPair(c,t,host,guest);int level=Housing.Level(c,t,host.Entity.UID);
                    if(level==0 || !SavedFriends(c,t,host,guest) || !Current(guest,invite))return;
                    using(var q=Q.Command(c,t,@"SELECT count(*) FROM entities WHERE uid=ANY(@uids) AND mapid=1002",
                        "uids",new[]{(long)host.Entity.UID,(long)guest.Entity.UID}))if(Convert.ToInt64(q.ExecuteScalar())!=2)throw new InvalidOperationException("House visitor location changed.");
                    using(var q=Q.Command(c,t,"SELECT map_id FROM revora_house_maps WHERE uid=@uid","uid",(long)host.Entity.UID))
                        if(Convert.ToInt32(q.ExecuteScalar())!=map.ID)throw new InvalidOperationException("House visit ownership changed.");
                    Q.Execute(c,t,"UPDATE revora_house_visits SET state=2 WHERE guest=@uid AND state=0","uid",(long)guest.Entity.UID);
                    if(Q.Execute(c,t,"INSERT INTO revora_house_visits(id,host,guest,map,level,accepted_at) VALUES(@id,@host,@guest,@map,@level,@now)",
                        "id",invite.Id,"host",(long)host.Entity.UID,"guest",(long)guest.Entity.UID,"map",(int)map.ID,"level",level,"now",Clock())!=1)
                        throw new InvalidOperationException("House visit consent was not saved.");
                    t.Commit();
                }}
                var entry=new Entry{Entity=guest.Entity,Map=map.ID,Host=host.Entity.UID,Entering=true};entries.Remove(guest);entries.Add(guest,entry);
                try{guest.Entity.Teleport(map.BaseID,map.ID,x,y);}finally{entry.Entering=false;}
                if(guest.Entity.MapID!=map.ID || guest.Entity.X!=x || guest.Entity.Y!=y)throw new InvalidOperationException("House visit did not arrive.");
                using(var c=DataHolder.MySqlConnection){c.Open();using(var q=Q.Command(c,null,@"SELECT EXISTS(SELECT 1 FROM revora_house_visits v
JOIN entities e ON e.uid=v.guest WHERE v.id=@id AND v.state=1 AND e.mapid=v.map)","id",invite.Id))
                    if(!Equals(q.ExecuteScalar(),true))throw new InvalidOperationException("House visit arrival requires reload.");}
                Recover(host);Recover(guest);Q.Refresh(host);Q.Refresh(guest);
            });
        }
        internal static bool AllowTeleport(GameState p,ushort destination)
        {
            Entry entry;MapsTable.MapInformation info;
            return enabled && Q.Player(p) && entries.TryGetValue(p,out entry) && ReferenceEquals(entry.Entity,p.Entity)
                && entry.Map==destination && MapsTable.MapInformations.TryGetValue(destination,out info) && info.Owner==entry.Host
                && (entry.Entering || p.Entity.MapID==destination);
        }
        internal static void Recover(GameState p)
        {
            if(!enabled || !Q.Player(p,false))return;
            lock(p.ItemSyncRoot)try{
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);
                    Guid? host=null,guest=null;
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_house_visits WHERE host=@uid AND state=1 ORDER BY accepted_at,id LIMIT 1","uid",(long)p.Entity.UID)){
                        object value=q.ExecuteScalar();if(value!=null)host=(Guid)value;}
                    using(var q=Q.Command(c,t,"SELECT id FROM revora_house_visits WHERE guest=@uid AND state=1 ORDER BY accepted_at,id LIMIT 1","uid",(long)p.Entity.UID)){
                        object value=q.ExecuteScalar();if(value!=null)guest=(Guid)value;}
                    if(host.HasValue)ProgressionEvidence.Complete(c,t,p.Entity.UID,10131,"house-visit-host-v1",host.Value.ToString("N"),1);
                    if(guest.HasValue)ProgressionEvidence.Complete(c,t,p.Entity.UID,10132,"house-visit-guest-v1",guest.Value.ToString("N"),1);
                    t.Commit();
                }}
            }catch(Exception ex){Q.Fail(p,"house-visit-recovery",ex);}
        }
        internal static bool HandleNpc(GameState p,NpcRequest request)
        {
            if(p==null || request==null || p.ActiveNpc!=30156 || !enabled || !Housing.Enabled)return false;
            byte choice=request.OptionID;
            if(choice!=12 && choice!=13 && choice!=14 && choice!=15 && choice!=16 && (choice<60 || choice>65) && choice!=70 && choice!=71)return false;
            if(p.ItemSyncRoot==null || request.NpcID!=0 && request.NpcID!=30156)return true;
            GameState peer=null;Invite accepted=null;
            lock(p.ItemSyncRoot){
                if(!Near(p))return true;var state=For(p);var d=new Npcs(p);
                if(choice==16){state.Menu=Kernel.GamePool.Values.Where(v=>Friends(p,v)).OrderBy(v=>v.Entity.UID).Take(50).ToArray();state.Page=0;state.MenuUntil=Clock().AddMinutes(2);}
                if(choice==12){d.Text("A friend is always welcome. What shall I arrange?");d.Option("Invite a friend.",16);d.Option("Any invitations for me?",13);d.Option("Withdraw my invitations.",14);}
                else if(choice==16 || choice==15){
                    if(Clock()>=state.MenuUntil)return true;if(choice==15)state.Page=state.Page+6>=state.Menu.Length?0:state.Page+6;
                    d.Text(state.Menu.Length==0?"Bring a friend here, and I will show them the way.":"Who would you like to invite?");
                    for(int i=state.Page;i<Math.Min(state.Page+6,state.Menu.Length);i++)d.Option(state.Menu[i].Entity.Name,(byte)(60+i-state.Page));
                    if(state.Menu.Length>6)d.Option("More friends.",15);
                }else if(choice>=60 && choice<=65){
                    int index=state.Page+choice-60;if(Clock()<state.MenuUntil && index<state.Menu.Length)peer=state.Menu[index];
                    state.Menu=new GameState[0];
                }else if(choice==13){
                    if(Current(p,state.Pending)){state.Reviewed=state.Pending.Id;d.Text(state.Pending.Host.Entity.Name+" invites you home. Shall I show you in?");d.Option("I'd like to visit.",70);d.Option("Another time.",71);}
                    else{state.Pending=null;state.Reviewed=Guid.Empty;d.Text("No one is expecting you just now.");}
                }else if(choice==14){state.Generation++;state.Menu=new GameState[0];d.Text("Your invitations have been withdrawn.");}
                else if(choice==70)accepted=state.Pending;
                else if(choice==71){if(state.Pending!=null && state.Reviewed==state.Pending.Id){state.Pending=null;state.Reviewed=Guid.Empty;}d.Text("Perhaps another time.");}
                if(peer==null && accepted==null){d.Option("Farewell.",255);d.Send();}
            }
            if(peer!=null)InviteFriend(p,peer);if(accepted!=null)Accept(p,accepted);return true;
        }
    }
}
