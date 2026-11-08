using System;
using System.Collections.Generic;
using System.Net.Sockets;
using Revora.Network;
using Revora.Network.Cryptography;
using Revora.Network.GamePackets;

internal static partial class CandidateAccountWireProbe
{
    private static int teamWireRole=-1;
    private static bool anniversaryWire,pirateWire,taoistWire,eliminationWire,regularTeamWire;
    private static readonly HashSet<ushort> pirateWireSpells=new HashSet<ushort>();
    private static int TeamWireMain() {
        string password=Environment.GetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD");
        Environment.SetEnvironmentVariable("REVORA_CANDIDATE_ACCOUNT_PASSWORD",null);
        try {
            Require(int.TryParse(Environment.GetEnvironmentVariable("REVORA_TEAM_WIRE_ROLE"),out teamWireRole) && teamWireRole>=0 && teamWireRole<=15,"team-role");
            Require(!string.IsNullOrEmpty(password),"team-credential");
            regularTeamWire=Environment.GetEnvironmentVariable("REVORA_REGULAR_TEAM_WIRE")=="1";
            eliminationWire=Environment.GetEnvironmentVariable("REVORA_ELIMINATION_WIRE")=="1";
            taoistWire=Environment.GetEnvironmentVariable("REVORA_TAOIST_WIRE")=="1";
            pirateWire=Environment.GetEnvironmentVariable("REVORA_PIRATE_WIRE")=="1";
            anniversaryWire=Environment.GetEnvironmentVariable("REVORA_ANNIVERSARY_WIRE")=="1";
            AccountName="TeamProbe"+teamWireRole;CharacterName="ArenaProbe"+teamWireRole;expectedUid=4294903001u+(uint)teamWireRole;characterCheck=true;
            Exchange(password,false);Console.WriteLine("[team-wire] stopped");return 0;
        }catch(Exception error){var socketError=error.InnerException as SocketException;Console.Error.WriteLine("[team-wire] failed "+error.GetType().Name+" stage="+(error.Data["probe-stage"]??phase)+" socket="+(socketError==null?"none":socketError.SocketErrorCode.ToString())+" action-ms="+(wireClock.ElapsedMilliseconds-actionStartedAt)+" idle-ms="+(wireClock.ElapsedMilliseconds-lastFrameAt)+" last-kind="+lastFrameKind+" frames="+gameFrames);return 1;}
        finally{password=null;}
    }
    private static void TeamWireWorld(NetworkStream stream,GameCryptography crypto,List<byte[]> memory,uint uid) {
        foreach(uint action in new uint[]{Data.SetLocation,Data.ConfirmFriends,Data.ConfirmProficiencies,Data.ConfirmSpells,Data.ConfirmGuild,Data.GetSurroundings,Data.CompleteLogin})ActionReply(stream,crypto,memory,uid,action);
        Console.WriteLine("[team-wire] ready");Console.Out.Flush();
        for(int step=0;step<(pirateWire || taoistWire || eliminationWire?1024:96);step++) {
            string command=Console.ReadLine();if(command=="stop")return;
            if(command=="snake-claim" && Environment.GetEnvironmentVariable("REVORA_SNAKE_WIRE")=="1") {
                CourierDialog(stream,crypto,memory,0,30115);SendNpc(stream,crypto,memory,30115,3);
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==1015,"snake-claim-map");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("sky-npc:") && Environment.GetEnvironmentVariable("REVORA_SKY_WIRE")=="1") {
                var fields=command.Split(':');uint npc=UInt32.Parse(fields[1]);
                Require(fields.Length==3 && (npc==30055 || npc==30054),"sky-actor");
                CourierDialog(stream,crypto,memory,0,npc);SendNpc(stream,crypto,memory,npc,1);
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==UInt32.Parse(fields[2]),"sky-map");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("perfection-boost:") && Environment.GetEnvironmentVariable("REVORA_PERFECTION_WIRE")=="1") {
                var parts=command.Split(':');Require(parts.Length==5 || parts.Length==6,"boost-command");uint item=UInt32.Parse(parts[1]),amount=UInt32.Parse(parts[2]);
                uint expectedLevel=parts.Length==6?UInt32.Parse(parts[5]):1u;
                var payload=new List<byte>();Action<uint> varint=value=>{while(value>=128){payload.Add((byte)(value|128));value>>=7;}payload.Add((byte)value);};
                payload.Add(8);varint(3);payload.Add(16);varint(item);payload.Add(32);varint(amount);
                var boostFrame=new byte[payload.Count+12];payload.CopyTo(boostFrame,4);Writer.WriteUInt16((ushort)(boostFrame.Length-8),0,boostFrame);Writer.WriteUInt16(3251,2,boostFrame);
                SendGame(stream,crypto,memory,boostFrame);
                // Injected storage failure fences the session; the parent verifies the
                // database and reconnect requirement, then a fresh process retries.
                if(parts.Length==6 && expectedLevel==0){Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;}
                ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Require(currentCps==UInt32.Parse(parts[3]) && currentBoundCps==UInt32.Parse(parts[4]),"boost-live-wallets");
                ItemRefineProto refinement;Require(perfectionUpdates.TryGetValue(item,out refinement) && refinement.PerfectionLevel==expectedLevel && refinement.PerfectionProgress==0 && refinement.CurrentUserId==(expectedLevel==0?0u:uid),"boost-live-refinement");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("pirate-passage:")) {
                var fields=command.Split(':');uint actor=UInt32.Parse(fields[1]);byte option=Byte.Parse(fields[2]);uint map=UInt32.Parse(fields[3]);
                Require(actor==4272 && option==151 && map==1700 || taoistWire && actor==10000 && option==154 && map==1700 || actor==3604 && option==90 && map==1004,"pirate-passage-allowed");
                CourierDialog(stream,crypto,memory,0,actor);SendNpc(stream,crypto,memory,actor,option);Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==map,"pirate-passage-map");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && (command.StartsWith("pirate-hit:") || eliminationWire && command.StartsWith("elimination-hit:"))) {
                uint monster=UInt32.Parse(command.Substring(command.IndexOf(':')+1));var here=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(eliminationWire || here.dwParam==1700,"pirate-hit-map");
                SendGame(stream,crypto,memory,new Attack(true){Attacker=uid,Attacked=monster,X=here.wParam1,Y=here.wParam2,AttackType=Attack.Melee}.ToArray());ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("pirate-pick:")) {
                var fields=command.Split(':');uint floor=UInt32.Parse(fields[1]),item=UInt32.Parse(fields[2]),type=UInt32.Parse(fields[3]);ushort x=UInt16.Parse(fields[4]),y=UInt16.Parse(fields[5]);
                Require(type==722732 || type==722736,"pirate-pick-flower");
                for(int i=0;i<2;i++){SendGame(stream,crypto,memory,new FloorItem(true){UID=floor,Type=FloorItem.Remove,X=x,Y=y,ItemID=type}.ToArray());ActionReply(stream,crypto,memory,uid,Data.SetLocation);}
                Require(bag.ContainsKey(item) && bag[item]==type,"pirate-pick-immediate-item");Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("pirate-dialog:")) {
                var parts=command.Split(':');Require(parts.Length==3,"pirate-dialog-command");uint actor=UInt32.Parse(parts[1]);byte option=Byte.Parse(parts[2]);
                Require(eliminationWire && actor>=300020 && actor<=300033 || actor>=199590 && actor<=199592 || taoistWire && (actor>=199580 && actor<=199584 || actor==10000),"epic-dialog-actor");
                if(eliminationWire)SendNpc(stream,crypto,memory,actor,option);else CourierDialog(stream,crypto,memory,option,actor);ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command=="taoist-enter" || command=="taoist-return") {
                if(command=="taoist-enter") {
                    CourierDialog(stream,crypto,memory,0,10000);Require(CourierDialog(stream,crypto,memory,150,10000).Contains(151),"taoist-entry-offer");SendNpc(stream,crypto,memory,10000,151);
                    Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==3933,"taoist-temple-entry");
                } else {
                    CourierDialog(stream,crypto,memory,0,199580);SendNpc(stream,crypto,memory,199580,254);
                    Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==1004,"taoist-trainer-return");
                }
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command=="pirate-enter") {
                Require(CourierDialog(stream,crypto,memory,0,4272).Contains(150),"pirate-entry-option");SendNpc(stream,crypto,memory,4272,150);
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==10271,"pirate-entry-map");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("elimination-state:")) {
                var fields=command.Split(':');Require(eliminationWire && fields.Length==3,"elimination-state-command");
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==UInt32.Parse(fields[1]),"elimination-map");
                Require(bag.Count==Int32.Parse(fields[2]),"elimination-bag-count");
                foreach(var item in bag)Require(boundBag.Contains(item.Key),"elimination-item-binding");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("pirate-use:")) {
                uint item=UInt32.Parse(command.Substring(11));
                SendGame(stream,crypto,memory,new ItemUsage(true){UID=item,Action=ItemUsage.Mode.EquipItem}.ToArray());ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("pirate-check:")) {
                var parts=command.Split(':');Require(parts.Length==4,"pirate-check-command");ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Require(currentCps==UInt32.Parse(parts[1]),"pirate-check-cps");var rows=parts[2].Split(';');Require(rows.Length==bag.Count,"pirate-check-item-count");
                foreach(var row in rows){var fields=row.Split(',');uint item=UInt32.Parse(fields[0]);Require(bag.ContainsKey(item) && bag[item]==UInt32.Parse(fields[1]) && boundBag.Contains(item)==(fields[2]=="1"),"pirate-check-item");}
                foreach(var spell in parts[3].Split(','))if(spell.Length>0)Require(pirateWireSpells.Contains(UInt16.Parse(spell)),"pirate-check-spell");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command=="pirate-search" || command=="pirate-search-replay") {
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==10266,"pirate-search-location");
                uint consumed=0;foreach(var entry in bag)if(entry.Value==3307443)consumed=entry.Key;
                Require(CourierDialog(stream,crypto,memory,0,199600).Contains(1),"pirate-search-menu");
                if(command=="pirate-search") {
                    Require(consumed!=0 && bag.Count==1,"pirate-search-earned-input");
                    Require(CourierDialog(stream,crypto,memory,1,199600).Contains(20),"pirate-search-offer");
                    SendNpc(stream,crypto,memory,199600,20);
                    Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==10271,"pirate-search-return");
                    Require(!bag.ContainsKey(consumed),"pirate-search-consumed-chart");
                }
                var saved=new Dictionary<uint,uint>(bag);
                SendNpc(stream,crypto,memory,199600,20);ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Require(bag.Count==1 && currentCps==73,"pirate-search-single-reward");
                foreach(var entry in bag) {
                    Require(saved.ContainsKey(entry.Key) && saved[entry.Key]==entry.Value,"pirate-search-no-duplicate");
                    Require(entry.Value==3307450 || entry.Value>=3307443 && entry.Value<=3307446,"pirate-search-valid-outcome");
                    if(entry.Value==3307450)Require(boundBag.Contains(entry.Key),"pirate-search-water-bound");
                }
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command=="pirate-buy" || command=="pirate-resume") {
                Require(CourierDialog(stream,crypto,memory,0,4272).Contains(150),"pirate-trainer-offer");
                SendNpc(stream,crypto,memory,4272,150);
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==10271,"pirate-hub-entry");
                Require(CourierDialog(stream,crypto,memory,0,199590).Contains(1),"pirate-chart-offer");
                if(command=="pirate-buy") {
                    Require(currentCps==100 && bag.Count==0,"pirate-before-buy");
                    Require(CourierDialog(stream,crypto,memory,1,199590).Contains(20),"pirate-chart-confirmation");
                    CourierDialog(stream,crypto,memory,20,199590);
                }
                CourierDialog(stream,crypto,memory,20,199590);
                ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Require(currentCps==73 && bag.Count==1 && bag.ContainsValue(3307443),"pirate-purchase-live-update-and-replay");
                uint chart=0;foreach(var entry in bag)if(entry.Value==3307443)chart=entry.Key;
                SendGame(stream,crypto,memory,new ItemUsage(true){UID=chart,Action=ItemUsage.Mode.EquipItem}.ToArray());
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==10266 && bag.ContainsKey(chart),"pirate-earned-chart-travel");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command=="anniv-start") {
                Require(CourierDialog(stream,crypto,memory,0,900028).Contains(8),"anniversary-offer");
                CourierDialog(stream,crypto,memory,8,900028);ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Require(bag.Count==1 && bag.ContainsValue(3001172),"anniversary-bead-update");
                Console.WriteLine("[team-wire] done anniv-start");Console.Out.Flush();continue;
            }
            if(command=="anniv-claim") {
                uint bead=0;foreach(var entry in bag)if(entry.Value==3001172)bead=entry.Key;
                Require(bead!=0,"anniversary-earned-bead");ulong goldBefore=currentGold,rvpBefore=currentRvp;
                for(int repeat=0;repeat<2;repeat++) {
                    SendGame(stream,crypto,memory,new ItemUsage(true){UID=bead,Action=ItemUsage.Mode.EquipItem}.ToArray());
                    ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                    int cards=0,souls=0;foreach(var entry in bag) {
                        if(entry.Value==3006048)cards++;
                        else if(entry.Value>=800000 && entry.Value<900000 && boundBag.Contains(entry.Key))souls++;
                    }
                    Console.WriteLine("[anniversary-wire] items="+bag.Count+" bead="+bag.ContainsKey(bead)+" cards="+cards+" bound-souls="+souls+" rvp="+currentRvp+" gold="+currentGold);
                    Require(!bag.ContainsKey(bead) && bag.Count==21 && cards==20 && souls==1 && currentRvp==rvpBefore+50000 && currentGold==goldBefore+50000000,"anniversary-earned-reward-updates");
                }
                Console.WriteLine("[team-wire] done anniv-claim");Console.Out.Flush();continue;
            }
            if(command=="combat-pk") {
                SendGame(stream,crypto,memory,new Data(true){UID=uid,ID=Data.ChangePKMode,dwParam=(uint)Revora.Game.Enums.PKMode.PK,TimeStamp=Time32.Now}.ToArray());
                ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Console.WriteLine("[team-wire] done combat-pk");Console.Out.Flush();continue;
            }
            if(command=="clan-lost") {
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==1002,"clan-loser-return");
                Require(moonDied && currentCps==0 && currentRvp==0 && bag.Count==0,"clan-loser-no-prize");
                Console.WriteLine("[team-wire] done clan-lost");Console.Out.Flush();continue;
            }
            if(command=="clan-enter") {
                moonDeathAllowed=true;
                var clanFrame=new byte[148];Writer.WriteUInt16(140,0,clanFrame);Writer.WriteUInt16(1313,2,clanFrame);clanFrame[4]=6;Writer.WriteUInt32(101895,12,clanFrame);
                SendGame(stream,crypto,memory,clanFrame);bool offered=false;
                for(int n=0;n<2048;n++) {
                    var reply=GameFrame(stream,crypto,memory);if(BitConverter.ToUInt16(reply,2)!=1313)continue;
                    Require(reply.Length==148 && reply[4]==6 && BitConverter.ToUInt32(reply,12)==101895 && reply[92]==1,"clan-entry-offer");offered=true;break;
                }
                Require(offered,"clan-entry-response");
                clanFrame=new byte[148];Writer.WriteUInt16(140,0,clanFrame);Writer.WriteUInt16(1313,2,clanFrame);clanFrame[4]=8;Writer.WriteUInt32(101895,12,clanFrame);SendGame(stream,crypto,memory,clanFrame);
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==1505,"clan-entry-map");
                Console.WriteLine("[team-wire] done clan-enter");Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("clan-prize:")) {
                var fields=command.Split(':');Require(fields.Length==5,"clan-prize-fields");
                uint cp=UInt32.Parse(fields[1]),item=UInt32.Parse(fields[3]),type=UInt32.Parse(fields[4]);ulong rvp=UInt64.Parse(fields[2]);
                Require(ActionReply(stream,crypto,memory,uid,Data.SetLocation).dwParam==1002,"clan-prize-return");
                Require(currentCps==cp && currentRvp==rvp && bag.Count==1 && bag.ContainsKey(item) && bag[item]==type && boundBag.Contains(item),"clan-prize-live-updates");
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command!=null && command.StartsWith("attack:")) {
                uint target;Require(UInt32.TryParse(command.Substring(7),out target) && target>=4294903001u && target<4294903017u && (target-4294903001u)/2!=(uint)teamWireRole/2,"team-attack-target");
                var here=ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                ActionReply(stream,crypto,memory,uid,Data.GetSurroundings);
                SendGame(stream,crypto,memory,new Attack(true){Attacker=uid,Attacked=target,X=(ushort)(here.wParam1+1),Y=here.wParam2,AttackType=Attack.Melee}.ToArray());
                ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();continue;
            }
            if(command=="claim") {
                Require(CourierDialog(stream,crypto,memory,0,regularTeamWire?417171u:801717u).Contains(3),"skill-team-claim-menu");
                CourierDialog(stream,crypto,memory,3,regularTeamWire?417171u:801717u);
                ActionReply(stream,crypto,memory,uid,Data.SetLocation);
                Console.WriteLine("[team-wire] done claim");Console.Out.Flush();continue;
            }
            if(command=="enter") {
                var options=CourierDialog(stream,crypto,memory,0,regularTeamWire?417171u:801717u);Require(options.Contains(2),"skill-team-wire-open-menu");
                SendNpc(stream,crypto,memory,regularTeamWire?417171u:801717u,2);
                var entered=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(entered.dwParam!=1002,"skill-team-wire-entry");
                Console.WriteLine("[team-wire] done enter");Console.Out.Flush();continue;
            }
            uint action;
            switch(command){case "create":action=0;break;case "invite":action=4;break;case "accept":action=3;break;case "kick":action=7;break;default:throw new InvalidOperationException("Unknown private team command.");}
            uint peer=4294903001u+(uint)(teamWireRole^1);
            var frame=new byte[32];Writer.WriteUInt16(24,0,frame);Writer.WriteUInt16(1023,2,frame);Writer.WriteUInt32(action,4,frame);Writer.WriteUInt32(command=="create"?uid:peer,8,frame);
            SendGame(stream,crypto,memory,frame);
            // A subsequent location response fences processing of the earlier command
            // on this same TCP stream. Membership is checked by the private host.
            var location=ActionReply(stream,crypto,memory,uid,Data.SetLocation);Require(location.dwParam==1002,"team-location");
            Console.WriteLine("[team-wire] done "+command);Console.Out.Flush();
        }
        throw new InvalidOperationException("Private team command bound exceeded.");
    }
}
