using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Revora.Network.Cryptography;
using Revora.Network;
using Revora.Network.GamePackets;

internal static partial class CandidateAccountWireProbe
{
    private sealed class Cipher {
        private readonly byte[] first=new byte[256],second=new byte[256];
        private ushort counter;
        internal Cipher() {
            byte a=0x9d,b=0x62;
            for(int i=0;i<256;i++) {first[i]=a;second[i]=b;unchecked {a=(byte)((0x0f+(byte)(a*0xfa))*a+0x13);b=(byte)((0x79-(byte)(b*0x5c))*b+0x6d);}}
        }
        internal void Transform(byte[] bytes) {
            for(int i=0;i<bytes.Length;i++) {byte value=(byte)(bytes[i]^first[counter&255]^second[counter>>8]);bytes[i]=(byte)(((value>>4)|(value<<4))^0xab);unchecked{counter++;}}
        }
    }
    private static void Require(bool value,string stage) {if(!value){var error=new InvalidOperationException("probe-check");error.Data["probe-stage"]=stage;throw error;}}
    private static byte[] Read(NetworkStream stream,Cipher cipher,List<byte[]> memory) {
        var header=new byte[4];ReadExact(stream,header);cipher.Transform(header);
        int length=BitConverter.ToUInt16(header,0);Require(length>=4 && length<=1024,"frame-bound");
        var body=new byte[length-4];memory.Add(body);ReadExact(stream,body);cipher.Transform(body);
        var frame=new byte[length];memory.Add(frame);Buffer.BlockCopy(header,0,frame,0,4);Buffer.BlockCopy(body,0,frame,4,body.Length);return frame;
    }
    private static void ReadExact(NetworkStream stream,byte[] bytes) {
        int used=0;while(used<bytes.Length){int n=stream.Read(bytes,used,bytes.Length-used);if(n==0)throw new InvalidOperationException("early-eof");used+=n;}
    }
    private static double Distance(int x,int y,int a,int b) {return Math.Sqrt((x-a)*(x-a)+(y-b)*(y-b));}
    private static bool MoonFinish {get{return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_MOON_FINISH")=="1";}}
    private static bool characterCheck, moonDeathAllowed, moonDied;
    private static uint expectedUid, currentUid;
    private static uint currentLevel, currentCps, currentClass, currentReborn, currentAttributes, currentStrength, currentVitality;
    private static bool heroOwned,heroEquipped,returnOwned,returnEquipped;
    private static bool HomecomingExpired { get { return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_HOMECOMING_EXPIRED")=="1"; } }
    private static bool HomecomingCheck { get { return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_HOMECOMING_RETURN")=="1"; } }
    private static bool SecondRebirthCheck {get{return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SECOND_REBIRTH")=="1";}}
    private static bool SecondPromotionCheck {get{return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SECOND_PROMOTION")=="1";}}
    private static bool SecondLifeCheck {get{return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SECOND_LIFE")=="1";}}
    private static bool FirstRebirthCheck {get{return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_FIRST_REBIRTH")=="1";}}
    private static ulong currentGold,currentRvp;
    private static uint currentBoundCps;
    private static readonly System.Diagnostics.Stopwatch wireClock=System.Diagnostics.Stopwatch.StartNew();
    private static long lastFrameAt,actionStartedAt;
    private static ushort lastFrameKind;
    private static int gameFrames;
    private static readonly Dictionary<uint,ItemRefineProto> perfectionUpdates=new Dictionary<uint,ItemRefineProto>();
    private static readonly HashSet<uint> boundBag=new HashSet<uint>();
    private sealed class Target { internal uint Uid; internal ushort X,Y; }
    private static readonly Dictionary<uint,Target> bandits=new Dictionary<uint,Target>();
    private static readonly HashSet<uint> killed=new HashSet<uint>();
    private static string lastDialogText="", phase="account", huntFamily="";
    private static int soulCombat, pathCombat, soulBatch=1;
    private static readonly Dictionary<uint,uint> bag = new Dictionary<uint,uint>();
    private static string CharacterName = "RoadmapProbe";
    private static bool kingdomWire,supervisorWire;
    private static bool kingdomTitleOwned,kingdomWingOwned,kingdomTitleEquipped,kingdomWingEquipped;
    private static void KingdomTitle(TitleStorageProto title) {
        if(title.Type==1 && title.Id==1000){kingdomTitleOwned=title.ActionId!=TitleStorage.Action.RemoveTitle;kingdomTitleEquipped=kingdomTitleOwned && title.Value!=null && title.Value.Equipped;}
        if(title.Type==4001 && title.Id==1000){kingdomWingOwned=title.ActionId!=TitleStorage.Action.RemoveTitle;kingdomWingEquipped=kingdomWingOwned && title.Value!=null && title.Value.Equipped;}
    }
    internal static int KingdomMain() {
        string password=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD");Environment.SetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD",null);
        try{kingdomWire=true;supervisorWire=Environment.GetEnvironmentVariable("REVORA_SUPERVISOR_WIRE")=="1";AccountName=supervisorWire?"SupervisorProbe":"KingdomProbe";CharacterName=supervisorWire?"ArenaProbe3003":"ArenaProbe3000";expectedUid=supervisorWire?4294906004u:4294906001u;characterCheck=true;Require(!string.IsNullOrEmpty(password),"kingdom-credential");Exchange(password,false);Console.WriteLine(supervisorWire?"[supervisor-wire] passed":"[kingdom-wire] passed");return 0;}
        catch(Exception e){Console.Error.WriteLine("[kingdom-wire] failed "+e.GetType().Name+" stage="+(e.Data["probe-stage"]??phase));return 1;}
        finally{password=null;}
    }
    private static void KingdomWorld(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        foreach(uint action in new uint[]{Data.SetLocation,Data.ConfirmFriends,Data.ConfirmProficiencies,Data.ConfirmSpells,Data.ConfirmGuild,Data.GetSurroundings,Data.CompleteLogin})ActionReply(stream,crypto,memory,uid,action);
        var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        if(supervisorWire && Environment.GetEnvironmentVariable("REVORA_SPUN_WIRE")=="1"){SpunWorld(stream,crypto,memory,uid);return;}
        if(supervisorWire) {
            Require(location.dwParam==1002 && currentCps==1900,"supervisor-world-purse");
            var request=new byte[20];Writer.WriteUInt16(12,0,request);Writer.WriteUInt16(1136,2,request);Writer.WriteUInt32(1,4,request);Writer.WriteUInt32(uid,8,request);SendGame(stream,crypto,memory,request);
            bool found=false;
            for(int i=0;i<128;i++) {
                var frame=GameFrame(stream,crypto,memory);
                if(BitConverter.ToUInt16(frame,2)!=1136 || frame.Length!=76 || BitConverter.ToUInt32(frame,4)!=1)continue;
                Require(BitConverter.ToUInt32(frame,8)==uid && BitConverter.ToUInt32(frame,12)==13,"supervisor-achievement-view-shape");
                const int position=(10224/100-101)*32+10224%100-1;
                Require((BitConverter.ToUInt32(frame,16+position/32*4)&(1u<<(position%32)))!=0,"supervisor-earned-achievement-view");found=true;break;
            }
            Require(found,"supervisor-achievement-view-received");Console.WriteLine("[supervisor-wire] ready");Console.Out.Flush();Require(Console.ReadLine()=="stop","supervisor-stop");return;
        }
        Require(location.dwParam==1002 && location.wParam1==316 && location.wParam2==325,"kingdom-safe-return");
        if(Environment.GetEnvironmentVariable("REVORA_KINGDOM_WIRE_RECOVERY")=="1") {
            Console.WriteLine("[kingdom-wire] recovery-ready");Console.Out.Flush();Require(Console.ReadLine()=="stop","kingdom-recovery-stop");return;
        }
        Require(kingdomTitleOwned && kingdomWingOwned && kingdomTitleEquipped && kingdomWingEquipped,"kingdom-earned-wardrobe-login");
        foreach(int key in new[]{11000,40011000})SendGame(stream,crypto,memory,new TitleStorage().FinalizeProtoBuf(new TitleStorageProto{ActionId=TitleStorage.Action.Equip,Type=key/10000,Id=key%10000,Value=new TitleValue{Type=key/10000,Id=key%10000}}));
        ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(kingdomTitleOwned && kingdomWingOwned && kingdomTitleEquipped && kingdomWingEquipped,"kingdom-earned-wardrobe-equip-replay");
    }
    private static void SpunWorld(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        Console.WriteLine("[spun-wire] ready");Console.Out.Flush();
        for(int step=0;step<512;step++) {
            string command=Console.ReadLine();if(command=="stop")return;
            Require(command!=null && command.Length<100,"spun-command-bound");var parts=command.Split(':');
            if(parts.Length==3 && parts[0]=="npc") {
                uint npc=UInt32.Parse(parts[1]);byte option=Byte.Parse(parts[2]);Require(npc==900071727 && option==61 || (npc==4464 || npc==4465) && option==1,"spun-npc-allowlist");
                CourierDialog(stream,crypto,memory,0,npc);SendNpc(stream,crypto,memory,npc,option);
            } else if(parts.Length==2 && parts[0]=="use") {
                uint item=UInt32.Parse(parts[1]);Require(bag.ContainsKey(item) && (bag[item]==729972 || bag[item]==729973),"spun-owned-use");
                SendGame(stream,crypto,memory,new ItemUsage(true){UID=item,Action=ItemUsage.Mode.EquipItem}.ToArray());
            } else if(parts.Length==2 && parts[0]=="hit") {
                uint monster=UInt32.Parse(parts[1]);var here=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(here.dwParam==1011,"spun-combat-map");
                SendGame(stream,crypto,memory,new Attack(true){Attacker=uid,Attacked=monster,X=here.wParam1,Y=here.wParam2,AttackType=Attack.Melee}.ToArray());
            } else if(parts.Length==3 && parts[0]=="reward") {
                uint item=UInt32.Parse(parts[1]),type=UInt32.Parse(parts[2]);Require(bag.ContainsKey(item) && bag[item]==type && boundBag.Contains(item),"spun-bound-reward-visible");
            } else throw new InvalidOperationException("Unknown Spun Gold command.");
            ActionReply(stream,crypto,memory,uid,Data.SetLocation);Console.WriteLine("[spun-wire] done "+command);Console.Out.Flush();
        }
        throw new InvalidOperationException("Spun Gold command limit.");
    }
    private static string AccountName = "MoonBoxTester";
    private static byte[] GameFrame(NetworkStream stream,GameCryptography crypto,List<byte[]> memory) {
        var header=new byte[4];memory.Add(header);ReadExact(stream,header);crypto.Decrypt(header,header.Length);
        int declared=BitConverter.ToUInt16(header,0);Require(declared>=4 && declared<=32760,"game-frame-bound");
        var body=new byte[declared+4];memory.Add(body);ReadExact(stream,body);crypto.Decrypt(body,body.Length);
        var received=header.Concat(body).ToArray();memory.Add(received);
        Require(Encoding.ASCII.GetString(received,received.Length-8,8)==Revora.Constants.ServerKey,"game-frame-seal-kind-"+BitConverter.ToUInt16(received,2)+"-length-"+declared);
        ushort kind=BitConverter.ToUInt16(received,2);lastFrameKind=kind;lastFrameAt=wireClock.ElapsedMilliseconds;gameFrames++;
        try {
        if(kind==3301) {
            var title=Revora.Ext.ConvertToProto<TitleStorageProto>(received);
            if(kingdomWire)KingdomTitle(title);
            if(title.Type==2031 && title.Id==2031) {returnOwned=title.ActionId!=TitleStorage.Action.RemoveTitle;returnEquipped=returnOwned && title.Value!=null && title.Value.Equipped;}
            if(title.Type==2057 && title.Id==2057) {
                heroOwned=title.ActionId!=TitleStorage.Action.RemoveTitle;
                heroEquipped=heroOwned && title.Value!=null && title.Value.Equipped;
            }
        }
        if(kind==3250) {var value=Revora.Ext.ConvertToProto<ItemRefineProto>(received);perfectionUpdates[value.ItemUID]=value;}
        if(kind==10017 && currentUid!=0) {
            var update=Revora.Ext.ConvertToProto<FlagsUpdate.CMsgUserAttrib>(received);
            if(update.UID==currentUid)foreach(var value in update.Values) {
                if(value.Type==10)currentAttributes=checked((uint)value.value);
                if(value.Type==15)currentStrength=checked((uint)value.value);
                if(value.Type==14)currentVitality=checked((uint)value.value);
                if(value.Type==22)currentReborn=checked((uint)value.value);
                if(value.Type==7)currentClass=checked((uint)value.value);
                if(value.Type==12)currentLevel=checked((uint)value.value);
                if(value.Type==29)currentCps=checked((uint)value.value);
                if(value.Type==(uint)FlagsUpdate.DataType.BoundConquerPoints)currentBoundCps=checked((uint)value.value);
                if(value.Type==4)currentGold=value.value;
                if(value.Type==91)currentRvp=value.value;
            }
        }
        if(kind==10014) {
            var spawn=Revora.Ext.ConvertToProto<Revora.Game.Entity.CMsgPlayer>(received);
            bool bandit=spawn.Names.Any(name=>name!=null && (Encoding.ASCII.GetString(name)=="Bandit" || Encoding.ASCII.GetString(name)=="BanditL33" || (huntFamily!="" && Encoding.ASCII.GetString(name)==huntFamily)));
            if(bandit && spawn._Hitpoints>0 && spawn._UID<=UInt32.MaxValue && spawn._X<=UInt16.MaxValue && spawn._Y<=UInt16.MaxValue)
                bandits[(uint)spawn._UID]=new Target{Uid=(uint)spawn._UID,X=(ushort)spawn._X,Y=(ushort)spawn._Y};
        }
        if(kind==1022) {
            var attack=Revora.Ext.ConvertToProto<Attack.CMsgInteract>(received);
            if(attack.Type==Attack.Kill && attack.Attacked==currentUid){Require(moonDeathAllowed,"combat-character-died");moonDied=true;}
            if(attack.Type==Attack.Kill && attack.Attacker==currentUid)killed.Add(attack.Attacked);
        }
        if((pirateWire || taoistWire) && kind==1103 && received.Length==40)pirateWireSpells.Add(BitConverter.ToUInt16(received,12));
        if(kind==1008 && received.Length>=41) {
            uint item=BitConverter.ToUInt32(received,4);bag[item]=BitConverter.ToUInt32(received,8);
            if(received.Length>=80)perfectionUpdates[item]=new ItemRefineProto{ItemUID=item,PerfectionLevel=BitConverter.ToUInt32(received,60),PerfectionProgress=BitConverter.ToUInt32(received,64),CurrentUserId=BitConverter.ToUInt32(received,68)};
            if(received[32]!=0)boundBag.Add(item);else boundBag.Remove(item);
        }
        if(kind==1009 && received.Length>=32 && BitConverter.ToUInt32(received,20)==3)bag.Remove(BitConverter.ToUInt32(received,8));
        } catch(Exception error) {error.Data["probe-stage"]="decode-kind-"+kind+"-length-"+declared;throw;}
        return received;
    }
    private static void SendGame(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,byte[] packet) {
        memory.Add(packet);Writer.WriteString("TQClient",packet.Length-8,packet);crypto.Encrypt(packet,packet.Length);stream.Write(packet,0,packet.Length);
    }
    private static Data ActionReply(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid,uint action,ushort x=0,ushort y=0) {
        phase="action-"+action;actionStartedAt=wireClock.ElapsedMilliseconds;
        var request=new Data(true){UID=uid,ID=action,TimeStamp=Time32.Now};
        request.Buffer.Coords_X=x;request.Buffer.Coords_Y=y;
        SendGame(stream,crypto,memory,request.ToArray());
        for(int n=0;n<(teamWireRole>=0?8192:512);n++) {
            var frame=GameFrame(stream,crypto,memory);if(BitConverter.ToUInt16(frame,2)!=10010)continue;
            var reply=new Data(false);try{reply.Deserialize(frame);}catch(Exception error){error.Data["probe-stage"]="action-decode-length-"+frame.Length+"-legacy20-"+(frame.Length>21?BitConverter.ToUInt16(frame,20):0)+"-legacy24-"+(frame.Length>25?BitConverter.ToUInt16(frame,24):0);throw;}
            if(reply.UID==uid && reply.ID==action) {
                if(action==Data.SetLocation && Environment.GetEnvironmentVariable("REVORA_LOGIN_TIMING")=="1")
                    Console.WriteLine("[wire-timing] action="+action+" elapsed-ms="+(wireClock.ElapsedMilliseconds-actionStartedAt)+" frames="+gameFrames);
                return reply;
            }
        }
        Require(false,"action-reply-missing");return null;
    }
    private static void PrepareHunt(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        for(int count=0;currentLevel<32 && count<2;count++) {
            uint item=bag.Where(pair=>pair.Value==723700).Select(pair=>pair.Key).FirstOrDefault();
            if(item==0) {
                uint before=currentCps;Require(before>=27,"exp-ball-purse");
                var buy=new ItemUsage(true){UID=2888,dwParam=723700,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC};
                SendGame(stream,crypto,memory,buy.ToArray());
                for(int n=0;n<(teamWireRole>=0?8192:256);n++) {
                    GameFrame(stream,crypto,memory);
                    item=bag.Where(pair=>pair.Value==723700).Select(pair=>pair.Key).FirstOrDefault();
                    if(item!=0 && currentCps==before-27)break;
                }
                Require(item!=0 && currentCps==before-27,"exp-ball-purchase-update");
            }
            uint levelBefore=currentLevel;
            SendGame(stream,crypto,memory,new ItemUsage(true){UID=item,Action=ItemUsage.Mode.EquipItem}.ToArray());
            for(int n=0;n<256 && (currentLevel<=levelBefore || bag.ContainsKey(item));n++)GameFrame(stream,crypto,memory);
            Require(currentLevel>levelBefore && !bag.ContainsKey(item),"exp-ball-level-and-consumption");
            Console.WriteLine("Private ordinary EXP Ball purchase/use: level="+currentLevel+"; CPs="+currentCps+".");
        }
        Require(currentLevel>=32,"courier-combat-level");
        // A response after use also proves the session remains usable.
        ActionReply(stream,crypto,memory,uid,Data.SetLocation);
    }
    private static void SendNpc(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint npc,byte option) {
        if(option==0) {
            var request=new CMsgNpc{UID=npc,TimeStamp=(ulong)(uint)Time32.Now.GetHashCode()};
            SendGame(stream,crypto,memory,Revora.Ext.ToConquerPacket(request,2031));
        } else {
            // Production limits NPC selections to three per 500 ms.
            System.Threading.Thread.Sleep(250);
            var request=new byte[28];Writer.WriteUInt16(20,0,request);Writer.WriteUInt16(2032,2,request);
            Writer.WriteUInt32(npc,8,request);request[14]=option;request[15]=NpcReply.Option;
            SendGame(stream,crypto,memory,request);
        }
    }
    private static HashSet<byte> CourierDialog(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,byte option,uint npc=900000017) {
        phase="npc-"+npc+"-"+option;
        SendNpc(stream,crypto,memory,npc,option);
        lastDialogText="";var options=new HashSet<byte>();
        for(int n=0;n<(teamWireRole>=0?8192:256);n++) {
            var frame=GameFrame(stream,crypto,memory);if(BitConverter.ToUInt16(frame,2)!=2032)continue;
            Require(frame.Length>=28,"npc-frame-bound");
            if(frame[15]==NpcReply.Dialog) {
                Require(frame[17]<=frame.Length-26,"npc-text-bound");
                lastDialogText+=Encoding.ASCII.GetString(frame,18,frame[17]);
            }
            if(frame[15]==NpcReply.Option)options.Add(frame[14]);
            if(frame[15]==NpcReply.Finish)return options;
        }
        Require(false,"npc-dialog-unfinished");return options;
    }
    private static void EnterWorld(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        if(kingdomWire){KingdomWorld(stream,crypto,memory,uid);return;}
        if(teamWireRole>=0){TeamWireWorld(stream,crypto,memory,uid);return;}
        var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        bool interruptedTown=location.dwParam==1002 && (Environment.GetEnvironmentVariable("REVORA_CANDIDATE_PATH_HUNT")=="1" || (location.wParam1==320 && location.wParam2==297) || (location.wParam1==323 && location.wParam2==303) || (location.wParam1==314 && location.wParam2==295) || (location.wParam1==301 && location.wParam2==288));
        bool interruptedMoon=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_MOON_COMBAT")=="1" && location.dwParam>=1042 && location.dwParam<=1050;
        bool interruptedSchool=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_PROMOTION")=="1" && location.dwParam==1004;
        bool interruptedSoul=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SOUL_TRIAL")=="1" && (location.dwParam==1000 || location.dwParam==1015 || location.dwParam==1020);
        Require((location.dwParam==1002 && location.wParam1==327 && location.wParam2==303) || location.dwParam==1011 || interruptedSoul || interruptedTown || interruptedSchool || interruptedMoon,"world-spawn-or-interrupted-hunt");
        foreach(uint action in new uint[]{Data.ConfirmFriends,Data.ConfirmProficiencies,Data.ConfirmSpells,Data.ConfirmGuild,Data.GetSurroundings,Data.CompleteLogin})
            ActionReply(stream,crypto,memory,uid,action);
        if(SecondRebirthCheck && currentReborn==2){Require(heroOwned && (HomecomingExpired ? !returnOwned && !returnEquipped : HomecomingCheck ? (Environment.GetEnvironmentVariable("REVORA_CANDIDATE_COURIER_RESUME")=="1" ? returnOwned && returnEquipped : heroEquipped || returnEquipped) : heroEquipped),"career-login-equipped");ReturnToCerys(stream,crypto,memory,uid);ReturnSoulSpawn(stream,crypto,memory,uid);CheckSecondRebirth(stream,crypto,memory,uid);if(HomecomingCheck)CheckHomecomingReturn(stream,crypto,memory,uid);if(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_TOURNAMENT_ACCESS")=="1")CheckTournamentAccess(stream,crypto,memory,uid);if(CrashCheck)AwaitCandidateCrash(stream,memory);return;}
        if(FirstRebirthCheck && currentReborn==1 && !SecondLifeCheck){ReturnToCerys(stream,crypto,memory,uid);ReturnSoulSpawn(stream,crypto,memory,uid);CheckFirstRebirth(stream,crypto,memory,uid);return;}
        if(interruptedMoon) {if(MoonFinish)CompleteMoonBox(stream,crypto,memory,uid);else CheckMoonTactic(stream,crypto,memory,uid);ReturnToCerys(stream,crypto,memory,uid);ReturnSoulSpawn(stream,crypto,memory,uid);if(MoonFinish)CheckPromotion(stream,crypto,memory,uid);return;}
        if(location.dwParam==1011 || interruptedSoul || interruptedTown || interruptedSchool)ReturnToCerys(stream,crypto,memory,uid);
        PrepareHunt(stream,crypto,memory,uid);
        if(SecondLifeCheck && currentLevel<120)AllocateCareerPoints(stream,crypto,memory);
        ActionReply(stream,crypto,memory,uid,Data.Jump,323,303);
        location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1002 && location.wParam1==323 && location.wParam2==303,"courier-nearby-movement");
        var options=CourierDialog(stream,crypto,memory,0);
        bool huntRestored=!options.Contains(1) && (options.Contains(4) || options.Contains(5) || options.Contains(2) || lastDialogText.Contains("You closed the loop in this life"));
        Require(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_COURIER_RESUME")!="1" || huntRestored,"courier-hunt-not-restored");
        if(options.Contains(1))options=CourierDialog(stream,crypto,memory,1);
        Require(options.Contains(3) || options.Contains(4) || options.Contains(5) || options.Contains(2)
            || lastDialogText.Contains("You closed the loop in this life"),"courier-known-stage");
        if(options.Contains(3)) {
            SendNpc(stream,crypto,memory,900000017,3);
            location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            Require(location.dwParam==1002 && location.wParam1==442 && location.wParam2==339,"commander-quest-travel");
            ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
            Require(CourierDialog(stream,crypto,memory,0,91001).Contains(10),"commander-report-option");
            Require(CourierDialog(stream,crypto,memory,10,91001).Contains(255),"commander-report-dialog");
            ReturnToCerys(stream,crypto,memory,uid);
            options=CourierDialog(stream,crypto,memory,0);
        }
        for(int hunt=0;hunt<3 && options.Contains(4);hunt++) {
        Require(CourierDialog(stream,crypto,memory,1).Contains(4),"courier-accept-keeps-hunt");
        var countMatch=System.Text.RegularExpressions.Regex.Match(lastDialogText,@"(\d+)/20");
        Require(countMatch.Success,"courier-hunt-counter");int beforeKills=Int32.Parse(countMatch.Groups[1].Value);
        bandits.Clear();killed.Clear();
        SendNpc(stream,crypto,memory,900000017,4);
        location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1011 && location.wParam1==112 && location.wParam2==272,"courier-hunt-travel");
        ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        int batch=SecondLifeCheck?20-beforeKills:1;
        for(int kill=0;kill<batch;kill++)HuntOne(stream,crypto,memory,uid);
        ReturnToCerys(stream,crypto,memory,uid);
        options=CourierDialog(stream,crypto,memory,0);
        Require(options.Contains(beforeKills+batch==20?(byte)5:(byte)4),"courier-hunt-return");
        countMatch=System.Text.RegularExpressions.Regex.Match(lastDialogText,@"(\d+)/20");
        Require(beforeKills+batch==20 ? options.Contains(5) : countMatch.Success && Int32.Parse(countMatch.Groups[1].Value)==beforeKills+batch,"courier-earned-kill-credit");
        Console.WriteLine("Private Courier live kill credit: "+beforeKills+" -> "+(beforeKills+batch)+"/20.");
        }
        if(options.Contains(5)) {
            SendNpc(stream,crypto,memory,900000017,5);
            location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            Require(location.dwParam==1002 && location.wParam1==418 && location.wParam2==330,"courier-clerk-travel");
            Require(CourierDialog(stream,crypto,memory,0,900000015).Contains(10),"courier-clerk-option");
            Require(CourierDialog(stream,crypto,memory,10,900000015).Contains(255),"courier-clerk-approval");
            ReturnToCerys(stream,crypto,memory,uid);options=CourierDialog(stream,crypto,memory,0);
            Require(options.Contains(2),"courier-turnin-ready");
        }
        if(options.Contains(2)) {
            uint cps=currentCps;ulong gold=currentGold,rvp=currentRvp;var oldItems=new HashSet<uint>(bag.Keys);
            CourierDialog(stream,crypto,memory,2);
            Require(lastDialogText.Contains("The loop is closed"),"courier-reward-dialog");
            Require(currentCps==cps+8000 && currentGold==gold+80000 && currentRvp==rvp+200 && currentLevel==120,"courier-reward-wallet-level");
            var souls=bag.Where(pair=>!oldItems.Contains(pair.Key) && pair.Value>=800000 && pair.Value<900000).ToArray();
            Require(souls.Length==1 && boundBag.Contains(souls[0].Key),"courier-bound-soul-update");
            Console.WriteLine("Private Courier earned turn-in: 8000 CPs, 80000 silver, 200 RVPs, level120 and one bound soul; uid="+souls[0].Key+" type="+souls[0].Value+".");
            options=CourierDialog(stream,crypto,memory,0);
        }
        bool paidVerified=false;
        if(lastDialogText.Contains("You closed the loop in this life")) {
            uint cps=currentCps;ulong gold=currentGold,rvp=currentRvp;int items=bag.Count;
            CourierDialog(stream,crypto,memory,2);
            Require(lastDialogText.Contains("You closed the loop in this life") && currentCps==cps && currentGold==gold && currentRvp==rvp && bag.Count==items,"courier-paid-replay");
            Console.WriteLine("Private Courier completed state restored and duplicate turn-in refused.");
            paidVerified=true;
        }
        System.Threading.Thread.Sleep(700);
        ActionReply(stream,crypto,memory,uid,Data.Jump,327,303);
        location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1002 && location.wParam1==327 && location.wParam2==303,"world-return-spawn");
        Console.WriteLine("Private Courier journey check passed; restored-progress="+huntRestored+". Native rendering and interruption recovery remain pending.");
        if(SecondLifeCheck){Require(paidVerified && currentReborn==1 && currentClass>=11 && currentClass<=15 && currentLevel==120,"second-life-earned-level");Console.WriteLine("Private Courier second-life earning complete; first-life quest receipts are preserved separately.");if(SecondPromotionCheck)CheckPromotion(stream,crypto,memory,uid);if(SecondRebirthCheck)CheckSecondRebirth(stream,crypto,memory,uid);return;}
        if(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SOUL_TRIAL")=="1")CheckHunt(stream,crypto,memory,uid,false);
        if(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_PATH_HUNT")=="1")CheckHunt(stream,crypto,memory,uid,true);
        if(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_PROMOTION")=="1")CheckPromotion(stream,crypto,memory,uid);
        if(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_MOON_ACCESS")=="1")CheckMoonAccess(stream,crypto,memory,uid);
        if(MoonFinish && currentClass==14 && bag.Values.Any(id=>id==721090 || (SecondPromotionCheck && id==721020)))CheckPromotion(stream,crypto,memory,uid);
        if(FirstRebirthCheck)CheckFirstRebirth(stream,crypto,memory,uid);
        if(CrashCheck) {
            Require(paidVerified,"crash-requires-earned-reward");
            AwaitCandidateCrash(stream,memory);
        }
    }
    private static void AwaitCandidateCrash(NetworkStream stream,List<byte[]> memory) {
        Console.WriteLine(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_TOURNAMENT_ACCESS")=="1" ? "Private Team PK live crash checkpoint ready." : "Private Courier live crash checkpoint ready.");Console.Out.Flush();
        stream.ReadTimeout=10000;
        var drain=new byte[4096];memory.Add(drain);
        var deadline=DateTime.UtcNow.AddSeconds(10);bool closed=false;
        try { while(DateTime.UtcNow<deadline) { if(stream.Read(drain,0,drain.Length)==0){closed=true;break;} } }
        catch(IOException error) {var socket=error.InnerException as SocketException;closed=socket!=null && (socket.SocketErrorCode==SocketError.ConnectionReset || socket.SocketErrorCode==SocketError.ConnectionAborted);}
        Require(closed,"crash-live-connection-not-closed");
        Console.WriteLine("Private ordinary live connection interrupted by candidate termination.");
    }
    private static void CheckHomecomingReturn(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        Require(HomecomingExpired ? !returnOwned && !returnEquipped : returnOwned,"homecoming-login-eligibility");
        SendGame(stream,crypto,memory,new TitleStorage().FinalizeProtoBuf(new TitleStorageProto{ActionId=TitleStorage.Action.Equip,Type=2031,Id=2031,Value=new TitleValue{Type=2031,Id=2031}}));
        ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        if(HomecomingExpired){Require(!returnOwned && !returnEquipped && heroOwned && heroEquipped,"homecoming-expired-equip-refused");Console.WriteLine("Private expired Homecoming title absent on login and re-equip refused; permanent career title preserved.");return;}
        Require(returnOwned && returnEquipped,"homecoming-ordinary-equip");
        Console.WriteLine("Private Homecoming title ordinarily equipped and retained for restart.");
    }
    private static void RequestHonoredHero(NetworkStream stream,GameCryptography crypto,List<byte[]> memory) {
        SendGame(stream,crypto,memory,new TitleStorage().FinalizeProtoBuf(new TitleStorageProto{ActionId=TitleStorage.Action.Equip,Type=2057,Id=2057,Value=new TitleValue{Type=2057,Id=2057}}));
    }
    private static void CheckSecondRebirth(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        Require(currentReborn==1?currentClass==15 && currentLevel==120:currentReborn==2 && currentClass==11 && currentLevel==15,"second-rebirth-career-profile");
        if(currentReborn==1) {
            Require(!heroOwned && !heroEquipped,"hero-not-earned-early");RequestHonoredHero(stream,crypto,memory);ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            Require(!heroOwned && !heroEquipped,"hero-premature-equip-refused");
            if(!bag.Values.Contains(723701u))BuyCareerMaterial(stream,crypto,memory,723701,888);
        }
        ushort[,] road={{340,316},{353,329},{366,342},{379,355},{392,364}};
        for(int n=0;n<road.GetLength(0);n++){System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,road[n,0],road[n,1]);}
        Require(CourierDialog(stream,crypto,memory,0,59558).Contains(2),"second-rebirth-sereth-menu");
        if(currentReborn==1) {
            uint token=bag.Single(pair=>pair.Value==723701).Key,cps=currentCps;ulong gold=currentGold,rvp=currentRvp;var before=new Dictionary<uint,uint>(bag);
            Require(CourierDialog(stream,crypto,memory,2,59558).Contains(170),"second-rebirth-class-offer");
            Require(CourierDialog(stream,crypto,memory,170,59558).Contains(180),"second-rebirth-confirmation");
            CourierDialog(stream,crypto,memory,180,59558);
            Require(lastDialogText.Contains("Your former life is remembered") && currentReborn==2 && currentClass==11 && currentLevel==15 && !bag.ContainsKey(token),"second-rebirth-immediate-state");
            Require(currentCps==cps && currentGold==gold && currentRvp==rvp,"second-rebirth-no-extra-charge");
            var added=bag.Where(pair=>!before.ContainsKey(pair.Key)).ToArray();
            Require(added.Length==2 && added.Count(pair=>pair.Value==410077 && !boundBag.Contains(pair.Key))==1 && added.Count(pair=>pair.Value>=800000 && pair.Value<900000 && boundBag.Contains(pair.Key))==1,"second-rebirth-rewards");
            Require(before.Keys.Where(key=>key!=token).All(key=>bag.ContainsKey(key)),"second-rebirth-existing-items-retained");
            Console.WriteLine("Private rebirth second rite earned: token consumed; class11 level15 rebirth2; weapon and bound soul received.");
        }
        var claimed=new Dictionary<uint,uint>(bag);uint savedCps=currentCps;ulong savedGold=currentGold,savedRvp=currentRvp;
        CourierDialog(stream,crypto,memory,180,59558);
        Require(lastDialogText.Contains("highest rank in your second life") && currentReborn==2 && currentClass==11 && currentLevel==15 && currentCps==savedCps && currentGold==savedGold && currentRvp==savedRvp
            && bag.Count==claimed.Count && claimed.All(pair=>bag.ContainsKey(pair.Key) && bag[pair.Key]==pair.Value),"second-rebirth-repeat-refused");
        for(int attempt=0;attempt<5 && !heroEquipped;attempt++) {
            System.Threading.Thread.Sleep(1000);RequestHonoredHero(stream,crypto,memory);ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        }
        Require(heroOwned && heroEquipped,"hero-earned-equip");
        ReturnToCerys(stream,crypto,memory,uid);ReturnSoulSpawn(stream,crypto,memory,uid);
        Console.WriteLine("Private ordinary Honored Hero earned and equipped through wardrobe packets; rebirth replay refused.");
    }
    private static void AllocateCareerPoints(NetworkStream stream,GameCryptography crypto,List<byte[]> memory) {
        Require(currentReborn==1,"career-attributes-rebirth");if(currentAttributes==0)return;
        uint strength=currentAttributes*3/4,vitality=currentAttributes-strength,oldStrength=currentStrength,oldVitality=currentVitality;
        var packet=new byte[36];Writer.WriteUInt16(28,0,packet);Writer.WriteUInt16(1024,2,packet);
        Writer.WriteUInt32(strength,12,packet);Writer.WriteUInt32(vitality,20,packet);SendGame(stream,crypto,memory,packet);
        bool echoed=false;for(int n=0;n<256;n++){var reply=GameFrame(stream,crypto,memory);if(BitConverter.ToUInt16(reply,2)==1024){echoed=true;break;}}
        Require(echoed && currentAttributes==0 && currentStrength==oldStrength+strength && currentVitality==oldVitality+vitality,"career-attributes-exact-allocation");
        Console.WriteLine("Private ordinary second-life attributes allocated through the player packet.");
    }
    private static void CheckFirstRebirth(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        if(currentReborn==0 && !bag.Values.Contains(721259u))BuyCareerMaterial(stream,crypto,memory,721259,30);
        Require(currentReborn==0?currentClass==15 && currentLevel==120:currentReborn==1 && currentClass==11 && currentLevel==15,"rebirth-career-profile");
        byte[] floor=File.ReadAllBytes("C:/Revora/DisposablePostgres/moon-box-candidate/game/database/maps/1002.map");
        using(var sha=SHA256.Create())Require(BitConverter.ToString(sha.ComputeHash(floor)).Replace("-","")=="9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10","rebirth-terrain-identity");
        ushort[,] road={{340,316},{353,329},{366,342},{379,355},{392,364}};
        for(int n=0;n<road.GetLength(0);n++){System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,road[n,0],road[n,1]);}
        Require(CourierDialog(stream,crypto,memory,0,59558).Contains(1),"rebirth-sereth-menu");
        if(currentReborn==0) {
            uint stone=bag.Single(pair=>pair.Value==721259).Key,cps=currentCps;ulong gold=currentGold,rvp=currentRvp;var oldItems=new Dictionary<uint,uint>(bag);
            Require(CourierDialog(stream,crypto,memory,1,59558).Contains(200),"rebirth-class-offer");
            Require(CourierDialog(stream,crypto,memory,200,59558).Contains(210),"rebirth-body-offer");
            Require(CourierDialog(stream,crypto,memory,210,59558).Contains(212),"rebirth-gem-offer");
            Require(CourierDialog(stream,crypto,memory,212,59558).Contains(214),"rebirth-final-confirmation");
            CourierDialog(stream,crypto,memory,214,59558);
            Require(lastDialogText.Contains("Your former life is remembered") && currentReborn==1 && currentClass==11 && currentLevel==15 && !bag.ContainsKey(stone),"rebirth-immediate-state");
            Require(currentCps==cps && currentGold==gold && currentRvp==rvp,"rebirth-no-extra-charge");
            var added=bag.Where(pair=>!oldItems.ContainsKey(pair.Key)).ToArray();
            Require(added.Length==3 && added.All(pair=>!boundBag.Contains(pair.Key)) && added.Count(pair=>pair.Value==187405)==1 && added.Count(pair=>pair.Value==410077)==1 && added.Count(pair=>pair.Value>=700003 && pair.Value<=700063 && pair.Value%10==3)==1,"rebirth-three-unbound-rewards");
            Require(oldItems.Keys.Where(key=>key!=stone).All(key=>bag.ContainsKey(key)),"rebirth-existing-items-retained");
            Console.WriteLine("Private rebirth earned through Sereth: stone consumed; class11 level15; super gem, weapon and unbound garment received.");
        }
        var before=new Dictionary<uint,uint>(bag);uint savedCps=currentCps;ulong savedGold=currentGold,savedRvp=currentRvp;
        CourierDialog(stream,crypto,memory,214,59558);
        Require(lastDialogText.Contains("This rite is for your first rebirth") && currentReborn==1 && currentClass==11 && currentLevel==15 && currentCps==savedCps && currentGold==savedGold && currentRvp==savedRvp && bag.Count==before.Count && before.All(pair=>bag.ContainsKey(pair.Key) && bag[pair.Key]==pair.Value),"rebirth-repeat-refused");
        ReturnToCerys(stream,crypto,memory,uid);ReturnSoulSpawn(stream,crypto,memory,uid);
        Console.WriteLine("Private rebirth profile restored/verified; repeated first-rebirth confirmation refused.");
    }
    private static void BuyCareerMaterial(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint type,uint price) {
        uint before=currentCps;var oldItems=new HashSet<uint>(bag.Keys);
        Require(before>=price,"career-material-purse");
        SendGame(stream,crypto,memory,new ItemUsage(true){UID=2888,dwParam=type,dwExtraInfo=1,Action=ItemUsage.Mode.BuyFromNPC}.ToArray());
        for(int n=0;n<256 && (currentCps==before || !bag.Any(pair=>!oldItems.Contains(pair.Key) && pair.Value==type));n++)GameFrame(stream,crypto,memory);
        Require(currentCps==before-price && bag.Count(pair=>!oldItems.Contains(pair.Key) && pair.Value==type)==1,"career-material-exact-purchase");
        Console.WriteLine("Private ordinary career material bought: type="+type+" CPs="+price+".");
    }
    private static void CheckMoonAccess(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        if(MoonFinish && currentClass==15){Require(!bag.Values.Any(id=>id>=721010 && id<=721015 || id==721072 || id==721090),"moon-finished-materials-absent");Console.WriteLine("Private Moon Box completed career restored; no new run started.");return;}
        phase="moon-access";
        byte[] floor=File.ReadAllBytes("C:/Revora/DisposablePostgres/moon-box-candidate/game/database/maps/1002.map");
        using(var sha=SHA256.Create())Require(BitConverter.ToString(sha.ComputeHash(floor)).Replace("-","")=="9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10","moon-terrain-identity");
        ushort[,] road={{327,322},{327,341},{327,360},{327,379},{327,398},{327,417},{327,436},{327,455},{337,470},{350,483},{363,496},{376,509},{389,522},{402,535},{415,548},{428,561},{441,574},{454,587},{467,600},{480,613},{485,618}};
        for(int n=0;n<road.GetLength(0);n++){System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,road[n,0],road[n,1]);}
        Require(CourierDialog(stream,crypto,memory,0,600050).Contains(1),"moon-fortune-menu");
        uint cps=currentCps;ulong gold=currentGold,rvp=currentRvp;var before=new Dictionary<uint,uint>(bag);
        CourierDialog(stream,crypto,memory,1,600050);
        Require(lastDialogText.Contains("Maggie and the wandering souls"),"moon-entry-dialog");
        var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1042 && location.wParam1==28 && location.wParam2==33,"moon-portal-entry");
        ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,22,22);
        Require(CourierDialog(stream,crypto,memory,0,600003).Contains(1) && lastDialogText.Contains("six different tokens"),"moon-maggie-access");
        Require(currentCps==cps && currentGold==gold && currentRvp==rvp && before.Count==bag.Count && before.All(pair=>bag.ContainsKey(pair.Key) && bag[pair.Key]==pair.Value),"moon-entry-no-payment-or-reward");
        if(MoonFinish)CompleteMoonBox(stream,crypto,memory,uid);
        else if(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_MOON_COMBAT")=="1")CheckMoonTactic(stream,crypto,memory,uid);
        ReturnToCerys(stream,crypto,memory,uid);ReturnSoulSpawn(stream,crypto,memory,uid);
        Console.WriteLine("Private Moon Box ordinary entry/resume and Maggie access passed; entry itself issued no payment or reward.");
    }
    private static readonly ushort[,] lifeGhosts={{184,165},{160,161},{183,187},{132,124},{100,109},{81,103},{134,96},{94,73},{79,130},{121,178},{93,41},{156,119},{184,134},{82,82},{54,96},{175,123},{152,111},{125,100},{122,139},{139,156},{138,123},{50,40},{59,28},{87,48},{114,67}};
    private static bool LifeFloor(int x,int y) {
        for(int i=0;i<lifeGhosts.GetLength(0);i++)if(Math.Abs(x-lifeGhosts[i,0])<=1 && Math.Abs(y-lifeGhosts[i,1])<=1)return false;
        return true;
    }
    private static void CompleteMoonBox(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        if(location.dwParam==1042) {
            Require(MoonTokens()==6,"moon-finish-earned-tokens");MoonWalk(stream,crypto,memory,uid,1042,22,22);
            Require(CourierDialog(stream,crypto,memory,0,600003).Contains(1),"moon-death-route-menu");CourierDialog(stream,crypto,memory,1,600003);
            location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(location.dwParam==1049,"moon-full-set-death-route");
        }
        if(location.dwParam==1049) {
            var tokens=bag.Where(pair=>pair.Value>=721010 && pair.Value<=721015).ToArray();Require(tokens.Length==6,"moon-exchange-six-tokens");
            MoonWalk(stream,crypto,memory,uid,1049,9,63);Require(CourierDialog(stream,crypto,memory,0,600017).Contains(1),"moon-jade-menu");
            CourierDialog(stream,crypto,memory,1,600017);
            Require(tokens.All(pair=>!bag.ContainsKey(pair.Key)) && bag.Count(pair=>pair.Value==721072 && boundBag.Contains(pair.Key))==1,"moon-jade-consumption");
            location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(location.dwParam==1050,"moon-life-entry");
            Console.WriteLine("Private Moon Box six earned tokens exchanged for one bound Soul Jade.");
        }
        Require(location.dwParam==1050,"moon-finish-location");uint jade=bag.Single(pair=>pair.Value==721072).Key;
        uint cps=currentCps;ulong gold=currentGold,rvp=currentRvp;var oldItems=new Dictionary<uint,uint>(bag);int refused=0;
        var remaining=new HashSet<int>(Enumerable.Range(0,25));
        while(remaining.Count>0) {
            location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            int index=remaining.OrderBy(i=>Distance(location.wParam1,location.wParam2,lifeGhosts[i,0],lifeGhosts[i,1])).First();remaining.Remove(index);
            int gx=lifeGhosts[index,0],gy=lifeGhosts[index,1];byte[] floor=MoonFloor(1050);
            var approach=(from dx in Enumerable.Range(-4,9) from dy in Enumerable.Range(-4,9)
                let x=gx+dx let y=gy+dy where x>0 && y>0 && x<224 && y<224 && Distance(x,y,gx,gy)<=6 && LifeFloor(x,y) && floor[8+y*224+x]!=1
                orderby Distance(location.wParam1,location.wParam2,x,y) select new{X=x,Y=y}).FirstOrDefault();
            Require(approach!=null,"moon-life-approach");MoonWalk(stream,crypto,memory,uid,1050,approach.X,approach.Y);
            uint ghost=600018u+(uint)index;Require(CourierDialog(stream,crypto,memory,0,ghost).Contains(1),"moon-life-offer");CourierDialog(stream,crypto,memory,1,ghost);
            Require(currentCps==cps && currentGold==gold && currentRvp==rvp,"moon-finish-wallet");
            if(lastDialogText.Contains("Another spirit awaits")) {
                Require(bag.Count==oldItems.Count && oldItems.All(pair=>bag.ContainsKey(pair.Key) && bag[pair.Key]==pair.Value),"moon-wrong-ghost-keeps-jade");refused++;continue;
            }
            Require(lastDialogText.Contains("My spirit is free") && !bag.ContainsKey(jade),"moon-finish-jade-consumed");
            var added=bag.Where(pair=>!oldItems.ContainsKey(pair.Key)).ToArray();
            Require(added.Length==2 && added.Count(pair=>pair.Value==721090 && !boundBag.Contains(pair.Key))==1 && added.Count(pair=>pair.Value>=800000 && pair.Value<900000 && boundBag.Contains(pair.Key))==1,"moon-exact-box-and-bound-soul");
            Require(bag.Count==oldItems.Count+1 && oldItems.Where(pair=>pair.Key!=jade).All(pair=>bag.ContainsKey(pair.Key) && bag[pair.Key]==pair.Value),"moon-existing-items-retained");
            location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(location.dwParam==1002,"moon-finish-town");
            var claimed=new Dictionary<uint,uint>(bag);SendNpc(stream,crypto,memory,ghost,1);ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            Require(bag.Count==claimed.Count && claimed.All(pair=>bag.ContainsKey(pair.Key) && bag[pair.Key]==pair.Value) && currentCps==cps && currentGold==gold && currentRvp==rvp,"moon-out-of-range-replay");
            Console.WriteLine("Private Moon Box earned: one unbound box and one bound soul; incorrect ghosts refused="+refused+"; stale claim made no change.");return;
        }
        Require(false,"moon-correct-spirit-not-found");
    }
    private static void CheckTournamentAccess(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        var cp=currentCps;var rvp=currentRvp;var gold=currentGold;var items=bag.OrderBy(p=>p.Key).ToArray();
        MoonWalk(stream,crypto,memory,uid,1002,275,140);
        var menu=CourierDialog(stream,crypto,memory,0,900183);
        Require(lastDialogText.Contains("We muster") && !menu.Contains(1),"tournament-closed-calendar-dialog");
        SendNpc(stream,crypto,memory,900183,1);
        var loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(loc.dwParam==1002 && Distance(loc.wParam1,loc.wParam2,273,140)<=6,"tournament-closed-entry-refused");
        Require(currentCps==cp && currentRvp==rvp && currentGold==gold && items.SequenceEqual(bag.OrderBy(p=>p.Key)),"tournament-closed-entry-no-mutation");
        MoonWalk(stream,crypto,memory,uid,1002,293,146);
        Console.WriteLine("Private ordinary Team Registrar approach completed.");
        menu=CourierDialog(stream,crypto,memory,0,417171);
        Require(lastDialogText.Contains("top eight leaders") && menu.Contains(3) && !menu.Contains(2),"local-team-pk-public-closed-menu");
        CourierDialog(stream,crypto,memory,2,417171);
        Require(lastDialogText.Contains("Registration is closed"),"local-team-pk-closed-entry-refused");
        CourierDialog(stream,crypto,memory,3,417171);
        Require(lastDialogText.Contains("No unclaimed tournament purse"),"local-team-pk-unearned-purse-refused");
        Console.WriteLine("Private ordinary Team Registrar menu, closed entry and unearned claim replies verified.");
        loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(loc.dwParam==1002 && Distance(loc.wParam1,loc.wParam2,291,146)<=6,"local-team-pk-closed-entry-position");
        Require(currentCps==cp && currentRvp==rvp && currentGold==gold && items.SequenceEqual(bag.OrderBy(p=>p.Key)),"local-team-pk-closed-entry-no-mutation");
        if(CrashCheck&&!SkillPkCheck) {
            Console.WriteLine("Private Team PK opening checkpoint ready.");Console.Out.Flush();
            bool opened=false;
            for(int attempt=0;attempt<10;attempt++) {
                System.Threading.Thread.Sleep(1000);menu=CourierDialog(stream,crypto,memory,0,417171);
                if(menu.Contains(2)){opened=true;break;}
            }
            Require(opened,"local-team-pk-private-opening");
            SendNpc(stream,crypto,memory,417171,2);
            loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            Require(loc.dwParam!=1002,"local-team-pk-live-admission");
            Require(currentCps==cp && currentRvp==rvp && currentGold==gold && items.SequenceEqual(bag.OrderBy(p=>p.Key)),"local-team-pk-admission-no-charge");
            Console.WriteLine("Private ordinary Team PK NPC admission reached its waiting map without currency or item changes.");return;
        }
        MoonWalk(stream,crypto,memory,uid,1002,302,146);
        menu=CourierDialog(stream,crypto,memory,0,801717);
        Require(lastDialogText.Contains("19:45")&&lastDialogText.Contains("20:00")&&menu.Contains(1)&&!menu.Contains(2)&&menu.Contains(3)&&menu.Contains(4),"skill-team-pk-public-menu");
        CourierDialog(stream,crypto,memory,1,801717);
        Require(lastDialogText.Contains("same level group")&&lastDialogText.Contains("ten minutes")&&lastDialogText.Contains("fifteen"),"skill-team-pk-approved-rules");
        CourierDialog(stream,crypto,memory,4,801717);
        Require(lastDialogText.Contains("10,000")&&lastDialogText.Contains("bound P7")&&lastDialogText.Contains("SkillTeamPKPack")&&!lastDialogText.Contains("Honor"),"skill-team-pk-actual-prize-description");
        CourierDialog(stream,crypto,memory,2,801717);
        Require(lastDialogText.Contains("Registration is closed"),"skill-team-pk-closed-entry-refused");
        CourierDialog(stream,crypto,memory,3,801717);
        Require(lastDialogText.Contains("No unclaimed tournament purse"),"skill-team-pk-unearned-purse-refused");
        loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(loc.dwParam==1002&&Distance(loc.wParam1,loc.wParam2,300,146)<=6,"skill-team-pk-closed-entry-position");
        Require(currentCps==cp&&currentRvp==rvp&&currentGold==gold&&items.SequenceEqual(bag.OrderBy(p=>p.Key)),"skill-team-pk-closed-entry-and-unearned-claim-no-mutation");
        Console.WriteLine("Private ordinary Skill Registrar TCP dialogue, rules, prizes, closed admission and unearned claim verified without currency or item changes.");
        // Establish the team through the authenticated player protocol before any
        // operator opening; @steam must not supply this prerequisite for the probe.
        var createTeam=new byte[32];Writer.WriteUInt16(24,0,createTeam);Writer.WriteUInt16(1023,2,createTeam);Writer.WriteUInt32(0,4,createTeam);Writer.WriteUInt32(uid,8,createTeam);
        SendGame(stream,crypto,memory,createTeam);
        bool teamFlags=false,teamCreated=false;
        for(int n=0;n<512 && !teamCreated;n++) {
            var reply=GameFrame(stream,crypto,memory);
            if(BitConverter.ToUInt16(reply,2)!=1023)continue;
            Require(reply.Length>=16 && BitConverter.ToUInt32(reply,8)==uid,"skill-team-create-owner");
            uint action=BitConverter.ToUInt32(reply,4);
            if(action==15)teamFlags=true;
            if(action==0){Require(teamFlags,"skill-team-create-order");teamCreated=true;}
        }
        Require(teamCreated,"skill-team-created-by-player");
        Require(currentCps==cp&&currentRvp==rvp&&currentGold==gold&&items.SequenceEqual(bag.OrderBy(p=>p.Key)),"skill-team-create-no-charge");
        Console.WriteLine("Private ordinary TCP team creation acknowledged before operator opening; no currency or item changes.");
        if(CrashCheck&&SkillPkCheck) {
            Console.WriteLine("Private Team PK opening checkpoint ready.");Console.Out.Flush();
            bool opened=false;
            for(int attempt=0;attempt<10;attempt++) {
                System.Threading.Thread.Sleep(1000);menu=CourierDialog(stream,crypto,memory,0,801717);
                if(menu.Contains(2)){opened=true;break;}
            }
            Require(opened,"skill-team-pk-private-opening");
            SendNpc(stream,crypto,memory,801717,2);
            loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            Require(loc.dwParam!=1002,"skill-team-pk-live-admission");
            Require(currentCps==cp&&currentRvp==rvp&&currentGold==gold&&items.SequenceEqual(bag.OrderBy(p=>p.Key)),"skill-team-pk-admission-no-charge");
            Console.WriteLine("Private ordinary Skill PK NPC admission reached its waiting map without currency or item changes.");return;
        }
        MoonWalk(stream,crypto,memory,uid,1002,327,280);
        MoonWalk(stream,crypto,memory,uid,1002,327,303);
        Console.WriteLine("Private ordinary TCP TeamMarshal and TeamRegistrar dialogs reached; closed registration and unearned purse refused, inventory/currencies unchanged, return position restored.");
    }
    private static byte[] MoonFloor(uint map) {
        Require(map==1002 || map>=1042 && map<=1050,"moon-map-range");
        byte[] floor=File.ReadAllBytes("C:/Revora/DisposablePostgres/moon-box-candidate/game/database/maps/"+map+".map");
        string hash=map==1002?"9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10":map==1042?"B42E2DD9779D37AB3E1730F0A003ED69FBBF8E6DC3E0F24FD519529249635DAD":"214CC433060E138C5FAF67CB329CFA6546275DB09DDAA60997204094AD138803";
        using(var sha=SHA256.Create())Require(BitConverter.ToString(sha.ComputeHash(floor)).Replace("-","")==hash,"moon-map-identity");
        return floor;
    }
    private static void MoonWalk(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid,uint map,int tx,int ty) {
        byte[] floor=MoonFloor(map);int width=BitConverter.ToInt32(floor,0),height=BitConverter.ToInt32(floor,4);
        Func<int,int,bool> walk=(x,y)=>x>0 && y>0 && x<width && y<height && floor[8+y*width+x]!=1
            && !(map==1002 && Math.Abs(x-273)<=1 && Math.Abs(y-140)<=1)
            && !(map==1002 && Math.Abs(x-291)<=1 && Math.Abs(y-146)<=1)
            && !(map==1002 && Math.Abs(x-300)<=1 && Math.Abs(y-146)<=1)
            && !(map==1042 && Math.Abs(x-19)<=1 && Math.Abs(y-19)<=1)
            && !(map>=1043 && map<=1049 && Math.Abs(x-6)<=1 && Math.Abs(y-66)<=1) && (map!=1050 || LifeFloor(x,y));
        var loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(loc.dwParam==map,"moon-route-map");
        if(!walk(tx,ty)) {
            var landing=(from dx in Enumerable.Range(-4,9) from dy in Enumerable.Range(-4,9)
                where walk(tx+dx,ty+dy) orderby Math.Abs(dx)+Math.Abs(dy) select new{X=tx+dx,Y=ty+dy}).FirstOrDefault();
            Require(landing!=null,"moon-route-destination");tx=landing.X;ty=landing.Y;
        }
        int start=loc.wParam2*width+loc.wParam1,goal=ty*width+tx;
        var queue=new Queue<int>();var parents=new Dictionary<int,int>();parents[start]=-1;queue.Enqueue(start);
        while(queue.Count>0 && !parents.ContainsKey(goal)) {
            int cell=queue.Dequeue(),x=cell%width,y=cell/width;
            for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++) {
                int next=(y+dy)*width+x+dx;
                if((dx!=0 || dy!=0) && walk(x+dx,y+dy) && !parents.ContainsKey(next)){parents[next]=cell;queue.Enqueue(next);}
            }
        }
        Require(parents.ContainsKey(goal),"moon-route-path");var path=new List<int>();
        for(int cell=goal;cell!=-1;cell=parents[cell])path.Add(cell);path.Reverse();
        for(int index=0;index<path.Count-1;) {
            int next=index+1;
            while(next+1<path.Count && Distance(path[index]%width,path[index]/width,path[next+1]%width,path[next+1]/width)<=19)next++;
            System.Threading.Thread.Sleep(700);try {ActionReply(stream,crypto,memory,uid,Data.Jump,(ushort)(path[next]%width),(ushort)(path[next]/width));}catch {Console.WriteLine("Private ordinary route failure map="+map+" target="+(path[next]%width)+","+(path[next]/width)+" from="+(path[index]%width)+","+(path[index]/width));throw;}index=next;
        }
    }
    private static int MoonTokens() {return bag.Count(pair=>pair.Value>=721010 && pair.Value<=721015);}
    private static void CheckMoonTactic(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        int before=MoonTokens();var earnedBefore=bag.Where(pair=>pair.Value>=721010 && pair.Value<=721015).ToArray();
        if(before==6){Console.WriteLine("Private Moon Box six earned tokens restored.");return;}
        for(int route=0;route<16;route++) {
            var loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            if(loc.dwParam==1042) {
                MoonWalk(stream,crypto,memory,uid,1042,22,22);
                Require(CourierDialog(stream,crypto,memory,0,600003).Contains(1),"moon-route-offer");
                CourierDialog(stream,crypto,memory,1,600003);loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            }
            uint map=loc.dwParam;Require(map>=1043 && map<=1049,"moon-tactic-entry");
            Console.WriteLine("Private Moon Box tactic entered: map="+map+"; owned tokens="+MoonTokens()+".");
            uint token=721010+map-1043,ghost=600005+2*(map-1043);
            if(map==1049 || !bag.Values.Contains(token)) {
                huntFamily="ToughHorn";moonDied=false;moonDeathAllowed=true;
                try {
                    for(int kill=0;kill<20 && !bag.Values.Contains(token) && !moonDied;kill++) {
                        bandits.Clear();ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
                        Target target=null;
                        foreach(var point in new[]{new[]{50,32},new[]{65,32},new[]{65,50},new[]{50,65},new[]{35,65}}) {
                            loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                            target=bandits.Values.Where(m=>!killed.Contains(m.Uid) && Distance(m.X,m.Y,loc.wParam1,loc.wParam2)<=17).OrderBy(m=>Distance(m.X,m.Y,loc.wParam1,loc.wParam2)).FirstOrDefault();
                            if(target!=null)break;
                            MoonWalk(stream,crypto,memory,uid,map,point[0],point[1]);bandits.Clear();ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
                        }
                        Require(target!=null,"moon-visible-tough-horn");
                        byte[] floor=MoonFloor(map);int width=BitConverter.ToInt32(floor,0);
                        var near=(from dx in Enumerable.Range(-1,3) from dy in Enumerable.Range(-1,3)
                            let x=(int)target.X+dx let y=(int)target.Y+dy
                            where (dx!=0 || dy!=0) && x>0 && y>0 && x<width && y<width && floor[8+y*width+x]!=1
                            select new{X=x,Y=y}).FirstOrDefault();
                        Require(near!=null,"moon-combat-floor");MoonWalk(stream,crypto,memory,uid,map,near.X,near.Y);
                        if(map!=1049)SendGame(stream,crypto,memory,new Attack(true){Attacker=uid,Attacked=target.Uid,X=target.X,Y=target.Y,AttackType=Attack.Melee}.ToArray());
                        phase="moon-combat";DateTime deadline=DateTime.UtcNow.AddSeconds(40);
                        for(int n=0;n<4096 && DateTime.UtcNow<deadline && !moonDied && !killed.Contains(target.Uid);n++)GameFrame(stream,crypto,memory);
                        Require(moonDied || killed.Contains(target.Uid),"moon-combat-result");
                        if(!moonDied){ActionReply(stream,crypto,memory,uid,Data.SetLocation);Console.WriteLine("Private Moon Box ToughHorn defeated: map="+map+"; owned tokens="+MoonTokens()+".");}
                    }
                } finally {moonDeathAllowed=false;huntFamily="";}
                if(moonDied) {
                    System.Threading.Thread.Sleep(1500);loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                    Require(loc.dwParam==1042,"moon-defeat-return");Console.WriteLine("Private Moon Box defeat returned to Maggie; owned tokens="+MoonTokens()+".");continue;
                }
                Require(bag.Values.Contains(token),"moon-twenty-kill-token");
                Require(bag.Where(pair=>pair.Value==token).All(pair=>boundBag.Contains(pair.Key)),"moon-token-bound");
            }
            MoonWalk(stream,crypto,memory,uid,map,9,63);
            Require(CourierDialog(stream,crypto,memory,0,ghost).Contains(1),"moon-ghost-menu");CourierDialog(stream,crypto,memory,1,ghost);
            loc=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(loc.dwParam==1042,"moon-ghost-return");
            Require(earnedBefore.All(pair=>bag.ContainsKey(pair.Key) && bag[pair.Key]==pair.Value),"moon-prior-tokens-retained");
            Console.WriteLine("Private Moon Box earned token checkpoint: total="+MoonTokens()+".");
            if(MoonTokens()==6)return;
        }
        Require(false,"moon-route-budget");
    }
    private static void CheckPromotion(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        phase="trojan-promotion";
        if(SecondPromotionCheck && currentClass<15 && !bag.Values.Any(id=>id==721020 || id==721090)) {
            if(!bag.Values.Contains(3001405u))BuyCareerMaterial(stream,crypto,memory,3001405,30);
            uint pack=bag.Single(pair=>pair.Value==3001405).Key,cps=currentCps;ulong gold=currentGold,rvp=currentRvp;var before=new Dictionary<uint,uint>(bag);
            SendGame(stream,crypto,memory,new ItemUsage(true){UID=pack,Action=ItemUsage.Mode.EquipItem}.ToArray());
            for(int n=0;n<256 && (bag.ContainsKey(pack) || !bag.Any(pair=>!before.ContainsKey(pair.Key) && pair.Value==721020));n++)GameFrame(stream,crypto,memory);
            Require(!bag.ContainsKey(pack) && bag.Count==before.Count && bag.Count(pair=>!before.ContainsKey(pair.Key) && pair.Value==721020 && !boundBag.Contains(pair.Key))==1
                && currentCps==cps && currentGold==gold && currentRvp==rvp,"second-life-moon-box-pack");
            Console.WriteLine("Private ordinary career MoonBoxPackage opened: one unbound promotion box, no additional charge.");
        }

        if(currentClass<=12 && !bag.Values.Contains(1080001u))BuyCareerMaterial(stream,crypto,memory,1080001,1);
        if(currentClass<=13 && !bag.Values.Contains(1088001u))BuyCareerMaterial(stream,crypto,memory,1088001,3);
        ushort[,] road={{342,303},{357,303},{372,303},{387,303},{402,303},{417,303},{432,303},{447,303},{462,303},{475,293}};
        foreach(int map in new[]{1002,1004}) {
            byte[] floor=File.ReadAllBytes("C:/Revora/DisposablePostgres/moon-box-candidate/game/database/maps/"+map+".map");
            string hash=map==1002?"9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10":"9A52E59F7E03E19F74B81F8939A89A710958C09F2596B324B97B8FF96AB155E4";
            using(var sha=SHA256.Create())Require(BitConverter.ToString(sha.ComputeHash(floor)).Replace("-","")==hash,"promotion-terrain-identity");
        }
        for(int n=0;n<road.GetLength(0);n++){System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,road[n,0],road[n,1]);}
        SendGame(stream,crypto,memory,new Data(true){UID=uid,ID=Data.UsePortal,dwParam=475u|(293u<<16),TimeStamp=Time32.Now}.ToArray());
        var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1004 && location.wParam1==59 && location.wParam2==88,"promotion-school-entry");
        ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        foreach(var step in new[]{new ushort[]{59,73},new ushort[]{59,58},new ushort[]{50,47}}){System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,step[0],step[1]);}
        for(int n=0;n<5 && (currentClass<14 || (MoonFinish && currentClass==14 && bag.Values.Any(id=>id==721090 || (SecondPromotionCheck && id==721020))));n++) {
            Require(CourierDialog(stream,crypto,memory,0,10022).Contains(1),"promotion-menu");
            Require(CourierDialog(stream,crypto,memory,1,10022).Contains(3),"promotion-offer");
            uint before=currentClass,cps=currentCps;ulong gold=currentGold,rvp=currentRvp;int items=bag.Count;
            uint material=before==12?1080001u:before==13?1088001u:before==14?(bag.Values.Contains(721090u)?721090u:721020u):0;
            uint materialUid=material==0?0:bag.Single(pair=>pair.Value==material).Key;
            var oldItems=new HashSet<uint>(bag.Keys);
            CourierDialog(stream,crypto,memory,3,10022);
            Require(lastDialogText.Contains("You have earned your new rank") && currentClass==before+1 && currentCps==cps && currentGold==gold && currentRvp==rvp && bag.Count==items-(before==12?1:0),"promotion-immediate-update");
            Require(materialUid==0 || !bag.ContainsKey(materialUid),"promotion-material-consumed");
            if(before==13)Require(bag.Count(pair=>!oldItems.Contains(pair.Key) && pair.Value==700031)==1,"promotion-rainbow-gem-reward");
            if(before==14)Require(bag.Count(pair=>!oldItems.Contains(pair.Key) && pair.Value==1088000 && !boundBag.Contains(pair.Key))==1,"promotion-dragonball-reward");
            CourierDialog(stream,crypto,memory,3,10022);
            Require(lastDialogText.Contains("Let us discuss your next rank first") && currentClass==before+1,"promotion-offer-replay");
            Console.WriteLine("Private Trojan promotion earned through trainer: class="+currentClass+"; required material consumed, no extra currency charge.");
        }
        if(currentClass==15) {
            Require(MoonFinish,"promotion-unexpected-final-rank");
            Require(CourierDialog(stream,crypto,memory,0,10022).Contains(1),"promotion-final-menu");
            Require(!CourierDialog(stream,crypto,memory,1,10022).Contains(3) && lastDialogText.Contains("highest rank"),"promotion-final-cap");
            Console.WriteLine("Private Trojan class15 verified; final rank and DragonBall verified after owned Moon Box consumption.");
        } else {
        Require(currentClass==14,"promotion-class14");
        Require(CourierDialog(stream,crypto,memory,0,10022).Contains(1),"promotion-next-menu");
        Require(CourierDialog(stream,crypto,memory,1,10022).Contains(3) && lastDialogText.Contains("Moon Box"),"promotion-moon-box-requirement");
        CourierDialog(stream,crypto,memory,3,10022);
        Require(currentClass==14 && lastDialogText.Contains("Bring the required offering"),"promotion-missing-moon-box-rejected");
        CourierDialog(stream,crypto,memory,3,10022);
        Require(lastDialogText.Contains("Let us discuss your next rank first"),"promotion-missing-offer-replay");
        Require(CourierDialog(stream,crypto,memory,0,10022).Contains(1),"promotion-reopen-menu");
        Require(CourierDialog(stream,crypto,memory,1,10022).Contains(3) && lastDialogText.Contains("Moon Box"),"promotion-reopen-offer");
        Console.WriteLine("Private Trojan class14 restored/verified; final rank requires an earned Moon Box.");
        }
        foreach(var step in new[]{new ushort[]{59,58},new ushort[]{51,73}}){System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,step[0],step[1]);}
        SendGame(stream,crypto,memory,new Data(true){UID=uid,ID=Data.UsePortal,dwParam=51u|(73u<<16),TimeStamp=Time32.Now}.ToArray());
        location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1002 && location.wParam1==477 && location.wParam2==297,"promotion-school-exit");
        ReturnToCerys(stream,crypto,memory,uid);ReturnSoulSpawn(stream,crypto,memory,uid);
    }
    private static void CheckHunt(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid,bool path) {
        phase=path?"path-hunt":"soul-trial-access";
        uint npc=path?900000016u:900000013u;
        string quest=path?"Path Hunt":"Soul Trial";
        string paid=path?"You finished the Path Hunt in this life":"You already finished the Soul Trial in this life";
        int combat=path?pathCombat:soulCombat;
        ulong goldReward=path?25000UL:50000UL,rvpReward=path?100UL:500UL;
        int potions=path?11:15;
        System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,320,297);
        var options=CourierDialog(stream,crypto,memory,0,npc);
        if(lastDialogText.Contains(paid)) { VerifyHuntPaid(stream,crypto,memory,npc,paid,quest);ReturnSoulSpawn(stream,crypto,memory,uid);return; }
        bool resumed=!options.Contains(1) && options.Contains(3) && options.Contains(4) && options.Contains(5);
        Require(Environment.GetEnvironmentVariable("REVORA_CANDIDATE_COURIER_RESUME")!="1" || resumed,"soul-trial-not-restored");
        if(options.Contains(1)) { CourierDialog(stream,crypto,memory,1,npc); options=CourierDialog(stream,crypto,memory,0,npc); }
        Require(options.Contains(3) && options.Contains(4) && options.Contains(5),"soul-trial-travel-options");
        string[] families=path?new[]{"Pheasants","Robins","Turtledoves","Apparitions"}:new[]{"Birdmen","Macaques","Sand Monsters"};
        string[] monsters=path?new[]{"Pheasant","Robin","Turtledove","Apparition"}:new[]{"Birdman","Macaque","SandMonster"};
        ushort[,] destinations=path?new ushort[,]{{1002,520,490},{1002,320,700},{1002,450,630},{1002,200,650}}:new ushort[,]{{1015,717,571},{1020,565,562},{1000,500,650}};
        for(int i=0;i<families.Length && !options.Contains(2);i++) {
            if(combat!=0 && combat!=i+1)continue;
            var before=System.Text.RegularExpressions.Regex.Match(lastDialogText,System.Text.RegularExpressions.Regex.Escape(families[i])+@" (\d+)/(\d+)");
            Require(before.Success,"soul-trial-progress-before");
            int prior=int.Parse(before.Groups[1].Value),needed=int.Parse(before.Groups[2].Value);
            int count=combat==0?0:Math.Min(soulBatch,needed-prior);
            Require(count>=0,"soul-progress-over-cap");
            huntFamily=monsters[i];bandits.Clear();
            SendNpc(stream,crypto,memory,npc,(byte)(3+i));
            var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
            Require(location.dwParam==destinations[i,0] && location.wParam1==destinations[i,1] && location.wParam2==destinations[i,2],"soul-trial-destination");
            ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
            for(int kill=0;kill<count;kill++) {
                bool slain=HuntSoulMonster(stream,crypto,memory,uid,path?i+3:i);
                if(!slain) {
                    ReturnToCerys(stream,crypto,memory,uid);
                    System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,320,297);
                    Require(CourierDialog(stream,crypto,memory,0,npc).Contains((byte)(3+i)),"soul-return-travel-option");
                    SendNpc(stream,crypto,memory,npc,(byte)(3+i));
                    location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                    Require(location.dwParam==destinations[i,0] && location.wParam1==destinations[i,1] && location.wParam2==destinations[i,2],"soul-return-travel-destination");
                    slain=HuntSoulMonster(stream,crypto,memory,uid,path?i+3:i);
                }
                Require(slain,"visible-soul-target-after-return");
                Console.WriteLine("Private "+quest+" confirmed combat death: "+families[i]+" batch="+(kill+1)+"/"+count+".");
            }
            huntFamily="";bandits.Clear();
            ReturnToCerys(stream,crypto,memory,uid);
            System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,320,297);
            options=CourierDialog(stream,crypto,memory,0,npc);
            Require(options.Contains(3) && options.Contains(4) && options.Contains(5) && !options.Contains(1),"soul-trial-active-state");
            var after=System.Text.RegularExpressions.Regex.Match(lastDialogText,System.Text.RegularExpressions.Regex.Escape(families[i])+@" (\d+)/(\d+)");
            Console.WriteLine("Private "+quest+" counter comparison: "+families[i]+" before="+prior+" expected="+(prior+count)+" observed="+(after.Success?after.Groups[1].Value:"missing")+".");
            bool complete=options.Contains(2) && lastDialogText.Contains("The hunt is done");
            Require((complete && prior+count==needed) || (after.Success && int.Parse(after.Groups[1].Value)==prior+count && int.Parse(after.Groups[2].Value)==needed),"soul-trial-exact-earned-progress");
            if(combat!=0)Console.WriteLine("Private "+quest+" real combat progress: "+families[i]+" "+(prior+count)+"/"+needed+".");
            Console.WriteLine("Private "+quest+" ordinary travel passed: map="+destinations[i,0]+"; restored="+resumed+".");
        }
        if(options.Contains(2)) {
            uint cps=currentCps,level=currentLevel;ulong gold=currentGold,rvp=currentRvp;var oldItems=new HashSet<uint>(bag.Keys);
            Require(level==120,"soul-reward-profile");
            CourierDialog(stream,crypto,memory,2,npc);
            Require(lastDialogText.Contains(path?"The Path Hunt is complete":"The trial is complete"),"soul-reward-dialog");
            Require(currentCps==cps && currentGold==gold+goldReward && currentRvp==rvp+rvpReward && currentLevel==level,"soul-reward-wallet-level");
            var added=bag.Where(pair=>!oldItems.Contains(pair.Key)).ToArray();
            var souls=added.Where(pair=>pair.Value>=800000 && pair.Value<900000 && boundBag.Contains(pair.Key)).ToArray();
            Require(added.Length==potions+1 && souls.Length==1 && added.Count(pair=>pair.Value==723017 && !boundBag.Contains(pair.Key))==potions,"soul-reward-items");
            Console.WriteLine("Private "+quest+" earned reward: "+rvpReward+" RVPs, "+goldReward+" silver, "+potions+" Exp Potions and one bound soul; uid="+souls[0].Key+" type="+souls[0].Value+".");
            VerifyHuntPaid(stream,crypto,memory,npc,paid,quest);
        }
        ReturnSoulSpawn(stream,crypto,memory,uid);
    }
    private static void VerifyHuntPaid(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint npc,string paid,string quest) {
        uint cps=currentCps,level=currentLevel;ulong gold=currentGold,rvp=currentRvp;int count=bag.Count;
        CourierDialog(stream,crypto,memory,2,npc);
        Require(lastDialogText.Contains(paid) && currentCps==cps && currentGold==gold && currentRvp==rvp && currentLevel==level && bag.Count==count,"soul-paid-replay");
        Console.WriteLine("Private "+quest+" paid state verified; duplicate turn-in refused.");
    }
    private static void ReturnSoulSpawn(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,327,303);
        var saved=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(saved.dwParam==1002 && saved.wParam1==327 && saved.wParam2==303,"soul-trial-return-spawn");
    }
    private static void HuntOne(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        byte[] floor=File.ReadAllBytes("C:/Revora/DisposablePostgres/moon-box-candidate/game/database/maps/1011.map");
        using(var sha=SHA256.Create())Require(BitConverter.ToString(sha.ComputeHash(floor)).Replace("-","")=="7E8F7A8262F8AE93A314699AF6D276F0324FFF6C32BCC6061BFFC2F336F1E817","hunt-terrain-identity");
        Require(floor.Length==8+944*944 && BitConverter.ToInt32(floor,0)==944 && BitConverter.ToInt32(floor,4)==944,"hunt-terrain-shape");
        Func<int,int,bool> walk=(a,b)=>a>=0 && b>=0 && a<944 && b<944 && floor[8+b*944+a]!=1;
        var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(location.dwParam==1011,"bandit-hunt-map");
        ushort x=location.wParam1,y=location.wParam2;
        bandits.Clear();ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        var search=new List<ushort[]>{new ushort[]{112,287},new ushort[]{112,302},new ushort[]{112,317},new ushort[]{127,330},new ushort[]{130,330}};
        int[,] offsets={{15,0},{15,15},{0,15},{-15,15},{-15,0},{-15,-15},{0,-15},{15,-15},{30,-15},{30,0},{30,15},{30,30},{15,30},{0,30},{-15,30},{-30,30},{-30,15},{-30,0},{-30,-15}};
        for(int n=0;n<offsets.GetLength(0);n++)if(x+offsets[n,0]>0 && y+offsets[n,1]>0)search.Add(new[]{(ushort)(x+offsets[n,0]),(ushort)(y+offsets[n,1])});
        var route=new ushort[search.Count,2];for(int n=0;n<search.Count;n++){route[n,0]=search[n][0];route[n,1]=search[n][1];}
        Target target=null;
        for(int step=0;step<=route.GetLength(0);step++) {
            target=bandits.Values.Where(m=>!killed.Contains(m.Uid) && Distance(m.X,m.Y,x,y)<=17)
                .OrderBy(m=>Distance(m.X,m.Y,x,y)).FirstOrDefault();
            if(target!=null || step==route.GetLength(0))break;
            var next=(from dx in Enumerable.Range(-4,9) from dy in Enumerable.Range(-4,9)
                let tx=(int)route[step,0]+dx let ty=(int)route[step,1]+dy
                where walk(tx,ty) && Distance(tx,ty,x,y)<=20
                orderby Math.Abs(dx)+Math.Abs(dy) select new{X=(ushort)tx,Y=(ushort)ty}).FirstOrDefault();
            if(next==null)continue;x=next.X;y=next.Y;
            System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,x,y);
            ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        }
        Require(target!=null && target.X>1,"visible-bandit-target");
        var adjacent=(from dx in Enumerable.Range(-1,3) from dy in Enumerable.Range(-1,3)
            let tx=(int)target.X+dx let ty=(int)target.Y+dy
            where (dx!=0 || dy!=0) && walk(tx,ty) && Distance(tx,ty,x,y)<=20
            orderby Distance(tx,ty,x,y) select new{X=(ushort)tx,Y=(ushort)ty}).FirstOrDefault();
        Require(adjacent!=null,"bandit-adjacent-floor");
        System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,adjacent.X,adjacent.Y);
        phase="bandit-combat";
        SendGame(stream,crypto,memory,new Attack(true){Attacker=uid,Attacked=target.Uid,X=target.X,Y=target.Y,AttackType=Attack.Melee}.ToArray());
        DateTime deadline=DateTime.UtcNow.AddSeconds(30);
        for(int n=0;n<2048 && DateTime.UtcNow<deadline && !killed.Contains(target.Uid);n++)GameFrame(stream,crypto,memory);
        Require(killed.Contains(target.Uid),"bandit-death-not-confirmed");
        Console.WriteLine("Private Courier ordinary Bandit defeat confirmed.");
    }
    private static bool HuntSoulMonster(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid,int family) {
        int[] maps={1015,1020,1000,1002,1002,1002,1002},sizes={1024,932,984,824,824,824,824};
        string[] hashes={"E47DF70501200965040755A424873395172C03E7DAD920D483414F85C0359BA7","BDB39E79FD6A7701DA0EA10A12C0753E8CD6F4187FF8719B05608699BC903B95","C874AA5659420DFCF03545F1BFAD67BEA0197AE55FBC8140FCD7E85FFBAE33F1","9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10","9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10","9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10","9F75CC638480A0485E6B17BDF9F9F76886A9AB7449317BEBE18E72EF6AD34A10"};
        byte[] floor=File.ReadAllBytes("C:/Revora/DisposablePostgres/moon-box-candidate/game/database/maps/"+maps[family]+".map");
        using(var sha=SHA256.Create())Require(BitConverter.ToString(sha.ComputeHash(floor)).Replace("-","")==hashes[family],"soul-terrain-identity");
        int size=sizes[family];Require(floor.Length==8+size*size && BitConverter.ToInt32(floor,0)==size && BitConverter.ToInt32(floor,4)==size,"soul-terrain-shape");
        Func<int,int,bool> walk=(a,b)=>a>=0 && b>=0 && a<size && b<size && floor[8+b*size+a]!=1;
        bandits.Clear();
        ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        var position=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(position.dwParam==maps[family],"soul-hunt-map");
        ushort x=position.wParam1,y=position.wParam2;
        ushort[,] route=family==0?new ushort[,]{{717,586},{717,601},{717,619},{717,631},{717,636},{717,651},{732,651},{747,651},{762,651},{777,651},{792,651},{807,651},{822,651},{837,651},{837,666},{822,666},{807,666},{792,666},{777,666},{762,666},{747,666},{732,666},{717,666},{702,666},{687,666},{672,666},{657,666},{657,681},{672,681},{687,681},{702,681},{717,681},{732,681},{747,681},{762,681},{777,681},{792,681},{807,681},{822,681},{837,681}}:
            family==1?new ushort[,]{{580,560},{598,558},{608,556},{623,556},{638,556},{653,556},{668,556},{668,571},{683,571},{683,586},{668,586},{653,586},{638,586},{623,586},{608,586},{608,601},{623,601},{638,601},{653,601},{668,601},{683,601},{698,601},{713,601},{728,601},{743,601}}:new ushort[,]{{515,650},{529,650},{545,650},{560,650},{569,661},{584,650},{599,645},{614,640},{629,639},{644,639},{659,639},{674,639},{689,639},{689,654},{704,654},{704,669},{719,669},{719,684},{719,699},{719,714},{704,714},{689,714},{674,714},{659,714},{644,714},{629,714},{614,714},{599,714},{584,714},{569,714},{554,714},{539,714},{539,729},{554,729},{569,729},{584,729},{599,729},{614,729},{629,729},{644,729},{659,729},{674,729},{689,729},{704,729},{719,729}};
        if(family>=3) {
            ushort[,] starts={{520,490},{320,700},{450,630},{200,650}};
            var points=new List<ushort[]>();int sx=starts[family-3,0],sy=starts[family-3,1];
            for(int row=0;row<5;row++)for(int column=0;column<9;column++) {
                int offset=row%2==0?column:8-column;
                points.Add(new[]{(ushort)(sx+offset*15),(ushort)(sy+row*15)});
            }
            route=new ushort[points.Count,2];for(int n=0;n<points.Count;n++){route[n,0]=points[n][0];route[n,1]=points[n][1];}
        }
        Target target=null;
        for(int step=0;step<=route.GetLength(0);step++) {
            target=bandits.Values.Where(m=>!killed.Contains(m.Uid) && Distance(m.X,m.Y,x,y)<=17)
                .OrderBy(m=>Distance(m.X,m.Y,x,y)).FirstOrDefault();
            if(target!=null || step==route.GetLength(0))break;
            if(Distance(route[step,0],route[step,1],x,y)>20)continue;
            var next=(from dx in Enumerable.Range(-4,9) from dy in Enumerable.Range(-4,9)
                let tx=(int)route[step,0]+dx let ty=(int)route[step,1]+dy
                where walk(tx,ty) && Distance(tx,ty,x,y)<=20
                orderby Math.Abs(dx)+Math.Abs(dy) select new{X=(ushort)tx,Y=(ushort)ty}).FirstOrDefault();
            if(next==null)continue;x=next.X;y=next.Y;
            System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,x,y);
            ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        }
        if(target==null)return false;
        Require(target.X>1,"visible-soul-target");
        var adjacent=(from dx in Enumerable.Range(-1,3) from dy in Enumerable.Range(-1,3)
            let tx=(int)target.X+dx let ty=(int)target.Y+dy
            where (dx!=0 || dy!=0) && walk(tx,ty) && Distance(tx,ty,x,y)<=20
            orderby Distance(tx,ty,x,y) select new{X=(ushort)tx,Y=(ushort)ty}).FirstOrDefault();
        Require(adjacent!=null,"soul-adjacent-floor");
        System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,adjacent.X,adjacent.Y);
        phase="soul-combat";
        SendGame(stream,crypto,memory,new Attack(true){Attacker=uid,Attacked=target.Uid,X=target.X,Y=target.Y,AttackType=Attack.Melee}.ToArray());
        DateTime deadline=DateTime.UtcNow.AddSeconds(30);
        for(int n=0;n<2048 && DateTime.UtcNow<deadline && !killed.Contains(target.Uid);n++)GameFrame(stream,crypto,memory);
        Require(killed.Contains(target.Uid),"soul-death-not-confirmed");
        return true;
    }
    private static void ReturnToCerys(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        SendGame(stream,crypto,memory,new Data(true){UID=uid,ID=Data.SendTC,TimeStamp=Time32.Now}.ToArray());
        var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1002 && location.wParam1==301 && location.wParam2==288,"public-return-action");
        ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
        System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,314,295);
        System.Threading.Thread.Sleep(700);ActionReply(stream,crypto,memory,uid,Data.Jump,323,303);
        location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
        Require(location.dwParam==1002 && location.wParam1==323 && location.wParam2==303,"return-courier-position");
    }
    private static void CharacterLogin(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,byte[] first) {
        if(expectedUid==0) {
            var create=new byte[84];memory.Add(create);Writer.WriteUInt16(76,0,create);Writer.WriteUInt16(1001,2,create);
            Writer.WriteString(CharacterName,24,create);Writer.WriteUInt16(1006,72,create);Writer.WriteUInt16(3,74,create);
            Writer.WriteString("TQClient",76,create);crypto.Encrypt(create,create.Length);stream.Write(create,0,create.Length);
            first=null;
        }
        for(int count=0;count<256;count++) {
            var frame=first??GameFrame(stream,crypto,memory);first=null;
            if(BitConverter.ToUInt16(frame,2)!=1006)continue;
            Require(frame.Length>=158 && frame[149]==CharacterName.Length && 150+frame[149]<=frame.Length-8,"character-frame-shape");
            Require(Encoding.ASCII.GetString(frame,150,frame[149])==CharacterName,"character-name");
            uint uid=BitConverter.ToUInt32(frame,8);Require(uid!=0 && (expectedUid==0 || uid==expectedUid),"character-identity");
            Require(((taoistWire && (frame[81]==135 || frame[81]==145) && frame[85]==2) || (pirateWire && frame[81]==75 && frame[85]==2) || frame[81]>=10 && frame[81]<=15) && (frame[85]==0 || ((pirateWire || taoistWire) && frame[85]==2) || ((FirstRebirthCheck || eliminationWire) && frame[85]==1) || (SecondRebirthCheck && frame[85]==2) || (anniversaryWire && frame[85]==2 && frame[80]==140)),"character-class-rebirth");
            currentReborn=frame[85];currentAttributes=BitConverter.ToUInt16(frame,70);currentStrength=BitConverter.ToUInt16(frame,62);currentVitality=BitConverter.ToUInt16(frame,66);
            currentClass=frame[81];currentUid=uid;currentLevel=frame[80];currentCps=BitConverter.ToUInt32(frame,30);currentGold=BitConverter.ToUInt64(frame,22);currentRvp=BitConverter.ToUInt64(frame,132);
            Console.WriteLine("Private ordinary character info received; uid="+uid+".");EnterWorld(stream,crypto,memory,uid);return;
        }
        throw new InvalidOperationException("character-info-not-received");
    }
    private static byte[] Hex(byte[] bytes) {return Encoding.ASCII.GetBytes(BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant());}
    private static byte[] Sized(BinaryReader reader,int maximum,List<byte[]> memory) {
        int length=reader.ReadInt32();Require(length>0 && length<=maximum && reader.BaseStream.Position+length<=reader.BaseStream.Length,"dh-field-bound");
        var bytes=reader.ReadBytes(length);memory.Add(bytes);return bytes;
    }
    private static void GameHandoff(uint grant,List<byte[]> memory,bool rejectReplay=false) {
        using(var socket=new TcpClient(AddressFamily.InterNetwork)) {
            var connecting=socket.BeginConnect(IPAddress.Loopback,19559,null,null);
            using(connecting.AsyncWaitHandle)Require(connecting.AsyncWaitHandle.WaitOne(3000),"game-connect-timeout");
            socket.EndConnect(connecting);socket.ReceiveTimeout=3000;socket.SendTimeout=3000;socket.NoDelay=true;
            using(var stream=socket.GetStream()) {
                var bootstrap=new GameCryptography(Encoding.ASCII.GetBytes(Revora.Constants.GameCryptographyKey));
                var prefix=new byte[15];memory.Add(prefix);ReadExact(stream,prefix);bootstrap.Decrypt(prefix,prefix.Length);
                int size=BitConverter.ToInt32(prefix,11);Require(size>=4 && size<=1024,"dh-hello-bound");
                var rest=new byte[size-4];memory.Add(rest);ReadExact(stream,rest);bootstrap.Decrypt(rest,rest.Length);
                byte[] clientIv,serverIv,p,g,serverPublic;
                using(var reader=new BinaryReader(new MemoryStream(rest))) {
                    Sized(reader,49,memory);clientIv=Sized(reader,8,memory);serverIv=Sized(reader,8,memory);
                    Require(clientIv.Length==8 && serverIv.Length==8,"dh-ivs");
                    p=Sized(reader,128,memory);g=Sized(reader,128,memory);serverPublic=Sized(reader,128,memory);
                    Require(reader.BaseStream.Length-reader.BaseStream.Position==8 && Encoding.ASCII.GetString(reader.ReadBytes(8))=="TQServer","dh-seal");
                }
                var exchange=new CO2_CORE_DLL.Security.Cryptography.DiffieHellman(Encoding.ASCII.GetString(p),Encoding.ASCII.GetString(g));
                var publicKey=Encoding.ASCII.GetBytes(exchange.GenerateRequest().PadLeft(128,'0'));memory.Add(publicKey);Require(publicKey.Length==128,"dh-public-bound");
                exchange.HandleResponse(Encoding.ASCII.GetString(serverPublic));var shared=exchange.ToBytes();memory.Add(shared);
                byte[] first,second,key;
                using(var md5=MD5.Create()) {
                    var significant=shared.TakeWhile(b=>b!=0).ToArray();memory.Add(significant);first=md5.ComputeHash(significant);memory.Add(first);
                    var encoded=Hex(first);memory.Add(encoded);second=md5.ComputeHash(encoded);memory.Add(second);
                    var combined=first.Concat(second).ToArray();memory.Add(combined);key=Hex(combined);memory.Add(key);
                }
                var game=new GameCryptography(key);game.SetIvs(serverIv,clientIv);
                var reply=new byte[166];memory.Add(reply);Writer.WriteUInt32(159,7,reply);Writer.WriteUInt32(11,11,reply);Writer.WriteUInt32(128,26,reply);
                Buffer.BlockCopy(publicKey,0,reply,30,128);Writer.WriteString("TQClient",158,reply);
                var outboundBootstrap=new GameCryptography(Encoding.ASCII.GetBytes(Revora.Constants.GameCryptographyKey));outboundBootstrap.Encrypt(reply,reply.Length);
                var connect=new byte[40];memory.Add(connect);Writer.WriteUInt16(32,0,connect);Writer.WriteUInt16(1052,2,connect);Writer.WriteUInt32(grant,4,connect);Writer.WriteUInt32(1,8,connect);Writer.WriteString("TQClient",32,connect);game.Encrypt(connect,connect.Length);
                var combinedRequest=reply.Concat(connect).ToArray();memory.Add(combinedRequest);stream.Write(combinedRequest,0,combinedRequest.Length);
                if(rejectReplay) {
                    var probe=new byte[1];memory.Add(probe);
                    try{Require(stream.Read(probe,0,1)==0,"replayed-game-grant-accepted");}
                    catch(IOException error) {
                        var failure=error.InnerException as SocketException;
                        Require(failure!=null && (failure.SocketErrorCode==SocketError.ConnectionReset || failure.SocketErrorCode==SocketError.ConnectionAborted),"replayed-game-grant-not-closed");
                    }
                    return;
                }
                var received=GameFrame(stream,game,memory);
                if(!characterCheck || expectedUid==0) {
                    var expected=Revora.Network.GamePackets.Message.EncodeNewRole();Writer.WriteString(Revora.Constants.ServerKey,expected.Length-8,expected);
                    Require(received.SequenceEqual(expected),"new-role-response");
                }
                if(characterCheck)CharacterLogin(stream,game,memory,received);
            }
        }
    }
    private static void Exchange(string password,bool wrong) {
        var memory=new List<byte[]>();
        try {using(var socket=new TcpClient(AddressFamily.InterNetwork)) {
            var connecting=socket.BeginConnect(IPAddress.Loopback,19558,null,null);
            using(connecting.AsyncWaitHandle)Require(connecting.AsyncWaitHandle.WaitOne(3000),"connect-timeout");
            socket.EndConnect(connecting);socket.ReceiveTimeout=3000;socket.SendTimeout=3000;socket.NoDelay=true;
            using(var stream=socket.GetStream()) {
                var outbound=new Cipher();var inbound=new Cipher();
                var seed=Read(stream,inbound,memory);Require(seed.Length==8 && BitConverter.ToUInt16(seed,2)==1059,"seed");
                var login=new byte[312];memory.Add(login);Writer.WriteUInt16(312,0,login);Writer.WriteUInt16(1636,2,login);
                var user=Encoding.ASCII.GetBytes(AccountName);Buffer.BlockCopy(user,0,login,4,user.Length);
                outbound.Transform(login);stream.Write(login,0,7);stream.Write(login,7,login.Length-7);
                var challenge=Read(stream,inbound,memory);
                Require(challenge.Length==653 && BitConverter.ToUInt16(challenge,2)==1213 && challenge[4]>=1 && challenge[4]<=32 && challenge[391]==32,"challenge");
                byte[] publicA;byte[] proof=SrpVerifierTests.Proof(challenge,AccountName,password,out publicA);memory.Add(publicA);memory.Add(proof);
                if(wrong)proof[0]^=1;
                var packet=new byte[262];memory.Add(packet);Writer.WriteUInt16(262,0,packet);Writer.WriteUInt16(1214,2,packet);
                packet[4]=(byte)publicA.Length;Buffer.BlockCopy(publicA,0,packet,5,publicA.Length);packet[133]=(byte)proof.Length;Buffer.BlockCopy(proof,0,packet,134,proof.Length);
                // Coalesce the successful request with its replay; the server must reject the second proof.
                var batch=new byte[wrong?262:524];memory.Add(batch);Buffer.BlockCopy(packet,0,batch,0,262);if(!wrong)Buffer.BlockCopy(packet,0,batch,262,262);
                outbound.Transform(batch);stream.Write(batch,0,batch.Length);
                var forward=Read(stream,inbound,memory);Require(forward.Length==224 && BitConverter.ToUInt16(forward,2)==1637,"forward");
                if(wrong)Require(BitConverter.ToUInt32(forward,12)==1 && BitConverter.ToUInt32(forward,4)==0,"invalid-proof-rejected");
                else {
                    Require(BitConverter.ToUInt32(forward,12)==2 && BitConverter.ToUInt32(forward,4)!=0 && BitConverter.ToUInt32(forward,16)==19559
                        && Encoding.ASCII.GetString(forward,24,16).TrimEnd('\0')=="127.0.0.1","private-forward");
                    var replay=Read(stream,inbound,memory);Require(replay.Length==224 && BitConverter.ToUInt16(replay,2)==1637
                        && BitConverter.ToUInt32(replay,12)==1 && BitConverter.ToUInt32(replay,4)==0,"replay-rejected");
                    GameHandoff(BitConverter.ToUInt32(forward,4),memory);
                    if(!CrashCheck)GameHandoff(BitConverter.ToUInt32(forward,4),memory,true);
                }
            }
        }}finally{foreach(var bytes in memory)Array.Clear(bytes,0,bytes.Length);}
    }
    private static bool SkillPkCheck {get{return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SKILL_PK")=="1";}}
    private static bool CrashCheck { get { return Environment.GetEnvironmentVariable("REVORA_CANDIDATE_CRASH_CHECK")=="1"; } }
    private static int Main() {
        if(Environment.GetEnvironmentVariable("REVORA_TEAM_WIRE_ROLE")!=null)return TeamWireMain();
        string password=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD");Environment.SetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD",null);
        string character=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_CHARACTER_UID");
        characterCheck=character!=null;
        try {var pc=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_PATH_COMBAT");Require(pc==null || (int.TryParse(pc,out pathCombat) && pathCombat>=1 && pathCombat<=4),"path-combat-mode");var batch=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SOUL_BATCH");Require(batch==null || (int.TryParse(batch,out soulBatch) && soulBatch>=1 && soulBatch<=50),"soul-batch-bound");var combat=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_SOUL_COMBAT");Require(combat==null || (int.TryParse(combat,out soulCombat) && soulCombat>=1 && soulCombat<=3),"soul-combat-mode");Require(!characterCheck || UInt32.TryParse(character,out expectedUid),"expected-character-uid");Require(!string.IsNullOrEmpty(password),"credential");Exchange(password,false);if(!CrashCheck)Exchange(password,true);
            if(CrashCheck)Console.WriteLine("Private ordinary live-session interruption observed; restart recovery must be checked separately.");
            else if(characterCheck)Console.WriteLine("Private character/quest probe passed; game-grant replay rejected. Native acceptance remains pending.");
            else Console.WriteLine("Private Account wire check passed: split login writes, accepted SRP proof, loopback game forwarding, coalesced replay rejection and invalid-proof rejection. Game DH, NEW_ROLE and single-use game handoff passed. No character creation or earned-reward recovery claimed.");return 0;
        }catch(Exception ex){Console.Error.WriteLine("Private Account wire check failed: "+ex.GetType().Name+" stage="+(ex.Data["probe-stage"]??phase));return 1;}
        finally{password=null;}
    }
}
