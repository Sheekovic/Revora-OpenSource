using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Revora;
using Revora.Client;
using Revora.Database;
using Revora.Game;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class PotencyHuntTests
{
    static readonly Type Starter=typeof(Entity).Assembly.GetType("Revora.Game.Features.StarterPacks");
    static bool fullStarterSchema;
    static readonly Type CustomStarter=typeof(Entity).Assembly.GetType("Revora.Game.Features.RevoraStarterPack");
    static readonly Type StarterSession=typeof(Entity).Assembly.GetType("Revora.Game.Features.SessionInstall");
    static GameState CustomStarterPlayer(Map town,byte cls=10,bool entitled=true) {
        var p=WelcomePlayer(town,1,0,cls);
        p.Account.Username="starter_"+p.Entity.UID;
        Query("UPDATE entities SET owner=@owner WHERE uid=@uid","owner",p.Account.Username,"uid",(long)p.Entity.UID);
        Query("INSERT INTO revora_character_creation(account_name,character_uid,character_name,revora_starter_pending) VALUES(@owner,@uid,@name,@pending)",
            "owner",p.Account.Username,"uid",(long)p.Entity.UID,"name",p.Entity.Name,"pending",entitled);
        Call(StarterSession,null,"Install",p);
        Call(CustomStarter,null,"OnLogin",p);
        return p;
    }
    static GameState ReloadCustomStarter(GameState p) {
        string owner=p.Account.Username;
        var fresh=ReloadBall(p); fresh.Account.Username=owner;
        Call(StarterSession,null,"Install",fresh);
        return fresh;
    }
    static ConquerItem CustomPack(GameState p) {return p.Inventory.Objects.Single(i=>i.ID==727990);}
    static long CustomClaims(GameState p) {return Scalar("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid",p.Entity.UID);}
    static void VerifyRevoraStarter(string runtime,Map town) {
        var info=new ConquerItemBaseInformation();
        info.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../obj/chi-refill-validation/revora-starter-catalog/starter-row.txt"))).Trim());
        ConquerItemInformation.BaseInformations.Add(info.ID,info);
        Call(CustomStarter,null,"Initialize");Call(CustomStarter,null,"Initialize");
        foreach(byte cls in new byte[]{10,20,40,50,60,70,80,100,132,142,160}) {
            var p=CustomStarterPlayer(town,cls);var pack=CustomPack(p);uint bcp=p.Entity.BoundCps,cp=p.Entity.ConquerPoints;ulong silver=p.Entity.Money;
            var expected=(uint[])Call(CustomStarter,null,"GearIds",cls);
            Check(!p.Fake && p.Account.State==AccountTable.AccountState.Player && pack.Bound && pack.StackSize==1,"ordinary player receives retained bound starter pack: "+cls);
            Call(CustomStarter,null,"OnLogin",p);
            Parallel.For(0,16,n=>OpenStarter(p,pack));
            Check(!Fenced(p) && CustomClaims(p)==1 && p.Entity.BoundCps==bcp+50000 && p.Entity.ConquerPoints==cp && p.Entity.Money==silver+1000000
                && CountId(p,727990)==1 && p.Inventory.Count==expected.Length+1,"one atomic opening grant under concurrent requests: "+cls);
            foreach(var item in p.Inventory.Objects.Where(i=>i.ID!=727990)) {
                if(item.ID==1050000) {Check(item.Bound&&item.Plus==0&&item.Bless==0&&item.SocketOne==0&&item.SocketTwo==0,"Archer receives ordinary bound low-level ammunition");continue;}
                int gem=item.ID==201009?103:item.ID==202009?123:(cls/10==10||cls/10==13||cls/10==14)?3:13;
                Check(item.ID%10==9 && item.Plus==12 && item.Bound && item.Bless==(item.ID==201009||item.ID==202009?1:7)
                    && (int)item.SocketOne==gem && item.SocketOne==item.SocketTwo && item.Position==0,"Super +12 bound set has correct blessing/gems: "+cls+"/"+item.ID);
            }
            Check(p.Inventory.Objects.Where(i=>i.ID!=727990).Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(expected.OrderBy(i=>i)),"exact complete class kit: "+cls);
            p=ReloadCustomStarter(p);Call(CustomStarter,null,"OnLogin",p);OpenStarter(p,CustomPack(p));
            Check(!Fenced(p) && CustomClaims(p)==1 && p.Entity.BoundCps==bcp+50000 && p.Entity.Money==silver+1000000
                && p.Inventory.Count==expected.Length+1 && p.Inventory.Objects.Where(i=>i.ID!=727990&&i.ID!=1050000).All(i=>i.Plus==12 && i.Bound && i.SocketOne!=0 && i.SocketTwo==i.SocketOne),"production reload preserves full gear/currencies and rejects replay: "+cls);
            p.Entity.Reborn=2;Query("UPDATE entities SET reborn=0 WHERE uid=@uid","uid",(long)p.Entity.UID);OpenStarter(p,CustomPack(p));
            Check(CustomClaims(p)==1 && p.Entity.BoundCps==bcp+50000,"rebirth cannot repeat the opening bundle before a new reward policy: "+cls);
        }
        var denied=CustomStarterPlayer(town,10,false);
        Check(CountId(denied,727990)==0 && CustomClaims(denied)==0,"old characters without entitlement receive no giveaway");
        var full=CustomStarterPlayer(town);while(full.Inventory.Count<40)Pack(full,1088001,1);
        OpenStarter(full,CustomPack(full));
        Check(!Fenced(full) && CustomClaims(full)==0 && full.Inventory.Count==40,"full bag preserves pack and reward");
        foreach(uint id in full.Inventory.Objects.Where(i=>i.ID==1088001).Take(((uint[])Call(CustomStarter,null,"GearIds",(byte)10)).Length).Select(i=>i.UID).ToArray()) {
            Query("DELETE FROM items WHERE uid=@uid","uid",(long)id);full.Inventory.Remove(id,Enums.ItemUse.None,false);
        }
        OpenStarter(full,CustomPack(full));Check(!Fenced(full)&&CustomClaims(full)==1&&full.Inventory.Count==40,"exact fit keeps the pack slot occupied");
        foreach(bool boundOverflow in new[]{true,false}) {
            var rich=CustomStarterPlayer(town);
            Query("UPDATE entities SET "+(boundOverflow?"boundcps=4294967295":"money=9223372036854775807")+" WHERE uid=@uid","uid",(long)rich.Entity.UID);
            rich=ReloadCustomStarter(rich);OpenStarter(rich,CustomPack(rich));
            Check(!Fenced(rich)&&CustomClaims(rich)==0&&rich.Inventory.Count==1,"currency overflow preserves all rewards: "+boundOverflow);
        }
        var malformed=CustomStarterPlayer(town);var bad=PrayerPacket(CustomPack(malformed).UID);bad[bad.Length-1]^=1;ItemUsage.Process(bad,malformed);
        Check(CustomClaims(malformed)==0,"invalid item-use seal grants nothing");
        var forged=Pack(malformed,727990,1);forged.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)forged.UID);
        OpenStarter(malformed,forged);Check(CustomClaims(malformed)==0,"unregistered duplicate pack cannot claim");
        foreach(bool postCommit in new[]{false,true}) {
            var p=CustomStarterPlayer(town);uint cp=p.Entity.BoundCps;
            var hook=CustomStarter.GetField(postCommit?"AfterCommit":"BeforeCommit",Hidden);
            hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("fixture interruption");}));
            try {OpenStarter(p,CustomPack(p));} finally {hook.SetValue(null,null);}
            Check(Fenced(p)&&CustomClaims(p)==(postCommit?1:0),"failure fences session with correct transaction outcome: "+postCommit);
            p=ReloadCustomStarter(p);OpenStarter(p,CustomPack(p));
            Check(!Fenced(p)&&CustomClaims(p)==1&&p.Entity.BoundCps==cp+50000,"reconnect recovers exactly one reward: "+postCommit);
        }
        var lost=CustomStarterPlayer(town);OpenStarter(lost,CustomPack(lost));uint oldUid=CustomPack(lost).UID;
        Query("DELETE FROM items WHERE uid=@uid","uid",(long)oldUid);lost.Inventory.Remove(oldUid,Enums.ItemUse.None,false);
        Call(CustomStarter,null,"OnLogin",lost);OpenStarter(lost,CustomPack(lost));
        Check(CustomPack(lost).UID!=oldUid&&CustomClaims(lost)==1&&CountId(lost,727990)==1,"lost handle can recover without resetting reward claims");
        Check(ConquerItemInformation.BaseInformations[201009].Level==100 && ConquerItemInformation.BaseInformations[202009].Level==100
            && ConquerItemInformation.BaseInformations[900009].Level==40 && ConquerItemInformation.BaseInformations[130009].Level==15,"original equip requirements preserved");
        VerifyStarterSteeds(town);
        VerifyStarterTalismans(town);
        VerifyFirstRebirthStarter(town);
        VerifyFinalStarter(town);
        VerifyBoundCpPurchases(town);
    }
    static GameState StarterAfterSteed(Map town,byte life=0) {
        var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));
        StarterLife(p,40,life);OpenStarter(p,CustomPack(p));return p;
    }
    static long MilestoneClaims(GameState p,int milestone) {
        return Scalar("SELECT count(*) FROM revora_custom_starter_claims WHERE uid=@uid AND milestone="+milestone,p.Entity.UID);
    }
    static void VerifyStarterTalismans(Map town) {
        var choiceType=typeof(Entity).Assembly.GetType("Revora.Game.Features.P7SoulChoicePack");
        Call(choiceType,null,"Initialize");
        var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));
        Check(CountId(p,201009)==0&&CountId(p,202009)==0,"fan and tower moved out of opening bundle");
        uint cps=p.Entity.BoundCps;ulong silver=p.Entity.Money;
        for(byte cycle=0;cycle<1;cycle++) {
            byte life=(byte)(cycle*2);
            StarterLife(p,40,life);OpenStarter(p,CustomPack(p));
            StarterLife(p,69,life);OpenStarter(p,CustomPack(p));
            Check(CountId(p,203009)==cycle&&MilestoneClaims(p,70)==cycle,"level 69 cannot claim crop in life "+life);
            StarterLife(p,70,life);Parallel.For(0,12,n=>OpenStarter(p,CustomPack(p)));
            Check(CountId(p,203009)==cycle+1&&MilestoneClaims(p,70)==cycle+1,"one crop per rebirth life under concurrency: "+life);
            var crop=p.Inventory.Objects.Where(i=>i.ID==203009).OrderBy(i=>i.UID).Last();
            Check(crop.Bound&&crop.Plus==12&&crop.Bless==0&&crop.SocketOne==0&&crop.SocketTwo==0,"Super +12 crop has no blessing or sockets");
            StarterLife(p,99,life);OpenStarter(p,CustomPack(p));
            Check(CountId(p,204009)==cycle&&CountId(p,201009)==cycle&&CountId(p,202009)==cycle,"level 99 cannot claim level 100 set");
            StarterLife(p,100,life);Parallel.For(0,12,n=>OpenStarter(p,CustomPack(p)));
            Check(!Fenced(p)&&MilestoneClaims(p,100)==cycle+1&&CountId(p,204009)==cycle+1&&CountId(p,201009)==cycle+1&&CountId(p,202009)==cycle+1,"one full level 100 set per life");
            p=ReloadCustomStarter(p);OpenStarter(p,CustomPack(p));
            foreach(uint type in new uint[]{203009,204009,201009,202009}) {
                var item=p.Inventory.Objects.Where(i=>i.ID==type).OrderBy(i=>i.UID).Last();
                int one=type==203009?0:type==202009?123:103;
                int two=type==203009?0:type==201009?103:123;
                Check(item.Bound&&item.Plus==12&&item.Bless==(type==201009||type==202009?1:0)
                    &&(int)item.SocketOne==one&&(int)item.SocketTwo==two&&item.Days==0,"reload preserves exact permanent talisman blessing/gems: "+type);
            }
            Check(MilestoneClaims(p,100)==cycle+1&&p.Entity.BoundCps==cps&&p.Entity.Money==silver,"talisman replay grants neither duplicate set nor currency");
            StarterLife(p,119,life);OpenStarter(p,CustomPack(p));
            Check(MilestoneClaims(p,120)==cycle,"level 119 cannot claim soul choice pack");
            StarterLife(p,120,life);Parallel.For(0,12,n=>OpenStarter(p,CustomPack(p)));
            p=ReloadCustomStarter(p);OpenStarter(p,CustomPack(p));
            var choice=p.Inventory.Objects.Single(i=>i.ID==3006233);
            Check(MilestoneClaims(p,120)==cycle+1&&choice.Bound&&choice.Plus==0&&choice.Bless==0&&choice.SocketOne==0&&choice.SocketTwo==0
                &&p.Entity.BoundCps==cps&&p.Entity.Money==silver,"one plain bound soul choice pack per life, reload/replay and no extra currency");
            var souls=ConquerItemInformation.BaseInformations.Where(x=>x.Value.PurificationLevel==7).Select(x=>x.Key).OrderBy(x=>x).ToArray();
            OpenStarter(p,choice);SoulChoiceReply(p,251);SoulChoiceReply(p,250);SoulChoiceReply(p,1);SoulChoiceReply(p,1);
            Check(!Fenced(p)&&CountId(p,3006233)==0&&CountId(p,souls[0])==cycle+1
                &&p.Inventory.Objects.Where(i=>i.ID==souls[0]).All(i=>i.Bound),"selected soul exact, bound, once only through normal NPC dispatch");
        }
        VerifySoulChoicePack(town,choiceType);
        var full=StarterAfterSteed(town);StarterLife(full,100,0);OpenStarter(full,CustomPack(full)); // Crop first.
        while(full.Inventory.Count<38)Pack(full,1088001,1);
        OpenStarter(full,CustomPack(full));
        Check(!Fenced(full)&&MilestoneClaims(full,100)==0&&CountId(full,204009)==0&&CountId(full,201009)==0,"three-piece set never partially fills two empty slots");
        var filler=full.Inventory.Objects.First(i=>i.ID==1088001);Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);full.Inventory.Remove(filler.UID,Enums.ItemUse.None,false);
        OpenStarter(full,CustomPack(full));Check(full.Inventory.Count==40&&MilestoneClaims(full,100)==1,"level 100 set fits exactly three slots");
        foreach(bool post in new[]{false,true}) {
            var failed=StarterAfterSteed(town);StarterLife(failed,100,0);OpenStarter(failed,CustomPack(failed));
            var hook=CustomStarter.GetField(post?"AfterCommit":"BeforeCommit",Hidden);
            hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("talisman fixture interruption");}));
            try {OpenStarter(failed,CustomPack(failed));} finally {hook.SetValue(null,null);}
            Check(Fenced(failed)&&MilestoneClaims(failed,100)==(post?1:0),"level 100 set transaction outcome: "+post);
            failed=ReloadCustomStarter(failed);OpenStarter(failed,CustomPack(failed));
            Check(!Fenced(failed)&&MilestoneClaims(failed,100)==1&&CountId(failed,204009)==1&&CountId(failed,201009)==1&&CountId(failed,202009)==1,"level 100 recovery delivers exactly one complete set: "+post);
        }
    }
    static void SoulChoiceReply(GameState p,byte option) {
        var bytes=new byte[28];Writer.WriteUInt32(3006233,8,bytes);bytes[14]=option;
        var request=new NpcRequest(false);request.Deserialize(bytes);Npcs.GetDialog(request,p);
    }
    static void VerifySoulChoicePack(Map town,Type choiceType) {
        var souls=ConquerItemInformation.BaseInformations.Where(x=>x.Value.PurificationLevel==7).Select(x=>x.Key).OrderBy(x=>x).ToArray();
        for(int index=0;index<souls.Length;index++) {
            var p=CustomStarterPlayer(town);var pack=Pack(p,3006233,1);pack.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)pack.UID);
            SoulChoiceReply(p,1);Check(p.Inventory.ContainsUID(pack.UID),"unsolicited soul selection cannot redeem");
            OpenStarter(p,pack);SoulChoiceReply(p,255);SoulChoiceReply(p,1);
            Check(p.Inventory.ContainsUID(pack.UID),"cancel preserves choice pack");
            OpenStarter(p,pack);for(int page=0;page<index/6;page++)SoulChoiceReply(p,251);
            SoulChoiceReply(p,(byte)(index%6+1));p=ReloadCustomStarter(p);
            Check(!Fenced(p)&&CountId(p,3006233)==0&&CountId(p,souls[index])==1&&p.Inventory.Objects.Single(i=>i.ID==souls[index]).Bound,"every P7 selection persists exact bound soul: "+souls[index]);
        }
        foreach(bool post in new[]{false,true}) {
            var p=CustomStarterPlayer(town);var pack=Pack(p,3006233,1);pack.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)pack.UID);
            while(p.Inventory.Count<40)Pack(p,1088001,1);
            OpenStarter(p,pack);var hook=choiceType.GetField(post?"AfterCommit":"BeforeCommit",Hidden);
            hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("choice interruption");}));
            try{SoulChoiceReply(p,1);}finally{hook.SetValue(null,null);}
            Check(Fenced(p),"soul choice interrupted session fenced");p=ReloadCustomStarter(p);
            if(!post){OpenStarter(p,p.Inventory.Objects.Single(i=>i.ID==3006233));SoulChoiceReply(p,1);}
            Check(!Fenced(p)&&p.Inventory.Count==40&&CountId(p,3006233)==0&&CountId(p,souls[0])==1,"choice recovery at full bag produces exactly one soul: "+post);
        }
    }
    static void StarterLife(GameState p,byte level,byte life) {
        p.Entity.Level=level;p.Entity.Reborn=life;
        Check(EntityTable.SaveEntity(p),"starter milestone progression saved");
    }
    static void VerifyStarterSteeds(Map town) {
        var field=CustomStarter.GetField("SteedColorIndex",Hidden);var original=field.GetValue(null);
        var colors=new uint[]{0xff9600,0x00ff96,0x9600ff};
        try {
            int draws=0,index=0;
            field.SetValue(null,(Func<int>)(()=>{draws++;return index;}));
            var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));
            uint cp=p.Entity.BoundCps,ordinary=p.Entity.ConquerPoints;ulong money=p.Entity.Money;
            for(byte cycle=0;cycle<1;cycle++) {
            byte life=(byte)(cycle*2);
                index=life;
                StarterLife(p,39,life);OpenStarter(p,CustomPack(p));
                Check(draws==cycle&&CountId(p,300000)==cycle,"level 39 cannot claim steed or roll color: life "+life);
                StarterLife(p,40,life);Parallel.For(0,20,n=>OpenStarter(p,CustomPack(p)));
                Check(!Fenced(p)&&CustomClaims(p)==cycle+2&&CountId(p,300000)==cycle+1&&draws==cycle+1,"one steed per life under concurrent opens: "+life);
                var steed=p.Inventory.Objects.Where(i=>i.ID==300000).OrderBy(i=>i.UID).Last();uint uid=steed.UID;
                Check(steed.Bound&&steed.Plus==12&&SavedRune(steed,"bless")==0&&steed.SocketOne==0&&steed.SocketTwo==0&&steed.Days==0
                    &&steed.Effect==Enums.ItemEffect.Horse&&steed.SocketProgress==colors[life]
                    &&SavedRune(steed,"socketprogress")==colors[life]&&SavedRune(steed,"nextsteedcolor")==colors[life],"bound +12 steed saves exact standard color with no blessing/gems: "+life);
                p=ReloadCustomStarter(p);OpenStarter(p,CustomPack(p));steed=p.Inventory.Objects.Single(i=>i.UID==uid);
                uint preview=(uint)steed.NextGreen|((uint)steed.NextBlue<<8)|((uint)steed.NextRed<<16);
                Check(draws==cycle+1&&steed.SocketProgress==colors[life]&&preview==colors[life]&&steed.Plus==12&&steed.Bound
                    &&p.Entity.BoundCps==cp&&p.Entity.ConquerPoints==ordinary&&p.Entity.Money==money,"reload/replay preserves color and grants no extra currency: "+life);
            }
            var full=CustomStarterPlayer(town);OpenStarter(full,CustomPack(full));StarterLife(full,70,0);
            while(full.Inventory.Count<40)Pack(full,1088001,1);
            int before=draws;OpenStarter(full,CustomPack(full));
            Check(!Fenced(full)&&CustomClaims(full)==1&&draws==before,"full bag does not consume milestone or roll color");
            var filler=full.Inventory.Objects.First(i=>i.ID==1088001);Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);full.Inventory.Remove(filler.UID,Enums.ItemUse.None,false);
            OpenStarter(full,CustomPack(full));Check(!Fenced(full)&&full.Inventory.Count==40&&CountId(full,300000)==1,"skipped level 40 remains claimable with exactly one free slot");
            foreach(bool post in new[]{false,true}) {
                var failed=CustomStarterPlayer(town);OpenStarter(failed,CustomPack(failed));StarterLife(failed,40,0);
                var hook=CustomStarter.GetField(post?"AfterCommit":"BeforeCommit",Hidden);
                hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("steed fixture interruption");}));
                try {OpenStarter(failed,CustomPack(failed));} finally {hook.SetValue(null,null);}
                Check(Fenced(failed)&&CustomClaims(failed)==(post?2:1),"steed interruption preserves transaction boundary: "+post);
                int savedDraws=draws;failed=ReloadCustomStarter(failed);OpenStarter(failed,CustomPack(failed));
                Check(!Fenced(failed)&&CountId(failed,300000)==1&&CustomClaims(failed)==2&&draws==savedDraws+(post?0:1),"steed recovery never rerolls a committed reward: "+post);
            }
            var stale=CustomStarterPlayer(town);OpenStarter(stale,CustomPack(stale));StarterLife(stale,40,0);
            Query("UPDATE entities SET level=39 WHERE uid=@uid","uid",(long)stale.Entity.UID);OpenStarter(stale,CustomPack(stale));
            Check(Fenced(stale)&&CountId(stale,300000)==0&&CustomClaims(stale)==1,"durable level mismatch cannot award steed");
        } finally {field.SetValue(null,original);}
    }
    static void VerifyBoundCpPurchases(Map town) {
        var service=typeof(Entity).Assembly.GetType("Revora.Game.Features.CpShopPurchases");Call(service,null,"Initialize");
        ShopFile.Load();EShopFile.Load();
        var shop=EShopFile.Shops[2888];var originalStock=shop.Items;var originalMoney=shop.MoneyType;
        shop.Items=shop.Items.Concat(new uint[]{723700,730001,724002,727000,721224,720757,727991}).Distinct().ToList();
        DateTime now=new DateTime(2035,1,1,0,0,0,DateTimeKind.Utc);
        service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>now));
        Func<GameState> buyer=()=>{var p=CustomStarterPlayer(town);p.Entity.Level=15;p.Entity.BoundCps=1000000;p.Entity.ConquerPoints=1000000;EntityTable.SaveEntity(p);return p;};
        Func<GameState,long> receipts=p=>Scalar("SELECT count(*) FROM revora_cp_shop_purchases WHERE uid=@uid",p.Entity.UID);
        try {
            ShopFile.Shop oldGarmentShop; ShopFile.Shops.TryGetValue(502, out oldGarmentShop);
            Revora.Interfaces.INpc oldMerchant; town.Npcs.TryGetValue(6002, out oldMerchant);
            try {
                ShopFile.Shops[502]=new ShopFile.Shop { UID=502, MoneyType=ShopFile.MoneyType.ConquerPoints, Items=new List<uint>{723700} };
                town.Npcs[6002]=new NpcSpawn { UID=6002, MapID=town.ID, X=396, Y=360 };
                var local=buyer();local.Entity.X=396;local.Entity.Y=360;
                var order=BuyBook(723700,1,502);order.dwExtraInfo3=2;
                ItemHandler.HandleBuyFromNPC(order,local);
                Check(!Fenced(local)&&receipts(local)==1&&local.Inventory.Objects.Single(i=>i.ID==723700).Bound,"garment shop 502 resolves nearby merchant 6002");
                local=ReloadCustomStarter(local);
                Check(receipts(local)==1&&local.Inventory.Objects.Single(i=>i.ID==723700).Bound,"garment merchant purchase persists");
                var remote=buyer();remote.Entity.X=450;remote.Entity.Y=450;
                ItemHandler.HandleBuyFromNPC(order,remote);
                Check(receipts(remote)==0&&CountId(remote,723700)==0,"garment shop rejects distant merchant access");
            } finally {
                if(oldGarmentShop==null)ShopFile.Shops.Remove(502);else ShopFile.Shops[502]=oldGarmentShop;
                if(oldMerchant==null)town.Npcs.Remove(6002);else town.Npcs[6002]=oldMerchant;
            }
            foreach(bool bound in new[]{false,true}) {
                var p=buyer();var other=Pack(p,723700,3);other.Bound=!bound;Query("UPDATE items SET bound=@b WHERE uid=@uid","b",bound?0:1,"uid",(long)other.UID);
                var matching=Pack(p,723700,8);matching.Bound=bound;Query("UPDATE items SET bound=@b WHERE uid=@uid","b",bound?1:0,"uid",(long)matching.UID);
                var request=BuyBook(723700,5,2888);request.dwExtraInfo3=bound?2u:0u;
                Parallel.For(0,12,n=>ItemHandler.HandleBuyFromNPC(request,p));
                uint cost=ConquerItemInformation.BaseInformations[723700].ConquerPointsWorth*5;
                Check(!Fenced(p)&&receipts(p)==1&&other.StackSize==3&&other.Bound==!bound&&matching.StackSize==10
                    &&p.Inventory.Objects.Where(i=>i.ID==723700&&i.Bound==bound).Sum(i=>i.StackSize)==13,"CP purchase keeps mixed stacks separate and pays once: "+bound);
                p=ReloadCustomStarter(p);ItemHandler.HandleBuyFromNPC(request,p);
                Check(!Fenced(p)&&receipts(p)==1&&(bound?p.Entity.BoundCps:p.Entity.ConquerPoints)==1000000-cost
                    &&(bound?p.Entity.ConquerPoints:p.Entity.BoundCps)==1000000
                    &&p.Inventory.Objects.Where(i=>i.ID==723700&&i.Bound==bound).Sum(i=>i.StackSize)==13,"bound state, quantity, payment and duplicate receipt persist: "+bound);
            }
            var full=buyer();var stack=Pack(full,723700,8);stack.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)stack.UID);
            while(full.Inventory.Count<40)Pack(full,1088001,1);
            var fit=BuyBook(723700,2,2888);fit.dwExtraInfo3=2;ItemHandler.HandleBuyFromNPC(fit,full);
            Check(!Fenced(full)&&receipts(full)==1&&stack.StackSize==10&&full.Inventory.Count==40,"full bag can buy into same-bound stack");
            now=now.AddSeconds(2);var denied=BuyBook(723700,1,2888);denied.dwExtraInfo3=2;uint balance=full.Entity.BoundCps;
            ItemHandler.HandleBuyFromNPC(denied,full);Check(receipts(full)==1&&full.Entity.BoundCps==balance,"insufficient new-slot space charges nothing");
            foreach(string mode in new[]{"funds","quantity","currency","seal","stock","distance","stale-balance"}) {
                var p=buyer();var request=BuyBook(723700,1,2888);request.dwExtraInfo3=2;
                if(mode=="funds"){p.Entity.BoundCps=0;EntityTable.SaveEntity(p);}
                if(mode=="quantity")request.dwExtraInfo=uint.MaxValue;
                if(mode=="currency")request.dwExtraInfo3=3;
                if(mode=="seal")request.ToArray()[request.ToArray().Length-1]^=1;
                if(mode=="stock")request.dwParam=727990;
                if(mode=="distance"){EShopFile.Shops[432].Items.Add(723700);request.UID=432;}
                if(mode=="stale-balance")Query("UPDATE entities SET boundcps=0 WHERE uid=@uid","uid",(long)p.Entity.UID);
                ItemHandler.HandleBuyFromNPC(request,p);Check(receipts(p)==0&&CountId(p,723700)==0,"invalid CP purchase cannot charge or grant: "+mode);
            }
            shop.MoneyType=ShopFile.MoneyType.BoundCps;
            var only=buyer();var boundOnly=BuyBook(730001,1,2888);ItemHandler.HandleBuyFromNPC(boundOnly,only);
            Check(!Fenced(only)&&only.Inventory.Objects.Single(i=>i.ID==730001).Bound&&only.Inventory.Objects.Single(i=>i.ID==730001).Plus==1
                &&only.Entity.ConquerPoints==1000000&&only.Entity.BoundCps==1000000-ConquerItemInformation.BaseInformations[730001].ConquerPointsWorth,"bound-only shop forces binding and correct plus-stone metadata");
            shop.MoneyType=originalMoney;
            foreach(bool post in new[]{false,true}) {
                var p=buyer();var request=BuyBook(723700,2,2888);request.dwExtraInfo3=2;
                var hook=service.GetField(post?"AfterCommit":"BeforeCommit",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("purchase interruption");}));
                try{ItemHandler.HandleBuyFromNPC(request,p);}finally{hook.SetValue(null,null);}
                Check(Fenced(p)&&receipts(p)==(post?1:0),"purchase interruption transaction outcome: "+post);
                p=ReloadCustomStarter(p);ItemHandler.HandleBuyFromNPC(request,p);
                Check(!Fenced(p)&&receipts(p)==1&&p.Inventory.Objects.Where(i=>i.ID==723700&&i.Bound).Sum(i=>i.StackSize)==2
                    &&p.Entity.BoundCps==1000000-2*ConquerItemInformation.BaseInformations[723700].ConquerPointsWorth,"purchase recovery grants and charges exactly once: "+post);
            }
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.FixedOutputPacks"),null,"Initialize");
            Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.QuestGiftPacks"),null,"Initialize");
            foreach(uint type in new uint[]{724002,727000,721224,720757,727991}) {
                var p=buyer();var request=BuyBook(type,1,2888);request.dwExtraInfo3=2;ItemHandler.HandleBuyFromNPC(request,p);
                var pack=p.Inventory.Objects.Single(i=>i.ID==type);Check(pack.Bound,"purchased pack is bound: "+type);
                OpenStarter(p,pack);p=ReloadCustomStarter(p);
                Check(!Fenced(p)&&CountId(p,type)==0&&p.Inventory.Objects.Where(i=>i.ID!=727990).Any()
                    &&p.Inventory.Objects.All(i=>i.Bound),"bound CP pack contents stay bound after reload: "+type);
            }
            // Generic inventory auto-stack must not merge a bound item into an unbound stack.
            var mixed=buyer();var unbound=Pack(mixed,723700,2);mixed.SpiltStack=true;
            var incoming=new ConquerItem(true){ID=723700,Bound=true,StackSize=1};mixed.Inventory.Add(incoming,Enums.ItemUse.CreateAndAdd);
            mixed=ReloadCustomStarter(mixed);
            Check(mixed.Inventory.Objects.Any(i=>i.ID==723700&&i.Bound)&&mixed.Inventory.Objects.Single(i=>i.UID==unbound.UID).StackSize==2,"ordinary inventory stacking preserves separate binding identities");
            mixed.SpiltStack=false;
            var boundStack=Pack(mixed,723700,4);boundStack.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)boundStack.UID);
            var beforeSplit=mixed.Inventory.Objects.Select(i=>i.UID).ToArray();
            var split=BuyBook(2,1,boundStack.UID,49);ItemUsage.Process(split.ToArray(),mixed);
            var piece=mixed.Inventory.Objects.Single(i=>!beforeSplit.Contains(i.UID));
            Check(piece.Bound&&piece.StackSize==2&&boundStack.StackSize==2,"manual stack split copies binding");
            var merge=BuyBook(piece.UID,1,unbound.UID,48);ItemUsage.Process(merge.ToArray(),mixed);
            Check(mixed.Inventory.ContainsUID(piece.UID)&&mixed.Inventory.Objects.Single(i=>i.UID==unbound.UID).StackSize==2,"manual merge rejects mixed binding");
            mixed=ReloadCustomStarter(mixed);
            Check(mixed.Inventory.Objects.Single(i=>i.UID==piece.UID).Bound&&mixed.Inventory.Objects.Single(i=>i.UID==piece.UID).StackSize==2,"manual split binding survives reload");
        }finally{shop.Items=originalStock;shop.MoneyType=originalMoney;service.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>DateTime.UtcNow));}
    }
    static void VerifyFinalStarter(Map town) {
        foreach(bool cp in new[]{false,true}) {
            var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));StarterLife(p,15,2);
            Query("UPDATE entities SET "+(cp?"boundcps=4294967295":"money=9223372036854775807")+" WHERE uid=@uid","uid",(long)p.Entity.UID);
            p=ReloadCustomStarter(p);OpenStarter(p,CustomPack(p));
            Check(!Fenced(p)&&CustomClaims(p)==1&&CountId(p,727990)==1&&CountId(p,3006233)==0,"final currency overflow keeps entire reward and pack: "+cp);
        }
        foreach(byte cls in new byte[]{11,21,41,51,61,71,81,101,132,142,161}) {
            var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));
            p.Entity.Class=cls;StarterLife(p,15,2);var pack=CustomPack(p);
            var old=p.Inventory.Objects.Where(i=>i.ID!=727990).Select(i=>i.UID).ToArray();
            uint cps=p.Entity.BoundCps;ulong silver=p.Entity.Money;
            Parallel.For(0,12,n=>OpenStarter(p,pack));ItemHandler.UseItem(pack,p);
            var added=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();
            var expected=((uint[])Call(CustomStarter,null,"GearIds",cls)).Concat(new uint[]{3006233,3006233}).ToArray();bool tao=cls==101||cls==132||cls==142;
            Check(!Fenced(p)&&CountId(p,727990)==0&&CustomClaims(p)==2&&added.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(expected.OrderBy(i=>i)),"final claim gives exact new-class set and consumes pack once: "+cls);
            Check(added.All(i=>i.Bound)&&added.Where(i=>i.ID!=1050000&&i.ID!=3006233).All(i=>i.Plus==12&&i.Bless==7&&i.SocketOne==(tao?Enums.Gem.SuperPhoenixGem:Enums.Gem.SuperDragonGem)&&i.SocketTwo==i.SocketOne),"final class set retains full enhancement policy: "+cls);
            Check(CountId(p,723981)==0&&CountId(p,201009)==0&&CountId(p,202009)==0&&CountId(p,203009)==0&&CountId(p,204009)==0
                &&CountId(p,3006233)==2&&p.Entity.BoundCps==cps+100000&&p.Entity.Money==silver+5000000,"final set grants exactly two choice packs, 100K bound CPs and 5M silver without garment/talismans");
            p=ReloadCustomStarter(p);Call(CustomStarter,null,"OnLogin",p);
            Check(!Fenced(p)&&CountId(p,727990)==0&&CustomClaims(p)==2&&Scalar("SELECT count(*) FROM revora_character_creation WHERE character_uid=@uid AND revora_starter_pending",p.Entity.UID)==0,"completed pack cannot return on login");
            var forged=Pack(p,727990,1);forged.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)forged.UID);OpenStarter(p,forged);
            Check(CustomClaims(p)==2,"replacement token cannot reopen closed entitlement");
        }
        foreach(bool post in new[]{false,true}) {
            var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));StarterLife(p,15,2);
            int slots=((uint[])Call(CustomStarter,null,"GearIds",p.Entity.Class)).Length+2;
            while(p.Inventory.Count<42-slots)Pack(p,1088001,1);
            OpenStarter(p,CustomPack(p));Check(CustomClaims(p)==1&&CountId(p,727990)==1,"final reward insufficient space keeps pack and entitlement");
            var filler=p.Inventory.Objects.First(i=>i.ID==1088001);Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);p.Inventory.Remove(filler.UID,Enums.ItemUse.None,false);
            var hook=CustomStarter.GetField(post?"AfterCommit":"BeforeCommit",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("final reward interruption");}));
            try{OpenStarter(p,CustomPack(p));}finally{hook.SetValue(null,null);}
            Check(Fenced(p)&&CustomClaims(p)==(post?2:1),"final reward atomic commit boundary: "+post);
            p=ReloadCustomStarter(p);Call(CustomStarter,null,"OnLogin",p);
            Check(CountId(p,727990)==(post?0:1),"final pack deletion follows durable commit: "+post);
            if(!post)OpenStarter(p,CustomPack(p));
            Check(!Fenced(p)&&CustomClaims(p)==2&&CountId(p,727990)==0&&p.Inventory.Count==40,"final reward recovers exactly once and credits consumed pack slot: "+post);
        }
    }
    static void VerifyFirstRebirthStarter(Map town) {
        foreach(byte cls in new byte[]{11,21,41,51,61,71,81,101,132,142,161}) {
            var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));
            var old=p.Inventory.Objects.Select(i=>i.UID).ToArray();uint cps=p.Entity.BoundCps;ulong silver=p.Entity.Money;
            p.Entity.Class=cls;StarterLife(p,15,1);
            Parallel.For(0,12,n=>OpenStarter(p,CustomPack(p)));
            var ids=((uint[])Call(CustomStarter,null,"GearIds",cls)).Concat(new uint[]{723981}).OrderBy(i=>i).ToArray();
            var added=p.Inventory.Objects.Where(i=>!old.Contains(i.UID)).ToArray();
            bool tao=cls==101||cls==132||cls==142;
            Check(!Fenced(p)&&CustomClaims(p)==2&&added.Select(i=>i.ID).OrderBy(i=>i).SequenceEqual(ids),"one first-rebirth bundle for current class: "+cls);
            Check(added.All(i=>i.Bound)&&added.Where(i=>i.ID!=723981&&i.ID!=1050000).All(i=>i.Plus==12&&i.Bless==7
                &&i.SocketOne==(tao?Enums.Gem.SuperPhoenixGem:Enums.Gem.SuperDragonGem)&&i.SocketTwo==i.SocketOne),"first-rebirth Super gear has +12 -7 and two correct gems: "+cls);
            Check(old.All(id=>p.Inventory.ContainsUID(id))&&p.Entity.BoundCps==cps&&p.Entity.Money==silver
                &&CountId(p,201009)==0&&CountId(p,202009)==0&&CountId(p,203009)==0&&CountId(p,204009)==0,"new bundle preserves original items, no talismans/currency");
            p=ReloadCustomStarter(p);
            foreach(byte level in new byte[]{40,70,100,120}){StarterLife(p,level,1);OpenStarter(p,CustomPack(p));}
            Check(!Fenced(p)&&CustomClaims(p)==2&&CountId(p,723981)==1&&p.Inventory.Count==old.Length+ids.Length,"first-rebirth level milestones do not repeat after reload");
        }
        foreach(bool post in new[]{false,true}) {
            var p=CustomStarterPlayer(town);OpenStarter(p,CustomPack(p));StarterLife(p,15,1);
            int slots=((uint[])Call(CustomStarter,null,"GearIds",p.Entity.Class)).Length+1;
            while(p.Inventory.Count<41-slots)Pack(p,1088001,1);
            OpenStarter(p,CustomPack(p));Check(CustomClaims(p)==1&&CountId(p,723981)==0,"first-rebirth bundle cannot partially fill insufficient bag");
            var filler=p.Inventory.Objects.First(i=>i.ID==1088001);Query("DELETE FROM items WHERE uid=@uid","uid",(long)filler.UID);p.Inventory.Remove(filler.UID,Enums.ItemUse.None,false);
            var hook=CustomStarter.GetField(post?"AfterCommit":"BeforeCommit",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("rebirth reward interruption");}));
            try{OpenStarter(p,CustomPack(p));}finally{hook.SetValue(null,null);}
            Check(Fenced(p)&&CustomClaims(p)==(post?2:1),"first-rebirth commit boundary: "+post);
            p=ReloadCustomStarter(p);OpenStarter(p,CustomPack(p));
            Check(!Fenced(p)&&CustomClaims(p)==2&&CountId(p,723981)==1&&p.Inventory.Count==40,"first-rebirth exact-fit recovery once: "+post);
        }
        var stale=CustomStarterPlayer(town);OpenStarter(stale,CustomPack(stale));stale.Entity.Reborn=1;
        Query("UPDATE entities SET reborn=0 WHERE uid=@uid","uid",(long)stale.Entity.UID);
        OpenStarter(stale,CustomPack(stale));Check(Fenced(stale)&&CustomClaims(stale)==1,"forged cached rebirth cannot claim new equipment");
        VerifyRebirthGarments(town);
    }
    static void VerifyRebirthGarments(Map town) {
        var type=typeof(Entity).Assembly.GetType("Revora.Game.Features.RebirthGarmentPack");Call(type,null,"Initialize");
        if(StorageItem.Garments.Count==0)StorageItem.Load();
        var pool=(uint[])Call(type,null,"Pool");
        Check(pool.Length>0&&pool.All(id=>StorageItem.Garments[id].Type==1&&(StorageItem.Garments[id].Stars==4||StorageItem.Garments[id].Stars==5))
            &&pool.Any(id=>StorageItem.Garments[id].Stars==4)&&pool.Any(id=>StorageItem.Garments[id].Stars==5),"garment pool includes only four/five-star garments, both tiers available");
        var field=type.GetField("Roll",Hidden);var original=field.GetValue(null);
        try {
            foreach(int stars in new[]{4,5})foreach(int roll in new[]{0,9,10,99}) {
                int draws=0,index=Array.FindIndex(pool,id=>StorageItem.Garments[id].Stars==stars);
                field.SetValue(null,(Func<int,int>)(count=>{draws++;return draws%2==1?index:roll;}));
                var p=CustomStarterPlayer(town);var pack=Pack(p,723981,1);pack.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)pack.UID);
                while(p.Inventory.Count<40)Pack(p,1088001,1);
                var malformed=PrayerPacket(pack.UID);malformed[malformed.Length-1]^=1;ItemUsage.Process(malformed,p);
                Check(draws==0&&p.Inventory.ContainsUID(pack.UID),"malformed garment frame cannot consume or roll");
                Parallel.For(0,12,n=>OpenStarter(p,pack));ItemHandler.UseItem(pack,p);
                p=ReloadCustomStarter(p);var garment=p.Inventory.Objects.Single(i=>i.ID==pool[index]);
                Check(!Fenced(p)&&draws==2&&p.Inventory.Count==40&&CountId(p,723981)==0&&garment.Bound&&garment.Bless==(roll<10?1:0)
                    &&garment.Plus==0&&garment.SocketOne==0&&garment.SocketTwo==0&&garment.Days==0,"garment tier/blessing boundary, full bag, replay and reload: "+stars+"/"+roll);
            }
            foreach(bool post in new[]{false,true}) {
                int draws=0;field.SetValue(null,(Func<int,int>)(count=>{draws++;return 0;}));
                var p=CustomStarterPlayer(town);var pack=Pack(p,723981,1);pack.Bound=true;Query("UPDATE items SET bound=1 WHERE uid=@uid","uid",(long)pack.UID);
                var hook=type.GetField(post?"AfterCommit":"BeforeCommit",Hidden);hook.SetValue(null,(Action)(()=>{throw new InvalidOperationException("garment interruption");}));
                try{OpenStarter(p,pack);}finally{hook.SetValue(null,null);}
                Check(Fenced(p),"garment interruption fences session");p=ReloadCustomStarter(p);
                if(!post)OpenStarter(p,p.Inventory.Objects.Single(i=>i.ID==723981));
                Check(!Fenced(p)&&CountId(p,723981)==0&&CountId(p,pool[0])==1&&draws==(post?2:4),"garment committed result cannot reroll on recovery: "+post);
            }
        }finally{field.SetValue(null,original);}
    }
    static DateTime starterNow=DateTime.UtcNow;
    static long StarterUses(GameState p){return Scalar("SELECT count(*) FROM revora_starter_pack_uses WHERE uid=@uid",p.Entity.UID);}
    static void OpenStarter(GameState p,ConquerItem item){ItemUsage.Process(PrayerPacket(item.UID),p);}
    static void VerifyStarterPacks(string runtime,Map town){
        Call(Starter,null,"Initialize");Call(Starter,null,"Initialize");
        Starter.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>starterNow));
        // Creation delivery is exercised through DoLogin and durable item reload
        // by REVIEW-293/295 in ConquerPointBagTests, including rollback and replay.
        string handler=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/ItemHandler.cs")));
        Check(handler.IndexOf("case 727046:")>=0 && handler.IndexOf("case 727050:")>=0 && handler.IndexOf("Starter pack must use the owned service.")>=0
            && handler.IndexOf("Add(727047, 0, 1)")<0 && handler.IndexOf("BeginnerPackL120")>=0,
            "727026-727050 starter cases are fail-closed on the live handler");
        var other=WelcomePlayer(town,1,0,15);var side=Pack(other,723753,1);
        Check(!(bool)Call(Starter,null,"TryUse",side,other) && other.Inventory.ContainsUID(side.UID) && StarterUses(other)==0,"723753 stays outside the 727026 starter service");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,1,0,15);var pack=NestedPack(p,727026,stack,bound);ulong silver=p.Entity.Money;
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenStarter(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && StarterUses(p)==expected && p.Entity.Money==silver+15000UL*(ulong)expected && CountId(p,722136)==10*expected && CountId(p,1001000)==5*expected && CountId(p,727027)==expected
                && CountId(p,727026)==0,"L1 starter pack pays 15,000 silver, ten EXP Balls, five ores and 727027: "+bound+"/"+stack);
            ItemHandler.UseItem(pack,p);p=ReloadBall(p);
            Check(!Fenced(p) && StarterUses(p)==expected && CountId(p,727027)==expected && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)(silver+15000UL*(ulong)expected),"L1 starter reload retains balls, ores, successor and silver");
        }
        var l1=WelcomePlayer(town,1,0,15);var first=Pack(l1,727026,1);OpenStarter(l1,first);
        Check(l1.Inventory.Objects.Where(i=>i.ID==722136||i.ID==1001000||i.ID==727027).All(i=>!i.Bound && i.Plus==0),
            "L1 starter outputs stay unbound plus-0");
        var ball=l1.Inventory.Objects.First(i=>i.ID==722136);Call(ExpItems,null,"Initialize");ulong xp=l1.Entity.Experience;ItemHandler.UseItem(ball,l1);
        Check(!Fenced(l1) && (l1.Entity.Experience>xp || l1.ExpBalls>0 || !l1.Inventory.ContainsUID(ball.UID) || ball.StackSize==0),"starter EXP Ball remains usable through ExperienceConsumables");
        foreach(var row in new[]{
            new object[]{727027u,(byte)5,(byte)4,1001000u,5,727028u,1500UL,0u},
            new object[]{727028u,(byte)10,(byte)9,723790u,3,727029u,5000UL,0u},
            new object[]{727029u,(byte)15,(byte)14,723790u,5,727030u,0UL,0u},
            new object[]{727030u,(byte)20,(byte)19,727031u,1,727031u,0UL,500u},
            new object[]{727031u,(byte)25,(byte)24,203009u,1,727032u,0UL,0u},
            new object[]{727032u,(byte)30,(byte)29,1200000u,1,727033u,0UL,0u}}){
            uint id=(uint)row[0];byte level=(byte)row[1];byte below=(byte)row[2];uint gift=(uint)row[3];int gifts=(int)row[4];uint next=(uint)row[5];ulong pay=(ulong)row[6];uint cp=(uint)row[7];
            var early=WelcomePlayer(town,below,0,15);var pack=Pack(early,id,1);OpenStarter(early,pack);
            Check(!Fenced(early) && StarterUses(early)==0 && early.Inventory.ContainsUID(pack.UID),"starter pack "+id+" refuses below level "+level);
            var p=WelcomePlayer(town,level,0,15);pack=Pack(p,id,1);uint beforeCp=p.Entity.ConquerPoints;ulong beforeSilver=p.Entity.Money;OpenStarter(p,pack);
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,gift)==gifts && CountId(p,next)==1 && p.Entity.ConquerPoints==beforeCp+cp && p.Entity.Money==beforeSilver+pay,
                "starter pack "+id+" pays its existing items and currency at level "+level);
            p=ReloadBall(p);Check(CountId(p,next)==1 && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)(beforeSilver+pay)
                && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==beforeCp+cp,"starter pack "+id+" survives production reload");
        }
        var l10=WelcomePlayer(town,10,0,15);var ten=Pack(l10,727028,1);OpenStarter(l10,ten);
        Check(CountId(l10,721623)==1 && CountId(l10,721624)==1 && CountId(l10,723790)==3 && CountId(l10,727029)==1,"L10 starter pack also issues 721623 and 721624");
        var l15=WelcomePlayer(town,15,0,15);var fifteen=Pack(l15,727029,1);OpenStarter(l15,fifteen);
        Check(CountId(l15,1060020)==3 && CountId(l15,723790)==5 && CountId(l15,727030)==1,"L15 starter pack issues three Twin City gates");
        var stone=WelcomePlayer(town,30,0,15);var last=Pack(stone,727032,1);OpenStarter(stone,last);
        Check(CountId(stone,1200000)==1 && CountId(stone,727033)==1 && stone.Inventory.Objects.Where(i=>i.ID==1200000||i.ID==727033).All(i=>!i.Bound && i.Plus==0),
            "L30 starter pack issues unbound plus-0 PrayingStone(S) and owned 727033");
        var pray=stone.Inventory.Objects.First(i=>i.ID==1200000);
        Check(!(bool)Call(Starter,null,"TryUse",pray,stone) && stone.Inventory.ContainsUID(pray.UID) && StarterUses(stone)==1 && !Fenced(stone),
            "issued PrayingStone(S) remains outside StarterPacks on its existing ordinary handler");
        var full=WelcomePlayer(town,1,0,15);var starter=Pack(full,727026,1);while(full.Inventory.Count<40)Pack(full,1088001,1);OpenStarter(full,starter);
        Check(!Fenced(full) && StarterUses(full)==0 && full.Inventory.ContainsUID(starter.UID),"exact L1 16-output starter pack refuses a full bag");
        var room=WelcomePlayer(town,1,0,15);starter=Pack(room,727026,1);while(room.Inventory.Count<25)Pack(room,1088001,1);OpenStarter(room,starter);
        Check(!Fenced(room) && StarterUses(room)==1 && CountId(room,727027)==1,"L1 starter pack credits the source slot and fits sixteen outputs");
        var packed=WelcomePlayer(town,10,0,15);var six=Pack(packed,727028,1);while(packed.Inventory.Count<35)Pack(packed,1088001,1);OpenStarter(packed,six);
        Check(!Fenced(packed) && StarterUses(packed)==1 && CountId(packed,727029)==1,"L10 six-output starter pack fits when one source slot is credited");
        var packedFull=WelcomePlayer(town,10,0,15);var sixFull=Pack(packedFull,727028,1);while(packedFull.Inventory.Count<36)Pack(packedFull,1088001,1);OpenStarter(packedFull,sixFull);
        Check(!Fenced(packedFull) && StarterUses(packedFull)==0 && packedFull.Inventory.ContainsUID(sixFull.UID),"exact L10 6-output starter pack refuses when source credit is not enough");
        var stacked=WelcomePlayer(town,1,0,15);var pile=Pack(stacked,727026,2);OpenStarter(stacked,pile);
        Check(!Fenced(stacked) && StarterUses(stacked)==1 && pile.StackSize==1 && CountId(stacked,727027)==1,"starter pack keeps a remaining stacked source");
        var overflow=WelcomePlayer(town,20,0,15);Query("UPDATE entities SET conquerpoints=4294967295 WHERE uid=@uid","uid",(long)overflow.Entity.UID);overflow=ReloadBall(overflow);var rich=Pack(overflow,727030,1);OpenStarter(overflow,rich);
        Check(!Fenced(overflow) && StarterUses(overflow)==0 && overflow.Inventory.ContainsUID(rich.UID) && overflow.Entity.ConquerPoints==uint.MaxValue,"L20 CP overflow preserves the starter pack");
        foreach(string fault in new[]{"source","output","silver","receipt"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,727026,1);ulong silver=p.Entity.Money;
            string table=fault=="silver"?"entities":fault=="receipt"?"revora_starter_pack_uses":"items";
            string verb=fault=="source"?"DELETE":fault=="silver"?"UPDATE":"INSERT";
            Query("CREATE FUNCTION suppress_starter() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_starter BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_starter()");OpenStarter(p,item);
            Check(Fenced(p) && StarterUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,727027)==0 && p.Entity.Money==silver
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)silver,"suppressed "+fault+" preserves starter source, outputs and silver");
            Query("DROP TRIGGER suppress_starter ON "+table+";DROP FUNCTION suppress_starter()");p=ReloadBall(p);OpenStarter(p,p.Inventory.Objects.Single(i=>i.ID==727026));
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,727027)==1,"production reload retries the full starter pack after "+fault+" rollback");
        }
        var cpFault=WelcomePlayer(town,20,0,15);var cpPack=Pack(cpFault,727030,1);uint cpBefore=cpFault.Entity.ConquerPoints;
        Query("CREATE FUNCTION suppress_starter_cp() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_starter_cp BEFORE UPDATE ON entities FOR EACH ROW EXECUTE FUNCTION suppress_starter_cp()");OpenStarter(cpFault,cpPack);
        Check(Fenced(cpFault) && StarterUses(cpFault)==0 && cpFault.Inventory.ContainsUID(cpPack.UID) && CountId(cpFault,727031)==0 && cpFault.Entity.ConquerPoints==cpBefore,"suppressed CP preserves L20 starter source and successor");
        Query("DROP TRIGGER suppress_starter_cp ON entities;DROP FUNCTION suppress_starter_cp()");cpFault=ReloadBall(cpFault);OpenStarter(cpFault,cpFault.Inventory.Objects.Single(i=>i.ID==727030));
        Check(!Fenced(cpFault) && StarterUses(cpFault)==1 && cpFault.Entity.ConquerPoints==cpBefore+500,"production reload retries the L20 starter pack after CP rollback");
        var stale=WelcomePlayer(town,1,0,15);var supply=Pack(stale,727026,1);Query("UPDATE entities SET money=money+1 WHERE uid=@uid","uid",(long)stale.Entity.UID);ulong staleSilver=stale.Entity.Money;OpenStarter(stale,supply);
        Check(Fenced(stale) && StarterUses(stale)==0 && stale.Inventory.ContainsUID(supply.UID) && Scalar("SELECT money FROM entities WHERE uid=@uid",stale.Entity.UID)==(long)staleSilver+1,"stale silver preimage cannot open an L1 starter pack");
        var staleCp=WelcomePlayer(town,20,0,15);var cpSupply=Pack(staleCp,727030,1);Query("UPDATE entities SET conquerpoints=conquerpoints+1 WHERE uid=@uid","uid",(long)staleCp.Entity.UID);uint staleBefore=staleCp.Entity.ConquerPoints;OpenStarter(staleCp,cpSupply);
        Check(Fenced(staleCp) && StarterUses(staleCp)==0 && staleCp.Inventory.ContainsUID(cpSupply.UID) && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",staleCp.Entity.UID)==staleBefore+1,"stale CP preimage cannot open an L20 starter pack");
        foreach(string guard in new[]{"dead","fake","trade","booth","locked","suspicious","wardrobe","expired","substituted"}){
            var p=WelcomePlayer(town,1,0,15);var item=Pack(p,727026,1);ulong silver=p.Entity.Money;
            if(guard=="dead")p.Entity.Hitpoints=0;if(guard=="fake")p.Fake=true;if(guard=="trade")p.Trade.InTrade=true;
            if(guard=="booth")p.Booth=(Revora.Game.ConquerStructures.Booth)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Revora.Game.ConquerStructures.Booth));if(guard=="locked")item.Lock=1;
            if(guard=="suspicious")item.Suspicious=true;if(guard=="wardrobe")item.InWardrobe=true;if(guard=="expired"){item.Days=1;item.DayStamp=starterNow.ToLocalTime().AddDays(-2);}
            if(guard=="substituted")ItemHandler.UseItem(new ConquerItem(true){ID=item.ID,UID=item.UID,StackSize=1},p);else OpenStarter(p,item);
            Check(StarterUses(p)==0 && CountId(p,727027)==0 && p.Entity.Money==silver && p.Inventory.ContainsUID(item.UID),"ineligible starter pack leaves source unchanged: "+guard);
        }
        var frames=WelcomePlayer(town,1,0,15);var wire=Pack(frames,727026,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(wire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(wire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,frames);Check(!Fenced(frames) && StarterUses(frames)==0 && frames.Inventory.ContainsUID(wire.UID),"bounded starter pack rejects "+bad+" frame");
        }OpenStarter(frames,wire);Check(!Fenced(frames) && StarterUses(frames)==1 && CountId(frames,727027)==1,"ordinary starter pack works after malformed frames");
        var replay=WelcomePlayer(town,1,0,15);var once=Pack(replay,727026,1);uint uid=once.UID;OpenStarter(replay,once);
        ItemHandler.UseItem(once,replay);Check(StarterUses(replay)==1 && !replay.Inventory.ContainsUID(uid),"consumed starter pack UID cannot replay");
        var keep=WelcomePlayer(town,1,0,15);var keptPack=Pack(keep,727026,1);OpenStarter(keep,keptPack);long kept=StarterUses(keep);
        Call(Starter,null,"Initialize");
        Check(StarterUses(keep)==kept && Scalar("SELECT count(*) FROM revora_starter_pack_uses WHERE uid=@uid AND item_type=727026",keep.Entity.UID)==kept,
            "widening the starter receipt check keeps existing 727026-727032 uses");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,35,0,15);var pack=NestedPack(p,727033,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenStarter(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && StarterUses(p)==expected && CountId(p,723017)==expected && CountId(p,727034)==expected && CountId(p,727033)==0
                && p.Inventory.Objects.Where(i=>i.ID==723017||i.ID==727034).All(i=>i.Bound==bound && i.Plus==0),
                "L35 starter pack pays one EXP Potion and 727034 unbound plus-0: "+bound+"/"+stack);
            p=ReloadBall(p);Check(!Fenced(p) && StarterUses(p)==expected && CountId(p,727034)==expected,"L35 starter reload retains the potion and successor");
        }
        foreach(var row in new[]{
            new object[]{727033u,(byte)35,(byte)34,723017u,1,727034u},
            new object[]{727034u,(byte)40,(byte)39,723017u,1,727035u},
            new object[]{727035u,(byte)45,(byte)44,1200001u,1,727036u},
            new object[]{727036u,(byte)50,(byte)49,1200002u,1,727037u},
            new object[]{727037u,(byte)55,(byte)54,723017u,1,727038u},
            new object[]{727038u,(byte)60,(byte)59,723700u,1,727039u},
            new object[]{727039u,(byte)65,(byte)64,2100025u,1,727040u}}){
            uint id=(uint)row[0];byte level=(byte)row[1];byte below=(byte)row[2];uint gift=(uint)row[3];int gifts=(int)row[4];uint next=(uint)row[5];
            var earlyPack=WelcomePlayer(town,below,0,15);var pack=Pack(earlyPack,id,1);OpenStarter(earlyPack,pack);
            Check(!Fenced(earlyPack) && StarterUses(earlyPack)==0 && earlyPack.Inventory.ContainsUID(pack.UID),"starter pack "+id+" refuses below level "+level);
            var p=WelcomePlayer(town,level,0,15);pack=Pack(p,id,1);uint beforeCp=p.Entity.ConquerPoints;ulong beforeSilver=p.Entity.Money;OpenStarter(p,pack);
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,gift)==gifts && CountId(p,next)==1 && p.Entity.ConquerPoints==beforeCp && p.Entity.Money==beforeSilver
                && p.Inventory.Objects.Where(i=>i.ID==gift||i.ID==next).All(i=>!i.Bound && i.Plus==0),"starter pack "+id+" pays its existing items with no currency at level "+level);
            p=ReloadBall(p);Check(CountId(p,next)==1 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==beforeCp
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)beforeSilver,"starter pack "+id+" survives production reload");
        }
        var l55=WelcomePlayer(town,55,0,15);var tulipPack=Pack(l55,727037,1);OpenStarter(l55,tulipPack);
        Check(CountId(l55,723017)==1 && CountId(l55,723584)==1 && CountId(l55,727038)==1 && l55.Inventory.Objects.Where(i=>i.ID==723584).All(i=>!i.Bound && i.Plus==0),
            "L55 starter pack also issues unbound plus-0 retired BlackTulip 723584");
        var l60=WelcomePlayer(town,60,0,15);var lilyPack=Pack(l60,727038,1);OpenStarter(l60,lilyPack);
        Check(CountId(l60,723700)==1 && CountId(l60,752001)==1 && CountId(l60,727039)==1 && l60.Inventory.Objects.Where(i=>i.ID==752001).All(i=>!i.Bound && i.Plus==0),
            "L60 starter pack also issues unbound plus-0 lily 752001");
        var twoFull=WelcomePlayer(town,35,0,15);var two=Pack(twoFull,727033,1);while(twoFull.Inventory.Count<40)Pack(twoFull,1088001,1);OpenStarter(twoFull,two);
        Check(!Fenced(twoFull) && StarterUses(twoFull)==0 && twoFull.Inventory.ContainsUID(two.UID),"exact L35 2-output starter pack refuses a full bag");
        var twoRoom=WelcomePlayer(town,35,0,15);two=Pack(twoRoom,727033,1);while(twoRoom.Inventory.Count<39)Pack(twoRoom,1088001,1);OpenStarter(twoRoom,two);
        Check(!Fenced(twoRoom) && StarterUses(twoRoom)==1 && CountId(twoRoom,727034)==1,"L35 starter pack credits the source slot and fits two outputs");
        var threeFull=WelcomePlayer(town,55,0,15);var three=Pack(threeFull,727037,1);while(threeFull.Inventory.Count<39)Pack(threeFull,1088001,1);OpenStarter(threeFull,three);
        Check(!Fenced(threeFull) && StarterUses(threeFull)==0 && threeFull.Inventory.ContainsUID(three.UID),"exact L55 3-output starter pack refuses when source credit is not enough");
        var threeRoom=WelcomePlayer(town,55,0,15);three=Pack(threeRoom,727037,1);while(threeRoom.Inventory.Count<38)Pack(threeRoom,1088001,1);OpenStarter(threeRoom,three);
        Check(!Fenced(threeRoom) && StarterUses(threeRoom)==1 && CountId(threeRoom,727038)==1,"L55 starter pack credits the source slot and fits three outputs");
        var potion=WelcomePlayer(town,35,0,15);var drinkPack=Pack(potion,727033,1);OpenStarter(potion,drinkPack);
        var dose=potion.Inventory.Objects.First(i=>i.ID==723017);Call(ExpPotions,null,"Initialize");potion.Entity.DoubleExperienceTime=0;Drink(potion,dose);
        Check(!Fenced(potion) && potion.Entity.DoubleExperienceTime==3600,"starter EXP Potion remains usable through ExperiencePotions");
        var mid=WelcomePlayer(town,45,0,15);var midPack=Pack(mid,727035,1);OpenStarter(mid,midPack);
        var medium=mid.Inventory.Objects.First(i=>i.ID==1200001);
        Check(!(bool)Call(Starter,null,"TryUse",medium,mid) && mid.Inventory.ContainsUID(medium.UID),"issued PrayingStone(M) remains outside StarterPacks");
        var large=WelcomePlayer(town,50,0,15);var largePack=Pack(large,727036,1);OpenStarter(large,largePack);
        var largeStone=large.Inventory.Objects.First(i=>i.ID==1200002);
        Check(!(bool)Call(Starter,null,"TryUse",largeStone,large) && large.Inventory.ContainsUID(largeStone.UID),"issued PrayingStone(L) remains outside StarterPacks");
        var ballPack=WelcomePlayer(town,60,0,15);var sixty=Pack(ballPack,727038,1);OpenStarter(ballPack,sixty);
        var expBall=ballPack.Inventory.Objects.First(i=>i.ID==723700);Call(ExpItems,null,"Initialize");ulong xp2=ballPack.Entity.Experience;ItemHandler.UseItem(expBall,ballPack);
        Check(!Fenced(ballPack) && (ballPack.Entity.Experience>xp2 || ballPack.ExpBalls>0 || !ballPack.Inventory.ContainsUID(expBall.UID) || expBall.StackSize==0),"starter EXP Ball 723700 remains usable through ExperienceConsumables");
        var tulip=l55.Inventory.Objects.First(i=>i.ID==723584);
        Check(!(bool)Call(Starter,null,"TryUse",tulip,l55) && l55.Inventory.ContainsUID(tulip.UID)
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/RetiredBlackTulip.cs"))).IndexOf("id == 723584")>=0
            && handler.IndexOf("RetiredBlackTulip.TryUse")<handler.IndexOf("StarterPacks.TryUse"),
            "issued BlackTulip stays on RetiredBlackTulip ahead of StarterPacks");
        var lily=l60.Inventory.Objects.First(i=>i.ID==752001);
        Check(!(bool)Call(Starter,null,"TryUse",lily,l60) && l60.Inventory.ContainsUID(lily.UID)
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/Flowers/Flowers.cs"))).IndexOf("ID >= 752001 && ID <= 752999")>=0
            && ConquerItemInformation.BaseInformations.ContainsKey(752001u),"752001 remains the catalog lily flower identity and is not claimed by StarterPacks");
        var bottle=WelcomePlayer(town,65,0,15);var bottlePack=Pack(bottle,727039,1);OpenStarter(bottle,bottlePack);
        var jar=bottle.Inventory.Objects.First(i=>i.ID==2100025);
        Check(!(bool)Call(Starter,null,"TryUse",jar,bottle) && bottle.Inventory.ContainsUID(jar.UID) && CountId(bottle,727040)==1
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/CityGuardSystem.cs"))).IndexOf("Equip(gs, ConquerItem.Bottle, 2100025)")>=0
            && ConquerItemInformation.BaseInformations.ContainsKey(2100025u),"2100025 remains the City Guard bottle catalog identity and is not claimed by StarterPacks");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,35,0,15);var item=Pack(p,727033,1);
            string table=fault=="receipt"?"revora_starter_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_starter_b() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_starter_b BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_starter_b()");OpenStarter(p,item);
            Check(Fenced(p) && StarterUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,727034)==0,"suppressed "+fault+" preserves L35 starter source and outputs");
            Query("DROP TRIGGER suppress_starter_b ON "+table+";DROP FUNCTION suppress_starter_b()");p=ReloadBall(p);OpenStarter(p,p.Inventory.Objects.Single(i=>i.ID==727033));
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,727034)==1,"production reload retries the L35 starter pack after "+fault+" rollback");
        }
        var lateReplay=WelcomePlayer(town,35,0,15);var lateOnce=Pack(lateReplay,727033,1);uint lateUid=lateOnce.UID;OpenStarter(lateReplay,lateOnce);
        ItemHandler.UseItem(lateOnce,lateReplay);Check(StarterUses(lateReplay)==1 && !lateReplay.Inventory.ContainsUID(lateUid),"consumed L35 starter pack UID cannot replay");
        var lateFrames=WelcomePlayer(town,35,0,15);var lateWire=Pack(lateFrames,727033,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(lateWire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(lateWire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,lateFrames);Check(!Fenced(lateFrames) && StarterUses(lateFrames)==0 && lateFrames.Inventory.ContainsUID(lateWire.UID),"bounded L35 starter pack rejects "+bad+" frame");
        }OpenStarter(lateFrames,lateWire);Check(!Fenced(lateFrames) && StarterUses(lateFrames)==1 && CountId(lateFrames,727034)==1,"ordinary L35 starter pack works after malformed frames");
        var cKeep=WelcomePlayer(town,35,0,15);var cKept=Pack(cKeep,727033,1);OpenStarter(cKeep,cKept);long cKeptUses=StarterUses(cKeep);
        Call(Starter,null,"Initialize");
        Check(StarterUses(cKeep)==cKeptUses && Scalar("SELECT count(*) FROM revora_starter_pack_uses WHERE uid=@uid AND item_type=727033",cKeep.Entity.UID)==cKeptUses,
            "widening the starter receipt check keeps existing 727026-727039 uses");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,70,0,15);var pack=NestedPack(p,727040,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenStarter(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && StarterUses(p)==expected && CountId(p,723700)==2*expected && CountId(p,727041)==expected && CountId(p,727040)==0
                && p.Inventory.Objects.Where(i=>i.ID==723700||i.ID==727041).All(i=>i.Bound==bound && i.Plus==0),
                "L70 starter pack pays two EXP Balls and 727041 unbound plus-0: "+bound+"/"+stack);
            p=ReloadBall(p);Check(!Fenced(p) && StarterUses(p)==expected && CountId(p,727041)==expected,"L70 starter reload retains balls and successor");
        }
        foreach(var row in new[]{
            new object[]{727040u,(byte)70,(byte)69,723700u,2,727041u},
            new object[]{727041u,(byte)75,(byte)74,1200000u,1,727042u},
            new object[]{727042u,(byte)80,(byte)79,723700u,2,727043u},
            new object[]{727043u,(byte)85,(byte)84,723017u,2,727044u},
            new object[]{727045u,(byte)95,(byte)94,723268u,1,727046u}}){
            uint id=(uint)row[0];byte level=(byte)row[1];byte below=(byte)row[2];uint gift=(uint)row[3];int gifts=(int)row[4];uint next=(uint)row[5];
            var cEarly=WelcomePlayer(town,below,0,15);var pack=Pack(cEarly,id,1);OpenStarter(cEarly,pack);
            Check(!Fenced(cEarly) && StarterUses(cEarly)==0 && cEarly.Inventory.ContainsUID(pack.UID),"starter pack "+id+" refuses below level "+level);
            var p=WelcomePlayer(town,level,0,15);pack=Pack(p,id,1);uint beforeCp=p.Entity.ConquerPoints;ulong beforeSilver=p.Entity.Money;OpenStarter(p,pack);
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,gift)==gifts && CountId(p,next)==1 && p.Entity.ConquerPoints==beforeCp && p.Entity.Money==beforeSilver
                && p.Inventory.Objects.Where(i=>i.ID==gift||i.ID==next).All(i=>!i.Bound && i.Plus==0),"starter pack "+id+" pays its existing items with no currency at level "+level);
            p=ReloadBall(p);Check(CountId(p,next)==1 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==beforeCp
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)beforeSilver,"starter pack "+id+" survives production reload");
        }
        var mismatch90=WelcomePlayer(town,90,0,15);var mismatchPack=Pack(mismatch90,727044,1);OpenStarter(mismatch90,mismatchPack);
        Check(!Fenced(mismatch90) && StarterUses(mismatch90)==0 && mismatch90.Inventory.ContainsUID(mismatchPack.UID),"727044 refuses at level 90 despite the L90 region message");
        var mismatch94=WelcomePlayer(town,94,0,15);var mismatchPack94=Pack(mismatch94,727044,1);OpenStarter(mismatch94,mismatchPack94);
        Check(!Fenced(mismatch94) && StarterUses(mismatch94)==0 && mismatch94.Inventory.ContainsUID(mismatchPack94.UID),"727044 refuses below the executable level-95 gate");
        var mismatch95=WelcomePlayer(town,95,0,15);var mismatchPack95=Pack(mismatch95,727044,1);OpenStarter(mismatch95,mismatchPack95);
        Check(!Fenced(mismatch95) && StarterUses(mismatch95)==1 && CountId(mismatch95,723268)==1 && CountId(mismatch95,723017)==2 && CountId(mismatch95,727045)==1
            && mismatch95.Inventory.Objects.Where(i=>i.ID==723268||i.ID==723017||i.ID==727045).All(i=>!i.Bound && i.Plus==0),
            "727044 pays MeteorBox, two potions and 727045 at the executable level-95 gate");
        var l75=WelcomePlayer(town,75,0,15);var seventyFive=Pack(l75,727041,1);OpenStarter(l75,seventyFive);
        Check(CountId(l75,1200000)==1 && CountId(l75,721672)==1 && CountId(l75,721673)==1 && CountId(l75,727042)==1
            && l75.Inventory.Objects.Where(i=>i.ID==721672||i.ID==721673).All(i=>!i.Bound && i.Plus==0),"L75 starter pack also issues unbound plus-0 721672 and 721673");
        var threeBag=WelcomePlayer(town,70,0,15);var threeSrc=Pack(threeBag,727040,1);while(threeBag.Inventory.Count<40)Pack(threeBag,1088001,1);OpenStarter(threeBag,threeSrc);
        Check(!Fenced(threeBag) && StarterUses(threeBag)==0 && threeBag.Inventory.ContainsUID(threeSrc.UID),"exact L70 3-output starter pack refuses a full bag");
        var threeFit=WelcomePlayer(town,70,0,15);threeSrc=Pack(threeFit,727040,1);while(threeFit.Inventory.Count<38)Pack(threeFit,1088001,1);OpenStarter(threeFit,threeSrc);
        Check(!Fenced(threeFit) && StarterUses(threeFit)==1 && CountId(threeFit,727041)==1,"L70 starter pack credits the source slot and fits three outputs");
        var fourBag=WelcomePlayer(town,75,0,15);var fourSrc=Pack(fourBag,727041,1);while(fourBag.Inventory.Count<39)Pack(fourBag,1088001,1);OpenStarter(fourBag,fourSrc);
        Check(!Fenced(fourBag) && StarterUses(fourBag)==0 && fourBag.Inventory.ContainsUID(fourSrc.UID),"exact L75 4-output starter pack refuses when source credit is not enough");
        var fourFit=WelcomePlayer(town,75,0,15);fourSrc=Pack(fourFit,727041,1);while(fourFit.Inventory.Count<37)Pack(fourFit,1088001,1);OpenStarter(fourFit,fourSrc);
        Check(!Fenced(fourFit) && StarterUses(fourFit)==1 && CountId(fourFit,727042)==1,"L75 starter pack credits the source slot and fits four outputs");
        var cPotion=WelcomePlayer(town,85,0,15);var cDrink=Pack(cPotion,727043,1);OpenStarter(cPotion,cDrink);
        var cDose=cPotion.Inventory.Objects.First(i=>i.ID==723017);Call(ExpPotions,null,"Initialize");cPotion.Entity.DoubleExperienceTime=0;Drink(cPotion,cDose);
        Check(!Fenced(cPotion) && cPotion.Entity.DoubleExperienceTime==3600,"L85 starter EXP Potion remains usable through ExperiencePotions");
        var cStone=WelcomePlayer(town,75,0,15);var cPrayPack=Pack(cStone,727041,1);OpenStarter(cStone,cPrayPack);
        var cPray=cStone.Inventory.Objects.First(i=>i.ID==1200000);
        Check(!(bool)Call(Starter,null,"TryUse",cPray,cStone) && cStone.Inventory.ContainsUID(cPray.UID),"issued PrayingStone(S) remains outside StarterPacks");
        var cBall=WelcomePlayer(town,70,0,15);var cBallPack=Pack(cBall,727040,1);OpenStarter(cBall,cBallPack);
        var cExp=cBall.Inventory.Objects.First(i=>i.ID==723700);Call(ExpItems,null,"Initialize");ulong cXp=cBall.Entity.Experience;ItemHandler.UseItem(cExp,cBall);
        Check(!Fenced(cBall) && (cBall.Entity.Experience>cXp || cBall.ExpBalls>0 || !cBall.Inventory.ContainsUID(cExp.UID) || cExp.StackSize==0),"L70 EXP Ball remains usable through ExperienceConsumables");
        var cAtk=l75.Inventory.Objects.First(i=>i.ID==721672);var cDef=l75.Inventory.Objects.First(i=>i.ID==721673);
        Check(!(bool)Call(Starter,null,"TryUse",cAtk,l75) && !(bool)Call(Starter,null,"TryUse",cDef,l75)
            && l75.Inventory.ContainsUID(cAtk.UID) && l75.Inventory.ContainsUID(cDef.UID)
            && handler.IndexOf("case 721672:")>=0 && handler.IndexOf("Attack defense potion must use the owned service.")>=0
            && handler.IndexOf("AttackDefensePotions.TryUse")>=0,
            "721672/721673 are issued by StarterPacks and used through AttackDefensePotions");
        var cBox=mismatch95.Inventory.Objects.First(i=>i.ID==723268);
        Check(!(bool)Call(Starter,null,"TryUse",cBox,mismatch95) && mismatch95.Inventory.ContainsUID(cBox.UID),
            "723268 is not consumed by StarterPacks");
        Call(FixedPacks,null,"Initialize");OpenFixed(mismatch95,cBox);
        Check(!Fenced(mismatch95) && FixedUses(mismatch95)==1 && !mismatch95.Inventory.ContainsUID(cBox.UID)
            && CountId(mismatch95,1088001)==5 && mismatch95.Inventory.Objects.Where(i=>i.ID==1088001).All(i=>!i.Bound),
            "the actual starter-issued MeteorBox opens through FixedOutputPacks for five unbound Meteors");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,70,0,15);var item=Pack(p,727040,1);
            string table=fault=="receipt"?"revora_starter_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_starter_c() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_starter_c BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_starter_c()");OpenStarter(p,item);
            Check(Fenced(p) && StarterUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,727041)==0,"suppressed "+fault+" preserves L70 starter source and outputs");
            Query("DROP TRIGGER suppress_starter_c ON "+table+";DROP FUNCTION suppress_starter_c()");p=ReloadBall(p);OpenStarter(p,p.Inventory.Objects.Single(i=>i.ID==727040));
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,727041)==1,"production reload retries the L70 starter pack after "+fault+" rollback");
        }
        var cReplay=WelcomePlayer(town,70,0,15);var cOnce=Pack(cReplay,727040,1);uint cUid=cOnce.UID;OpenStarter(cReplay,cOnce);
        ItemHandler.UseItem(cOnce,cReplay);Check(StarterUses(cReplay)==1 && !cReplay.Inventory.ContainsUID(cUid),"consumed L70 starter pack UID cannot replay");
        var cFrames=WelcomePlayer(town,70,0,15);var cWire=Pack(cFrames,727040,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(cWire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(cWire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,cFrames);Check(!Fenced(cFrames) && StarterUses(cFrames)==0 && cFrames.Inventory.ContainsUID(cWire.UID),"bounded L70 starter pack rejects "+bad+" frame");
        }OpenStarter(cFrames,cWire);Check(!Fenced(cFrames) && StarterUses(cFrames)==1 && CountId(cFrames,727041)==1,"ordinary L70 starter pack works after malformed frames");
        var dKeep=WelcomePlayer(town,70,0,15);var dKept=Pack(dKeep,727040,1);OpenStarter(dKeep,dKept);long dKeptUses=StarterUses(dKeep);
        Call(Starter,null,"Initialize");
        Check(StarterUses(dKeep)==dKeptUses && Scalar("SELECT count(*) FROM revora_starter_pack_uses WHERE uid=@uid AND item_type=727040",dKeep.Entity.UID)==dKeptUses,
            "widening the starter receipt check keeps existing 727026-727045 uses");
        foreach(bool bound in new[]{false,true})foreach(ushort stack in new ushort[]{0,1,2}){
            var p=WelcomePlayer(town,100,0,15);var pack=NestedPack(p,727046,stack,bound);
            Parallel.For(0,24,n=>{if(n%3==0)EntityTable.SaveEntity(p);else OpenStarter(p,pack);});
            int expected=stack>1?2:1;
            Check(!Fenced(p) && StarterUses(p)==expected && CountId(p,751099)==expected && CountId(p,727047)==expected && CountId(p,727046)==0
                && p.Inventory.Objects.Where(i=>i.ID==751099||i.ID==727047).All(i=>i.Bound==bound && i.Plus==0),
                "L100 starter pack pays 751099 and 727047 unbound plus-0: "+bound+"/"+stack);
            p=ReloadBall(p);Check(!Fenced(p) && StarterUses(p)==expected && CountId(p,727047)==expected,"L100 starter reload retains 751099 successor");
        }
        foreach(var row in new[]{
            new object[]{727046u,(byte)100,(byte)99,751099u,1,727047u},
            new object[]{727047u,(byte)105,(byte)104,1200000u,1,727048u},
            new object[]{727048u,(byte)110,(byte)109,751099u,2,727049u}}){
            uint id=(uint)row[0];byte level=(byte)row[1];byte below=(byte)row[2];uint gift=(uint)row[3];int gifts=(int)row[4];uint next=(uint)row[5];
            var dEarly=WelcomePlayer(town,below,0,15);var pack=Pack(dEarly,id,1);OpenStarter(dEarly,pack);
            Check(!Fenced(dEarly) && StarterUses(dEarly)==0 && dEarly.Inventory.ContainsUID(pack.UID),"starter pack "+id+" refuses below level "+level);
            var p=WelcomePlayer(town,level,0,15);pack=Pack(p,id,1);uint beforeCp=p.Entity.ConquerPoints;ulong beforeSilver=p.Entity.Money;OpenStarter(p,pack);
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,gift)==gifts && CountId(p,next)==1 && p.Entity.ConquerPoints==beforeCp && p.Entity.Money==beforeSilver
                && p.Inventory.Objects.Where(i=>i.ID==gift||i.ID==next).All(i=>!i.Bound && i.Plus==0),"starter pack "+id+" pays its existing items with no currency at level "+level);
            p=ReloadBall(p);Check(CountId(p,next)==1 && Scalar("SELECT conquerpoints FROM entities WHERE uid=@uid",p.Entity.UID)==beforeCp
                && Scalar("SELECT money FROM entities WHERE uid=@uid",p.Entity.UID)==(long)beforeSilver,"starter pack "+id+" survives production reload");
        }
        var dLow=WelcomePlayer(town,114,0,15);var dLowPack=Pack(dLow,727049,1);OpenStarter(dLow,dLowPack);
        Check(!Fenced(dLow) && StarterUses(dLow)==0 && dLow.Inventory.ContainsUID(dLowPack.UID),"727049 refuses below level 115");
        var dDragon=WelcomePlayer(town,115,0,75);var dDragonPack=Pack(dDragon,727049,1);OpenStarter(dDragon,dDragonPack);
        Check(!Fenced(dDragon) && StarterUses(dDragon)==1 && CountId(dDragon,700012)==2 && CountId(dDragon,700002)==0 && CountId(dDragon,727050)==1
            && dDragon.Inventory.Objects.Where(i=>i.ID==700012||i.ID==727050).All(i=>!i.Bound && i.Plus==0),"L115 class 75 pays two 700012 gems and 727050");
        var dPhoenix=WelcomePlayer(town,115,0,76);var dPhoenixPack=Pack(dPhoenix,727049,1);OpenStarter(dPhoenix,dPhoenixPack);
        Check(!Fenced(dPhoenix) && StarterUses(dPhoenix)==1 && CountId(dPhoenix,700002)==2 && CountId(dPhoenix,700012)==0 && CountId(dPhoenix,727050)==1
            && dPhoenix.Inventory.Objects.Where(i=>i.ID==700002||i.ID==727050).All(i=>!i.Bound && i.Plus==0),"L115 class 76 pays two 700002 gems and 727050");
        var dStale=WelcomePlayer(town,115,0,15);var dStalePack=Pack(dStale,727049,1);
        Query("UPDATE entities SET class=80 WHERE uid=@uid","uid",(long)dStale.Entity.UID);OpenStarter(dStale,dStalePack);
        Check(Fenced(dStale) && StarterUses(dStale)==0 && dStale.Inventory.ContainsUID(dStalePack.UID) && CountId(dStale,700012)==0 && CountId(dStale,700002)==0,
            "stale durable class preimage cannot open the L115 starter pack");
        var dEndLow=WelcomePlayer(town,119,0,15);var dEndLowPack=Pack(dEndLow,727050,1);OpenStarter(dEndLow,dEndLowPack);
        Check(!Fenced(dEndLow) && StarterUses(dEndLow)==0 && dEndLow.Inventory.ContainsUID(dEndLowPack.UID),"727050 refuses below level 120");
        string heard="";Starter.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        var dEnd=WelcomePlayer(town,120,0,15);var dEndPack=Pack(dEnd,727050,1);OpenStarter(dEnd,dEndPack);
        Starter.GetField("Spoken",Hidden).SetValue(null,null);
        Check(!Fenced(dEnd) && StarterUses(dEnd)==1 && CountId(dEnd,193300)==1 && CountId(dEnd,751099)==3 && CountId(dEnd,721627)==1 && CountId(dEnd,721628)==1
            && CountId(dEnd,727050)==0 && CountId(dEnd,727051)==0
            && dEnd.Inventory.Objects.Where(i=>i.ID==193300||i.ID==751099||i.ID==721627||i.ID==721628).All(i=>!i.Bound && i.Plus==0)
            && heard.IndexOf("You should reach Level 120 before you can open this pack")>=0,
            "terminal L120 starter pack pays 193300, three 751099, 721627 and 721628 with no successor and keeps the Level-120 message quirk");
        dEnd=ReloadBall(dEnd);Check(CountId(dEnd,193300)==1 && CountId(dEnd,751099)==3 && CountId(dEnd,721627)==1 && CountId(dEnd,721628)==1,"terminal L120 starter pack survives production reload");
        var twoBag=WelcomePlayer(town,100,0,15);var twoSrc=Pack(twoBag,727046,1);while(twoBag.Inventory.Count<40)Pack(twoBag,1088001,1);OpenStarter(twoBag,twoSrc);
        Check(!Fenced(twoBag) && StarterUses(twoBag)==0 && twoBag.Inventory.ContainsUID(twoSrc.UID),"exact L100 2-output starter pack refuses a full bag");
        var twoFit=WelcomePlayer(town,100,0,15);twoSrc=Pack(twoFit,727046,1);while(twoFit.Inventory.Count<39)Pack(twoFit,1088001,1);OpenStarter(twoFit,twoSrc);
        Check(!Fenced(twoFit) && StarterUses(twoFit)==1 && CountId(twoFit,727047)==1,"L100 starter pack credits the source slot and fits two outputs");
        var dFourBag=WelcomePlayer(town,105,0,15);var dFourSrc=Pack(dFourBag,727047,1);while(dFourBag.Inventory.Count<39)Pack(dFourBag,1088001,1);OpenStarter(dFourBag,dFourSrc);
        Check(!Fenced(dFourBag) && StarterUses(dFourBag)==0 && dFourBag.Inventory.ContainsUID(dFourSrc.UID),"exact L105 4-output starter pack refuses when source credit is not enough");
        var dFourFit=WelcomePlayer(town,105,0,15);dFourSrc=Pack(dFourFit,727047,1);while(dFourFit.Inventory.Count<37)Pack(dFourFit,1088001,1);OpenStarter(dFourFit,dFourSrc);
        Check(!Fenced(dFourFit) && StarterUses(dFourFit)==1 && CountId(dFourFit,727048)==1,"L105 starter pack credits the source slot and fits four outputs");
        var dThreeBag=WelcomePlayer(town,110,0,15);var dThreeSrc=Pack(dThreeBag,727048,1);while(dThreeBag.Inventory.Count<40)Pack(dThreeBag,1088001,1);OpenStarter(dThreeBag,dThreeSrc);
        Check(!Fenced(dThreeBag) && StarterUses(dThreeBag)==0 && dThreeBag.Inventory.ContainsUID(dThreeSrc.UID),"exact L110 3-output starter pack refuses a full bag");
        var dThreeFit=WelcomePlayer(town,110,0,15);dThreeSrc=Pack(dThreeFit,727048,1);while(dThreeFit.Inventory.Count<38)Pack(dThreeFit,1088001,1);OpenStarter(dThreeFit,dThreeSrc);
        Check(!Fenced(dThreeFit) && StarterUses(dThreeFit)==1 && CountId(dThreeFit,727049)==1,"L110 starter pack credits the source slot and fits three outputs");
        var sixBag=WelcomePlayer(town,120,0,15);var sixSrc=Pack(sixBag,727050,1);while(sixBag.Inventory.Count<40)Pack(sixBag,1088001,1);OpenStarter(sixBag,sixSrc);
        Check(!Fenced(sixBag) && StarterUses(sixBag)==0 && sixBag.Inventory.ContainsUID(sixSrc.UID),"exact L120 6-output starter pack refuses a full bag");
        var sixFit=WelcomePlayer(town,120,0,15);sixSrc=Pack(sixFit,727050,1);while(sixFit.Inventory.Count<35)Pack(sixFit,1088001,1);OpenStarter(sixFit,sixSrc);
        Check(!Fenced(sixFit) && StarterUses(sixFit)==1 && CountId(sixFit,193300)==1,"L120 starter pack credits the source slot and fits six outputs");
        var dPotion=WelcomePlayer(town,105,0,15);var dDrink=Pack(dPotion,727047,1);OpenStarter(dPotion,dDrink);
        var dDose=dPotion.Inventory.Objects.First(i=>i.ID==723017);Call(ExpPotions,null,"Initialize");dPotion.Entity.DoubleExperienceTime=0;Drink(dPotion,dDose);
        Check(!Fenced(dPotion) && dPotion.Entity.DoubleExperienceTime==3600,"L105 starter EXP Potion remains usable through ExperiencePotions");
        var dStone=dPotion.Inventory.Objects.First(i=>i.ID==1200000);
        Check(!(bool)Call(Starter,null,"TryUse",dStone,dPotion) && dPotion.Inventory.ContainsUID(dStone.UID),"issued PrayingStone(S) remains outside StarterPacks");
        var dAtk=dEnd.Inventory.Objects.First(i=>i.ID==721627);var dDef=dEnd.Inventory.Objects.First(i=>i.ID==721628);
        Check(!(bool)Call(Starter,null,"TryUse",dAtk,dEnd) && !(bool)Call(Starter,null,"TryUse",dDef,dEnd)
            && handler.IndexOf("case 721627:")>=0 && handler.IndexOf("Attack defense potion must use the owned service.")>=0
            && handler.IndexOf("AttackDefensePotions.TryUse")>=0,
            "721627/721628 are issued by StarterPacks and used through AttackDefensePotions");
        var dBall=dEnd.Inventory.Objects.First(i=>i.ID==751099);
        Check(!(bool)Call(Starter,null,"TryUse",dBall,dEnd) && ConquerItemInformation.BaseInformations.ContainsKey(751099u)
            && File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/ExperienceConsumables.cs"))).IndexOf("751099")<0,
            "751099 remains a catalog identity outside ExperienceConsumables and StarterPacks");
        var dHat=dEnd.Inventory.Objects.First(i=>i.ID==193300);
        Check(!(bool)Call(Starter,null,"TryUse",dHat,dEnd) && ConquerItemInformation.BaseInformations.ContainsKey(193300u),"193300 remains a catalog identity and is not claimed by StarterPacks");
        Check(ConquerItemInformation.BaseInformations.ContainsKey(700012u) && ConquerItemInformation.BaseInformations.ContainsKey(700002u),"700012 and 700002 remain catalog gem identities");
        foreach(string fault in new[]{"source","output","receipt"}){
            var p=WelcomePlayer(town,100,0,15);var item=Pack(p,727046,1);
            string table=fault=="receipt"?"revora_starter_pack_uses":"items";
            string verb=fault=="source"?"DELETE":"INSERT";
            Query("CREATE FUNCTION suppress_starter_d() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_starter_d BEFORE "+verb+" ON "+table+" FOR EACH ROW EXECUTE FUNCTION suppress_starter_d()");OpenStarter(p,item);
            Check(Fenced(p) && StarterUses(p)==0 && p.Inventory.ContainsUID(item.UID) && CountId(p,727047)==0,"suppressed "+fault+" preserves L100 starter source and outputs");
            Query("DROP TRIGGER suppress_starter_d ON "+table+";DROP FUNCTION suppress_starter_d()");p=ReloadBall(p);OpenStarter(p,p.Inventory.Objects.Single(i=>i.ID==727046));
            Check(!Fenced(p) && StarterUses(p)==1 && CountId(p,727047)==1,"production reload retries the L100 starter pack after "+fault+" rollback");
        }
        var dReplay=WelcomePlayer(town,100,0,15);var dOnce=Pack(dReplay,727046,1);uint dUid=dOnce.UID;OpenStarter(dReplay,dOnce);
        ItemHandler.UseItem(dOnce,dReplay);Check(StarterUses(dReplay)==1 && !dReplay.Inventory.ContainsUID(dUid),"consumed L100 starter pack UID cannot replay");
        var dFrames=WelcomePlayer(town,100,0,15);var dWire=Pack(dFrames,727046,1);
        foreach(string bad in new[]{"short","long","length","type","seal"}){
            byte[] frame=bad=="short"?new byte[31]:bad=="long"?new byte[129]:PrayerPacket(dWire.UID);
            Writer.WriteUInt16((ushort)(frame.Length-8),0,frame);Writer.WriteUInt16(1009,2,frame);Writer.WriteUInt32(dWire.UID,8,frame);Writer.WriteUInt16(4,20,frame);Writer.WriteUInt64(PacketHandler.ClientSeal,frame.Length-8,frame);
            if(bad=="length")Writer.WriteUInt16(1,0,frame);if(bad=="type")Writer.WriteUInt16(1008,2,frame);if(bad=="seal")frame[frame.Length-1]^=1;
            ItemUsage.Process(frame,dFrames);Check(!Fenced(dFrames) && StarterUses(dFrames)==0 && dFrames.Inventory.ContainsUID(dWire.UID),"bounded L100 starter pack rejects "+bad+" frame");
        }OpenStarter(dFrames,dWire);Check(!Fenced(dFrames) && StarterUses(dFrames)==1 && CountId(dFrames,727047)==1,"ordinary L100 starter pack works after malformed frames");
    }
}
