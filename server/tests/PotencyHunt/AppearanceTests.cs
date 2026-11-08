using System;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Npgsql;
using FlowerState=Revora.Game.Features.Flowers;
using Revora.Network.GamePackets;
using BitConverter=System.BitConverter;

internal static partial class PotencyHuntTests
{
    static readonly Type Appearance = typeof(Entity).Assembly.GetType("Revora.Game.Features.AppearanceState");
    static void LoadAppearance(GameState p) {
        using(var q=new MySqlCommand(MySqlCommandType.SELECT).Select("entities").Where("uid",p.Entity.UID))
        using(var r=new MySqlReader(q)){if(!r.Read())throw new Exception("Appearance fixture missing");Call(Appearance,null,"Loaded",p,r);}
    }
    static GameState AppearancePlayer(Map map) {
        var p=StoredProgressionPlayer(map);p.Entity.Body=1003;p.Entity.Face=1;p.Entity.HairStyle=2;
        if(!EntityTable.SaveEntity(p))throw new Exception("Appearance initial save failed");LoadAppearance(p);return p;
    }
    static void AppearanceTransaction(GameState p,ushort body,ushort face,ushort hair) {
        lock(p.ItemSyncRoot){
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                Call(Appearance,null,"Persist",c,t,p,body,face,hair);t.Commit();}}
            Call(Appearance,null,"ApplyCommitted",p,body,face,hair);
        }
    }
    static void VerifyAppearance(Map map) {
        Query("DROP TABLE entities; CREATE TABLE entities AS TABLE public.entities WITH NO DATA");
        VerifyFlowerMembership(map);
        var p=AppearancePlayer(map);p.Entity.Face=2;p.Entity.HairStyle=6002;
        Check(p.Account.State==AccountTable.AccountState.Player && EntityTable.SaveEntity(p)
            && Scalar("SELECT face FROM entities WHERE uid=@uid",p.Entity.UID)==2 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",p.Entity.UID)==6002,
            "ordinary appearance edits save against the loaded preimage and preserve the full 6711 value");
        Check(EntityTable.SaveEntity(p) && !Fenced(p),"successful save advances the appearance preimage for the next save");
        foreach(string column in new[]{"body","face","hairstyle"}) {
            var stale=AppearancePlayer(map);Query("UPDATE entities SET "+column+"=6003 WHERE uid=@uid","uid",(long)stale.Entity.UID);
            Check(!EntityTable.SaveEntity(stale) && Fenced(stale) && Scalar("SELECT "+column+" FROM entities WHERE uid=@uid",stale.Entity.UID)==6003,
                "stale production save cannot overwrite an external "+column+" change");
        }
        var nullable=AppearancePlayer(map);Query("UPDATE entities SET body=NULL,face=NULL,hairstyle=NULL WHERE uid=@uid","uid",(long)nullable.Entity.UID);
        nullable.Entity.Body=0;nullable.Entity.Face=0;nullable.Entity.HairStyle=0;LoadAppearance(nullable);
        Check(EntityTable.SaveEntity(nullable) && Scalar("SELECT count(*) FROM entities WHERE uid=@uid AND body=0 AND face=0 AND hairstyle=0",nullable.Entity.UID)==1,
            "nullable legacy appearance preserves exact null preimages while saving normalized zeros");
        foreach(string column in new[]{"body","face","hairstyle"})foreach(int value in new[]{-1,65536}) {
            var invalid=AppearancePlayer(map);Query("UPDATE entities SET "+column+"=@value WHERE uid=@uid","value",value,"uid",(long)invalid.Entity.UID);bool failed=false;
            try{LoadAppearance(invalid);}catch(OverflowException){failed=true;}
            Check(failed && Fenced(invalid) && Scalar("SELECT "+column+" FROM entities WHERE uid=@uid",invalid.Entity.UID)==value,
                "invalid stored "+column+" fails closed without rewriting it: "+value);
        }
        var tx=AppearancePlayer(map);tx.Entity.Money=10000;EntityTable.UpdateMoney(tx);
        Query("CREATE TABLE appearance_receipt_fixture(uid bigint,valid integer CHECK(valid=1))");
        bool rolled=false;
        try{lock(tx.ItemSyncRoot){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            Call(SilverStore,null,"Persist",c,t,tx,9000UL);Call(Appearance,null,"Persist",c,t,tx,(ushort)1003,(ushort)2,(ushort)6002);
            using(var q=new NpgsqlCommand("INSERT INTO appearance_receipt_fixture(uid,valid) VALUES(@uid,0)",c,t)){q.Parameters.AddWithValue("uid",(long)tx.Entity.UID);q.ExecuteNonQuery();}
            t.Commit();}}}}catch(PostgresException){rolled=true;}
        Check(rolled && tx.Entity.Money==10000 && tx.Entity.Face==1 && tx.Entity.HairStyle==2 && Scalar("SELECT money FROM entities WHERE uid=@uid",tx.Entity.UID)==10000
            && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",tx.Entity.UID)==2 && EntityTable.SaveEntity(tx),"failed companion receipt rolls back payment and appearance without advancing either image");
        lock(tx.ItemSyncRoot){using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
            Call(SilverStore,null,"Persist",c,t,tx,9000UL);Call(Appearance,null,"Persist",c,t,tx,(ushort)1003,(ushort)2,(ushort)6002);t.Commit();}}
            Query("CREATE FUNCTION reject_appearance_projection() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'no projection writes'; END $$; CREATE TRIGGER reject_appearance_projection BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION reject_appearance_projection()");
            try{Call(SilverStore,null,"ApplyCommitted",tx,9000UL);Call(Appearance,null,"ApplyCommitted",tx,(ushort)1003,(ushort)2,(ushort)6002);}
            finally{Query("DROP TRIGGER reject_appearance_projection ON entities; DROP FUNCTION reject_appearance_projection()");}
        }
        Check(tx.Entity.Money==9000 && tx.Entity.Face==2 && tx.Entity.HairStyle==6002 && EntityTable.SaveEntity(tx),"committed payment and appearance project without a second database update and survive the next save");
        foreach(string column in new[]{"body","face","hairstyle"}){
            var stale=AppearancePlayer(map);Query("UPDATE entities SET "+column+"=3 WHERE uid=@uid","uid",(long)stale.Entity.UID);bool failed=false;
            try{AppearanceTransaction(stale,1003,2,6002);}catch(InvalidOperationException){failed=true;}
            Check(failed && stale.Entity.Face==1 && stale.Entity.HairStyle==2 && Scalar("SELECT "+column+" FROM entities WHERE uid=@uid",stale.Entity.UID)==3,
                "transaction rejects a changed durable "+column+" before projection");
        }
        var suppressed=AppearancePlayer(map);Query("CREATE FUNCTION suppress_appearance_update() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RETURN NULL; END $$; CREATE TRIGGER suppress_appearance_update BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_appearance_update()");
        bool silent=false;try{AppearanceTransaction(suppressed,1003,2,6002);}catch(InvalidOperationException){silent=true;}
        finally{Query("DROP TRIGGER suppress_appearance_update ON entities; DROP FUNCTION suppress_appearance_update()");}
        Check(silent && suppressed.Entity.HairStyle==2 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",suppressed.Entity.UID)==2,"silently suppressed appearance writes cannot advance the cache");
        AppearanceTransaction(suppressed,1003,2,6002);Check(EntityTable.SaveEntity(suppressed) && suppressed.Entity.HairStyle==6002,"rolled-back update can retry with its unchanged durable image");
        var pending=AppearancePlayer(map);pending.Entity.Face=2;
        AppearanceTransaction(pending,pending.Entity.Body,pending.Entity.Face,6002);
        Check(pending.Entity.Face==2 && EntityTable.SaveEntity(pending) && Scalar("SELECT face FROM entities WHERE uid=@uid",pending.Entity.UID)==2,"a transaction can preserve legitimate pending appearance edits while checking the saved image");
        var race=AppearancePlayer(map);Parallel.Invoke(()=>{for(int i=0;i<30;i++)AppearanceTransaction(race,1003,2,(ushort)(1002+i));},
            ()=>{for(int i=0;i<30;i++)if(!EntityTable.SaveEntity(race))throw new Exception("Concurrent appearance save failed");});
        Check(!Fenced(race) && EntityTable.SaveEntity(race) && race.Entity.HairStyle==1031 && Scalar("SELECT hairstyle FROM entities WHERE uid=@uid",race.Entity.UID)==1031,"concurrent character saves and appearance transactions keep the final committed selection");
        bool noLock=false;try{Call(Appearance,null,"ApplyCommitted",race,(ushort)1003,(ushort)2,(ushort)6002);}catch(InvalidOperationException){noLock=true;}
        Check(noLock && race.Entity.HairStyle==1031,"appearance projection requires the character ownership lock");
        VerifyFlowerTop100();
        VerifyInteractionCancellation(map);
        VerifyInteractionConsent(map);
        VerifyInteractionDisconnect(map);
        VerifyInteractionMovement(map);
        VerifyHandHolding(map);
    }
    static void VerifyInteractionCancellation(Map map) {
        foreach(bool stop in new[]{false,true})foreach(string guard in new[]{"unpaired","one-way","self","distant","wrong-map","fake","dead","wrong-entity"}) {
            var p=AppearancePlayer(map);var peer=AppearancePlayer(map);
            p.Entity.X=peer.Entity.X=330;p.Entity.Y=peer.Entity.Y=330;
            p.Entity.InteractionWith=peer.Entity.UID;peer.Entity.InteractionWith=p.Entity.UID;
            p.Entity.InteractionType=peer.Entity.InteractionType=7;
            p.Entity.InteractionInProgress=peer.Entity.InteractionInProgress=true;
            p.Entity.InteractionSet=peer.Entity.InteractionSet=true;
            var actor=p.Entity;uint target=peer.Entity.UID;
            if(guard=="unpaired")p.Entity.InteractionWith=0;
            if(guard=="one-way")peer.Entity.InteractionWith=0;
            if(guard=="self"){target=p.Entity.UID;p.Entity.InteractionWith=p.Entity.UID;}
            if(guard=="distant")peer.Entity.X=400;
            if(guard=="wrong-map")peer.Entity.MapID=9999;
            if(guard=="fake")p.Fake=true;
            if(guard=="dead")p.Entity.Hitpoints=0;
            if(guard=="wrong-entity")actor=new Entity(EntityFlag.Player,false){Owner=p,UID=p.Entity.UID};
            uint own=p.Entity.InteractionWith,other=peer.Entity.InteractionWith;
            var packet=new Attack(true){Attacked=target,AttackType=stop?Attack.InteractionStopEffect:Attack.InteractionRefuse};
            if(stop)new Revora.Game.Attacking.Handle.InteractionStopEffect(packet,actor);else new Revora.Game.Attacking.Handle.InteractionRefuse(packet,actor);
            Check(p.Entity.InteractionWith==own && peer.Entity.InteractionWith==other && p.Entity.InteractionType==7 && peer.Entity.InteractionType==7
                && p.Entity.InteractionInProgress && peer.Entity.InteractionInProgress,"interaction cancellation rejects unrelated or invalid pair: "+guard+" stop="+stop);
        }
        foreach(bool stop in new[]{false,true}) {
        var a=AppearancePlayer(map);var b=AppearancePlayer(map);
        a.Entity.X=b.Entity.X=330;a.Entity.Y=b.Entity.Y=330;
        a.Entity.InteractionWith=b.Entity.UID;b.Entity.InteractionWith=a.Entity.UID;
        a.Entity.InteractionInProgress=b.Entity.InteractionInProgress=true;a.Entity.InteractionSet=b.Entity.InteractionSet=true;
        var cancel=new Attack(true){Attacked=b.Entity.UID,AttackType=stop?Attack.InteractionStopEffect:Attack.InteractionRefuse};
        if(stop)new Revora.Game.Attacking.Handle.InteractionStopEffect(cancel,a.Entity);else new Revora.Game.Attacking.Handle.InteractionRefuse(cancel,a.Entity);
        Check(a.Entity.InteractionWith==0 && b.Entity.InteractionWith==0 && !a.Entity.InteractionInProgress && !b.Entity.InteractionInProgress
            && !a.Entity.InteractionSet && !b.Entity.InteractionSet,"ordinary reciprocal cancellation clears both pairing and effect state: stop="+stop);
        }
    }
    static void VerifyInteractionConsent(Map map) {
        var clock=typeof(Revora.Game.Attacking.Handle).GetField("InteractionClock",Hidden);
        var prior=clock.GetValue(null);DateTime now=new DateTime(2035,1,1,0,0,0,DateTimeKind.Utc);
        clock.SetValue(null,(Func<DateTime>)(()=>now));
        try {
            foreach(string scenario in new[]{"normal","expired","cancelled","replaced","stale-session","distant","wrong-map"}) {
                var sender=AppearancePlayer(map);var recipient=AppearancePlayer(map);recipient.Entity.Body=2001;
                sender.Entity.X=recipient.Entity.X=330;sender.Entity.Y=recipient.Entity.Y=330;
                new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=recipient.Entity.UID,AttackType=Attack.InteractionRequest,ResponseDamage=3},sender.Entity);
                Check(sender.Entity.InteractionWith==recipient.Entity.UID && recipient.Entity.InteractionWith==sender.Entity.UID
                    && !sender.Entity.InteractionInProgress && !recipient.Entity.InteractionInProgress,"ordinary invitation stays pending until recipient consents: "+scenario);
                new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=recipient.Entity.UID,Damage=7},sender.Entity);
                Check(!sender.Entity.InteractionInProgress && !recipient.Entity.InteractionInProgress,"inviter cannot accept on behalf of recipient: "+scenario);
                if(scenario=="expired")now=now.AddMinutes(2);
                if(scenario=="cancelled")new Revora.Game.Attacking.Handle.InteractionRefuse(new Attack(true){Attacked=sender.Entity.UID},recipient.Entity);
                if(scenario=="replaced") {
                    var other=AppearancePlayer(map);other.Entity.X=other.Entity.Y=330;
                    new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=recipient.Entity.UID},other.Entity);
                }
                if(scenario=="stale-session")Kernel.GamePool[sender.Entity.UID]=new GameState(null);
                if(scenario=="distant")sender.Entity.X=400;
                if(scenario=="wrong-map")sender.Entity.MapID=9999;
                new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=sender.Entity.UID,Damage=7},recipient.Entity);
                bool accepted=scenario=="normal";
                Check(sender.Entity.InteractionInProgress==accepted && recipient.Entity.InteractionInProgress==accepted,"recipient acceptance validates owned live offer: "+scenario);
                if(scenario=="stale-session")Kernel.GamePool[sender.Entity.UID]=sender;
                if(accepted) {
                    uint acceptedMode=sender.Entity.InteractionType;
                    new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=sender.Entity.UID,Damage=99},recipient.Entity);
                    Check(sender.Entity.InteractionType==acceptedMode && recipient.Entity.InteractionType==acceptedMode,"accepted invitation cannot replay a different interaction mode");
                    sender.Entity.InteractionSet=recipient.Entity.InteractionSet=false;
                    new Revora.Game.Attacking.Handle.InteractionEffect(new Attack(true){Attacked=0},sender.Entity);
                    Check(sender.Entity.InteractionSet && recipient.Entity.InteractionSet,"accepted effect resolves the server-owned partner without trusting a target UID");
                    new Revora.Game.Attacking.Handle.InteractionRefuse(new Attack(true){Attacked=recipient.Entity.UID},sender.Entity);
                    sender.Entity.InteractionWith=recipient.Entity.UID;recipient.Entity.InteractionWith=sender.Entity.UID;
                    sender.Entity.InteractionInProgress=recipient.Entity.InteractionInProgress=true;
                    new Revora.Game.Attacking.Handle.InteractionEffect(new Attack(true){Attacked=recipient.Entity.UID},sender.Entity);
                    Check(!sender.Entity.InteractionSet && !recipient.Entity.InteractionSet,"legacy flags cannot recreate cancelled accepted-pair proof");
                }
            }
        } finally {clock.SetValue(null,prior);}
    }
    static void VerifyInteractionDisconnect(Map map) {
        foreach(bool accepted in new[]{false,true})foreach(bool senderLeaves in new[]{false,true}) {
            var sender=AppearancePlayer(map);var recipient=AppearancePlayer(map);
            sender.Entity.X=recipient.Entity.X=330;sender.Entity.Y=recipient.Entity.Y=330;
            new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=recipient.Entity.UID},sender.Entity);
            if(accepted)new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=sender.Entity.UID},recipient.Entity);
            Check((bool)Call(typeof(Revora.Game.Attacking.Handle),null,"HasAcceptedInteraction",sender,recipient)==accepted,"only accepted consent authorizes paired movement: accepted="+accepted);
            var departed=senderLeaves?sender:recipient;var survivor=senderLeaves?recipient:sender;
            departed.Disconnect();
            Check(!(bool)Call(typeof(Revora.Game.Attacking.Handle),null,"HasAcceptedInteraction",sender,recipient),"actual disconnect invalidates paired movement proof: sender="+senderLeaves+" accepted="+accepted);
            var replacement=AppearancePlayer(map);uint temporary=replacement.Entity.UID;GameState discarded;
            Kernel.GamePool.TryRemove(temporary,out discarded);replacement.Entity.UID=departed.Entity.UID;Kernel.GamePool[replacement.Entity.UID]=replacement;
            replacement.Entity.X=survivor.Entity.X=330;replacement.Entity.Y=survivor.Entity.Y=330;
            new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=replacement.Entity.UID},survivor.Entity);
            Check(!survivor.Entity.InteractionInProgress && survivor.Entity.InteractionWith==replacement.Entity.UID
                && replacement.Entity.InteractionWith==survivor.Entity.UID,"remaining player starts a fresh invitation without relogging: sender="+senderLeaves+" accepted="+accepted);
            new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=survivor.Entity.UID},replacement.Entity);
            Check((bool)Call(typeof(Revora.Game.Attacking.Handle),null,"HasAcceptedInteraction",survivor,replacement),"replacement session must earn its own accepted pair");
            Call(typeof(Revora.Game.Attacking.Handle),null,"InteractionDisconnected",departed);
            Check((bool)Call(typeof(Revora.Game.Attacking.Handle),null,"HasAcceptedInteraction",survivor,replacement),"late cleanup of old UID session cannot close replacement pair");
            replacement.Entity.MapID=9999;
            Check(!(bool)Call(typeof(Revora.Game.Attacking.Handle),null,"HasAcceptedInteraction",survivor,replacement),"accepted pair cannot authorize cross-map follower movement");
        }
    }
    static Tuple<GameState,GameState> MovingPair(Map map,ushort x,ushort y,bool accept=true) {
        var a=AppearancePlayer(map);var b=AppearancePlayer(map);a.Entity.Body=1006;b.Entity.Body=2001;
        a.Entity.X=b.Entity.X=x;a.Entity.Y=b.Entity.Y=y;
        new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=b.Entity.UID},a.Entity);
        if(accept)new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=a.Entity.UID},b.Entity);
        return Tuple.Create(a,b);
    }
    static void VerifyInteractionMovement(Map map) {
        ushort x=0,y=0;
        for(ushort ty=330;ty<360 && x==0;ty++)for(ushort tx=330;tx<360;tx++)
            if(map.Floor[tx,ty,MapObjectType.Player] && map.Floor[tx+1,ty,MapObjectType.Player] && map.Floor[tx+2,ty,MapObjectType.Player]){x=tx;y=ty;break;}
        Check(x!=0,"paired movement fixture has a legal contiguous corridor");
        foreach(uint mode in new[]{GroundMovement.Walk,GroundMovement.Run,GroundMovement.Slide}) {
            var pair=MovingPair(map,x,y);var a=pair.Item1;var b=pair.Item2;
            GeneralData.PlayerGroundMovment(new GroundMovement(true){UID=a.Entity.UID,Direction=Enums.ConquerAngle.SouthEast,GroundMovementType=mode},a);
            ushort destination=(ushort)(x+(mode==GroundMovement.Slide?2:1));
            Check(!Fenced(a) && !Fenced(b) && a.Entity.X==destination && b.Entity.X==destination && a.Entity.Y==y && b.Entity.Y==y,
                "production paired ground movement reaches the same accepted tile: "+mode);
        }
        var jumping=MovingPair(map,x,y);var leader=jumping.Item1;var follower=jumping.Item2;
        leader.lastJumpDistance=0;
        GeneralData.PlayerJump(new Data(true){UID=leader.Entity.UID,ID=Data.Jump,wParam1=x,wParam2=y,Xt1=(ushort)(x+2),Yt1=y,TimeStamp=Time32.Now},leader);
        Check(!Fenced(leader) && !Fenced(follower) && leader.Entity.X==x+2 && follower.Entity.X==x+2 && follower.Entity.Y==y,
            "production paired jump moves an accepted follower to the legal destination");
        var race=MovingPair(map,x,y);race.Item1.Entity.X=(ushort)(x+1);int publications=0,moves=0;
        Parallel.For(0,12,n=> {
            if((bool)Call(typeof(Revora.Game.Attacking.Handle),null,"MoveInteractionFollower",race.Item1,x,y,(ushort)(x+1),y,false,GroundMovement.Walk,
                (Action<GameState>)(peer=>System.Threading.Interlocked.Increment(ref publications))))System.Threading.Interlocked.Increment(ref moves);
        });
        Check(moves==1 && publications==1 && race.Item2.Entity.X==x+1,"concurrent follower replay moves and publishes once from the accepted starting tile");
        var busy=MovingPair(map,x,y);busy.Item1.Entity.X=(ushort)(x+1);
        using(var entered=new System.Threading.ManualResetEventSlim(false))using(var release=new System.Threading.ManualResetEventSlim(false)) {
            var hold=Task.Run(()=>{lock(busy.Item2.ItemSyncRoot){entered.Set();release.Wait(5000);}});
            bool moved=true;int sent=0;
            try {
                Check(entered.Wait(5000),"follower lock contention fixture owns the peer lock");
                moved=(bool)Call(typeof(Revora.Game.Attacking.Handle),null,"MoveInteractionFollower",busy.Item1,x,y,(ushort)(x+1),y,false,GroundMovement.Walk,(Action<GameState>)(peer=>sent++));
            } finally {release.Set();hold.Wait(5000);}
            Check(!moved && sent==0 && busy.Item2.Entity.X==x,"contended peer lock rejects follower mutation without waiting or deadlocking");
        }
        var outsider=AppearancePlayer(map);outsider.Entity.X=x;outsider.Entity.Y=y;
        new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=busy.Item2.Entity.UID},outsider.Entity);
        Check(busy.Item2.Entity.InteractionWith==busy.Item1.Entity.UID && (bool)Call(typeof(Revora.Game.Attacking.Handle),null,"HasAcceptedInteraction",busy.Item1,busy.Item2),
            "third-party invitation cannot dissolve somebody else's separated pair");
        new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=busy.Item2.Entity.UID},busy.Item1.Entity);
        Check(!busy.Item1.Entity.InteractionInProgress && !busy.Item2.Entity.InteractionInProgress
            && busy.Item1.Entity.X==x+1 && busy.Item2.Entity.X==x
            && !(bool)Call(typeof(Revora.Game.Attacking.Handle),null,"HasAcceptedInteraction",busy.Item1,busy.Item2),
            "participant reinvitation closes stale consent without teleporting or auto-accepting either player");
        GeneralData.PlayerGroundMovment(new GroundMovement(true){UID=busy.Item2.Entity.UID,Direction=Enums.ConquerAngle.SouthEast,GroundMovementType=GroundMovement.Walk},busy.Item2);
        Check(busy.Item2.Entity.X==x+1 && busy.Item1.Entity.X==x+1 && !busy.Item1.Entity.InteractionInProgress,
            "follower can walk to the shared tile while replacement consent remains pending");
        new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=busy.Item1.Entity.UID},busy.Item2.Entity);
        GeneralData.PlayerGroundMovment(new GroundMovement(true){UID=busy.Item1.Entity.UID,Direction=Enums.ConquerAngle.SouthEast,GroundMovementType=GroundMovement.Walk},busy.Item1);
        Check(!Fenced(busy.Item1) && !Fenced(busy.Item2) && busy.Item1.Entity.X==x+2 && busy.Item2.Entity.X==x+2,
            "new recipient consent restores real paired walking after a skipped follower update");

        foreach(string guard in new[]{"pending","closed","effect-not-ready","separated","stale-leader","blocked","too-far","wrong-map","dead"}) {
            var pair=MovingPair(map,x,y,guard!="pending");var a=pair.Item1;var b=pair.Item2;ushort nx=(ushort)(guard=="too-far"?x+21:x+1);
            a.Entity.X=nx;
            if(guard=="closed")Call(typeof(Revora.Game.Attacking.Handle),null,"InteractionDisconnected",b);
            if(guard=="effect-not-ready")b.Entity.InteractionSet=false;
            if(guard=="separated")b.Entity.X=(ushort)(x+2);
            if(guard=="stale-leader")a.Entity.X=(ushort)(x+2);
            if(guard=="wrong-map")b.Entity.MapID=9999;
            if(guard=="dead")b.Entity.Hitpoints=0;
            ushort beforeX=b.Entity.X,beforeY=b.Entity.Y;int published=0;
            var old=map.Floor.Locations[nx,y];if(guard=="blocked")map.Floor.Locations[nx,y]|=Floor.DMapPointFlag.Invalid;
            bool moved;
            try{moved=(bool)Call(typeof(Revora.Game.Attacking.Handle),null,"MoveInteractionFollower",a,x,y,nx,y,true,GroundMovement.Walk,(Action<GameState>)(peer=>published++));}
            finally{map.Floor.Locations[nx,y]=old;}
            Check(!moved && published==0 && b.Entity.X==beforeX && b.Entity.Y==beforeY,"follower admission preserves coordinates on rejection: "+guard);
        }
    }
    static Tuple<GameState,GameState> HandHoldingPair(Map map,ushort x,ushort y,uint effect=100004,ushort body=2001,bool accept=true) {
        var a=AppearancePlayer(map);var b=AppearancePlayer(map);
        AppearanceTransaction(b,body,b.Entity.Face,b.Entity.HairStyle);
        a.Entity.X=b.Entity.X=x;a.Entity.Y=b.Entity.Y=y;
        new Revora.Game.Attacking.Handle.InteractionRequest(new Attack(true){Attacked=b.Entity.UID,Damage=effect},a.Entity);
        if(accept)new Revora.Game.Attacking.Handle.InteractionAccept(new Attack(true){Attacked=a.Entity.UID,Damage=100010},b.Entity);
        return Tuple.Create(a,b);
    }
    static long HandHoldingEvidence(GameState p) {
        return Scalar("SELECT count(*) FROM revora_achievement_evidence WHERE uid=@uid AND achievement_id=10130 AND source='hand-holding-v1'",p.Entity.UID);
    }
    static void HandHoldingStep(GameState p,uint mode=GroundMovement.Walk) {
        GeneralData.PlayerGroundMovment(new GroundMovement(true){UID=p.Entity.UID,Direction=Enums.ConquerAngle.SouthEast,GroundMovementType=mode},p);
    }
    static void VerifyHandHolding(Map map) {
        Query("CREATE TABLE IF NOT EXISTS titles (LIKE public.titles INCLUDING ALL)");
        StorageManager.Load();StorageItem.Load();PerfectionManager.AbilityScore.Read(out PerfectionManager.AbilitiesScore);
        ushort x=0,y=0;
        for(ushort ty=330;ty<360 && x==0;ty++)for(ushort tx=330;tx<360;tx++)
            if(map.Floor[tx,ty,MapObjectType.Player] && map.Floor[tx+1,ty,MapObjectType.Player] && map.Floor[tx+2,ty,MapObjectType.Player]){x=tx;y=ty;break;}
        Check(x!=0,"hand-holding fixture has a legal ordinary-player corridor");
        foreach(bool femaleLeads in new[]{false,true}) {
            var pair=HandHoldingPair(map,x,y);var a=pair.Item1;var b=pair.Item2;
            Check(a.Account.State==AccountTable.AccountState.Player && b.Account.State==AccountTable.AccountState.Player
                && HandHoldingEvidence(a)==0 && HandHoldingEvidence(b)==0,"accepted opposite-sex invitation alone awards nothing");
            HandHoldingStep(femaleLeads?b:a,femaleLeads?GroundMovement.Run:GroundMovement.Walk);
            Check(!Fenced(a) && !Fenced(b) && a.Entity.X==x+1 && b.Entity.X==x+1
                && HandHoldingEvidence(a)==1 && HandHoldingEvidence(b)==1,"ordinary hand-holding movement durably awards both: female leader="+femaleLeads);
            Check(HasAchievement(a,10130) && HasAchievement(b,10130),"both achievement views immediately observe committed hand-holding evidence");
            HandHoldingStep(femaleLeads?b:a);
            Check(HandHoldingEvidence(a)==1 && HandHoldingEvidence(b)==1,"subsequent paired movement cannot duplicate either completion");
            foreach(var p in new[]{a,b}) {
                p.Entity.MyAchievement=new Achievement(p.Entity);p.Entity.MyAchievement.Load("");
                new TitleStorage().Login(p);
                Check(HasAchievement(p,10130),"production title login recovers hand-holding from durable evidence with empty legacy bits");
            }
        }
        foreach(string rejection in new[]{"dance","hug","same-sex","pending","closed","jump","slide","forged-effect","separated","dead"}) {
            uint effect=rejection=="dance" || rejection=="forged-effect"?100010u:rejection=="hug"?100005u:100004u;
            var pair=HandHoldingPair(map,x,y,effect,(ushort)(rejection=="same-sex"?1004:2001),rejection!="pending");var a=pair.Item1;var b=pair.Item2;
            if(rejection=="closed")Call(typeof(Revora.Game.Attacking.Handle),null,"InteractionDisconnected",b);
            if(rejection=="forged-effect")a.InteractionEffect=b.InteractionEffect=100004;
            if(rejection=="separated")b.Entity.X=(ushort)(x+2);
            if(rejection=="dead")b.Entity.Hitpoints=0;
            if(rejection=="jump") {a.lastJumpDistance=0;GeneralData.PlayerJump(new Data(true){UID=a.Entity.UID,ID=Data.Jump,wParam1=x,wParam2=y,Xt1=(ushort)(x+1),Yt1=y,TimeStamp=Time32.Now},a);}
            else HandHoldingStep(a,rejection=="slide"?GroundMovement.Slide:GroundMovement.Walk);
            Check(HandHoldingEvidence(a)==0 && HandHoldingEvidence(b)==0,"nonqualifying movement cannot award hand-holding: "+rejection);
        }
        var race=HandHoldingPair(map,x,y);race.Item1.Entity.X=(ushort)(x+1);int moves=0;
        Parallel.For(0,12,n=>{if((bool)Call(typeof(Revora.Game.Attacking.Handle),null,"MoveInteractionFollower",race.Item1,x,y,(ushort)(x+1),y,false,GroundMovement.Walk,(Action<GameState>)(peer=>{})))System.Threading.Interlocked.Increment(ref moves);});
        Check(moves==1 && HandHoldingEvidence(race.Item1)==1 && HandHoldingEvidence(race.Item2)==1,"concurrent replay produces one movement and one completion per owner");
        var failed=HandHoldingPair(map,x,y);
        Query("CREATE TABLE hand_holding_failure(uid bigint PRIMARY KEY)");
        Query("INSERT INTO hand_holding_failure VALUES(@uid)","uid",(long)failed.Item2.Entity.UID);
        Query("CREATE FUNCTION reject_hand_holding() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.achievement_id=10130 AND EXISTS(SELECT 1 FROM hand_holding_failure WHERE uid=NEW.uid) THEN RAISE EXCEPTION 'second receipt failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER reject_hand_holding BEFORE INSERT ON revora_achievement_evidence FOR EACH ROW EXECUTE FUNCTION reject_hand_holding()");
        try{HandHoldingStep(failed.Item1);}finally{Query("DROP TRIGGER reject_hand_holding ON revora_achievement_evidence; DROP FUNCTION reject_hand_holding(); DROP TABLE hand_holding_failure");}
        Check(Fenced(failed.Item1) && Fenced(failed.Item2) && HandHoldingEvidence(failed.Item1)==0 && HandHoldingEvidence(failed.Item2)==0,
            "second-owner receipt failure rolls back the first and fences both sessions");
        var stale=HandHoldingPair(map,x,y);
        Query("UPDATE entities SET body=1003 WHERE uid=@uid","uid",(long)stale.Item2.Entity.UID);HandHoldingStep(stale.Item1);
        Check(Fenced(stale.Item1) && Fenced(stale.Item2) && HandHoldingEvidence(stale.Item1)==0 && HandHoldingEvidence(stale.Item2)==0,
            "changed persisted appearance cannot earn using stale opposite-sex memory");
    }
    static void VerifyFlowerMembership(Map map) {
        foreach(ushort body in new ushort[]{1003,1004,1005,1006,2001,2002,2005,2006}) {
            var p=AppearancePlayer(map);p.Entity.Body=body;EntityTable.SetFlowers(p);var flowers=p.Entity.MyFlowers;
            flowers.Orchads=17;EntityTable.SetFlowers(p);bool female=body>=2000;
            Check(EntityTable.IsGirl(body)==female && FlowerState.IsGirl(body)==female
                && ReferenceEquals(flowers,p.Entity.MyFlowers) && flowers.Orchads==17
                && FlowerState.Flowers_Poll.ContainsKey(p.Entity.UID)==female && FlowerState.BoyFlowers.ContainsKey(p.Entity.UID)!=female,
                "flower login and delivery agree and retain one history for body "+body);
        }
        var moving=AppearancePlayer(map);EntityTable.SetFlowers(moving);var original=moving.Entity.MyFlowers;
        original.RedRoses=12;original.RedRoses2day=7;original.aFlower=0;original.SendDay=12;
        FlowerState.CulculateRankKiss(original);
        AppearanceTransaction(moving,2001,moving.Entity.Face,moving.Entity.HairStyle);
        Check(!Fenced(moving) && ReferenceEquals(original,moving.Entity.MyFlowers) && original.RedRoses==12
            && original.RedRoses2day==7 && original.aFlower==0 && original.SendDay==12
            && FlowerState.Flowers_Poll[moving.Entity.UID]==original && !FlowerState.BoyFlowers.ContainsKey(moving.Entity.UID)
            && FlowerState.RedRousesTop100.Contains(original) && !FlowerState.KissTop100.Contains(original),
            "committed body change moves existing totals, daily allowance and rank without duplicating history");
        AppearanceTransaction(moving,1004,moving.Entity.Face,moving.Entity.HairStyle);
        Parallel.For(0,16,n=>EntityTable.SetFlowers(moving));
        Check(ReferenceEquals(original,moving.Entity.MyFlowers) && FlowerState.BoyFlowers[moving.Entity.UID]==original
            && !FlowerState.Flowers_Poll.ContainsKey(moving.Entity.UID) && FlowerState.KissTop100.Contains(original)
            && !FlowerState.RedRousesTop100.Contains(original) && original.RedRoses==12,
            "reverse body change and concurrent attachment preserve one ranked history");
        var legacy=AppearancePlayer(map);EntityTable.SetFlowers(legacy);var legacyFlowers=legacy.Entity.MyFlowers;legacyFlowers.Lilies=23;
        legacy.Entity.Body=2002;EntityTable.SetFlowers(legacy);
        Check(ReferenceEquals(legacyFlowers,legacy.Entity.MyFlowers) && legacyFlowers.Lilies==23
            && FlowerState.Flowers_Poll[legacy.Entity.UID]==legacyFlowers && !FlowerState.BoyFlowers.ContainsKey(legacy.Entity.UID),
            "login repairs a female character's legacy male dictionary placement without resetting gifts");
        var stale=AppearancePlayer(map);EntityTable.SetFlowers(stale);var untouched=stale.Entity.MyFlowers;
        Query("UPDATE entities SET body=2001 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        bool denied=false;try{AppearanceTransaction(stale,2002,stale.Entity.Face,stale.Entity.HairStyle);}catch(InvalidOperationException){denied=true;}
        Check(denied && FlowerState.BoyFlowers[stale.Entity.UID]==untouched && !FlowerState.Flowers_Poll.ContainsKey(stale.Entity.UID),
            "failed appearance transaction cannot move flower membership before commit");
        var conflict=AppearancePlayer(map);EntityTable.SetFlowers(conflict);var first=conflict.Entity.MyFlowers;
        var second=new FlowerState(conflict.Entity.UID,"conflict"){RedRoses=99};FlowerState.Flowers_Poll[conflict.Entity.UID]=second;
        bool rejected=false;try{EntityTable.SetFlowers(conflict);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && FlowerState.BoyFlowers[conflict.Entity.UID]==first && FlowerState.Flowers_Poll[conflict.Entity.UID]==second
            && second.RedRoses==99,"conflicting preexisting histories remain intact instead of being merged or discarded");
        FlowerState discarded;FlowerState.Flowers_Poll.TryRemove(conflict.Entity.UID,out discarded);
    }
    static void VerifyFlowerTop100() {
        foreach(bool female in new[]{false,true}) {
            uint first=female?9001000u:9000000u;
            var candidates=Enumerable.Range(0,102).Select(i=>new FlowerState(first+(uint)i,"rank-fixture")
                {RedRoses=1000,Lilies=1000,Orchads=1000,Tulips=1000}).ToArray();
            foreach(var f in candidates.Reverse())Call(typeof(FlowerState),null,"RefreshReceived",f,female);
            string[] names=female?new[]{"RedRousesTop100","LiliesTop100","OrchidsTop100","TulipsTop100"}:new[]{"KissTop100","LoveTop100","TineTop100","JadeTop100"};
            foreach(string name in names) {
                var ranks=(FlowerState[])typeof(FlowerState).GetField(name).GetValue(null);
                Check(ranks.Length==100 && ranks.Select(f=>f.UID).SequenceEqual(candidates.Take(100).Select(f=>f.UID)),
                    "full hundred-place ranking has deterministic UID ties in "+name);
            }
            Check(candidates[99].RankRoses==100 && candidates[99].RankLilies==100 && candidates[99].RankOrchids==100 && candidates[99].RankTuilps==100
                && candidates[100].RankRoses==0 && candidates[101].RankTuilps==0,"hundredth place is ranked while lower candidates remain eligible but unranked");
            uint[] categories=female?new[]{GenericRanking.RoseFairy,GenericRanking.LilyFairy,GenericRanking.OrchidFairy,GenericRanking.TulipFairy}
                :new[]{GenericRanking.Kiss,GenericRanking.Love,GenericRanking.Tins,GenericRanking.Jade};
            foreach(uint category in categories) {
                var request=new GenericRanking(true){RankingType=category,Page=9};
                var response=(GenericRanking)Call(typeof(GeneralData),null,"FlowerRankingPage",request);
                byte[] bytes=response.ToArray();int last=24+9*80;
                Check(response.Count==10 && response.RegisteredCount==100 && response.Page==9 && response.RankingType==category
                    && bytes.Length==832 && BitConverter.ToUInt16(bytes,0)==824 && BitConverter.ToUInt16(bytes,2)==1151
                    && BitConverter.ToUInt32(bytes,last)==100 && BitConverter.ToUInt32(bytes,last+8)==1000
                    && BitConverter.ToUInt32(bytes,last+16)==candidates[99].UID,
                    "last ranking page serializes all ten records including place 100 for category "+category);
                request.Page=ushort.MaxValue;
                Check(Call(typeof(GeneralData),null,"FlowerRankingPage",request)==null,"out-of-range flower page is rejected for "+category);
            }
            candidates[0].RedRoses=0;candidates[0].Lilies=0;candidates[0].Orchads=0;candidates[0].Tulips=0;
            Call(typeof(FlowerState),null,"RefreshReceived",candidates[0],female);
            foreach(string name in names) {
                var ranks=(FlowerState[])typeof(FlowerState).GetField(name).GetValue(null);
                Check(ranks.Select(f=>f.UID).SequenceEqual(candidates.Skip(1).Take(100).Select(f=>f.UID)),
                    "previously unranked recipient fills the vacancy from complete membership in "+name);
            }
            candidates[101].RedRoses=2000;Call(typeof(FlowerState),null,"RefreshReceived",candidates[101],female);
            Check(((FlowerState[])typeof(FlowerState).GetField(names[0]).GetValue(null))[0]==candidates[101]
                && candidates[101].RankRoses==1,"new gifts promote an unranked recipient without waiting for startup");
        }
        Check(Call(typeof(GeneralData),null,"FlowerRankingPage",new GenericRanking(true){RankingType=uint.MaxValue})==null,
            "unknown ranking category cannot dereference a missing list");
    }
}
