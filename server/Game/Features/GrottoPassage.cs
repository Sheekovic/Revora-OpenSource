using System;
using System.Globalization;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static class GrottoPassage
    {
        internal const uint Guardian=7764,ReturnGuardian=7765,Key=721347;
        internal static void Initialize() {
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                Q.Execute(c,t,@"CREATE TABLE IF NOT EXISTS revora_grotto_passages(
id uuid PRIMARY KEY,uid bigint NOT NULL CHECK(uid>0),kind integer NOT NULL CHECK(kind IN(1,2)),
key_uid bigint NOT NULL,stack_before integer NOT NULL,bound boolean NOT NULL,
paid_at timestamptz NOT NULL DEFAULT now(),entered_at timestamptz,closed_at timestamptz,
CHECK((kind=1 AND key_uid>0 AND stack_before BETWEEN 1 AND 65535) OR (kind=2 AND key_uid=0 AND stack_before=0 AND NOT bound)));
CREATE UNIQUE INDEX IF NOT EXISTS revora_grotto_open_passage ON revora_grotto_passages(uid) WHERE closed_at IS NULL;
CREATE OR REPLACE FUNCTION revora_grotto_map_changed() RETURNS trigger LANGUAGE plpgsql SET search_path FROM CURRENT AS $$
BEGIN
 IF NEW.mapid=2055 AND OLD.mapid IS DISTINCT FROM NEW.mapid THEN
  UPDATE revora_grotto_passages SET entered_at=COALESCE(entered_at,now()) WHERE uid=NEW.uid AND closed_at IS NULL;
 ELSIF OLD.mapid IN(2055,2056) AND NEW.mapid NOT IN(2055,2056) THEN
  UPDATE revora_grotto_passages SET closed_at=now() WHERE uid=NEW.uid AND entered_at IS NOT NULL AND closed_at IS NULL;
 END IF;
 RETURN NEW;
END $$;
DO $revora_trigger$ BEGIN
IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_grotto_map_changed') THEN
CREATE TRIGGER revora_grotto_map_changed AFTER UPDATE OF mapid ON entities FOR EACH ROW EXECUTE FUNCTION revora_grotto_map_changed();
ELSIF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid='entities'::regclass AND tgname='revora_grotto_map_changed' AND NOT tgisinternal
AND tgenabled='O' AND tgtype=17 AND tgfoid='revora_grotto_map_changed()'::regprocedure
AND tgnargs=0 AND tgqual IS NULL AND tgattr::text=((SELECT attnum::text FROM pg_attribute WHERE attrelid='entities'::regclass AND attname='mapid' AND NOT attisdropped))) THEN
RAISE EXCEPTION 'Revora trigger definition mismatch: revora_grotto_map_changed';
END IF; END $revora_trigger$;");
                t.Commit();
            }}
        }
        private static bool Near(GameState p,uint id,ushort map) {
            return Q.Near(p,id,map) && p.Map.ID==map && p.Map.BaseID==map;
        }
        private static bool Landing(ushort id,ref ushort x,ref ushort y) {
            Map map;MapsTable.MapInformation info;
            return Kernel.Maps.TryGetValue(id,out map) && map.ID==id && map.BaseID==id && map.Floor!=null
                && MapsTable.MapInformations.TryGetValue(id,out info) && info.ID==id && info.BaseID==id && info.Owner==0
                && PotencyGuide.FindLanding(map,ref x,ref y);
        }
        private static bool Usable(ConquerItem item) {
            return item.ID==Key && item.Position==0 && item.Warehouse==0 && item.StackSize>0 && item.Lock==0
                && !item.Suspicious && !item.InWardrobe && (item.Days==0 || DateTime.Now<item.DayStamp.AddDays(item.Days));
        }
        private static void Debit(NpgsqlConnection c,NpgsqlTransaction t,GameState p,ConquerItem key) {
            string sql=key.StackSize==1?"DELETE FROM items":"UPDATE items SET stacksize=stacksize-1";
            if(Q.Execute(c,t,sql+@" WHERE uid=@item AND entityid=@uid AND id=721347 AND position=0 AND warehouse=0
AND stacksize=@stack AND bound=@bound AND locked=0 AND COALESCE(suspicious,0)=0 AND COALESCE(inwardrobe,0)=0
AND COALESCE(NULLIF(days,''),'0')=@days AND (@permanent OR daystamp=@stamp)","item",(long)key.UID,"uid",(long)p.Entity.UID,
                "stack",(int)key.StackSize,"bound",key.Bound?1:0,"days",key.Days.ToString(CultureInfo.InvariantCulture),
                "permanent",key.Days==0,"stamp",key.DayStamp.ToBinary())!=1)
                throw new InvalidOperationException("Grotto key ownership changed.");
        }
        private static void Enter(GameState p,bool vip) {
            ushort x=537,y=568;if(!Landing(2055,ref x,ref y)){p.Send("The passage is closed for now.");return;}
            ConquerItem paid=null;
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                using(var q=Q.Command(c,t,"SELECT mapid FROM entities WHERE uid=@uid FOR UPDATE","uid",(long)p.Entity.UID)) {
                    var saved=q.ExecuteScalar();if(saved==null || Convert.ToUInt32(saved)!=2054)throw new InvalidOperationException("Grotto visitor location changed.");
                }
                if(!Near(p,Guardian,2054))return;
                bool existing;
                using(var q=Q.Command(c,t,"SELECT EXISTS(SELECT 1 FROM revora_grotto_passages WHERE uid=@uid AND closed_at IS NULL)","uid",(long)p.Entity.UID))existing=Equals(q.ExecuteScalar(),true);
                if(!existing) {
                    if(vip) {if(p.Entity.VIPLevel<4){p.Send("VIP 4 opens this passage without a key.");return;}}
                    else {
                        paid=p.Inventory.Objects.Where(Usable).OrderByDescending(i=>i.Bound).ThenBy(i=>i.UID).FirstOrDefault();
                        if(paid==null){p.Send("Hunt on the third or fourth floor for a Grotto Key.");return;}
                        Debit(c,t,p,paid);
                    }
                    if(Q.Execute(c,t,@"INSERT INTO revora_grotto_passages(id,uid,kind,key_uid,stack_before,bound)
VALUES(@id,@uid,@kind,@item,@stack,@bound)","id",Guid.NewGuid(),"uid",(long)p.Entity.UID,"kind",vip?2:1,
                        "item",paid==null?0L:(long)paid.UID,"stack",paid==null?0:(int)paid.StackSize,"bound",paid!=null && paid.Bound)!=1)
                        throw new InvalidOperationException("Grotto passage was not saved.");
                }
                t.Commit();
            }}
            if(paid!=null) {
                if(paid.StackSize==1)Q.Install(p,new[]{paid.UID},new ConquerItem[0]);
                else {paid.StackSize--;paid.Mode=Enums.ItemMode.Update;paid.Send(p);}
            }
            p.Entity.Teleport(2055,x,y);
            if(p.Entity.MapID!=2055 || p.Entity.X!=x || p.Entity.Y!=y)throw new InvalidOperationException("Grotto passage requires resuming.");
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(p==null || request==null)return false;
            bool entrance=p.ActiveNpc==Guardian;
            bool returning=p.ActiveNpc==ReturnGuardian && request.OptionID==4;
            if(!entrance && !returning)return false;
            if(p.ItemSyncRoot==null)return true;
            lock(p.ItemSyncRoot)try {
                if((request.NpcID!=0 && request.NpcID!=p.ActiveNpc) || !Near(p,p.ActiveNpc,entrance?(ushort)2054:(ushort)2055))return true;
                if(returning) {
                    ushort x=388,y=463;if(Landing(2054,ref x,ref y))p.Entity.Teleport(2054,x,y);
                    else p.Send("The way back is closed for now.");return true;
                }
                if(request.OptionID==3 || request.OptionID==4){Enter(p,request.OptionID==4);return true;}
                if(request.OptionID==0 || request.OptionID==1 || request.OptionID==2) {
                    var dialog=new Npcs(p);dialog.Avatar(52);
                    dialog.Text("A Grotto Key from the third or fourth floor opens this gate. VIP 4 may pass freely. If you've paid already, I'll let you through.");
                    dialog.Option("Open the gate.",3);dialog.Option("Use my VIP passage.",4);dialog.Option("Not yet.",255);dialog.Send();
                }
            }catch(Exception ex){Q.Fail(p,"grotto-passage",ex);}
            return true;
        }
    }
}
