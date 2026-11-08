using System;
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
    static string Review395Fingerprint(object value)
    {
        if(value==null)return "null";
        Type type=value.GetType();
        if(type.IsPrimitive || type.IsEnum || value is string || value is Guid || value is DateTime)return type.Name+":"+value.ToString();
        Array array=value as Array;
        if(array!=null)return "["+string.Join("|",array.Cast<object>().Select(Review395Fingerprint))+"]";
        return "{"+string.Join("|",type.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public)
            .OrderBy(f=>f.Name,StringComparer.Ordinal).Select(f=>f.Name+"="+Review395Fingerprint(f.GetValue(value))))+"}";
    }
    static void Review395DerivedRules(object snapshot,DateTime clock,uint[] ids,uint[] levels,uint[] percents)
    {
        object item=IdentityField(((Array)IdentityField(snapshot,"Inscriptions")).GetValue(0),"Item");
        object purify=IdentityField(item,"Purification"), permanent=IdentityField(item,"PermanentRefinery"), temporary=IdentityField(item,"TemporaryRefinery");
        Action<object,string,object> set=(o,n,v)=>o.GetType().GetField(n).SetValue(o,v);
        Func<DateTime,object> derive=now=>{
            string before=Review395Fingerprint(snapshot);
            object image=Call(GuildIdentityType,null,"Derive",snapshot,now,ids,levels,percents);
            Check((bool)IdentityField(image,"Ok"),"REVIEW-395 derivation succeeds: "+IdentityField(image,"Detail"));
            Check(before==Review395Fingerprint(snapshot),"REVIEW-395 each derivation preserves every raw snapshot field");
            return ((Array)IdentityField(image,"Items")).GetValue(0);
        };
        Func<object,string,string> status=(o,n)=>IdentityField(o,n).ToString();
        set(purify,"Duration",(uint)0);set(purify,"AddedOnRaw",long.MaxValue);
        Check(status(IdentityField(derive(clock),"Purification"),"Status")=="Malformed","REVIEW-395 permanent purification still decodes its saved date");
        set(purify,"AddedOnRaw",clock.ToBinary());
        set(item,"Days",0);set(item,"DayStampRaw",long.MaxValue);
        Check(status(derive(clock),"DayStatus")=="Malformed","REVIEW-395 zero days cannot hide a malformed loader date");
        set(item,"DayStampRaw",clock.ToBinary());set(item,"Lock",1);set(item,"UnlockEndRaw",long.MaxValue);
        Check(status(derive(clock),"LockStatus")=="Malformed","REVIEW-395 non-timed locks still decode the saved date");
        set(item,"Lock",2);set(item,"UnlockEndRaw",clock.ToBinary());
        foreach(int offset in new[]{-1,0,1}){
            object d=derive(clock.AddSeconds(offset));
            Check(status(d,"LockStatus")== (offset<0?"Active":"Expired") && (bool)IdentityField(d,"LoaderWouldClearLock")== (offset>=0),"REVIEW-395 lock before at and after unlock");
        }
        set(item,"Days",1);set(item,"DayStampRaw",clock.AddDays(-1).ToBinary());
        foreach(int offset in new[]{-1,0,1}){
            object d=derive(clock.AddSeconds(offset));
            Check(status(d,"DayStatus")== (offset<0?"Active":"Expired"),"REVIEW-395 day before at and after expiry");
        }
        foreach(long seconds in new[]{2147483648L,4294967295L,4294967296L}){
            set(item,"DayStampRaw",clock.AddSeconds(seconds).AddDays(-1).ToBinary());
            object d=derive(clock);
            Check(status(d,"DayStatus")== (seconds<=uint.MaxValue?"Active":"Overflow")
                && (seconds>uint.MaxValue || (uint)IdentityField(d,"TimeLeftSeconds")==seconds),"REVIEW-395 item seconds use the full uint range without wrapping");
        }
        set(item,"Days",int.MaxValue);
        Check(status(derive(clock),"DayStatus")=="Malformed","REVIEW-395 out-of-byte day count cannot wrap duration arithmetic");
        set(item,"Days",0);set(item,"DayStampRaw",clock.ToBinary());
        set(purify,"Duration",(uint)1);set(purify,"AddedOnRaw",clock.AddSeconds(-1).ToBinary());
        foreach(double offset in new[]{-1.0,-0.5,0.0,1.0}){
            object effect=IdentityField(derive(clock.AddSeconds(offset)),"Purification");
            Check(status(effect,"Status")== (offset<=-1?"Active":"Expired"),"REVIEW-395 purification uses truncated signed seconds including the fractional boundary");
        }
        set(purify,"AddedOnRaw",clock.ToBinary());set(purify,"Duration",uint.MaxValue);
        Check(status(IdentityField(derive(clock),"Purification"),"Status")=="Overflow","REVIEW-395 purification retains its signed packet range");
        set(purify,"Duration",(uint)0);
        set(temporary,"AddedOnRaw",clock.ToBinary());set(temporary,"Duration",uint.MaxValue);
        object far=derive(clock);
        Check(status(IdentityField(far,"TemporaryRefinery"),"Status")=="Active" && (uint)IdentityField(far,"CurrentRefineryId")==990002,"REVIEW-395 refinery selection is a date comparison without signed seconds overflow");
        set(temporary,"Duration",(uint)120);set(temporary,"AddedOnRaw",clock.AddSeconds(-120).ToBinary());
        var stateType=typeof(Entity).Assembly.GetType("Revora.Game.Features.RefineryProgress+State");
        object state=Activator.CreateInstance(stateType,true);
        var livePermanent=new ItemAdding.Refinery_{Available=true,EffectID=990001,AddedOn=clock,EffectDuration=0};
        var liveTemporary=new ItemAdding.Refinery_{Available=true,EffectID=990002,AddedOn=clock.AddSeconds(-120),EffectDuration=120};
        var instanceFields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        stateType.GetField("Permanent",instanceFields).SetValue(state,livePermanent);stateType.GetField("Temporary",instanceFields).SetValue(state,liveTemporary);
        foreach(int offset in new[]{-1,0,1}){
            uint expected=offset<0?990002u:990001u;
            object d=derive(clock.AddSeconds(offset));
            var actual=(ItemAdding.Refinery_)stateType.GetMethod("Current",instanceFields).Invoke(state,new object[]{clock.AddSeconds(offset)});
            Check((uint)IdentityField(d,"CurrentRefineryId")==expected && actual.EffectID==expected,"REVIEW-395 refinery boundary agrees with independently specified expectation and live read-only selection");
        }
        set(temporary,"Duration",(uint)0);
        Check(status(IdentityField(derive(clock),"TemporaryRefinery"),"Status")=="Malformed","REVIEW-395 conflicting temporary slot is not a permanent effect");
        set(temporary,"Duration",(uint)120);set(permanent,"Duration",(uint)1);
        Check(status(IdentityField(derive(clock),"PermanentRefinery"),"Status")=="Malformed","REVIEW-395 conflicting permanent slot remains malformed");
        set(permanent,"Duration",(uint)0);set(temporary,"AddingId",990099u);
        object unknown=derive(clock);
        Check(IdentityField(unknown,"CurrentRefineryId")==null && IdentityField(unknown,"DerivedCredit")==null,"REVIEW-395 rejected refinery state does not expose usable derived credit");
        set(temporary,"AddingId",990002u);
        foreach(uint[] badIds in new[]{new uint[]{990001,990001},new uint[]{990001,990002,990002},new uint[]{0,990002}}){
            uint[] badLevels=badIds.Length==3?new uint[]{5,4,6}:levels;
            uint[] badPercents=badIds.Length==3?new uint[]{44,15,99}:percents;
            object image=Call(GuildIdentityType,null,"Derive",snapshot,clock,badIds,badLevels,badPercents);
            Check(!(bool)IdentityField(image,"Ok") && ((Array)IdentityField(image,"Items")).Length==0,"REVIEW-395 duplicate or zero catalog keys discard the entire image");
        }
        object uneven=Call(GuildIdentityType,null,"Derive",snapshot,clock,ids,new uint[0],percents);
        Check(!(bool)IdentityField(uneven,"Ok") && ((Array)IdentityField(uneven,"Items")).Length==0,"REVIEW-395 uneven catalog discards the image");
        set(item,"PermanentRefinery",null);set(item,"TemporaryRefinery",null);set(item,"RefineItem",724440u);set(item,"RefineryTimeRaw",0L);
        object legacy=derive(clock);
        Check(!(bool)IdentityField(legacy,"LoaderWouldProjectRefinery") && !(bool)IdentityField(legacy,"LoaderWouldReplaceRefineItem") && IdentityField(legacy,"DerivedCredit")==null,"REVIEW-395 no addition rows preserve an unexpired legacy refinery id");
        set(item,"RefineryTimeRaw",clock.AddSeconds(-1).ToBinary());
        object cleared=derive(clock);
        Check((bool)IdentityField(cleared,"LoaderWouldClearSavedRefinery") && (bool)IdentityField(cleared,"LoaderWouldProjectRefinery"),"REVIEW-395 saved expiry clears legacy id before refinery Load guard");
        set(item,"RefineItem",0u);set(item,"RefineryTimeRaw",0L);
        Check((bool)IdentityField(derive(clock),"LoaderWouldProjectRefinery"),"REVIEW-395 empty refinery state still projects when the saved id is zero");
        set(item,"PermanentRefinery",permanent);set(item,"TemporaryRefinery",temporary);
        set(permanent,"StabilizationPoints",500);set(temporary,"StabilizationPoints",20);
        Check((int)IdentityField(derive(clock),"DerivedCredit")==500,"REVIEW-395 stored credit above permanent cost survives");
        set(item,"PermanentRefinery",null);set(temporary,"StabilizationPoints",0);
        Check((int)IdentityField(derive(clock),"DerivedCredit")==0,"REVIEW-395 stored zero credit differs from absent rows");
    }
    static void Review400Scores(object snapshot,DateTime clock,uint weaponUid,int registries,int ads,int sessions,int unions,GameState member){
        object item=IdentityField(((Array)IdentityField(snapshot,"Inscriptions")).GetValue(0),"Item");
        object purify=IdentityField(item,"Purification"),permanent=IdentityField(item,"PermanentRefinery"),temporary=IdentityField(item,"TemporaryRefinery");
        uint savedId=(uint)IdentityField(item,"Id");int savedPlus=(int)IdentityField(item,"Plus");
        int savedOne=(int)IdentityField(item,"SocketOne");int savedTwo=(int)IdentityField(item,"SocketTwo");
        int savedBless=(int)IdentityField(item,"Bless");uint savedRefine=(uint)IdentityField(item,"RefineItem");
        long savedTime=(long)IdentityField(item,"RefineryTimeRaw");uint savedPerfection=(uint)IdentityField(item,"PerfectionLevel");
        long purifyAdded=(long)IdentityField(purify,"AddedOnRaw");uint purifyDuration=(uint)IdentityField(purify,"Duration");
        long temporaryAdded=(long)IdentityField(temporary,"AddedOnRaw");uint temporaryPercent=(uint)IdentityField(temporary,"Percent");
        uint permanentDuration=(uint)IdentityField(permanent,"Duration");
        Action<object,string,object> set=(o,n,v)=>o.GetType().GetField(n).SetValue(o,v);
        uint[] ids=new uint[]{990001,990002};uint[] levels=new uint[]{5,4};uint[] percents=new uint[]{44,15};
        uint[] itemIds=new uint[]{410009};int[] itemLevels=new int[]{100};
        int[] sorts=new int[]{20,15,12,10,11,9,6,4,13,14};
        int[] mins=new int[]{1,1,1,1,1,1,1,1,1,1};
        int[] maxes=new int[]{200,7,12,2,9,9,27,255,20,20};
        int[] values=new int[]{100,4,50,30,7,9,20,10,11,11};
        string itemSql="SELECT hashtextextended(row_to_json(i)::text,0) FROM items i WHERE uid=@uid";
        string additionSql="SELECT hashtextextended(string_agg(row_to_json(a)::text,'|' ORDER BY iditemadding),0) FROM itemadding a WHERE uid=@uid";
        long itemHash=Scalar(itemSql,weaponUid),additionHash=Scalar(additionSql,weaponUid);
        Func<uint[],uint[],uint[],uint[],int[],int[],int[],int[],int[],object> score=(cids,clevels,cpercents,iids,ilevels,csorts,cmins,cmaxes,cvalues)=>{
            string before=Review395Fingerprint(snapshot);
            object image=Call(GuildIdentityType,null,"DeriveScores",snapshot,clock,cids,clevels,cpercents,iids,ilevels,csorts,cmins,cmaxes,cvalues);
            Check(before==Review395Fingerprint(snapshot),"REVIEW-400 scoring leaves every raw snapshot field unchanged");
            return image;
        };
        Func<object,object> row=image=>((Array)IdentityField(image,"Items")).GetValue(0);
        object scored=score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values);
        object effect=row(scored);
        var stateType=typeof(Entity).Assembly.GetType("Revora.Game.Features.RefineryProgress+State");
        var stateFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        object state=Activator.CreateInstance(stateType,true);
        stateType.GetField("Permanent",stateFlags).SetValue(state,new ItemAdding.Refinery_{Available=true,EffectID=990001,EffectLevel=5,EffectPercent=44,EffectDuration=0,AddedOn=clock});
        stateType.GetField("Temporary",stateFlags).SetValue(state,new ItemAdding.Refinery_{Available=true,EffectID=990002,EffectLevel=4,EffectPercent=15,EffectDuration=120,AddedOn=clock.AddSeconds(-30)});
        object liveCurrent=stateType.GetMethod("Current",stateFlags).Invoke(state,new object[]{clock});
        Check((bool)IdentityField(scored,"Ok") && (bool)IdentityField(scored,"PacketSecondsTruncate")
            && (string)IdentityField(scored,"Detail")=="scored"
            && (uint)IdentityField(effect,"RawDonation")==456 && (uint)IdentityField(effect,"DerivedDonation")==1049990
            && !((bool)IdentityField(effect,"DonationMatches")) && (int)IdentityField(effect,"DerivedBattlePower")==14
            && IdentityField(effect,"PrestigeStatus").ToString()=="UnknownCatalog" && IdentityField(effect,"PrestigeScore")==null
            && (bool)IdentityField(effect,"PacketWouldIncludePurification") && (int)IdentityField(effect,"PacketSeconds")==60
            && (bool)IdentityField(effect,"PacketWouldIncludeRefinery") && (uint)IdentityField(effect,"PacketEffectId")==990002
            && (uint)IdentityField(effect,"RefineryPacketSeconds")==90
            && IdentityField(effect,"SoulCatalogStatus").ToString()=="Active" && (uint)IdentityField(effect,"SoulEffectId")==990002 && (uint)IdentityField(effect,"SoulSeconds")==90
            && (uint)IdentityField(effect,"CurrentRefineryId")==(uint)liveCurrent.GetType().GetField("EffectID").GetValue(liveCurrent)
            && (Guid)IdentityField(effect,"Token")==(Guid)IdentityField(item,"Token")
            && (long)IdentityField(effect,"DayStampRaw")==(long)IdentityField(item,"DayStampRaw")
            && ItemHandler.GetPositionFromID(410009)==ItemHandler.Positions.Right,
            "REVIEW-400 donation, battle power, and packet seconds stay explicit while the missing saved refinery is unknown");
        object again=score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values);
        Check(!ReferenceEquals(row(scored),row(again)) && !ReferenceEquals(IdentityField(effect,"Purification"),IdentityField(row(again),"Purification")),
            "REVIEW-400 scored items and nested effects are independent objects");
        object plain=Call(GuildIdentityType,null,"Derive",snapshot,clock,ids,levels,percents);
        Check((bool)IdentityField(plain,"Ok") && !((bool)IdentityField(plain,"PacketSecondsTruncate"))
            && (uint)IdentityField(row(plain),"DerivedDonation")==0 && IdentityField(row(plain),"PrestigeScore")==null
            && !((bool)IdentityField(row(plain),"PacketWouldIncludeRefinery")),
            "REVIEW-400 Derive leaves the score and packet fields unset");
        set(item,"RefineItem",0u);
        object open=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(open,"PrestigeStatus").ToString()=="Active" && (int)IdentityField(open,"PrestigeScore")==211
            && (uint)IdentityField(open,"DerivedDonation")==1049990,
            "REVIEW-400 supplied bands score the active purification without a saved refinery id");
        int[] materialMaxes=(int[])maxes.Clone();materialMaxes[9]=5;int[] materialMins=(int[])mins.Clone();materialMins[9]=5;
        set(item,"RefineItem",990001u);
        object material=row(score(ids,levels,percents,itemIds,itemLevels,sorts,materialMins,materialMaxes,values));
        Check((int)IdentityField(material,"PrestigeScore")==222,"REVIEW-400 material phase uses the saved refinery catalog level");
        set(item,"RefineItem",990002u);
        object missed=row(score(ids,levels,percents,itemIds,itemLevels,sorts,materialMins,materialMaxes,values));
        Check(IdentityField(missed,"PrestigeStatus").ToString()=="UnknownCatalog" && IdentityField(missed,"PrestigeScore")==null
            && (uint)IdentityField(missed,"DerivedDonation")==1049990,
            "REVIEW-400 a saved refinery level outside its band does not invent a material score");
        set(item,"RefineItem",0u);set(item,"PerfectionLevel",28u);
        object perfected=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((int)IdentityField(perfected,"PrestigeScore")==316,"REVIEW-400 perfection past 27 adds 100 and keeps the capped band");
        set(item,"PerfectionLevel",uint.MaxValue);
        object huge=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(huge,"PrestigeStatus").ToString()=="Overflow" && IdentityField(huge,"PrestigeScore")==null
            && (uint)IdentityField(huge,"DerivedDonation")==1049990,
            "REVIEW-400 a perfection total above a signed integer is overflow and does not wrap");
        set(item,"PerfectionLevel",savedPerfection);set(item,"Bless",0);
        object unprotected=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((int)IdentityField(unprotected,"PrestigeScore")==207,"REVIEW-400 bless zero skips protection");
        set(item,"Bless",savedBless);
        set(purify,"AddedOnRaw",clock.AddSeconds(-100).ToBinary());
        object expiredSoul=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(IdentityField(expiredSoul,"Purification"),"Status").ToString()=="Expired"
            && !((bool)IdentityField(expiredSoul,"PacketWouldIncludePurification"))
            && (int)IdentityField(expiredSoul,"PrestigeScore")==200,
            "REVIEW-400 an expired purification is omitted from the packet and from soul phase");
        set(purify,"AddedOnRaw",purifyAdded);
        uint[] worn=new uint[]{181009,2100009,200009,350009};
        for(int i=0;i<worn.Length;i++){
            set(item,"Id",worn[i]);
            object skipped=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
            Check(IdentityField(skipped,"PrestigeStatus").ToString()=="StoredZero" && (int)IdentityField(skipped,"PrestigeScore")==0
                && (int)IdentityField(skipped,"DerivedBattlePower")==14,
                "REVIEW-400 garment, bottle, steed armor, and accessory prestige stay zero: "+worn[i]);
        }
        set(item,"Id",savedId);set(item,"RefineItem",savedRefine);
        set(item,"Plus",12);set(item,"SocketOne",1);set(item,"SocketTwo",11);
        object both=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(both,"DerivedDonation")==1362330 && (int)IdentityField(both,"DerivedBattlePower")==18,
            "REVIEW-400 plus 12 and two sockets use the both-socket donation and do not add a gem-three bonus");
        set(item,"Plus",0);set(item,"SocketOne",0);set(item,"SocketTwo",0);
        object bare=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(bare,"DerivedDonation")==16660 && (int)IdentityField(bare,"DerivedBattlePower")==4,
            "REVIEW-400 plus zero and no sockets donate only the super quality");
        set(item,"SocketOne",3);
        object gemThree=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(gemThree,"DerivedDonation")==49990 && (int)IdentityField(gemThree,"DerivedBattlePower")==6,
            "REVIEW-400 one socket whose last digit is 3 adds the battle-power gem bonus");
        set(item,"SocketOne",0);set(item,"SocketTwo",1);
        object secondOnly=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(secondOnly,"DerivedDonation")==16660 && (int)IdentityField(secondOnly,"DerivedBattlePower")==5,
            "REVIEW-400 a second socket without the first adds no donation");
        set(item,"SocketTwo",0);set(item,"Plus",1);
        object plusOne=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(plusOne,"DerivedDonation")==16750 && (int)IdentityField(plusOne,"DerivedBattlePower")==5,
            "REVIEW-400 plus 1 adds its donation step");
        set(item,"Plus",13);
        object plusPast=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(plusPast,"DerivedDonation")==16660 && (int)IdentityField(plusPast,"DerivedBattlePower")==17,
            "REVIEW-400 plus outside 1 through 12 adds no donation and still adds battle power");
        set(item,"Plus",0);set(item,"Id",410008u);
        object normal=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(normal,"DerivedDonation")==1000 && (int)IdentityField(normal,"DerivedBattlePower")==3,
            "REVIEW-400 quality 8 donates 1000");
        set(item,"Id",410007u);
        object elite=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(elite,"DerivedDonation")==0 && (int)IdentityField(elite,"DerivedBattlePower")==2,
            "REVIEW-400 quality below 8 donates nothing");
        set(item,"Id",500009u);
        object bow=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        set(item,"Id",421009u);
        object backsword=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        set(item,"Id",612009u);
        object pistol=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((int)IdentityField(bow,"DerivedBattlePower")==8 && (int)IdentityField(backsword,"DerivedBattlePower")==8
            && (int)IdentityField(pistol,"DerivedBattlePower")==4,
            "REVIEW-400 battle power doubles ids starting with 5 and family 421, not every two-handed family");
        set(item,"Id",savedId);set(item,"Plus",savedPlus);set(item,"SocketOne",savedOne);set(item,"SocketTwo",savedTwo);
        set(purify,"AddedOnRaw",clock.AddSeconds(-98.5).ToBinary());set(purify,"Duration",(uint)100);
        object fraction=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((int)IdentityField(fraction,"PacketSeconds")==1 && (long)IdentityField(IdentityField(fraction,"Purification"),"SecondsRemaining")==1
            && (bool)IdentityField(fraction,"PacketWouldIncludePurification"),
            "REVIEW-400 packet seconds truncate a fractional remainder toward zero");
        set(purify,"AddedOnRaw",purifyAdded);set(purify,"Duration",purifyDuration);
        set(temporary,"Percent",0u);
        object silent=row(score(ids,levels,new uint[]{44,0},itemIds,itemLevels,sorts,mins,maxes,values));
        Check((bool)IdentityField(silent,"PacketWouldIncludeRefinery") && IdentityField(silent,"RefineryPacketSeconds")==null
            && (uint)IdentityField(silent,"PacketEffectId")==990002,
            "REVIEW-400 a zero refinery percent includes the effect and omits the time");
        set(temporary,"Percent",temporaryPercent);
        set(temporary,"AddedOnRaw",clock.AddYears(1000).ToBinary());
        object distant=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(IdentityField(distant,"TemporaryRefinery"),"Status").ToString()=="Active"
            && (long)IdentityField(IdentityField(distant,"TemporaryRefinery"),"SecondsRemaining")>uint.MaxValue
            && !(bool)IdentityField(distant,"PacketWouldIncludeRefinery") && IdentityField(distant,"RefineryPacketSeconds")==null
            && IdentityField(distant,"RefineryPacketStatus").ToString()=="Overflow" && IdentityField(distant,"SoulCatalogStatus").ToString()=="Overflow",
            "REVIEW-400 a refinery interval above uint is omitted instead of wrapped");
        set(temporary,"AddedOnRaw",temporaryAdded);
        string beforeFallback=Review395Fingerprint(snapshot);
        object fallbackRow=row(Call(GuildIdentityType,null,"DeriveScores",snapshot,clock.AddSeconds(90),ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        object liveFallback=stateType.GetMethod("Current",stateFlags).Invoke(state,new object[]{clock.AddSeconds(90)});
        Check(beforeFallback==Review395Fingerprint(snapshot)
            && (uint)IdentityField(fallbackRow,"CurrentRefineryId")==990001
            && (uint)IdentityField(fallbackRow,"CurrentRefineryId")==(uint)liveFallback.GetType().GetField("EffectID").GetValue(liveFallback)
            && IdentityField(fallbackRow,"RefineryPacketSeconds")==null
            && IdentityField(fallbackRow,"RefineryPacketStatus").ToString()=="Unresolved" && (uint)IdentityField(fallbackRow,"SoulSeconds")==0
            && (uint)IdentityField(fallbackRow,"PacketEffectId")==990001,
            "REVIEW-401 permanent fallback has zero ItemSoul seconds while negative Append seconds remain unresolved");
        set(permanent,"Duration",(uint)5);
        object conflict=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((bool)IdentityField(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values),"Ok")
            && IdentityField(IdentityField(conflict,"PermanentRefinery"),"Status").ToString()=="Malformed"
            && IdentityField(conflict,"CurrentRefineryId")==null && !((bool)IdentityField(conflict,"PacketWouldIncludeRefinery"))
            && (uint)IdentityField(conflict,"DerivedDonation")==1049990,
            "REVIEW-400 a permanent slot with a duration does not invent a current refinery");
        set(permanent,"Duration",permanentDuration);
        set(item,"PermanentRefinery",null);set(item,"TemporaryRefinery",null);set(item,"Purification",null);set(item,"RefineItem",724440u);
        object legacy=row(score(new uint[]{724440},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(legacy,"SoulCatalogStatus").ToString()=="Active" && (uint)IdentityField(legacy,"SoulEffectId")==724440
            && IdentityField(legacy,"PacketEffectId")==null && !(bool)IdentityField(legacy,"PacketWouldIncludeRefinery") && (bool)IdentityField(legacy,"SoulWouldInclude") && !((bool)IdentityField(legacy,"LoaderWouldReplaceRefineItem"))
            && (uint)IdentityField(item,"RefineItem")==724440,
            "REVIEW-401 a legacy refinery emits only the unrewritten ItemSoul id and is not a replacement");
        object legacyMiss=row(score(new uint[]{990001},new uint[]{5},new uint[]{44},itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(legacyMiss,"SoulCatalogStatus").ToString()=="UnknownCatalog" && IdentityField(legacyMiss,"PacketEffectId")==null,
            "REVIEW-400 a legacy id missing from the catalog does not invent a packet effect");
        set(item,"RefineItem",3004140u);
        object legacyWide=row(score(new uint[]{3004140},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values));
        set(item,"RefineItem",724439u);
        object legacyBelow=row(score(new uint[]{724439},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values));
        set(item,"RefineItem",724445u);
        object legacyAbove=row(score(new uint[]{724445},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(legacyWide,"SoulEffectId")==3004140 && (uint)IdentityField(legacyBelow,"SoulEffectId")==724439
            && (uint)IdentityField(legacyAbove,"SoulEffectId")==724445 && IdentityField(legacyWide,"PacketEffectId")==null,
            "REVIEW-401 ItemSoul preserves legacy ids on both sides of the ItemAdding rewrite range");
        set(item,"RefineItem",724440u);set(item,"RefineryTimeRaw",clock.AddSeconds(-1).ToBinary());
        object legacyCleared=row(score(new uint[]{724440},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values));
        Check((bool)IdentityField(legacyCleared,"LoaderWouldClearSavedRefinery") && (bool)IdentityField(legacyCleared,"LoaderWouldReplaceRefineItem")
            && !((bool)IdentityField(legacyCleared,"PacketWouldIncludeRefinery")) && IdentityField(legacyCleared,"PacketEffectId")==null
            && !(bool)IdentityField(legacyCleared,"SoulWouldInclude") && IdentityField(legacyCleared,"SoulEffectId")==null,
            "REVIEW-400 expiry clears a legacy id before the packet would include it");
        set(item,"PermanentRefinery",permanent);set(item,"TemporaryRefinery",temporary);set(item,"Purification",purify);
        set(item,"RefineItem",savedRefine);set(item,"RefineryTimeRaw",savedTime);
        object overlap=score(ids,levels,percents,itemIds,itemLevels,new int[]{12,12},new int[]{1,10},new int[]{12,12},new int[]{50,50});
        Check(!((bool)IdentityField(overlap,"Ok")) && ((Array)IdentityField(overlap,"Items")).Length==0
            && (string)IdentityField(overlap,"Detail")=="ambiguous catalog",
            "REVIEW-400 overlapping score bands reject the whole image");
        object repeated=score(ids,levels,percents,new uint[]{410009,410009},new int[]{100,90},sorts,mins,maxes,values);
        Check(!((bool)IdentityField(repeated,"Ok")) && ((Array)IdentityField(repeated,"Items")).Length==0,
            "REVIEW-400 one item id with two levels rejects the whole image");
        set(item,"RefineItem",0u);
        object sameLevel=row(score(ids,levels,percents,new uint[]{410009,410009},new int[]{100,100},sorts,mins,maxes,values));
        Check((int)IdentityField(sameLevel,"PrestigeScore")==211,"REVIEW-400 the same item id and level is one catalog entry");
        set(item,"RefineItem",savedRefine);
        object zeroKey=score(new uint[]{0},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values);
        object missing=score(ids,levels,percents,null,null,sorts,mins,maxes,values);
        object uneven=score(ids,levels,percents,itemIds,itemLevels,sorts,new int[0],maxes,values);
        Check(!((bool)IdentityField(zeroKey,"Ok")) && (string)IdentityField(zeroKey,"Detail")=="malformed catalog"
            && !((bool)IdentityField(missing,"Ok")) && (string)IdentityField(missing,"Detail")=="missing catalog"
            && !((bool)IdentityField(uneven,"Ok")) && ((Array)IdentityField(uneven,"Items")).Length==0,
            "REVIEW-400 a zero, missing, or uneven catalog returns no scored items");
        object badLevel=score(ids,levels,percents,itemIds,new int[]{0},sorts,mins,maxes,values);
        Check(!(bool)IdentityField(badLevel,"Ok") && ((Array)IdentityField(badLevel,"Items")).Length==0 && member.Guild==null,
            "REVIEW-400 a nonpositive item level is malformed and does not disconnect");
        int[] overflowValues=(int[])values.Clone();overflowValues[0]=int.MaxValue;
        object overflow=row(score(ids,levels,percents,itemIds,new int[]{200},sorts,mins,maxes,overflowValues));
        Check(IdentityField(overflow,"PrestigeStatus").ToString()=="Overflow" && IdentityField(overflow,"PrestigeScore")==null
            && (uint)IdentityField(overflow,"DerivedDonation")==1049990,
            "REVIEW-400 a scaled item-level product above a signed integer is overflow");
        object noBand=row(score(ids,levels,percents,itemIds,itemLevels,new int[0],new int[0],new int[0],new int[0]));
        Check(IdentityField(noBand,"PrestigeStatus").ToString()=="UnknownCatalog" && (uint)IdentityField(noBand,"DerivedDonation")==1049990,
            "REVIEW-400 a missing ability band is unknown for that item");
        int[] tight=new int[]{int.MaxValue-5,10,50,30,7,9,20,10,11,11};
        object sum=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,tight));
        Check(IdentityField(sum,"PrestigeStatus").ToString()=="Overflow" && IdentityField(sum,"PrestigeScore")==null,
            "REVIEW-400 a running prestige total that leaves the signed range is overflow");
        // REVIEW-401: invalid raw values must never produce plausible donation or wrapped battle power.
        foreach(uint invalidId in new uint[]{0,uint.MaxValue}){
            set(item,"Id",invalidId);object bad=score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values);
            Check(!(bool)IdentityField(bad,"Ok") && ((Array)IdentityField(bad,"Items")).Length==0,"REVIEW-401 zero or signed-quality-overflow item id rejects atomically");
        }
        set(item,"Id",savedId);
        foreach(string field in new string[]{"Plus","SocketOne","SocketTwo","Bless","Enchant"}){
            int original=(int)IdentityField(item,field);
            foreach(int invalid in new int[]{-1,256,int.MaxValue}){
                set(item,field,invalid);object bad=score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values);
                Check(!(bool)IdentityField(bad,"Ok") && ((Array)IdentityField(bad,"Items")).Length==0,"REVIEW-401 malformed byte input rejects whole score image: "+field);
            }
            set(item,field,original);
        }
        object[] catalogs=new object[]{
            score(ids,levels,percents,new uint[]{410009,0},new int[]{100,100},sorts,mins,maxes,values),
            score(ids,new uint[]{uint.MaxValue,4},percents,itemIds,itemLevels,sorts,mins,maxes,values),
            score(ids,levels,percents,itemIds,itemLevels,new int[]{12},new int[]{12},new int[]{1},new int[]{1}),
            score(ids,levels,percents,itemIds,itemLevels,new int[]{12},new int[]{-1},new int[]{12},new int[]{1}),
            score(ids,levels,percents,itemIds,itemLevels,new int[]{12},new int[]{1},new int[]{12},new int[]{-1}),
            score(ids,levels,percents,itemIds,itemLevels,new int[]{999},new int[]{1},new int[]{12},new int[]{1})};
        foreach(object bad in catalogs) Check(!(bool)IdentityField(bad,"Ok") && ((Array)IdentityField(bad,"Items")).Length==0,"REVIEW-401 malformed catalogs reject atomically even if not consulted");
        set(item,"RefineItem",0u);set(purify,"AddedOnRaw",long.MaxValue);
        object badSoul=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(badSoul,"PrestigeStatus").ToString()=="Malformed" && IdentityField(badSoul,"PrestigeScore")==null,"REVIEW-401 malformed purification cannot silently reduce prestige");
        set(purify,"AddedOnRaw",purifyAdded);uint oldSoulLevel=(uint)IdentityField(purify,"Level");set(purify,"Level",uint.MaxValue);
        object wideSoul=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(wideSoul,"PrestigeStatus").ToString()=="Overflow" && IdentityField(wideSoul,"PrestigeScore")==null,"REVIEW-401 soul level cannot wrap to a signed band key");
        set(purify,"Level",oldSoulLevel);set(item,"RefineItem",savedRefine);
        uint oldDuration=(uint)IdentityField(temporary,"Duration");
        set(temporary,"Duration",1u);set(temporary,"AddedOnRaw",clock.AddMilliseconds(-500).ToBinary());
        object half=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(IdentityField(half,"TemporaryRefinery"),"Status").ToString()=="Active"
            && IdentityField(half,"RefineryPacketStatus").ToString()=="Expired" && !(bool)IdentityField(half,"PacketWouldIncludeRefinery")
            && (uint)IdentityField(half,"SoulSeconds")==0 && (bool)IdentityField(half,"SoulWouldInclude"),"REVIEW-401 half-second active state is rejected by Append but preserved in ItemSoul");
        set(temporary,"Percent",0u);
        object zeroPercent=row(score(ids,levels,new uint[]{44,0},itemIds,itemLevels,sorts,mins,maxes,values));
        Check((bool)IdentityField(zeroPercent,"PacketWouldIncludeRefinery") && IdentityField(zeroPercent,"RefineryPacketSeconds")==null,"REVIEW-401 zero percent bypasses Append second cutoff");
        set(temporary,"Percent",temporaryPercent);set(temporary,"Duration",oldDuration);
        set(temporary,"AddedOnRaw",clock.AddSeconds((double)int.MaxValue + 1000).ToBinary());
        object signedEdge=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(signedEdge,"RefineryPacketStatus").ToString()=="Overflow" && IdentityField(signedEdge,"RefineryPacketSeconds")==null
            && (uint)IdentityField(signedEdge,"SoulSeconds")>(uint)int.MaxValue,"REVIEW-401 signed Append and unsigned ItemSoul bounds differ");
        set(temporary,"AddedOnRaw",temporaryAdded);
        uint originalEffect=(uint)IdentityField(temporary,"AddingId");set(temporary,"AddingId",724440u);
        object rewrite=row(score(new uint[]{990001,724440},levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(rewrite,"PacketEffectId")==301 && (uint)IdentityField(rewrite,"SoulEffectId")==724440,"REVIEW-401 actual addition rewrites only the ItemAdding id");
        set(temporary,"AddingId",originalEffect);
        set(item,"TemporaryRefinery",null);set(item,"PermanentRefinery",null);set(item,"RefineItem",724440u);set(item,"RefineryTimeRaw",long.MaxValue);
        object badLegacy=row(score(new uint[]{724440},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values));
        Check(IdentityField(badLegacy,"SoulCatalogStatus").ToString()=="Malformed" && !(bool)IdentityField(badLegacy,"SoulWouldInclude"),"REVIEW-401 malformed legacy expiry never advertises a packet");
        set(item,"RefineryTimeRaw",clock.AddSeconds(90.5).ToBinary());
        object timedLegacy=row(score(new uint[]{724440},new uint[]{1},new uint[]{1},itemIds,itemLevels,sorts,mins,maxes,values));
        Check((uint)IdentityField(timedLegacy,"SoulSeconds")==90 && !(bool)IdentityField(timedLegacy,"PacketWouldIncludeRefinery"),"REVIEW-401 legacy ItemSoul time truncates separately from absent addition");
        set(item,"RefineItem",savedRefine);set(item,"RefineryTimeRaw",savedTime);set(item,"TemporaryRefinery",temporary);set(item,"PermanentRefinery",permanent);
        var liveArsenal=new Revora.Game.Features.Arsenal(0);
        foreach(uint id in new uint[]{410008,410009,500009,421009,612009}){
            foreach(byte plus in new byte[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,255}){
                foreach(byte socket in new byte[]{0,3,255}){
                    // Disposable packet buffer only: these constructors neither publish nor save an item.
                    var live=new ConquerItem(true){ID=id,Plus=plus,SocketOne=(Enums.Gem)socket,SocketTwo=(Enums.Gem)(socket==0?1:0)};
                    set(item,"Id",id);set(item,"Plus",(int)plus);set(item,"SocketOne",(int)socket);set(item,"SocketTwo",socket==0?1:0);
                    string beforeLive=Review395Fingerprint(live.ToArray());
                    object compared=row(score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values));
                    Check((uint)IdentityField(compared,"DerivedDonation")==liveArsenal.GetDonation(live)
                        && (int)IdentityField(compared,"DerivedBattlePower")==live.BattlePower
                        && beforeLive==Review395Fingerprint(live.ToArray()),"REVIEW-401 independent live read-only donation and battle power agree across plus/socket/family boundaries");
                }
            }
        }
        set(item,"Id",savedId);set(item,"Plus",savedPlus);set(item,"SocketOne",savedOne);set(item,"SocketTwo",savedTwo);
        GuildIdentityType.GetField("BeforeDerive",Hidden).SetValue(null,(Action)delegate{throw new InvalidOperationException("review400 early");});
        object early=null;
        try{early=score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values);}
        finally{GuildIdentityType.GetField("BeforeDerive",Hidden).SetValue(null,null);}
        GuildIdentityType.GetField("AfterDeriveItem",Hidden).SetValue(null,(Action)delegate{throw new InvalidOperationException("review400 late");});
        object late=null;
        try{late=score(ids,levels,percents,itemIds,itemLevels,sorts,mins,maxes,values);}
        finally{GuildIdentityType.GetField("AfterDeriveItem",Hidden).SetValue(null,null);}
        Check(!((bool)IdentityField(early,"Ok")) && ((Array)IdentityField(early,"Items")).Length==0
            && (string)IdentityField(early,"Detail")=="InvalidOperationException"
            && !((bool)IdentityField(late,"Ok")) && ((Array)IdentityField(late,"Items")).Length==0
            && Scalar(itemSql,weaponUid)==itemHash && Scalar(additionSql,weaponUid)==additionHash
            && Kernel.Guilds.Count==registries && Kernel.Guilds_adv.Count==ads && Kernel.GamePool.Count==sessions
            && Kernel.Unions.Count==unions && member.Guild==null,
            "REVIEW-400 early and late score faults discard partial results and leave the saved rows in place");
    }
    static readonly Type HeroClaims=typeof(Entity).Assembly.GetType("Revora.Game.Features.WayOfHeroesClaims");
    static readonly Type HeroPacks=typeof(Entity).Assembly.GetType("Revora.Game.Features.GemDragonSoulPacks");
    static long HeroRows(GameState p,int stage,bool legacy){return Scalar("SELECT count(*) FROM revora_way_of_heroes_claims WHERE uid=@uid AND stage_uid="+stage.ToString()+" AND legacy="+ (legacy?"true":"false"),p.Entity.UID);}
    static int HeroPacksOf(GameState p,uint id){return p.Inventory.Objects.Count(i=>i.ID==id && i.Bound && i.Plus==0 && i.StackSize==1);}
    static byte[] HeroPacket(byte type,uint id){
        byte[] packet=new byte[17];
        Writer.WriteUInt16(9,0,packet);Writer.WriteUInt16(2830,2,packet);packet[4]=type;Writer.WriteUInt32(id,5,packet);Writer.WriteUInt64(PacketHandler.ClientSeal,9,packet);
        return packet;
    }
    static void HeroClaim(GameState p,byte type,uint id){PacketHandler.HandlePacket(HeroPacket(type,id),p);}
    static void HeroProgress(GameState p,byte reborn,byte level){
        p.Entity.Reborn=reborn;p.Entity.Level=level;
        if(!EntityTable.SaveEntity(p))throw new Exception("Way of Heroes progress did not save.");
        WayofHeroes.Load(p);
    }
    static void HeroSocket(GameState p,byte sockets){
        int left=sockets;
        byte[] positions={1,2,3,4,5,6,8,10};int slot=0;
        while(left>0){
            byte position=positions[slot++];
            if(Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND position="+position,p.Entity.UID)>0)continue;
            uint type=ConquerItemInformation.BaseInformations.Keys.First(id=>id<700000 && (byte)ItemHandler.GetPositionFromID(id)==position);
            var gear=Pack(p,type,1);
            byte one=(byte)(left>0?255:0);if(left>0)left--;
            byte two=(byte)(left>0?255:0);if(one==255 && two==255)left--;
            Query("UPDATE items SET position=@position,socketone=@one,sockettwo=@two WHERE uid=@uid","position",(int)position,"one",(int)one,"two",(int)two,"uid",(long)gear.UID);
            gear.Position=position;gear.SocketOne=(Enums.Gem)one;gear.SocketTwo=(Enums.Gem)two;
            p.Inventory.Remove(gear.UID,Enums.ItemUse.None,true);
        }
    }
    static void VerifyWayOfHeroesClaims(string runtime,Map town){
        Query("CREATE TABLE IF NOT EXISTS teamarena (LIKE public.teamarena INCLUDING ALL);CREATE TABLE IF NOT EXISTS itemadding (LIKE public.itemadding INCLUDING ALL)");
        Call(HeroClaims,null,"Initialize");Call(HeroClaims,null,"Initialize");Call(HeroPacks,null,"Initialize");
        HeroPacks.GetField("Roll",Hidden).SetValue(null,(Func<int,int,int>)((min,max)=>min));
        string way=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Network/GamePackets/WayofHeroes.cs")));
        string service=File.ReadAllText(Path.GetFullPath(Path.Combine(runtime,"../../Game/Features/WayOfHeroesClaims.cs")));
        Check(way.IndexOf("WayOfHeroesClaims.Claim")>=0 && way.IndexOf("AddBoundItem(substage.Prize[0]")<0
            && service.IndexOf("Plus=0,Bound=true")>=0 && service.IndexOf("That Way of Heroes prize cannot be claimed yet.")>=0,
            "Way of Heroes claims are owned and the third prize field is not applied as plus");
        string heard="";HeroClaims.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        var closed=BallPlayer(town);HeroProgress(closed,0,130);HeroSocket(closed,1);heard="";HeroClaim(closed,1,502);
        Check(!Fenced(closed) && HeroRows(closed,502,false)==0 && HeroPacksOf(closed,3005130)==0 && heard.IndexOf("stage is not open")>=0,
            "a player below the stage gate cannot claim the refined gem pack");
        var unmet=BallPlayer(town);HeroProgress(unmet,1,15);heard="";HeroClaim(unmet,1,502);
        Check(!Fenced(unmet) && HeroRows(unmet,502,false)==0 && HeroPacksOf(unmet,3005130)==0 && heard.IndexOf("have not finished")>=0,
            "an open stage without a socket cannot claim the refined gem pack");
        var earned=BallPlayer(town);HeroProgress(earned,1,15);HeroSocket(earned,1);heard="";HeroClaim(earned,1,502);
        Check(!Fenced(earned) && HeroRows(earned,502,false)==1 && HeroRows(earned,502,true)==0 && HeroPacksOf(earned,3005130)==1 && heard.IndexOf("You claimed 1 bound prize.")>=0,
            "one equipped socket lets an ordinary non-GM claim one bound refined gem pack");
        uint packUid=earned.Inventory.Objects.Single(i=>i.ID==3005130).UID;
        earned=ReloadBall(earned);
        Check(!Fenced(earned) && HeroRows(earned,502,false)==1 && HeroPacksOf(earned,3005130)==1 && earned.Inventory.ContainsUID(packUid),
            "reload keeps the single claimed pack");
        heard="";HeroClaim(earned,1,502);
        Check(!Fenced(earned) && HeroRows(earned,502,false)==1 && HeroPacksOf(earned,3005130)==1 && heard.IndexOf("already claimed")>=0,
            "replaying the claim packet does not grant a second pack");
        var pack=earned.Inventory.Objects.Single(i=>i.ID==3005130);
        ItemHandler.UseItem(pack,earned);
        Check(!Fenced(earned) && Scalar("SELECT count(*) FROM revora_gem_dragon_soul_pack_uses WHERE uid=@uid",earned.Entity.UID)==1
            && earned.Inventory.Objects.Count(i=>i.ID==700002 && i.Bound && i.Plus==0)==1 && HeroPacksOf(earned,3005130)==0,
            "the claimed pack opens once through GemDragonSoulPacks");
        earned=ReloadBall(earned);ItemHandler.UseItem(pack,earned);
        Check(!Fenced(earned) && Scalar("SELECT count(*) FROM revora_gem_dragon_soul_pack_uses WHERE uid=@uid",earned.Entity.UID)==1
            && earned.Inventory.Objects.Count(i=>i.ID==700002 && i.Bound && i.Plus==0)==1,
            "reload and replay do not open the claimed pack again");
        var four=BallPlayer(town);HeroProgress(four,1,101);HeroSocket(four,4);HeroClaim(four,1,601);
        Check(!Fenced(four) && HeroRows(four,601,false)==1 && HeroPacksOf(four,3005130)==2,
            "four equipped sockets claim two bound refined gem packs");
        HeroClaim(four,1,601);
        Check(HeroPacksOf(four,3005130)==2,"the four-socket claim does not pay again");
        var boss=BallPlayer(town);HeroProgress(boss,2,15);heard="";HeroClaim(boss,1,709);
        Check(!Fenced(boss) && HeroRows(boss,709,false)==0 && HeroPacksOf(boss,3005133)==0 && heard.IndexOf("have not finished")>=0,
            "a Fierce BOSS claim without a Snow Banshee or Terato victory pays nothing");
        var main=BallPlayer(town);HeroProgress(main,1,15);HeroSocket(main,1);heard="";HeroClaim(main,2,5);
        Check(!Fenced(main) && HeroPacksOf(main,3005111)==0 && heard.IndexOf("cannot be claimed yet")>=0,
            "a main-stage packet does not turn the third prize field into plus or pay the stage prize");
        heard="";var odd=BallPlayer(town);HeroProgress(odd,1,15);HeroClaim(odd,9,502);
        Check(!Fenced(odd) && HeroRows(odd,502,false)==0 && heard.IndexOf("cannot be claimed yet")>=0,
            "an unknown claim mode pays nothing");
        var full=BallPlayer(town);HeroProgress(full,1,15);HeroSocket(full,1);
        while(full.Inventory.Count<40)Pack(full,1088001,1);
        heard="";HeroClaim(full,1,502);
        Check(!Fenced(full) && HeroRows(full,502,false)==0 && HeroPacksOf(full,3005130)==0 && heard.IndexOf("Your inventory is Full.")>=0,
            "a full bag refuses the socket claim without recording it");
        var filler=full.Inventory.Objects.First(i=>i.ID==1088001);
        full.Inventory.Remove(filler.UID,Enums.ItemUse.Remove,true);
        HeroClaim(full,1,502);
        Check(!Fenced(full) && HeroRows(full,502,false)==1 && HeroPacksOf(full,3005130)==1,
            "freeing one slot lets the same socket claim pay once");
        var legacy=BallPlayer(town);HeroProgress(legacy,1,15);HeroSocket(legacy,1);
        var page=new WayofHeroes.StageInfo();page.UID=5;
        var prior=new WayofHeroes.SubStageInfo();prior.UID=502;prior.Done=true;prior.points=1;
        page.SubStages.Add(502,prior);legacy.Way2Heroes.Add(5,page);WayofHeroes.Save(legacy);
        legacy=ReloadBall(legacy);HeroProgress(legacy,1,15);
        heard="";HeroClaim(legacy,1,502);
        Check(!Fenced(legacy) && HeroRows(legacy,502,true)==1 && HeroRows(legacy,502,false)==0 && HeroPacksOf(legacy,3005130)==0 && heard.IndexOf("already claimed")>=0,
            "an old saved claim is kept and does not pay again");
        var race=BallPlayer(town);HeroProgress(race,1,15);HeroSocket(race,1);
        Parallel.For(0,12,n=>HeroClaim(race,1,502));
        Check(!Fenced(race) && HeroRows(race,502,false)==1 && HeroPacksOf(race,3005130)==1,
            "concurrent claim packets pay one refined gem pack");
        var fault=BallPlayer(town);HeroProgress(fault,1,15);HeroSocket(fault,1);
        Query("CREATE FUNCTION suppress_hero_claim() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_hero_claim BEFORE INSERT ON revora_way_of_heroes_claims FOR EACH ROW EXECUTE FUNCTION suppress_hero_claim()");
        try{HeroClaim(fault,1,502);
            Check(Fenced(fault) && HeroRows(fault,502,false)==0 && HeroPacksOf(fault,3005130)==0,
                "a failed claim insert leaves the pack unpaid");}
        finally{Query("DROP TRIGGER suppress_hero_claim ON revora_way_of_heroes_claims;DROP FUNCTION suppress_hero_claim()");}
        fault=ReloadBall(fault);HeroProgress(fault,1,15);HeroClaim(fault,1,502);
        Check(!Fenced(fault) && HeroRows(fault,502,false)==1 && HeroPacksOf(fault,3005130)==1,
            "reload retries the socket claim once after the failed insert");

        // REVIEW-326: packet validation applies to views as well as claims.
        var view=BallPlayer(town);HeroProgress(view,1,15);HeroSocket(view,1);
        WayofHeroes.Process(null,view);WayofHeroes.Process(new byte[4],view);
        var bad=HeroPacket(1,502);bad[9]^=1;WayofHeroes.Process(bad,view);
        bad=HeroPacket(1,502);bad[0]=8;WayofHeroes.Process(bad,view);
        for(uint id=0;id<300;id++)HeroClaim(view,0,id);
        Check(view.Way2Heroes.Count==0 && HeroRows(view,502,false)==0,"REVIEW-326 malformed packets and page reads never mutate progress");
        view.Trade.InTrade=true;HeroClaim(view,1,502);view.Trade.InTrade=false;
        Check(HeroRows(view,502,false)==0,"REVIEW-326 trading session cannot claim");
        var detached=BallPlayer(town);HeroProgress(detached,1,15);HeroSocket(detached,1);Kernel.GamePool.Remove(detached.Entity.UID);
        HeroClaim(detached,1,502);Check(HeroRows(detached,502,false)==0,"REVIEW-326 stale session cannot claim");

        foreach(int stage in new[]{708,803,903,1006}){
            var high=BallPlayer(town);HeroProgress(high,2,121);
            byte sockets=(byte)(stage==708?8:stage==1006?16:12);HeroSocket(high,sockets);
            if(stage>=900)Query("UPDATE items SET socketone=13,sockettwo=13 WHERE entityid=@uid AND position>0","uid",(long)high.Entity.UID);
            HeroClaim(high,1,(uint)stage);
            Check(HeroRows(high,stage,false)==1 && HeroPacksOf(high,stage>=900?3005135u:3005132u)==1,"REVIEW-326 exact socket/super-gem threshold pays stage "+stage);
        }
        var alternate=BallPlayer(town);HeroProgress(alternate,1,15);HeroSocket(alternate,1);
        Query("UPDATE items SET position=21 WHERE entityid=@uid AND position=1","uid",(long)alternate.Entity.UID);
        HeroClaim(alternate,1,502);Check(HeroRows(alternate,502,false)==0,"REVIEW-326 inactive alternate sockets do not count");
        Query("UPDATE items SET position=1,socketone=253 WHERE entityid=@uid AND position=21","uid",(long)alternate.Entity.UID);
        HeroClaim(alternate,1,502);Check(HeroRows(alternate,502,false)==0,"REVIEW-326 undefined gem values do not count");
        var duplicate=BallPlayer(town);HeroProgress(duplicate,1,101);HeroSocket(duplicate,4);
        Query("UPDATE items SET position=1 WHERE entityid=@uid AND position=2","uid",(long)duplicate.Entity.UID);
        HeroClaim(duplicate,1,601);Check(Fenced(duplicate) && HeroRows(duplicate,601,false)==0,"REVIEW-326 duplicate gear slots fence without payment");

        // Blob failure occurs after the claim and reward writes, and must roll back memory too.
        var blobFault=BallPlayer(town);HeroProgress(blobFault,1,15);HeroSocket(blobFault,1);
        Query("CREATE FUNCTION suppress_hero_blob() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_hero_blob BEFORE UPDATE ON wayofheroes FOR EACH ROW EXECUTE FUNCTION suppress_hero_blob()");
        try{HeroClaim(blobFault,1,502);Check(Fenced(blobFault) && blobFault.Way2Heroes.Count==0 && HeroRows(blobFault,502,false)==0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3005130",blobFault.Entity.UID)==0,"REVIEW-326 blob failure rolls back reward, receipt and in-memory progress");}
        finally{Query("DROP TRIGGER suppress_hero_blob ON wayofheroes;DROP FUNCTION suppress_hero_blob()");}
        WayofHeroes.Save(blobFault);blobFault=ReloadBall(blobFault);HeroClaim(blobFault,1,502);
        Check(HeroRows(blobFault,502,false)==1 && HeroPacksOf(blobFault,3005130)==1,"REVIEW-326 failed blob can reload and claim once");
        foreach(string target in new[]{"items","revora_way_of_heroes_claims"}){
            var insertFault=BallPlayer(town);HeroProgress(insertFault,1,15);HeroSocket(insertFault,1);
            Query("CREATE FUNCTION suppress_hero_insert() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_hero_insert BEFORE INSERT ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_hero_insert()");
            try{HeroClaim(insertFault,1,502);Check(Fenced(insertFault) && insertFault.Way2Heroes.Count==0 && HeroRows(insertFault,502,false)==0
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3005130",insertFault.Entity.UID)==0,"REVIEW-326 insert failure is atomic: "+target);}
            finally{Query("DROP TRIGGER suppress_hero_insert ON "+target+";DROP FUNCTION suppress_hero_insert()");}
            insertFault=ReloadBall(insertFault);HeroClaim(insertFault,1,502);
            Check(HeroRows(insertFault,502,false)==1 && HeroPacksOf(insertFault,3005130)==1,"REVIEW-326 insert failure retries once: "+target);
        }
        var projection=BallPlayer(town);HeroProgress(projection,1,15);HeroSocket(projection,1);
        HeroClaims.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(p=>{throw new Exception("fixture projection failure");}));
        try{HeroClaim(projection,1,502);Check(Fenced(projection) && HeroRows(projection,502,false)==1 && HeroPacksOf(projection,3005130)==0,"REVIEW-326 post-commit projection failure fences");}
        finally{HeroClaims.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        projection=ReloadBall(projection);WayofHeroes.Load(projection);HeroClaim(projection,1,502);
        Check(HeroPacksOf(projection,3005130)==1 && HeroRows(projection,502,false)==1,"REVIEW-326 committed projection recovers once");
        projection.Way2Heroes.Clear();WayofHeroes.Save(projection);projection=ReloadBall(projection);WayofHeroes.Load(projection);
        Check(projection.Way2Heroes[5].SubStages[502].Done && HeroPacksOf(projection,3005130)==1,"REVIEW-326 stale Save preserves durable claim state");
        Query("UPDATE wayofheroes SET wayofheroes=@blob WHERE uid=@uid","blob",new byte[]{0},"uid",(long)projection.Entity.UID);
        projection=ReloadBall(projection);WayofHeroes.Load(projection);HeroClaim(projection,1,502);
        Check(projection.Way2Heroes[5].SubStages[502].Done && HeroPacksOf(projection,3005130)==1,"REVIEW-326 receipt repairs missing blob without reward replay");
        legacy.Way2Heroes.Clear();HeroClaim(legacy,1,502);
        Check(HeroRows(legacy,502,true)==1 && HeroPacksOf(legacy,3005130)==0,"REVIEW-326 legacy receipt is authoritative over stale memory");
        var savedLegacy=BallPlayer(town);HeroProgress(savedLegacy,1,15);HeroSocket(savedLegacy,1);
        var legacyPage=new WayofHeroes.StageInfo{UID=5};legacyPage.SubStages.Add(502,new WayofHeroes.SubStageInfo{UID=502,Done=true});
        savedLegacy.Way2Heroes.Add(5,legacyPage);WayofHeroes.Save(savedLegacy);savedLegacy.Way2Heroes.Clear();HeroClaim(savedLegacy,1,502);
        Check(HeroRows(savedLegacy,502,true)==1 && HeroPacksOf(savedLegacy,3005130)==0,"REVIEW-326 durable legacy blob prevents stale-memory payout");

        var laterRebirth=BallPlayer(town);HeroProgress(laterRebirth,2,15);HeroSocket(laterRebirth,4);HeroClaim(laterRebirth,1,601);
        Check(HeroRows(laterRebirth,601,false)==1,"REVIEW-326 later rebirth retains access to an earlier stage");
        var saveRace=BallPlayer(town);HeroProgress(saveRace,1,101);HeroSocket(saveRace,4);
        Parallel.For(0,18,n=>{if(n%3==0)WayofHeroes.Save(saveRace);else HeroClaim(saveRace,(byte)(n%3==1?1:2),(uint)(n%3==1?601:6));});
        saveRace=ReloadBall(saveRace);WayofHeroes.Load(saveRace);
        Check(HeroRows(saveRace,601,false)==1 && HeroPacksOf(saveRace,3005130)==2 && saveRace.Way2Heroes[6].SubStages[601].Done,
            "REVIEW-326 concurrent Save/main/sub claims retain one payout");

        // Synthetic durable victory validates the consumer predicate; actual encounter completion is a separate fixture.
        Query("CREATE TABLE IF NOT EXISTS revora_terato_runs(id uuid PRIMARY KEY,uid bigint NOT NULL,route integer NOT NULL,party bigint[] NOT NULL,state varchar(10) NOT NULL,map integer NOT NULL,x integer NOT NULL,y integer NOT NULL,killer bigint NOT NULL DEFAULT 0,created_at timestamptz NOT NULL DEFAULT now(),defeated_at timestamptz)");
        var killer=BallPlayer(town);HeroProgress(killer,2,15);
        var absent=BallPlayer(town);HeroProgress(absent,2,15);Guid victory=Guid.NewGuid();
        Query("INSERT INTO revora_terato_runs(id,uid,route,party,state,map,x,y,killer,defeated_at) VALUES(@run,@uid,0,@party,'victory',1002,300,300,@uid,@now)",
            "run",victory,"uid",(long)killer.Entity.UID,"party",new long[]{killer.Entity.UID,absent.Entity.UID},"now",DateTime.UtcNow);
        HeroClaim(absent,1,709);HeroClaim(killer,1,709);
        Check(HeroRows(absent,709,false)==0 && HeroRows(killer,709,false)==1 && HeroPacksOf(killer,3005133)==1,
            "REVIEW-326 durable boss killer qualifies; historical party membership does not");
        killer=ReloadBall(killer);WayofHeroes.Load(killer);HeroClaim(killer,1,709);
        Check(HeroPacksOf(killer,3005133)==1,"REVIEW-326 boss claim reload/replay pays once");
        World(runtime,2056);
        VerifyWayOfHeroesEarned(town);
        VerifyWayOfHeroesStudy(town);
    }
    static void HeroGrow(GameState p,byte target){
        if(p.Entity.Reborn!=0 || p.Entity.Level>target){
            p.Entity.Level=1;p.Entity.Experience=0;
            if(!EntityTable.SaveEntity(p))throw new Exception("Way of Heroes level start did not save.");
        }
        int guard=0;
        while(p.Entity.Level<target && guard++<160){
            ulong need=DataHolder.LevelExperience(p.Entity.Level);
            if(need==0)need=1;
            p.IncreaseExperience(need,false);
        }
        if(p.Entity.Level!=target)throw new Exception("Production experience did not reach the stage level.");
        if(!EntityTable.SaveEntity(p))throw new Exception("Way of Heroes level did not save.");
        WayofHeroes.Load(p);
    }
    static GameState HeroReloadPrize(GameState p,uint stage,uint prize){
        var before=p.Inventory.Objects.Single(i=>i.ID==prize && i.Bound && i.Plus==0);
        p=ReloadBall(p);WayofHeroes.Load(p);HeroClaim(p,1,stage);
        Check(!Fenced(p) && HeroRows(p,(int)stage,false)==1 && HeroPacksOf(p,prize)==1
            && p.Inventory.Objects.Single(i=>i.ID==prize).UID==before.UID
            && p.Inventory.Objects.Single(i=>i.ID==prize).Bound && p.Inventory.Objects.Single(i=>i.ID==prize).Plus==0,
            "REVIEW-330 earned pack survives reload and claim replay before opening: "+stage);
        return p;
    }
    static void HeroFortune(GameState p,uint stage,uint prize){
        ulong silver=p.Entity.Money;HeroClaim(p,1,stage);
        p=HeroReloadPrize(p,stage,prize);
        var bag=p.Inventory.Objects.Single(i=>i.ID==prize && i.Bound && i.Plus==0);
        PacketHandler.HandlePacket(PrayerPacket(bag.UID),p);
        Check(!Fenced(p) && p.Entity.Money==silver+5000 && HeroRows(p,(int)stage,false)==1 && HeroPacksOf(p,prize)==0,
            "production experience claims fortune pack "+prize.ToString()+" once");
        ulong paid=p.Entity.Money;p=ReloadBall(p);WayofHeroes.Load(p);
        Check(p.Entity.Money==paid,"reload keeps fortune silver "+prize.ToString());
        HeroClaim(p,1,stage);ItemHandler.UseItem(bag,p);
        Check(p.Entity.Money==paid && HeroPacksOf(p,prize)==0,"reload does not repeat fortune pack "+prize.ToString());
    }
    static void HeroSocketPacket(GameState p,ConquerItem gear){
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.EquipmentSockets"),null,"Initialize");
        var material=Pack(p,1088000,1);
        PacketHandler.HandlePacket(DedicatedSocketFrame(gear.UID,material.UID),p);
    }
    static void VerifyWayOfHeroesEarned(Map town){
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.SilverStudyBags"),null,"Initialize");
        var novice=BallPlayer(town);HeroGrow(novice,19);string heard="";
        HeroClaims.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        HeroClaim(novice,1,101);
        Check(!Fenced(novice) && novice.Entity.Level==19 && HeroRows(novice,101,false)==0 && heard.IndexOf("have not finished")>=0,
            "level 19 does not claim the level 20 fortune pack");
        // Property setters persist immediately; inject only the memory preimage.
        var instanceFields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(Entity).GetField("_level",instanceFields).SetValue(novice.Entity,(byte)140);
        typeof(Entity).GetField("_reborn",instanceFields).SetValue(novice.Entity,(byte)2);
        HeroClaim(novice,1,101);HeroClaim(novice,1,501);HeroClaim(novice,1,701);
        Check(!Fenced(novice) && HeroRows(novice,101,false)==0 && HeroRows(novice,501,false)==0 && HeroRows(novice,701,false)==0,
            "REVIEW-330 forged memory level and rebirth cannot replace saved eligibility");
        typeof(Entity).GetField("_level",instanceFields).SetValue(novice.Entity,(byte)19);
        typeof(Entity).GetField("_reborn",instanceFields).SetValue(novice.Entity,(byte)0);
        HeroGrow(novice,20);ulong silver=novice.Entity.Money;HeroClaim(novice,1,101);
        novice=HeroReloadPrize(novice,101,3005117);
        var bag=novice.Inventory.Objects.Single(i=>i.ID==3005117 && i.Bound && i.Plus==0);
        PacketHandler.HandlePacket(PrayerPacket(bag.UID),novice);
        Check(!Fenced(novice) && novice.Entity.Money==silver+5000 && HeroRows(novice,101,false)==1 && HeroPacksOf(novice,3005117)==0,
            "production experience to level 20 claims Class1FortunePack and pays 5000 silver once");
        novice=ReloadBall(novice);WayofHeroes.Load(novice);silver=novice.Entity.Money;HeroClaim(novice,1,101);ItemHandler.UseItem(bag,novice);
        Check(!Fenced(novice) && novice.Entity.Money==silver && HeroPacksOf(novice,3005117)==0,
            "reload and replay do not repeat the level 20 silver");
        heard="";HeroClaim(novice,1,102);HeroClaim(novice,1,303);HeroClaim(novice,1,605);HeroClaim(novice,2,1);
        Check(HeroRows(novice,102,false)==0 && HeroRows(novice,303,false)==0 && HeroRows(novice,605,false)==0
            && novice.Inventory.Objects.Count(i=>i.ID==3005107 || i.ID==3005123 || i.ID==3005126)==0
            && heard.IndexOf("cannot be claimed yet")>=0,
            "equipment, study, Chi, and main prize 1 stay refused");
        HeroGrow(novice,30);HeroClaim(novice,1,201);
        Check(novice.Entity.Level==30 && HeroRows(novice,201,false)==0,"level 30 does not open the level 50 stage");
        HeroGrow(novice,49);HeroClaim(novice,1,201);
        Check(novice.Entity.Level==49 && HeroRows(novice,201,false)==0,"level 49 does not finish the level 50 stage");
        HeroGrow(novice,50);HeroFortune(novice,201,3005118);
        novice=ReloadBall(novice);WayofHeroes.Load(novice);
        HeroGrow(novice,79);HeroClaim(novice,1,301);
        Check(novice.Entity.Level==79 && HeroRows(novice,301,false)==0,"level 79 does not finish the level 80 stage");
        HeroGrow(novice,80);HeroFortune(novice,301,3005119);
        novice=ReloadBall(novice);WayofHeroes.Load(novice);
        HeroGrow(novice,109);HeroClaim(novice,1,401);
        Check(novice.Entity.Level==109 && HeroRows(novice,401,false)==0,"level 109 does not finish the level 110 stage");
        HeroGrow(novice,110);HeroFortune(novice,401,3005120);
        VerifyFortunePackFaults(town);
        var reborn=SecondPlayer(town);WayofHeroes.Load(reborn);silver=reborn.Entity.Money;HeroClaim(reborn,1,501);
        reborn=HeroReloadPrize(reborn,501,3005121);
        var fifth=reborn.Inventory.Objects.Single(i=>i.ID==3005121 && i.Bound);
        PacketHandler.HandlePacket(PrayerPacket(fifth.UID),reborn);
        Check(!Fenced(reborn) && reborn.Entity.Reborn==1 && reborn.Entity.Money==silver+10000,
            "production first rebirth claims Class5FortunePack and pays 10000 silver");
        reborn=ReloadBall(reborn);WayofHeroes.Load(reborn);HeroClaim(reborn,1,501);PacketHandler.HandlePacket(PrayerPacket(fifth.UID),reborn);
        Check(!Fenced(reborn) && reborn.Entity.Money==silver+10000 && HeroPacksOf(reborn,3005121)==0
            && Scalar("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_type=3005121",reborn.Entity.UID)==1,
            "REVIEW-330 first rebirth payout and one receipt survive reload and packet replay");
        var weapon=reborn.Inventory.Objects.Single(i=>i.ID==410077);
        HeroSocketPacket(reborn,weapon);
        ushort slot=(ushort)ItemHandler.GetPositionFromID(weapon.ID);
        ItemHandler.EquipItem(new ItemUsage(true){UID=weapon.UID,dwParam=slot,Action=ItemUsage.Mode.EquipItem},reborn);
        reborn=ReloadBall(reborn);WayofHeroes.Load(reborn);
        Check(Scalar("SELECT socketone FROM items WHERE uid=@uid",weapon.UID)==255 && Scalar("SELECT position FROM items WHERE uid=@uid",weapon.UID)==slot,
            "the production socket packet and equip persist one empty socket");
        HeroClaim(reborn,1,502);
        reborn=HeroReloadPrize(reborn,502,3005130);
        var pack=reborn.Inventory.Objects.Single(i=>i.ID==3005130 && i.Bound && i.Plus==0);
        PacketHandler.HandlePacket(PrayerPacket(pack.UID),reborn);
        Check(!Fenced(reborn) && HeroRows(reborn,502,false)==1 && reborn.Inventory.Objects.Count(i=>i.ID==700002 && i.Bound && i.Plus==0)==1,
            "the socketed earned weapon claims one refined gem pack and the pack opens once");
        reborn=ReloadBall(reborn);WayofHeroes.Load(reborn);HeroClaim(reborn,1,502);ItemHandler.UseItem(pack,reborn);
        Check(HeroPacksOf(reborn,3005130)==0 && reborn.Inventory.Objects.Count(i=>i.ID==700002 && i.Bound)==1,
            "reload does not repeat the earned socket claim or the pack");
        var secondType=typeof(Entity).Assembly.GetType("Revora.Game.Features.SecondRebirth");
        secondType.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>secondNow));
        Call(secondType,null,"Initialize");
        var twice=SecondPlayer(town);SecondOffer(twice,11);StoneNpc(twice,180);
        Check(!Fenced(twice) && twice.Entity.Reborn==2 && twice.Entity.Level==15,"production second rite reaches rebirth 2 at level 15");
        WayofHeroes.Load(twice);HeroClaim(twice,1,401);
        Check(HeroRows(twice,401,false)==0 && HeroPacksOf(twice,3005120)==0,
            "rebirth level reset does not invent the level 110 fortune pack");
        int study=twice.Entity.SubClasses.StudyPoints;silver=twice.Entity.Money;HeroClaim(twice,1,701);
        twice=HeroReloadPrize(twice,701,3005122);
        var sixth=twice.Inventory.Objects.Single(i=>i.ID==3005122 && i.Bound && i.Plus==0);
        PacketHandler.HandlePacket(PrayerPacket(sixth.UID),twice);
        Check(!Fenced(twice) && twice.Entity.Money==silver+20000 && twice.Entity.SubClasses.StudyPoints==study+400,
            "production second rebirth claims Class6FortunePack for 20000 silver and 400 study");
        ulong paidSilver=twice.Entity.Money;int paidStudy=twice.Entity.SubClasses.StudyPoints;
        twice=ReloadBall(twice);WayofHeroes.Load(twice);
        Check(twice.Entity.Money==paidSilver && twice.Entity.SubClasses.StudyPoints==paidStudy,
            "reload keeps the 20000 silver and 400 study");
        HeroClaim(twice,1,701);ItemHandler.UseItem(sixth,twice);
        Check(twice.Entity.Money==paidSilver && twice.Entity.SubClasses.StudyPoints==paidStudy,"reload does not repeat the rebirth pack");
        teratoNow=DateTime.UtcNow;Terato.GetField("Clock",Hidden).SetValue(null,(Func<DateTime>)(()=>teratoNow));
        Call(Terato,null,"Initialize");
        var map=Kernel.Maps[2056];var summoner=TeratoPlayer(map);var slayer=TeratoPlayer(map);
        HeroProgress(summoner,2,15);HeroProgress(slayer,2,15);TeratoTeam(summoner,slayer);TeratoSite(summoner);
        ItemHandler.UseItem(Pack(summoner,720598,1),summoner);var boss=TeratoBoss(summoner);
        summoner.Entity.X=boss.X;summoner.Entity.Y=boss.Y;slayer.Entity.X=boss.X;slayer.Entity.Y=boss.Y;
        uint hit=boss.Hitpoints;
        Revora.Game.Attacking.Handle.ReceiveAttack(slayer.Entity,boss,new Attack(true){AttackType=Attack.Melee,Attacked=boss.UID,Attacker=slayer.Entity.UID},ref hit,null);
        Check(boss.Dead && Scalar("SELECT killer FROM revora_terato_runs WHERE uid=@uid AND state='victory'",summoner.Entity.UID)==slayer.Entity.UID,
            "production lethal attack records the actual Terato killer");
        HeroClaim(summoner,1,709);
        Check(HeroRows(summoner,709,false)==0 && HeroPacksOf(summoner,3005133)==0,"the summoner who did not land the kill gets no Dragon Soul bag");
        HeroClaim(slayer,1,709);
        slayer=HeroReloadPrize(slayer,709,3005133);
        var soul=slayer.Inventory.Objects.Single(i=>i.ID==3005133 && i.Bound && i.Plus==0);
        uint expected=GemSoulPool(3005133)[0];
        var oldIds=slayer.Inventory.Objects.Select(i=>i.UID).ToArray();
        PacketHandler.HandlePacket(PrayerPacket(soul.UID),slayer);
        var result=slayer.Inventory.Objects.Single(i=>!oldIds.Contains(i.UID));
        Check(!Fenced(slayer) && HeroRows(slayer,709,false)==1 && result.ID==expected && result.Bound && result.Plus==0
            && ConquerItemInformation.BaseInformations[result.ID].PurificationLevel==4
            && Scalar("SELECT count(*) FROM revora_gem_dragon_soul_pack_uses WHERE uid=@uid AND pack_type=3005133",slayer.Entity.UID)==1
            && Scalar("SELECT result_uid FROM revora_gem_dragon_soul_pack_uses WHERE pack_uid=@uid",soul.UID)==result.UID
            && Scalar("SELECT result_type FROM revora_gem_dragon_soul_pack_uses WHERE pack_uid=@uid",soul.UID)==expected,
            "the production killer claims one bound P4 bag and opens it once");
        slayer=ReloadBall(slayer);WayofHeroes.Load(slayer);
        HeroClaim(slayer,1,709);PacketHandler.HandlePacket(PrayerPacket(soul.UID),slayer);
        Check(!Fenced(slayer) && HeroPacksOf(slayer,3005133)==0
            && slayer.Inventory.Objects.Count(i=>!oldIds.Contains(i.UID))==1
            && slayer.Inventory.Objects.Single(i=>i.UID==result.UID).ID==expected
            && slayer.Inventory.Objects.Single(i=>i.UID==result.UID).Bound
            && slayer.Inventory.Objects.Single(i=>i.UID==result.UID).Plus==0
            && Scalar("SELECT count(*) FROM revora_gem_dragon_soul_pack_uses WHERE uid=@uid AND pack_type=3005133",slayer.Entity.UID)==1,
            "reload does not repeat the earned boss claim or the opened reward");
        ClearTerato();
    }
    static void VerifyFortunePackFaults(Map town){
        var full=BallPlayer(town);HeroGrow(full,20);
        while(full.Inventory.Count<40)Pack(full,1088001,1);
        string heard="";
        HeroClaims.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        HeroClaim(full,1,101);
        Check(!Fenced(full) && HeroRows(full,101,false)==0 && HeroPacksOf(full,3005117)==0 && heard.IndexOf("Your inventory is Full.")>=0,
            "a full bag refuses the level 20 fortune pack without recording it");
        var filler=full.Inventory.Objects.First(i=>i.ID==1088001);
        full.Inventory.Remove(filler.UID,Enums.ItemUse.Remove,true);
        ulong silver=full.Entity.Money;HeroClaim(full,1,101);
        var bag=full.Inventory.Objects.Single(i=>i.ID==3005117 && i.Bound && i.Plus==0);
        ItemHandler.UseItem(bag,full);
        Check(!Fenced(full) && full.Entity.Money==silver+5000 && HeroRows(full,101,false)==1 && HeroPacksOf(full,3005117)==0,
            "freeing one slot lets the level 20 fortune pack pay once");
        var legacy=BallPlayer(town);HeroGrow(legacy,20);
        var page=new WayofHeroes.StageInfo{UID=1};
        page.SubStages.Add(101,new WayofHeroes.SubStageInfo{UID=101,Done=true,points=20});
        legacy.Way2Heroes.Add(1,page);WayofHeroes.Save(legacy);
        legacy=ReloadBall(legacy);WayofHeroes.Load(legacy);silver=legacy.Entity.Money;HeroClaim(legacy,1,101);
        Check(!Fenced(legacy) && HeroRows(legacy,101,true)==1 && HeroRows(legacy,101,false)==0 && HeroPacksOf(legacy,3005117)==0 && legacy.Entity.Money==silver,
            "a legacy level claim pays no fortune pack");
        var race=BallPlayer(town);HeroGrow(race,20);
        Parallel.For(0,12,n=>HeroClaim(race,1,101));
        race=ReloadBall(race);WayofHeroes.Load(race);
        Check(!Fenced(race) && HeroRows(race,101,false)==1 && HeroPacksOf(race,3005117)==1,
            "concurrent level claims pay one fortune pack");
        foreach(string target in new[]{"items","revora_way_of_heroes_claims"}){
            var insertFault=BallPlayer(town);HeroGrow(insertFault,20);
            Query("CREATE FUNCTION suppress_fortune_insert() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_fortune_insert BEFORE INSERT ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_fortune_insert()");
            try{HeroClaim(insertFault,1,101);Check(Fenced(insertFault) && insertFault.Way2Heroes.Count==0 && HeroRows(insertFault,101,false)==0
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3005117",insertFault.Entity.UID)==0,"fortune insert failure is atomic: "+target);}
            finally{Query("DROP TRIGGER suppress_fortune_insert ON "+target+";DROP FUNCTION suppress_fortune_insert()");}
            insertFault=ReloadBall(insertFault);WayofHeroes.Load(insertFault);HeroClaim(insertFault,1,101);
            Check(!Fenced(insertFault) && HeroRows(insertFault,101,false)==1 && HeroPacksOf(insertFault,3005117)==1,"fortune insert failure retries once: "+target);
        }
        var blobFault=BallPlayer(town);HeroGrow(blobFault,20);
        Query("CREATE FUNCTION suppress_fortune_blob() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_fortune_blob BEFORE UPDATE ON wayofheroes FOR EACH ROW EXECUTE FUNCTION suppress_fortune_blob()");
        try{HeroClaim(blobFault,1,101);Check(Fenced(blobFault) && blobFault.Way2Heroes.Count==0 && HeroRows(blobFault,101,false)==0
            && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3005117",blobFault.Entity.UID)==0,"fortune blob failure rolls back the pack and the claim");}
        finally{Query("DROP TRIGGER suppress_fortune_blob ON wayofheroes;DROP FUNCTION suppress_fortune_blob()");}
        blobFault=ReloadBall(blobFault);WayofHeroes.Load(blobFault);HeroClaim(blobFault,1,101);
        Check(!Fenced(blobFault) && HeroRows(blobFault,101,false)==1 && HeroPacksOf(blobFault,3005117)==1,"fortune blob failure retries once");
    }
    static void HeroEnroll(GameState p,byte id,uint material,ushort count){
        if(p.Entity.SubClasses==null || p.Entity.SubClasses.Classes==null)throw new Exception("Subclass state is missing.");
        Pack(p,material,count);SubRequest(p,9,id);
        if(Fenced(p) || !p.Entity.SubClasses.Classes.ContainsKey(id))throw new Exception("Production subclass enrollment failed.");
    }
    static GameState HeroStudyOpen(GameState p,uint stage,uint prize,int gain){
        HeroClaim(p,1,stage);
        p=HeroReloadPrize(p,stage,prize);
        int before=p.Entity.SubClasses.StudyPoints;
        var bag=p.Inventory.Objects.Single(i=>i.ID==prize && i.Bound && i.Plus==0);
        PacketHandler.HandlePacket(PrayerPacket(bag.UID),p);
        Check(!Fenced(p) && p.Entity.SubClasses.StudyPoints==before+gain
            && Scalar("SELECT count(*) FROM revora_silver_study_bag_uses WHERE uid=@uid AND item_uid="+bag.UID.ToString(),p.Entity.UID)==1,
            "the earned study pack "+prize.ToString()+" pays "+gain.ToString()+" study once");
        int paid=p.Entity.SubClasses.StudyPoints;
        p=ReloadBall(p);WayofHeroes.Load(p);HeroClaim(p,1,stage);PacketHandler.HandlePacket(PrayerPacket(bag.UID),p);
        Check(p.Entity.SubClasses.StudyPoints==paid && HeroPacksOf(p,prize)==0,"reload does not repeat study pack "+prize.ToString());
        return p;
    }
    static void VerifyWayOfHeroesStudy(Map town){
        Query("CREATE TABLE IF NOT EXISTS subclasses (LIKE public.subclasses INCLUDING ALL);CREATE TABLE IF NOT EXISTS guilds (LIKE public.guilds INCLUDING ALL)");
        Call(typeof(Entity).Assembly.GetType("Revora.Game.Features.SilverStudyBags"),null,"Initialize");
        var student=BallPlayer(town);HeroProgress(student,1,69);
        if(student.Entity.SubClasses==null)throw new Exception("Subclass state is missing.");
        var ore=Pack(student,721259,5);SubRequest(student,9,1);
        Check(!Fenced(student) && student.Entity.Level==69 && student.Entity.SubClasses.Classes.Count==0 && ore.StackSize==5,
            "level 69 does not consume subclass enrollment materials");
        string heard="";HeroClaims.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        HeroClaim(student,1,504);HeroClaim(student,1,408);
        Check(HeroRows(student,504,false)==0 && HeroRows(student,408,false)==0 && heard.IndexOf("have not finished")>=0 && heard.IndexOf("cannot be claimed yet")>=0,
            "no saved subclass leaves the study pack unpaid and the super-talisman stage stays refused");
        var memory=BallPlayer(town);HeroProgress(memory,1,70);
        memory.Entity.SubClasses.Classes[1]=new SubClass{ID=1,Level=1,Phase=1};HeroClaim(memory,1,504);
        Check(HeroRows(memory,504,false)==0,"forged subclass memory does not qualify");
        var odd=BallPlayer(town);HeroProgress(odd,1,70);
        Query("INSERT INTO subclasses(id,uid,level,phase) VALUES(@uid,7,1,1)","uid",(long)odd.Entity.UID);
        Query("INSERT INTO subclasses(id,uid,level,phase) VALUES(@uid,2,1,0)","uid",(long)odd.Entity.UID);
        HeroClaim(odd,1,504);
        Check(HeroRows(odd,504,false)==0,"an unsupported class and a zero phase do not count as enrollment");
        foreach(var invalid in new[]{new[]{1,10,1},new[]{2,1,2},new[]{3,0,1},new[]{257,1,1}}){
            Query("DELETE FROM subclasses WHERE id=@uid","uid",(long)odd.Entity.UID);
            Query("INSERT INTO subclasses(id,uid,level,phase) VALUES(@uid,@class,@level,@phase)",
                "uid",(long)odd.Entity.UID,"class",invalid[0],"level",invalid[1],"phase",invalid[2]);
            HeroClaim(odd,1,504);
            Check(!Fenced(odd) && HeroRows(odd,504,false)==0 && HeroPacksOf(odd,3005124)==0,
                "REVIEW-334 invalid subclass id/level/phase cannot earn study: "+string.Join(",",invalid));
        }
        var foreign=BallPlayer(town);HeroProgress(foreign,1,70);HeroEnroll(foreign,1,721259,5);
        HeroClaim(memory,1,504);
        Check(HeroRows(memory,504,false)==0,"REVIEW-334 another character's enrollment cannot qualify forged subclass memory");
        HeroProgress(student,1,70);SubRequest(student,9,1);
        Check(!Fenced(student) && student.Entity.SubClasses.Classes.ContainsKey(1) && Scalar("SELECT count(*) FROM subclasses WHERE id=@uid AND uid=1 AND level=1 AND phase=1",student.Entity.UID)==1,
            "the production enrollment packet saves one subclass");
        student=ReloadBall(student);WayofHeroes.Load(student);
        student=HeroStudyOpen(student,504,3005124,300);
        HeroClaim(student,1,608);
        Check(student.Entity.Level==70 && HeroRows(student,608,false)==0,"level 70 does not open the two-subclass stage");
        HeroProgress(student,1,101);HeroClaim(student,1,608);
        Check(HeroRows(student,608,false)==0,"REVIEW-334 one saved subclass does not finish the two-subclass stage");
        HeroEnroll(student,2,721261,10);
        student=ReloadBall(student);WayofHeroes.Load(student);
        student=HeroStudyOpen(student,608,3005125,500);
        HeroClaim(student,1,711);
        Check(student.Entity.Reborn==1 && HeroRows(student,711,false)==0,"first rebirth does not open the three-subclass stage");
        HeroProgress(student,2,70);HeroClaim(student,1,711);
        Check(HeroRows(student,711,false)==0,"two saved subclasses do not finish the three-subclass stage");
        HeroEnroll(student,3,711188,1);
        student=ReloadBall(student);WayofHeroes.Load(student);
        student=HeroStudyOpen(student,711,3005125,500);
        var duplicated=BallPlayer(town);HeroProgress(duplicated,1,70);
        Query("INSERT INTO subclasses(id,uid,level,phase) VALUES(@uid,1,1,1)","uid",(long)duplicated.Entity.UID);
        bool duplicateBlocked=false;
        try{Query("INSERT INTO subclasses(id,uid,level,phase) VALUES(@uid,1,1,1)","uid",(long)duplicated.Entity.UID);}catch(Exception){duplicateBlocked=true;}
        if(duplicateBlocked)Check(Scalar("SELECT count(*) FROM subclasses WHERE id=@uid AND uid=1",duplicated.Entity.UID)==1,"the subclass key rejects a second copy of the same class");
        else{HeroClaim(duplicated,1,504);Check(Fenced(duplicated) && HeroRows(duplicated,504,false)==0 && HeroPacksOf(duplicated,3005124)==0,"duplicate subclass rows fence without a pack");}
        var full=BallPlayer(town);HeroProgress(full,1,70);HeroEnroll(full,1,721259,5);
        while(full.Inventory.Count<40)Pack(full,1088001,1);
        heard="";HeroClaims.GetField("Spoken",Hidden).SetValue(null,(Action<string>)(m=>{heard=heard+m+"|";}));
        HeroClaim(full,1,504);
        Check(!Fenced(full) && HeroRows(full,504,false)==0 && heard.IndexOf("Your inventory is Full.")>=0,"a full bag refuses the subclass pack without recording it");
        var filler=full.Inventory.Objects.First(i=>i.ID==1088001);
        full.Inventory.Remove(filler.UID,Enums.ItemUse.Remove,true);
        full=HeroStudyOpen(full,504,3005124,300);
        var legacy=BallPlayer(town);HeroProgress(legacy,1,70);HeroEnroll(legacy,1,721259,5);
        var page=new WayofHeroes.StageInfo{UID=5};
        page.SubStages.Add(504,new WayofHeroes.SubStageInfo{UID=504,Done=true,points=1});
        legacy.Way2Heroes.Add(5,page);WayofHeroes.Save(legacy);
        legacy=ReloadBall(legacy);WayofHeroes.Load(legacy);int legacyStudy=legacy.Entity.SubClasses.StudyPoints;HeroClaim(legacy,1,504);
        Check(HeroRows(legacy,504,true)==1 && HeroPacksOf(legacy,3005124)==0 && legacy.Entity.SubClasses.StudyPoints==legacyStudy,
            "a legacy subclass claim pays no study pack");
        var race=BallPlayer(town);HeroProgress(race,1,70);HeroEnroll(race,1,721259,5);
        Parallel.For(0,12,n=>{if(n%3==0)WayofHeroes.Save(race);else HeroClaim(race,1,504);});
        race=ReloadBall(race);WayofHeroes.Load(race);
        Check(HeroRows(race,504,false)==1 && HeroPacksOf(race,3005124)==1,"concurrent subclass claims and Save pay one pack");
        var projection=BallPlayer(town);HeroProgress(projection,1,70);HeroEnroll(projection,1,721259,5);
        HeroClaims.GetField("BeforeInstall",Hidden).SetValue(null,(Action<GameState>)(p=>{throw new Exception("fixture projection failure");}));
        try{HeroClaim(projection,1,504);Check(Fenced(projection) && HeroRows(projection,504,false)==1 && HeroPacksOf(projection,3005124)==0,
            "REVIEW-334 subclass projection failure fences after one durable claim");}
        finally{HeroClaims.GetField("BeforeInstall",Hidden).SetValue(null,null);}
        var oldSession=projection;projection=ReloadBall(projection);WayofHeroes.Load(projection);HeroClaim(oldSession,1,504);
        projection=HeroStudyOpen(projection,504,3005124,300);
        Check(HeroRows(projection,504,false)==1,"REVIEW-334 reconnect recovers subclass pack once and stale session cannot pay again");
        foreach(string target in new[]{"items","revora_way_of_heroes_claims"}){
            var fault=BallPlayer(town);HeroProgress(fault,1,70);HeroEnroll(fault,1,721259,5);
            Query("CREATE FUNCTION suppress_study_insert() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_study_insert BEFORE INSERT ON "+target+" FOR EACH ROW EXECUTE FUNCTION suppress_study_insert()");
            try{HeroClaim(fault,1,504);Check(Fenced(fault) && fault.Way2Heroes.Count==0 && HeroRows(fault,504,false)==0
                && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3005124",fault.Entity.UID)==0,"study insert failure is atomic: "+target);}
            finally{Query("DROP TRIGGER suppress_study_insert ON "+target+";DROP FUNCTION suppress_study_insert()");}
            fault=ReloadBall(fault);WayofHeroes.Load(fault);HeroClaim(fault,1,504);
            Check(!Fenced(fault) && HeroRows(fault,504,false)==1 && HeroPacksOf(fault,3005124)==1,"study insert failure retries once: "+target);
        }
        var blob=BallPlayer(town);HeroProgress(blob,1,70);HeroEnroll(blob,1,721259,5);
        Query("CREATE FUNCTION suppress_study_blob() RETURNS trigger LANGUAGE plpgsql AS 'BEGIN RETURN NULL; END';CREATE TRIGGER suppress_study_blob BEFORE UPDATE ON wayofheroes FOR EACH ROW EXECUTE FUNCTION suppress_study_blob()");
        try{HeroClaim(blob,1,504);Check(Fenced(blob) && HeroRows(blob,504,false)==0 && Scalar("SELECT count(*) FROM items WHERE entityid=@uid AND id=3005124",blob.Entity.UID)==0,
            "study blob failure rolls back the pack and the claim");}
        finally{Query("DROP TRIGGER suppress_study_blob ON wayofheroes;DROP FUNCTION suppress_study_blob()");}
        blob=ReloadBall(blob);WayofHeroes.Load(blob);HeroClaim(blob,1,504);
        Check(HeroRows(blob,504,false)==1 && HeroPacksOf(blob,3005124)==1,"study blob failure retries once");
        // REVIEW-334: guild issuance remains disabled until membership and paid
        // creation have an atomic, fenced durable owner. A raw saved id is not evidence.
        var guildOnly=BallPlayer(town);HeroProgress(guildOnly,0,61);
        Query("INSERT INTO guilds(id,name,leadername,enrollmentdate,bulletindate,bulletin,silverfund) VALUES(2001,'ReviewGuild',@name,20260924,20260924,'',500000)","name",guildOnly.Entity.Name);
        Query("UPDATE entities SET guildid=2001 WHERE uid=@uid","uid",(long)guildOnly.Entity.UID);
        HeroClaim(guildOnly,1,303);
        Check(!Fenced(guildOnly) && HeroRows(guildOnly,303,false)==0 && HeroPacksOf(guildOnly,3005123)==0,
            "REVIEW-334 saved guild membership cannot activate unfinished stage 303");
        guildOnly=ReloadBall(guildOnly);WayofHeroes.Load(guildOnly);HeroClaim(guildOnly,1,303);
        Check(HeroRows(guildOnly,303,false)==0 && HeroPacksOf(guildOnly,3005123)==0,
            "REVIEW-334 guild stage refusal survives reload and replay");
        VerifyReview338GuildGuards(town,guildOnly);
    }
    static void Review338Join(GameState p,uint guild){
        byte[] raw=new byte[20];Writer.WriteUInt16(12,0,raw);Writer.WriteUInt16(2227,2,raw);
        raw[4]=1;Writer.WriteUInt32(guild,8,raw);Writer.WriteUInt64(PacketHandler.ClientSeal,12,raw);
        PacketHandler.HandlePacket(raw,p);
    }
    static void VerifyReview338GuildGuards(Map town,GameState leader){
        // Guild identity and leader are prerequisites supplied by this fixture.
        // This proves packet/save guards, not atomic creation or production earning.
        var guild=new Revora.Game.ConquerStructures.Society.Guild(leader.Entity.Name);
        guild.ID=2001;guild.Name="ReviewGuild";
        guild.Leader=new Revora.Game.ConquerStructures.Society.Guild.Member(2001){
            ID=leader.Entity.UID,Name=leader.Entity.Name,Rank=Enums.GuildMemberRank.GuildLeader};
        guild.Members.Add(leader.Entity.UID,guild.Leader);guild.MemberCount=1;
        Revora.Kernel.Guilds.Add(2001,guild);

        var memory=BallPlayer(town);HeroProgress(memory,0,61);
        memory.AsMember=new Revora.Game.ConquerStructures.Society.Guild.Member(2001){
            ID=memory.Entity.UID,Name=memory.Entity.Name,Rank=Enums.GuildMemberRank.Member};
        memory.Guild=guild;
        if(!EntityTable.SaveEntity(memory))throw new Exception("Review memory save failed.");
        Check(Scalar("SELECT COALESCE(guildid,0) FROM entities WHERE uid=@uid",memory.Entity.UID)==0,
            "REVIEW-338 character Save cannot create guild ownership from memory");
        Query("UPDATE entities SET guildrank=@rank WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)leader.Entity.UID);
        if(!EntityTable.SaveEntity(leader))throw new Exception("Review null projection save failed.");
        Check(Scalar("SELECT guildid FROM entities WHERE uid=@uid",leader.Entity.UID)==2001,
            "REVIEW-338 missing memory membership cannot erase durable guild id");
        Check(Scalar("SELECT guildrank FROM entities WHERE uid=@uid",leader.Entity.UID)==(int)Enums.GuildMemberRank.GuildLeader,
            "REVIEW-343 missing memory membership cannot erase durable guild rank");
        Query("UPDATE entities SET guildid=2002 WHERE uid=@uid","uid",(long)memory.Entity.UID);
        if(!EntityTable.SaveEntity(memory))throw new Exception("Review replaced membership save failed.");
        Check(Scalar("SELECT guildid FROM entities WHERE uid=@uid",memory.Entity.UID)==2002,
            "REVIEW-338 stale membership cannot replace a different durable guild id");

        var applicant=BallPlayer(town);HeroProgress(applicant,0,61);applicant.Entity.Class=11;
        guild.LevelRequirement=120;Review338Join(applicant,2001);
        Check(applicant.Guild==null && Scalar("SELECT COALESCE(guildid,0) FROM entities WHERE uid=@uid",applicant.Entity.UID)==0,
            "REVIEW-338 packet 2227 rejects a player below the guild level requirement");
        guild.LevelRequirement=0;guild.RebornRequirement=1;Review338Join(applicant,2001);
        Check(applicant.Guild==null,"REVIEW-338 packet 2227 enforces guild rebirth requirement");
        guild.RebornRequirement=0;guild.ClassRequirement=UInt32.MaxValue;Review338Join(applicant,2001);
        Check(applicant.Guild==null,"REVIEW-338 packet 2227 enforces guild class requirement");
        guild.ClassRequirement=0;guild.BlackList.Add(applicant.Entity.UID);Review338Join(applicant,2001);
        Check(applicant.Guild==null,"REVIEW-338 packet 2227 refuses a blacklisted applicant");
        guild.BlackList.Remove(applicant.Entity.UID);
        var stale=applicant;applicant=ReloadBall(applicant);Review338Join(stale,2001);
        Check(stale.Guild==null && applicant.Guild==null,
            "REVIEW-338 replaced session cannot join through packet 2227");
        typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(applicant.Entity,true);Review338Join(applicant,2001);
        Check(applicant.Guild==null,"REVIEW-338 fenced session cannot join through packet 2227");
        DemonReset(applicant);
        Parallel.For(0,12,n=>{if(n%3==0)EntityTable.SaveEntity(applicant);else Review338Join(applicant,2001);});
        Check(!Fenced(applicant) && applicant.Guild==guild && guild.MemberCount==2
            && guild.Members.ContainsKey(applicant.Entity.UID)
            && Scalar("SELECT guildid FROM entities WHERE uid=@uid",applicant.Entity.UID)==2001,
            "REVIEW-338 eligible ordinary applicant joins once with concurrent packets and Save");
        applicant=ReloadBall(applicant);
        Check(applicant.Guild==guild && applicant.AsMember!=null,
            "REVIEW-338 successful legacy join survives the production loader");
        HeroClaim(applicant,1,303);
        Check(HeroRows(applicant,303,false)==0 && HeroPacksOf(applicant,3005123)==0,
            "REVIEW-338 legacy join does not enable unfinished guild study issuance");
        var oldMember=applicant.AsMember;
        guild.ExpelMember(applicant.Entity.Name,true);
        applicant.AsMember=oldMember;applicant.Guild=guild;
        if(!EntityTable.SaveEntity(applicant))throw new Exception("Review departed member save failed.");
        Check(Scalar("SELECT COALESCE(guildid,0) FROM entities WHERE uid=@uid",applicant.Entity.UID)==0,
            "REVIEW-338 stale Save cannot restore membership after production departure");
        applicant=ReloadBall(applicant);HeroClaim(applicant,1,303);
        Check(applicant.Guild==null && HeroRows(applicant,303,false)==0,
            "REVIEW-338 departed player reconnect has no guild or study reward");
        VerifyReview343GuildCommands(town,leader,guild);
    }
    static void Review343Command(GameState actor,uint action,uint peer){
        // Disposable reload supplies entity state; normal login also initializes
        // these empty social collections used by invitation display packets.
        if(actor.Partners==null)actor.Partners=new System.Collections.Generic.SafeDictionary<uint,Revora.Game.ConquerStructures.Society.TradePartner>();
        if(actor.Apprentices==null)actor.Apprentices=new System.Collections.Generic.SafeDictionary<uint,Revora.Game.ConquerStructures.Society.Apprentice>();
        var command=new GuildCommand(true);command.Type=action;command.dwParam=peer;
        var packet=command.ToArray();Writer.WriteUInt64(PacketHandler.ClientSeal,packet.Length-8,packet);
        PacketHandler.HandlePacket(packet,actor);
    }
    static void VerifyReview343GuildCommands(Map town,GameState leader,Revora.Game.ConquerStructures.Society.Guild guild){
        // Supplied guild/leader prerequisites, followed by ordinary production
        // invitation and acceptance packets. This does not prove paid creation.
        leader=ReloadBall(leader);
        Check(leader.Guild==guild && leader.AsMember.Rank==Enums.GuildMemberRank.GuildLeader,
            "REVIEW-343 production reload restores the fixture leader for command checks");
        foreach(uint acceptingAction in new[]{GuildCommand.JoinRequest,GuildCommand.InviteRequest}){
            var applicant=BallPlayer(town);HeroProgress(applicant,0,61);applicant.Entity.Class=11;
            for(int rule=0;rule<4;rule++){
                guild.LevelRequirement=0;guild.RebornRequirement=0;guild.ClassRequirement=0;
                guild.BlackList.Remove(applicant.Entity.UID);
                leader.GuildJoinTarget=0;applicant.GuildJoinTarget=0;
                if(acceptingAction==GuildCommand.JoinRequest)Review343Command(leader,GuildCommand.InviteRequest,applicant.Entity.UID);
                else Review343Command(applicant,GuildCommand.JoinRequest,leader.Entity.UID);
                if(rule==0)guild.LevelRequirement=120;
                if(rule==1)guild.RebornRequirement=1;
                if(rule==2)guild.ClassRequirement=UInt32.MaxValue;
                if(rule==3)guild.BlackList.Add(applicant.Entity.UID);
                if(acceptingAction==GuildCommand.JoinRequest)Review343Command(applicant,acceptingAction,leader.Entity.UID);
                else Review343Command(leader,acceptingAction,applicant.Entity.UID);
                Check(applicant.Guild==null && Scalar("SELECT COALESCE(guildid,0) FROM entities WHERE uid=@uid",applicant.Entity.UID)==0,
                    "REVIEW-343 reciprocal acceptance rechecks requirement/blacklist: "+acceptingAction+"/"+rule);
            }
            guild.LevelRequirement=0;guild.RebornRequirement=0;guild.ClassRequirement=0;guild.BlackList.Remove(applicant.Entity.UID);
            leader.GuildJoinTarget=0;applicant.GuildJoinTarget=0;
            if(acceptingAction==GuildCommand.JoinRequest){
                Review343Command(leader,GuildCommand.InviteRequest,applicant.Entity.UID);
                Review343Command(applicant,acceptingAction,leader.Entity.UID);
            }else{
                Review343Command(applicant,GuildCommand.JoinRequest,leader.Entity.UID);
                Review343Command(leader,acceptingAction,applicant.Entity.UID);
            }
            Check(!Fenced(applicant) && applicant.Guild==guild && Scalar("SELECT guildid FROM entities WHERE uid=@uid",applicant.Entity.UID)==guild.ID,
                "REVIEW-343 ordinary reciprocal acceptance succeeds: "+acceptingAction);
            var stale=applicant;applicant=ReloadBall(applicant);
            Review343Command(stale,GuildCommand.Quit,0);
            Check(applicant.Guild==guild && Scalar("SELECT guildid FROM entities WHERE uid=@uid",applicant.Entity.UID)==guild.ID,
                "REVIEW-343 replaced session cannot quit the current membership: "+acceptingAction);
            typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(applicant.Entity,true);
            Review343Command(applicant,GuildCommand.Quit,0);
            Check(applicant.Guild==guild && Scalar("SELECT guildid FROM entities WHERE uid=@uid",applicant.Entity.UID)==guild.ID,
                "REVIEW-343 fenced session cannot quit: "+acceptingAction);
            DemonReset(applicant);HeroClaim(applicant,1,303);
            Check(HeroRows(applicant,303,false)==0 && HeroPacksOf(applicant,3005123)==0,
                "REVIEW-343 ordinary invitation does not enable unfinished guild study issuance: "+acceptingAction);
            Review343Command(applicant,GuildCommand.Quit,0);applicant=ReloadBall(applicant);
            Check(applicant.Guild==null && Scalar("SELECT COALESCE(guildid,0) FROM entities WHERE uid=@uid",applicant.Entity.UID)==0
                && Scalar("SELECT guildrank FROM entities WHERE uid=@uid",applicant.Entity.UID)==0,
                "REVIEW-343 current ordinary quit clears membership and rank across reload: "+acceptingAction);
        }
        var pending=BallPlayer(town);HeroProgress(pending,0,61);
        leader.GuildJoinTarget=0;
        typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(pending.Entity,true);
        Review343Command(leader,GuildCommand.InviteRequest,pending.Entity.UID);
        Check(leader.GuildJoinTarget==0 && pending.Guild==null,"REVIEW-343 invitation refuses a fenced target session");
        DemonReset(pending);pending.GuildJoinTarget=0;
        typeof(Entity).GetField("PersistenceRequiresReload",Instance).SetValue(leader.Entity,true);
        Review343Command(pending,GuildCommand.JoinRequest,leader.Entity.UID);
        Check(pending.GuildJoinTarget==0 && pending.Guild==null,"REVIEW-343 application refuses a fenced recruiter session");
        DemonReset(leader);
        VerifyGuildRowMaterialization(town);
    }

    static void VerifyGuildRowMaterialization(Map town){
        // REVIEW-348 supplied rows prove legacy materialization, not paid creation or identity recovery.
        Check(typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildRecovery")==null,
            "REVIEW-348 unsafe enrollment-based recovery is disconnected");
        Query("CREATE TABLE IF NOT EXISTS guildenemy (LIKE public.guildenemy INCLUDING ALL);CREATE TABLE IF NOT EXISTS guildally (LIKE public.guildally INCLUDING ALL);CREATE TABLE IF NOT EXISTS pt_arsenal (LIKE public.pt_arsenal INCLUDING ALL);CREATE TABLE IF NOT EXISTS pt_arsenal_inscribed (LIKE public.pt_arsenal_inscribed INCLUDING ALL)");
        var leader=BallPlayer(town);var deputy=BallPlayer(town);var spouse=BallPlayer(town);
        var weapon=Pack(leader,410009,1);
        Query("INSERT INTO guilds(id,name,leadername,bulletin,silverfund,conquerpointfund,enrollmentdate,bulletindate,levelrequirement,rebornrequirement,classrequirement,wins,losts,unionid,autorecruit,malefemale,adv,ad_rate,adend,ctfpoints,ctfreward,maxenchant) VALUES(4101,'HarborGuild',@leader,'Harbor bulletin',888888,12345,20260924,20260924,90,1,10,9,4,77,2,1,'Saved advertisement',123,0,56,78,3)","leader",leader.Entity.Name);
        Query("INSERT INTO guilds(id,name,leadername,bulletin,silverfund,enrollmentdate,bulletindate) VALUES(4104,'AllyHarbor','AllyLeader','',1,20260924,20260924)");
        Query("UPDATE entities SET guildid=4101,guildrank=@rank,spouse='None' WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)leader.Entity.UID);
        Query("UPDATE entities SET guildid=4101,guildrank=@rank,spouse='None' WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.DeputyLeader,"uid",(long)deputy.Entity.UID);
        Query("UPDATE entities SET guildid=4101,guildrank=@rank,spouse=@spouse WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.Member,"spouse",leader.Entity.Name,"uid",(long)spouse.Entity.UID);
        Query("INSERT INTO guildally(guildid,allyid) VALUES(4101,4104),(4104,4101);INSERT INTO guildenemy(guildid,enemyid) VALUES(4104,4101)");
        Query("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(4101,2,1,2,3)");
        Query("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,@name,@item,2,4101,456)","uid",(long)leader.Entity.UID,"name",leader.Entity.Name,"item",(long)weapon.UID);
        Kernel.Guilds.Remove(4101);Kernel.Guilds.Remove(4104);
        var single=(Revora.Game.ConquerStructures.Society.Guild)Call(typeof(GuildTable),null,"LoadRecord",(uint)4101);
        Check(!Kernel.Guilds.ContainsKey(4101) && !Kernel.Guilds_adv.ContainsKey(4101),
            "REVIEW-348 single-row materialization does not publish registry or advertisement entries");
        Check(single.Name=="HarborGuild" && single.SilverFund==888888 && single.ConquerPointFund==12345
            && single.Bulletin=="Harbor bulletin" && single.EnrollmentDate==20260924 && single.UnionID==77 && single.Wins==9 && single.Losts==4,
            "REVIEW-348 single-row loader retains real name, funds and saved guild metadata");
        Check(single.LevelRequirement==90 && single.RebornRequirement==1 && single.ClassRequirement==10 && single.AutoRecruit==2
            && single.MaleFemale==1 && single.adv=="Saved advertisement" && single.ad_rate==123 && single.Points==56 && single.CTFReward==78 && single.maxenchant==3,
            "REVIEW-348 single-row loader retains recruitment and event settings");
        Check(single.Leader!=null && single.Leader.ID==leader.Entity.UID && single.MemberCount==3
            && single.Members[deputy.Entity.UID].Rank==Enums.GuildMemberRank.DeputyLeader
            && single.Members[spouse.Entity.UID].Rank==Enums.GuildMemberRank.LeaderSpouse,
            "REVIEW-348 single-row loader applies the startup leader and spouse rules");
        var arsenal=single.Arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Weapon];
        var inscription=arsenal.Inscribed[weapon.UID];
        Check(arsenal.Unlocked && arsenal.Enhancement==2 && arsenal.ars_bp==3 && inscription.UID==leader.Entity.UID
            && inscription.Name==leader.Entity.Name && inscription.donation==456 && inscription.Item!=null && inscription.Item.ID==410009,
            "REVIEW-348 populated arsenal preserves saved inscription and materializes its actual item");
        GuildTable.Load();var startup=Kernel.Guilds[4101];
        Check(startup.Name==single.Name && startup.SilverFund==single.SilverFund && startup.Leader.ID==single.Leader.ID
            && startup.Members[spouse.Entity.UID].Rank==single.Members[spouse.Entity.UID].Rank
            && startup.Arsenal.Arsenals[Revora.Game.Features.Arsenal_ID.Weapon].Inscribed[weapon.UID].Item.ID==410009,
            "REVIEW-348 actual startup and single-row loader share metadata, spouse and populated arsenal results");
        Check(startup.Ally.ContainsKey(4104) && Kernel.Guilds[4104].Ally.ContainsKey(4101) && Kernel.Guilds[4104].Enemy.ContainsKey(4101),
            "REVIEW-348 startup resolves both relationship directions after every guild is registered");
        leader=ReloadBall(leader);
        var leadership=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildLeadership");
        Check((bool)Call(leadership,null,"Leader",leader),"REVIEW-348 ordinary reconnect retains legacy leader eligibility");
        HeroProgress(leader,0,61);HeroClaim(leader,1,303);
        Check(HeroRows(leader,303,false)==0 && HeroPacksOf(leader,3005123)==0,
            "REVIEW-348 materialized legacy guild does not pay stage 303");
        Check(Call(typeof(GuildTable),null,"LoadRecord",(uint)4199)==null && !Kernel.Guilds.ContainsKey(4199),
            "REVIEW-348 missing row returns no fabricated guild");
        Query("ALTER TABLE pt_arsenal RENAME TO review348_unavailable_arsenal");bool readFailed=false;
        try {Call(typeof(GuildTable),null,"LoadRecord",(uint)4101);}catch(Npgsql.NpgsqlException){readFailed=true;}
        finally {Query("ALTER TABLE review348_unavailable_arsenal RENAME TO pt_arsenal");}
        Check(readFailed && ReferenceEquals(Kernel.Guilds[4101],startup),
            "REVIEW-348 database read failure propagates without replacing the registered guild");
        VerifyGuildIdentity(town,leader);
    }
    static readonly Type GuildIdentityType=typeof(Entity).Assembly.GetType("Revora.Game.Features.GuildIdentity");
    static object IdentityRead(uint guild,uint uid){return Call(GuildIdentityType,null,"Read",guild,uid);}
    static string IdentityKind(object snap){return snap.GetType().GetField("Kind").GetValue(snap).ToString();}
    static object IdentityField(object snap,string name){return snap.GetType().GetField(name).GetValue(snap);}
    static void IdentityIndependentCopy(object saved, object copy, string path)
    {
        if(saved==null){Check(copy==null,path+" absent");return;}
        Type type=saved.GetType();
        if(type.IsValueType || saved is string){Check(saved.Equals(copy),path+" saved value");return;}
        Check(copy!=null && copy.GetType()==type && !ReferenceEquals(saved,copy),path+" independent object");
        Array rows=saved as Array;
        if(rows!=null){
            Array twins=(Array)copy;Check(rows.Length==twins.Length,path+" length");
            for(int i=0;i<rows.Length;i++)IdentityIndependentCopy(rows.GetValue(i),twins.GetValue(i),path+"["+i+"]");
        }else foreach(var field in type.GetFields())IdentityIndependentCopy(field.GetValue(saved),field.GetValue(copy),path+"."+field.Name);
    }
    // Supplied disconnected image evidence only; this is not a production addition writer.
    static Guid BindAdditionFixture(long id){
        Guid token=Guid.NewGuid();
        Query("UPDATE itemadding SET revora_addition_token=@token WHERE iditemadding=@id","token",token,"id",id);
        Query("UPDATE revora_item_addition_binding SET addition_token=@token WHERE addition_id=@id","token",token,"id",id);
        return token;
    }
    static void CheckAddition(object row,long id,int type,uint adding,uint level,uint percent,uint duration,long added,bool stored,int points,string name){
        Check(row!=null && (long)IdentityField(row,"RowId")==id && (int)IdentityField(row,"AddingType")==type
            && (uint)IdentityField(row,"AddingId")==adding && (uint)IdentityField(row,"Level")==level
            && (uint)IdentityField(row,"Percent")==percent && (uint)IdentityField(row,"Duration")==duration
            && (long)IdentityField(row,"AddedOnRaw")==added && (bool)IdentityField(row,"StabilizationStored")==stored
            && (int)IdentityField(row,"StabilizationPoints")==points, name);
    }
    static int IdentityRank(object snap,uint uid){
        foreach(object member in (Array)IdentityField(snap,"Members"))
            if((uint)member.GetType().GetField("Uid").GetValue(member)==uid)return (int)member.GetType().GetField("Rank").GetValue(member);
        return -1;
    }
    static void VerifyGuildIdentity(Map town,GameState untouched){
        // Supplied rows identify a guild. They are not a paid Guild Director purchase or a stage 303 claim.
        string root=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"..",".."));
        string guildSrc=File.ReadAllText(Path.Combine(root,"Database","GuildTable.cs"));
        string entitySrc=File.ReadAllText(Path.Combine(root,"Database","EntityTable.cs"));
        string npcs=File.ReadAllText(Path.Combine(root,"Npcs.cs"));
        string claims=File.ReadAllText(Path.Combine(root,"Game","Features","WayOfHeroesClaims.cs"));
        string itemSrc=File.ReadAllText(Path.Combine(root,"Database","ConquerItemTable.cs"));
        string arsenalSrc=File.ReadAllText(Path.Combine(root,"Database","ArsenalTable.cs"));
        string serverSrc=File.ReadAllText(Path.Combine(root,"Server.cs"));
        string addingSrc=File.ReadAllText(Path.Combine(root,"Database","ItemAddingTable.cs"));
        string refinerySrc=File.ReadAllText(Path.Combine(root,"Game","Features","RefineryProgress.cs"));
        string unionSrc=File.ReadAllText(Path.Combine(root,"Sistemas","Union","UnionTable.cs"));
        string nobilitySrc=File.ReadAllText(Path.Combine(root,"Database","NobilityTable.cs"));
        string identitySrc=File.ReadAllText(Path.Combine(root,"Game","Features","GuildIdentity.cs"));
        Check(guildSrc.IndexOf("GuildIdentity")<0 && entitySrc.IndexOf("GuildIdentity")<0 && npcs.IndexOf("GuildIdentity")<0 && claims.IndexOf("GuildIdentity")<0
            && itemSrc.IndexOf("GuildIdentity")<0 && arsenalSrc.IndexOf("GuildIdentity")<0 && serverSrc.IndexOf("GuildIdentity")<0
            && addingSrc.IndexOf("GuildIdentity")<0 && refinerySrc.IndexOf("GuildIdentity")<0 && unionSrc.IndexOf("GuildIdentity")<0
            && nobilitySrc.IndexOf("GuildIdentity")<0,
            "guild identity reads are disconnected from startup, reconnect, item loaders, NPC creation and claims");
        Check(identitySrc.IndexOf("Nobility.Sort(")<0 && identitySrc.IndexOf("Nobility.Board[")<0,
            "the disconnected read does not call live nobility ranking");
        Check(guildSrc.IndexOf("BlackList")<0,"blacklist rows are not loaded by the guild table");
        object uninstalled=IdentityRead(4201,untouched.Entity.UID);
        Check(IdentityKind(uninstalled)=="ReadFailure" && Scalar("SELECT count(*) FROM information_schema.columns WHERE table_schema=current_schema() AND table_name='guilds' AND column_name='incarnation'",0)==0,
            "REVIEW-353 read does not install schema or disguise an unavailable identity column as legacy");
        Query("CREATE TABLE IF NOT EXISTS unions (LIKE public.unions INCLUDING ALL)");
        Query("CREATE TABLE IF NOT EXISTS nobility (LIKE public.nobility INCLUDING ALL)");
        Call(GuildIdentityType,null,"EnsureSchema");
        var founder=BallPlayer(town);var deputy=BallPlayer(town);var ordinary=BallPlayer(town);var outsider=BallPlayer(town);
        Guid first=Guid.NewGuid();Guid second=Guid.NewGuid();Guid unionToken=Guid.NewGuid();
        Guid founderToken=Guid.NewGuid();Guid deputyToken=Guid.NewGuid();Guid ordinaryToken=Guid.NewGuid();Guid outsiderToken=Guid.NewGuid();Guid untouchedToken=Guid.NewGuid();
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",founderToken,"uid",(long)founder.Entity.UID);
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",deputyToken,"uid",(long)deputy.Entity.UID);
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",ordinaryToken,"uid",(long)ordinary.Entity.UID);
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",outsiderToken,"uid",(long)outsider.Entity.UID);
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",untouchedToken,"uid",(long)untouched.Entity.UID);
        string officials=founder.Entity.UID.ToString()+"#0#0#0#0#0#0#0";
        string haremRoles="0#0#0#0#0#0#0#0#0#0";
        string guardRoles="0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0";
        Query("INSERT INTO guilds(id,name,leadername,bulletin,silverfund,conquerpointfund,enrollmentdate,bulletindate,levelrequirement,rebornrequirement,classrequirement,wins,losts,unionid,autorecruit,malefemale,adv,ad_rate,adend,ctfpoints,ctfreward,maxenchant,incarnation,adlevelrequirement,adrebornrequirement,adclassrequirement,ctfdonationcps,ctfdonationsilver,ctfdonationcpsold,ctfdonationsilverold) VALUES(4201,'HarborGuild',@leader,'Harbor bulletin',888888,12345,20260924,20260925,90,1,10,9,4,77,2,1,'Saved advertisement',123,24680,56,78,3,@life,11,2,4,15,16,17,18)",
            "leader",founder.Entity.Name,"life",first);
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation,entity_token) VALUES(@uid,4201,@life,@token)","uid",(long)founder.Entity.UID,"life",first,"token",founderToken);
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation,entity_token) VALUES(@uid,4201,@life,@token)","uid",(long)deputy.Entity.UID,"life",first,"token",deputyToken);
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation,entity_token) VALUES(@uid,4201,@life,@token)","uid",(long)ordinary.Entity.UID,"life",first,"token",ordinaryToken);
        Query("UPDATE entities SET guildid=4201,guildrank=@rank,spouse='None' WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)founder.Entity.UID);
        Query("UPDATE entities SET guildid=4201,guildrank=@rank,spouse='None' WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.DeputyLeader,"uid",(long)deputy.Entity.UID);
        Query("UPDATE entities SET guildid=4201,guildrank=@rank,spouse=@spouse WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.Member,"spouse",founder.Entity.Name,"uid",(long)ordinary.Entity.UID);
        Query("INSERT INTO unions(id,goldbricks,unionfund,name,title,leadername,leaderuid,bulletin,declaration,coreofficals,harem,guards,revora_union_token) VALUES(77,12,880000,'HarborUnion','HarborTitle',@leader,@uid,'Union bulletin','Union declaration',@core,@harem,@guards,@token)",
            "leader",founder.Entity.Name,"uid",(long)founder.Entity.UID,"core",officials,"harem",haremRoles,"guards",guardRoles,"token",unionToken);
        Query("UPDATE entities SET unionid=77,unionexploits=640,offical=1,harem=0,guards=0 WHERE uid=@uid","uid",(long)founder.Entity.UID);
        Query("UPDATE entities SET unionid=0,unionexploits=0,offical=0,harem=0,guards=0 WHERE uid=@uid","uid",(long)deputy.Entity.UID);
        Query("UPDATE entities SET unionid=0,unionexploits=0,offical=0,harem=0,guards=0 WHERE uid=@uid","uid",(long)ordinary.Entity.UID);
        Query("UPDATE entities SET unionid=77,unionexploits=15,offical=0,harem=0,guards=0 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,77,@token,@entity)","uid",(long)founder.Entity.UID,"token",unionToken,"entity",founderToken);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,77,@token,@entity)","uid",(long)outsider.Entity.UID,"token",unionToken,"entity",outsiderToken);
        Query("INSERT INTO revora_guild_union_binding(incarnation,guild_id,union_id,union_token) VALUES(@life,4201,77,@token)","life",first,"token",unionToken);
        var stale=new Revora.Game.ConquerStructures.Society.Guild("WrongLeader");
        stale.ID=4201;stale.Name="Guild4201";stale.SilverFund=500000;Kernel.Guilds[4201]=stale;
        int registries=Kernel.Guilds.Count;int advertisements=Kernel.Guilds_adv.Count;int sessions=Kernel.GamePool.Count;int unionsBefore=Kernel.Unions.Count;
        object founderGuild=founder.Guild;object founderMember=founder.AsMember;ushort founderRank=founder.Entity.GuildRank;ushort founderGuildId=founder.Entity.GuildID;
        long claimsBefore=HeroRows(founder,303,false)+HeroRows(founder,303,true);
        Check(IdentityKind(IdentityRead(4201,founder.Entity.UID))=="Contradiction",
            "REVIEW-373 earlier legacy fixture guild 4101 cannot silently join a managed union by numeric id");
        Query("UPDATE guilds SET unionid=0 WHERE id=4101");
        object read=IdentityRead(4201,founder.Entity.UID);
        Check(IdentityKind(read)=="Consistent" && (Guid)IdentityField(read,"Incarnation")==first && (string)IdentityField(read,"Name")=="HarborGuild"
            && (ulong)IdentityField(read,"SilverFund")==888888 && (uint)IdentityField(read,"ConquerPointFund")==12345
            && (string)IdentityField(read,"Bulletin")=="Harbor bulletin" && (uint)IdentityField(read,"EnrollmentDate")==20260924
            && (uint)IdentityField(read,"LevelRequirement")==90 && (uint)IdentityField(read,"AutoRecruit")==2 && (uint)IdentityField(read,"UnionId")==77
            && Kernel.Unions.Count==unionsBefore
            && (uint)IdentityField(read,"Wins")==9 && (uint)IdentityField(read,"CtfPoints")==56 && (uint)IdentityField(read,"CtfReward")==78
            && (string)IdentityField(read,"Advertisement")=="Saved advertisement" && (uint)IdentityField(read,"MaleFemale")==1
            && (uint)IdentityField(read,"AdLevelRequirement")==11 && (uint)IdentityField(read,"AdRebornRequirement")==2
            && (uint)IdentityField(read,"AdClassRequirement")==4 && (long)IdentityField(read,"AdvertisementEndsRaw")==24680
            && (uint)IdentityField(read,"CtfDonationCps")==15 && (uint)IdentityField(read,"CtfDonationSilver")==16
            && (uint)IdentityField(read,"CtfDonationCpsOld")==17 && (uint)IdentityField(read,"CtfDonationSilverOld")==18
            && (uint)IdentityField(read,"LeaderUid")==founder.Entity.UID
            && IdentityRank(read,deputy.Entity.UID)==(int)Enums.GuildMemberRank.DeputyLeader
            && IdentityRank(read,ordinary.Entity.UID)==(int)Enums.GuildMemberRank.Member,
            "identity read returns the saved row, fund 888888 and exact ranks without deriving spouse rank");
        Check(ReferenceEquals(Kernel.Guilds[4201],stale) && stale.Name=="Guild4201" && stale.SilverFund==500000
            && Kernel.Guilds.Count==registries && Kernel.Guilds_adv.Count==advertisements && Kernel.GamePool.Count==sessions
            && founder.Guild==founderGuild && founder.AsMember==founderMember && founder.Entity.GuildRank==founderRank && founder.Entity.GuildID==founderGuildId
            && HeroRows(founder,303,false)+HeroRows(founder,303,true)==claimsBefore,
            "identity read leaves the stale cache, sessions and rewards unchanged");
        object savedUnion=IdentityField(read,"Union");
        Array unionMembers=(Array)IdentityField(savedUnion,"Members");
        bool sawOutsider=false;bool deputyInUnion=false;bool deputyUnjoined=false;bool founderStored=false;
        foreach(object member in unionMembers){
            uint uid=(uint)IdentityField(member,"Uid");
            if(uid==outsider.Entity.UID) sawOutsider=IdentityField(member,"GuildId")==null && (ulong)IdentityField(member,"Exploits")==15UL && (int)IdentityField(member,"Official")==0;
            if(uid==deputy.Entity.UID) deputyInUnion=true;
        }
        foreach(object member in (Array)IdentityField(read,"Members")){
            if((uint)IdentityField(member,"Uid")==founder.Entity.UID) founderStored=(uint)IdentityField(member,"StoredUnionId")==77 && (ulong)IdentityField(member,"UnionExploits")==640UL && (int)IdentityField(member,"UnionOfficial")==1;
            if((uint)IdentityField(member,"Uid")==deputy.Entity.UID && (uint)IdentityField(member,"StoredUnionId")==0) deputyUnjoined=true;
        }
        Check(savedUnion!=null && (Guid)IdentityField(savedUnion,"Token")==unionToken && (uint)IdentityField(savedUnion,"GoldBricks")==12
            && (uint)IdentityField(savedUnion,"Id")==77 && unionMembers.Length==2
            && (ulong)IdentityField(savedUnion,"Fund")==880000UL && (string)IdentityField(savedUnion,"Name")=="HarborUnion"
            && (string)IdentityField(savedUnion,"Title")=="HarborTitle" && (uint)IdentityField(savedUnion,"LeaderUid")==founder.Entity.UID
            && (string)IdentityField(savedUnion,"LeaderName")==founder.Entity.Name && (string)IdentityField(savedUnion,"Bulletin")=="Union bulletin"
            && (string)IdentityField(savedUnion,"Declaration")=="Union declaration" && (string)IdentityField(savedUnion,"OfficialsRaw")==officials
            && (string)IdentityField(savedUnion,"HaremRaw")==haremRoles && (string)IdentityField(savedUnion,"GuardsRaw")==guardRoles
            && ((uint[])IdentityField(savedUnion,"Officials")).Length==8 && ((uint[])IdentityField(savedUnion,"Officials"))[0]==founder.Entity.UID
            && ((uint[])IdentityField(savedUnion,"Harem")).Length==10 && ((uint[])IdentityField(savedUnion,"Guards")).Length==20
            && sawOutsider && founderStored && deputyUnjoined && !deputyInUnion && Kernel.Unions.Count==unionsBefore,
            "the saved union keeps its token, an independent member, and does not require every guild member to join");
        Query("DELETE FROM revora_guild_member_identity WHERE entity_uid=@uid","uid",(long)ordinary.Entity.UID);
        Check(IdentityKind(IdentityRead(4201,founder.Entity.UID))=="Contradiction",
            "REVIEW-353 a non-requester saved member without an identity invalidates the whole roster");
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation,entity_token) VALUES(@uid,4201,@life,@token)","uid",(long)ordinary.Entity.UID,"life",first,"token",ordinaryToken);
        Query("UPDATE entities SET guildrank=@rank WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)ordinary.Entity.UID);
        Check(IdentityKind(IdentityRead(4201,founder.Entity.UID))=="Contradiction",
            "REVIEW-353 two saved leaders cannot form a consistent roster");
        Query("UPDATE entities SET guildrank=@rank WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.Member,"uid",(long)ordinary.Entity.UID);
        Query("UPDATE revora_guild_member_identity SET guild_id=4299 WHERE entity_uid=@uid","uid",(long)ordinary.Entity.UID);
        Check(IdentityKind(IdentityRead(4201,founder.Entity.UID))=="Contradiction",
            "REVIEW-353 a non-requester identity with another guild id invalidates the roster");
        Query("UPDATE revora_guild_member_identity SET guild_id=4201 WHERE entity_uid=@uid","uid",(long)ordinary.Entity.UID);
        Check(IdentityKind(IdentityRead(4299,founder.Entity.UID))=="Contradiction",
            "REVIEW-353 a known managed requester cannot downgrade through another missing guild id");
        Query("UPDATE revora_guild_member_identity SET incarnation=@life WHERE entity_uid=@uid","life",Guid.NewGuid(),"uid",(long)ordinary.Entity.UID);
        Check(IdentityKind(IdentityRead(4201,founder.Entity.UID))=="Contradiction",
            "REVIEW-353 stale non-requester incarnation cannot be silently omitted");
        Query("UPDATE revora_guild_member_identity SET incarnation=@life WHERE entity_uid=@uid","life",first,"uid",(long)ordinary.Entity.UID);
        Query("UPDATE guilds SET incarnation=@life WHERE id=4201","life",Guid.Empty);
        Query("UPDATE revora_guild_member_identity SET incarnation=@life WHERE guild_id=4201","life",Guid.Empty);
        Check(IdentityKind(IdentityRead(4201,founder.Entity.UID))=="Contradiction",
            "REVIEW-353 Guid.Empty is not a managed identity even when all marks match");
        Query("UPDATE revora_guild_member_identity SET incarnation=@life WHERE guild_id=4201","life",first);
        Query("UPDATE guilds SET incarnation=@life WHERE id=4201","life",second);
        object replaced=IdentityRead(4201,founder.Entity.UID);
        Check(IdentityKind(replaced)=="Contradiction","a same-day same-name same-founder replacement does not keep the old member identity");
        Query("UPDATE revora_guild_member_identity SET incarnation=@life WHERE guild_id=4201","life",second);
        Query("UPDATE revora_guild_union_binding SET incarnation=@life WHERE guild_id=4201","life",second);
        object current=IdentityRead(4201,founder.Entity.UID);
        Check(IdentityKind(current)=="Consistent" && (Guid)IdentityField(current,"Incarnation")==second && (string)IdentityField(current,"Name")=="HarborGuild"
            && (string)IdentityField(current,"LeaderName")==founder.Entity.Name && (uint)IdentityField(current,"EnrollmentDate")==20260924
            && (ulong)IdentityField(current,"SilverFund")==888888 && stale.SilverFund==500000,
            "the replacement row is a new incarnation even when every display field matches");
        Query("INSERT INTO guilds(id,name,leadername,enrollmentdate,bulletindate,bulletin,silverfund,incarnation) VALUES(4202,'HarborGuild',@leader,20260924,20260924,'',888888,@life)","leader",founder.Entity.Name,"life",Guid.NewGuid());
        object unnamed=IdentityRead(4202,founder.Entity.UID);
        Check(IdentityKind(unnamed)=="Contradiction","a managed guild without a member identity is not treated as legacy");
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation) VALUES(@uid,4201,@life)","uid",(long)untouched.Entity.UID,"life",Guid.NewGuid());
        object mismatched=IdentityRead(4201,untouched.Entity.UID);
        Check(IdentityKind(mismatched)=="Contradiction","a different member incarnation does not match the guild row");
        Query("DELETE FROM revora_guild_member_identity WHERE entity_uid=@uid","uid",(long)untouched.Entity.UID);
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation) VALUES(420199,4201,@life)","life",second);
        object missingEntity=IdentityRead(4201,420199);
        Check(IdentityKind(missingEntity)=="Contradiction","a member identity without an entity row is a contradiction");
        Query("DELETE FROM revora_guild_member_identity WHERE entity_uid=420199");
        Query("UPDATE entities SET guildrank=@rank WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.DeputyLeader,"uid",(long)founder.Entity.UID);
        Query("UPDATE entities SET guildrank=@rank WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)deputy.Entity.UID);
        Query("UPDATE guilds SET leadername=@leader WHERE id=4201","leader",deputy.Entity.Name);
        object transferred=IdentityRead(4201,deputy.Entity.UID);
        Check(IdentityKind(transferred)=="Consistent" && (uint)IdentityField(transferred,"LeaderUid")==deputy.Entity.UID
            && IdentityRank(transferred,founder.Entity.UID)==(int)Enums.GuildMemberRank.DeputyLeader
            && IdentityRank(transferred,ordinary.Entity.UID)==(int)Enums.GuildMemberRank.Member
            && (ulong)IdentityField(transferred,"SilverFund")==888888,
            "a changed leader read keeps the complete saved ranks and fund");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE entities SET level=level+1 WHERE uid=@uid","uid",(long)ordinary.Entity.UID);
        });
        object raced;
        try{raced=IdentityRead(4201,deputy.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(raced)=="ReadFailure" && ((string)IdentityField(raced,"Detail")).IndexOf("40001")>=0
            && ReferenceEquals(Kernel.Guilds[4201],stale),
            "REVIEW-353 a committed roster change after the snapshot fails closed instead of mixing versions");
        Check(IdentityKind(IdentityRead(4201,ordinary.Entity.UID))=="Consistent",
            "REVIEW-353 a fresh ordinary-member read succeeds after the concurrent roster change");
        var started=new System.Threading.ManualResetEvent(false);
        var connected=new System.Threading.ManualResetEvent(false);Exception writerError=null;int writerPid=0;bool blocked=false;
        var writer=new System.Threading.Thread(delegate(){
            started.WaitOne();
            try{
                using(var c=DataHolder.MySqlConnection){c.Open();
                    using(var pid=new Npgsql.NpgsqlCommand("SELECT pg_backend_pid()",c))writerPid=Convert.ToInt32(pid.ExecuteScalar());
                    connected.Set();
                    using(var set=new Npgsql.NpgsqlCommand("SET lock_timeout='8s'",c))set.ExecuteNonQuery();
                    using(var upd=new Npgsql.NpgsqlCommand("UPDATE guilds SET silverfund=1,bulletin='Mixed bulletin' WHERE id=4201",c))upd.ExecuteNonQuery();}
            }catch(Exception ex){writerError=ex;}
        });
        writer.IsBackground=true;writer.Start();
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            started.Set();
            if(!connected.WaitOne(5000))throw new Exception("fixture writer did not connect");
            var limit=System.Diagnostics.Stopwatch.StartNew();
            while(limit.ElapsedMilliseconds<5000){
                if(Scalar("SELECT cardinality(pg_blocking_pids(CAST(@uid AS integer)))",(uint)writerPid)>0){blocked=true;break;}
                System.Threading.Thread.Sleep(10);
            }
            if(!blocked)throw new Exception("fixture writer never blocked on the guild lock");
        });
        object held;
        try{held=IdentityRead(4201,deputy.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        bool joined=writer.Join(10000);
        object after=IdentityRead(4201,deputy.Entity.UID);
        Check(joined && blocked && writerError==null && IdentityKind(held)=="Consistent" && (ulong)IdentityField(held,"SilverFund")==888888
            && (string)IdentityField(held,"Bulletin")=="Harbor bulletin" && (string)IdentityField(held,"LeaderName")==deputy.Entity.Name
            && IdentityKind(after)=="Consistent" && (ulong)IdentityField(after,"SilverFund")==1
            && (string)IdentityField(after,"Bulletin")=="Mixed bulletin" && (string)IdentityField(after,"LeaderName")==deputy.Entity.Name,
            "a concurrent guild update cannot mix the saved fund with the saved bulletin or leader");
        Query("DELETE FROM guilds WHERE id=4201");
        Query("DELETE FROM revora_guild_union_binding WHERE guild_id=4201");
        object deleted=IdentityRead(4201,deputy.Entity.UID);
        Check(IdentityKind(deleted)=="Contradiction" && ReferenceEquals(Kernel.Guilds[4201],stale) && stale.Name=="Guild4201",
            "a deleted guild row with a remaining member identity is a contradiction and does not repair the cache");
        Query("INSERT INTO guilds(id,name,leadername,enrollmentdate,bulletindate,bulletin,silverfund) VALUES(4203,'LegacyGuild','LegacyLeader',20260924,20260924,'',500000)");
        object legacy=IdentityRead(4203,untouched.Entity.UID);
        Check(IdentityKind(legacy)=="None","a null incarnation stays unmanaged");
        Check(IdentityKind(IdentityRead(4203,founder.Entity.UID))=="Contradiction",
            "REVIEW-353 a managed requester cannot downgrade through a legacy guild");
        Check(IdentityKind(IdentityRead(4201,untouched.Entity.UID))=="Contradiction",
            "REVIEW-353 remaining non-requester marks expose a deleted owner");
        Query("ALTER TABLE revora_guild_member_identity RENAME TO review353_unavailable_identity");
        object failed;
        try{failed=IdentityRead(4203,founder.Entity.UID);}
        finally{Query("ALTER TABLE review353_unavailable_identity RENAME TO revora_guild_member_identity");}
        Check(IdentityKind(failed)=="ReadFailure" && ((string)IdentityField(failed,"Detail")).IndexOf("42P01")>=0
            && ReferenceEquals(Kernel.Guilds[4201],stale) && Kernel.Guilds.Count==registries,
            "a database read failure is not reported as an absent guild and does not change the registry");
        var imageFounder=BallPlayer(town);var imageMember=BallPlayer(town);var stray=BallPlayer(town);
        Guid imageFounderToken=Guid.NewGuid();Guid imageMemberToken=Guid.NewGuid();Guid strayToken=Guid.NewGuid();
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",imageFounderToken,"uid",(long)imageFounder.Entity.UID);
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",strayToken,"uid",(long)stray.Entity.UID);
        Guid imageLife=Guid.NewGuid();Guid allyLife=Guid.NewGuid();Guid replacedAlly=Guid.NewGuid();Guid weaponToken=Guid.NewGuid();
        var imageWeapon=Pack(imageFounder,410009,1);
        Query("INSERT INTO guilds(id,name,leadername,bulletin,silverfund,enrollmentdate,bulletindate,unionid,incarnation) VALUES(4210,'ImageGuild',@leader,'',888888,20260924,20260924,77,@life)","leader",imageFounder.Entity.Name,"life",imageLife);
        Query("INSERT INTO revora_guild_union_binding(incarnation,guild_id,union_id,union_token) VALUES(@life,4210,77,@token)","life",imageLife,"token",unionToken);
        Query("INSERT INTO guilds(id,name,leadername,bulletin,silverfund,enrollmentdate,bulletindate,incarnation) VALUES(4211,'AllyHarbor','AllyLeader','',1,20260924,20260924,@life)","life",allyLife);
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation,entity_token) VALUES(@uid,4210,@life,@token)","uid",(long)imageFounder.Entity.UID,"life",imageLife,"token",imageFounderToken);
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation,entity_token) VALUES(@uid,4210,@life,@token)","uid",(long)imageMember.Entity.UID,"life",imageLife,"token",imageMemberToken);
        Query("UPDATE entities SET guildid=4210,guildrank=@rank,spouse='None',lastlogin=246801,guildsilverdonation=81,guildconquerpointdonation=82,arsenaldonation=83,pkpoints=84,exploits=85,ctfcpsreward=86,ctfsilverreward=87,guildrouses=88,guildlilies=89,guildtulips=90,guildorchids=91 WHERE uid=@uid",
            "rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)imageFounder.Entity.UID);
        Query("UPDATE entities SET guildid=4210,guildrank=@rank,spouse=@spouse WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.Member,"spouse",imageFounder.Entity.Name,"uid",(long)imageMember.Entity.UID);
        Query("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(4210,2,1,2,3)");
        Query("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,@name,@item,2,4210,456)","uid",(long)imageFounder.Entity.UID,"name",imageFounder.Entity.Name,"item",(long)imageWeapon.UID);
        Query("INSERT INTO guildally(guildid,allyid) VALUES(4210,4211)");
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id,peer_incarnation) VALUES(@life,4210,'arsenal',2,NULL),(@life,4210,'inscribed',@item,NULL),(@life,4210,'ally',4211,@peer)",
            "life",imageLife,"item",(long)imageWeapon.UID,"peer",allyLife);
        Query("UPDATE items SET plus=9,socketone=1,sockettwo=0,bound=1,inscribed=1,durability=3200,maximdurability=3600,bless=3,enchant=7,locked=2,unlockend=246801,suspicious=1,suspiciousstart=97531,color=4,warehouse=42,stacksize=2,socketprogress=11,plusprogress=12,effect=200,refineryitem=724440,refinerytime=86420,inwardrobe=1,daystamp=13579,days=3,perfectionlevel=2,perfectionowner='Harbor',perfectionownerguid=44,perfectionprogress=5,signature='HarborMark',runesexp=6,relicattrib1=7,relicattrib2=8,relicattrib3=9,relicattrib4=10,relicattrib5=11,agate='saved-agate',nextsteedcolor=66051,position=4,revora_item_token=@token WHERE uid=@item",
            "token",weaponToken,"item",(long)imageWeapon.UID);
        Query("INSERT INTO revora_guild_item_binding(item_uid,incarnation,owner_uid,item_token) VALUES(@item,@life,@owner,@token)",
            "item",(long)imageWeapon.UID,"life",imageLife,"owner",(long)imageFounder.Entity.UID,"token",weaponToken);
        object image=IdentityRead(4210,imageMember.Entity.UID);
        object owner=Call(GuildIdentityType,null,"ReadOwner",(uint)4210);
        object arsenal=((Array)IdentityField(image,"Arsenal")).GetValue(0);
        object inscription=((Array)IdentityField(image,"Inscriptions")).GetValue(0);
        object ally=((Array)IdentityField(image,"Allies")).GetValue(0);
        object imageRoster=null;
        foreach(object member in (Array)IdentityField(image,"Members"))
            if((uint)member.GetType().GetField("Uid").GetValue(member)==imageFounder.Entity.UID) imageRoster=member;
        Check(IdentityKind(image)=="Consistent" && (uint)IdentityField(image,"LeaderUid")==imageFounder.Entity.UID
            && IdentityRank(image,imageMember.Entity.UID)==(int)Enums.GuildMemberRank.Member
            && imageRoster!=null && (ulong)imageRoster.GetType().GetField("SilverDonation").GetValue(imageRoster)==81
            && (ulong)imageRoster.GetType().GetField("ConquerPointDonation").GetValue(imageRoster)==82
            && (ulong)imageRoster.GetType().GetField("ArsenalDonation").GetValue(imageRoster)==83
            && (ulong)imageRoster.GetType().GetField("PkPoints").GetValue(imageRoster)==84
            && (uint)imageRoster.GetType().GetField("Exploits").GetValue(imageRoster)==85
            && (uint)imageRoster.GetType().GetField("CtfCpsReward").GetValue(imageRoster)==86
            && (uint)imageRoster.GetType().GetField("CtfSilverReward").GetValue(imageRoster)==87
            && (uint)imageRoster.GetType().GetField("Lilies").GetValue(imageRoster)==89
            && (uint)imageRoster.GetType().GetField("Tulips").GetValue(imageRoster)==90
            && (uint)imageRoster.GetType().GetField("Orchids").GetValue(imageRoster)==91
            && (uint)imageRoster.GetType().GetField("Rouses").GetValue(imageRoster)==88
            && (long)imageRoster.GetType().GetField("LastLoginRaw").GetValue(imageRoster)==246801
            && (int)arsenal.GetType().GetField("Id").GetValue(arsenal)==2 && (bool)arsenal.GetType().GetField("Unlocked").GetValue(arsenal)
            && (uint)arsenal.GetType().GetField("BattlePower").GetValue(arsenal)==3
            && (uint)inscription.GetType().GetField("OwnerUid").GetValue(inscription)==imageFounder.Entity.UID
            && (string)inscription.GetType().GetField("OwnerName").GetValue(inscription)==imageFounder.Entity.Name
            && (uint)inscription.GetType().GetField("ItemUid").GetValue(inscription)==imageWeapon.UID
            && (int)inscription.GetType().GetField("ArsenalId").GetValue(inscription)==2
            && (uint)arsenal.GetType().GetField("Enhancement").GetValue(arsenal)==2 && (uint)inscription.GetType().GetField("ItemId").GetValue(inscription)==410009
            && (uint)inscription.GetType().GetField("Donation").GetValue(inscription)==456
            && (uint)ally.GetType().GetField("GuildId").GetValue(ally)==4211 && (Guid)ally.GetType().GetField("Incarnation").GetValue(ally)==allyLife
            && IdentityKind(owner)=="Consistent" && (uint)IdentityField(owner,"LeaderUid")==imageFounder.Entity.UID
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null,
            "the saved image keeps donations, arsenal, the real item, the ally incarnation, and the offline leader");
        object savedItem=IdentityField(inscription,"Item");
        Check(savedItem!=null && !ReferenceEquals(savedItem,image)
            && (int)IdentityField(savedItem,"Plus")==9 && (bool)IdentityField(savedItem,"Bound")
            && (int)IdentityField(savedItem,"SocketOne")==1 && (int)IdentityField(savedItem,"SocketTwo")==0
            && (bool)IdentityField(savedItem,"Inscribed") && (uint)IdentityField(savedItem,"Id")==410009
            && (uint)IdentityField(savedItem,"Uid")==imageWeapon.UID && (int)IdentityField(savedItem,"Durability")==3200
            && (int)IdentityField(savedItem,"MaxDurability")==3600 && (int)IdentityField(savedItem,"Bless")==3
            && (int)IdentityField(savedItem,"Enchant")==7 && (int)IdentityField(savedItem,"Lock")==2
            && (long)IdentityField(savedItem,"UnlockEndRaw")==246801 && (uint)IdentityField(savedItem,"Color")==4
            && (uint)IdentityField(savedItem,"SocketProgress")==11 && (uint)IdentityField(savedItem,"PlusProgress")==12
            && (long)IdentityField(savedItem,"DayStampRaw")==13579 && (uint)IdentityField(savedItem,"PerfectionLevel")==2
            && (string)IdentityField(savedItem,"PerfectionOwner")=="Harbor" && (string)IdentityField(savedItem,"Signature")=="HarborMark"
            && (uint)IdentityField(savedItem,"RunesExp")==6 && (uint)IdentityField(savedItem,"Relic1")==7
            && (uint)IdentityField(savedItem,"Relic5")==11 && (Guid)IdentityField(savedItem,"Token")==weaponToken
            && (uint)IdentityField(inscription,"Donation")==456,
            "stored plus, sockets, bound, inscribed state and donation stay raw; donation is not recomputed from plus");
        object[,] additionalSavedFields = {
            {"Position",4},{"Agate","saved-agate"},{"Effect",200},{"Suspicious",true},{"SuspiciousStartRaw",97531L},
            {"Warehouse",42U},{"StackSize",2},{"RefineItem",724440U},{"RefineryTimeRaw",86420L},{"InWardrobe",true},
            {"Days",3},{"PerfectionOwnerGuid",44U},{"PerfectionProgress",5U},{"Relic2",8U},{"Relic3",9U},{"Relic4",10U},
            {"NextSteedColorRaw",66051U}
        };
        for(int field=0;field<additionalSavedFields.GetLength(0);field++)
            Check(object.Equals(IdentityField(savedItem,(string)additionalSavedFields[field,0]),additionalSavedFields[field,1]),
                "REVIEW-363 nonzero stored item field: "+additionalSavedFields[field,0]);
        // REVIEW-363: malformed stored values cannot become valid-looking item defaults.
        string[] malformedSql = {
            "UPDATE items SET suspicious=2 WHERE uid=@item",
            "UPDATE items SET inwardrobe=2 WHERE uid=@item",
            "UPDATE items SET durability=-1 WHERE uid=@item",
            "UPDATE items SET position=256 WHERE uid=@item",
            "UPDATE items SET bless=256 WHERE uid=@item",
            "UPDATE items SET effect=17 WHERE uid=@item"
        };
        string[] restoredSql = {
            "UPDATE items SET suspicious=0 WHERE uid=@item",
            "UPDATE items SET inwardrobe=0 WHERE uid=@item",
            "UPDATE items SET durability=3200 WHERE uid=@item",
            "UPDATE items SET position=4 WHERE uid=@item",
            "UPDATE items SET bless=3 WHERE uid=@item",
            "UPDATE items SET effect=0 WHERE uid=@item"
        };
        for(int invalid=0;invalid<malformedSql.Length;invalid++){
            Query(malformedSql[invalid],"item",(long)imageWeapon.UID);
            object badItem=IdentityRead(4210,imageMember.Entity.UID);
            Check(IdentityKind(badItem)=="Contradiction", "REVIEW-363 rejects malformed saved item: "+invalid);
            Query(restoredSql[invalid],"item",(long)imageWeapon.UID);
        }
        Query("ALTER TABLE items ALTER COLUMN plus DROP NOT NULL");
        Query("UPDATE items SET plus=NULL WHERE uid=@item","item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-363 rejects NULL plus instead of zero");
        Query("UPDATE items SET plus=9 WHERE uid=@item","item",(long)imageWeapon.UID);
        Query("ALTER TABLE items ALTER COLUMN plus SET NOT NULL");
        Query("INSERT INTO revora_guild_item_binding(item_uid,incarnation,owner_uid,item_token) VALUES(3999999999,@life,@owner,@token)",
            "life",imageLife,"owner",(long)imageFounder.Entity.UID,"token",Guid.NewGuid());
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-363 rejects an orphan direct binding");
        Query("DELETE FROM revora_guild_item_binding WHERE item_uid=3999999999");
        int projectedRegistries=Kernel.Guilds.Count;int projectedAds=Kernel.Guilds_adv.Count;int projectedSessions=Kernel.GamePool.Count;
        object projected=Call(GuildIdentityType,null,"Project",image);
        object projectedItem=IdentityField(((Array)IdentityField(projected,"Inscriptions")).GetValue(0),"Item");
        Check((bool)IdentityField(projected,"Ok") && (uint)IdentityField(projected,"GuildId")==4210
            && (string)IdentityField(projected,"Name")=="ImageGuild" && (ulong)IdentityField(projected,"SilverFund")==888888
            && (uint)IdentityField(projected,"LeaderUid")==imageFounder.Entity.UID
            && IdentityRank(projected,imageMember.Entity.UID)==(int)Enums.GuildMemberRank.Member
            && IdentityRank(projected,imageFounder.Entity.UID)==(int)Enums.GuildMemberRank.GuildLeader
            && (uint)IdentityField(((Array)IdentityField(projected,"Arsenal")).GetValue(0),"Enhancement")==2
            && !ReferenceEquals(projectedItem,savedItem) && (int)IdentityField(projectedItem,"Plus")==9
            && (bool)IdentityField(projectedItem,"Bound") && (int)IdentityField(projectedItem,"SocketOne")==1
            && (bool)IdentityField(projectedItem,"Inscribed") && (uint)IdentityField(projectedItem,"Id")==410009
            && (int)IdentityField(projectedItem,"Durability")==3200 && (Guid)IdentityField(projectedItem,"Token")==weaponToken
            && (uint)IdentityField(((Array)IdentityField(projected,"Inscriptions")).GetValue(0),"Donation")==456
            && Kernel.Guilds.Count==projectedRegistries && Kernel.Guilds_adv.Count==projectedAds && Kernel.GamePool.Count==projectedSessions
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null,
            "the pure projection copies the saved item and ranks without publishing a guild");
        foreach(var field in savedItem.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
            Check(object.Equals(field.GetValue(savedItem),field.GetValue(projectedItem)),"REVIEW-363 projected item field: "+field.Name);
        Check(!ReferenceEquals(IdentityField(image,"Members"),IdentityField(projected,"Members"))
            && !ReferenceEquals(((Array)IdentityField(image,"Members")).GetValue(0),((Array)IdentityField(projected,"Members")).GetValue(0))
            && !ReferenceEquals(IdentityField(image,"Arsenal"),IdentityField(projected,"Arsenal"))
            && !ReferenceEquals(((Array)IdentityField(image,"Arsenal")).GetValue(0),((Array)IdentityField(projected,"Arsenal")).GetValue(0)),
            "REVIEW-363 roster and arsenal projection do not alias the snapshot");
        Query("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@owner,@name,@item,2,4211,1)",
            "owner",(long)imageFounder.Entity.UID,"name",imageFounder.Entity.Name,"item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-363 rejects a bound item inscribed in another guild");
        Query("DELETE FROM pt_arsenal_inscribed WHERE syn_id=4211 AND iten_id=@item","item",(long)imageWeapon.UID);
        GuildIdentityType.GetField("BeforeProject",Hidden).SetValue(null,(Action)delegate{throw new InvalidOperationException("projection fault");});
        object faulted;
        try{faulted=Call(GuildIdentityType,null,"Project",image);}
        finally{GuildIdentityType.GetField("BeforeProject",Hidden).SetValue(null,null);}
        Check(!(bool)IdentityField(faulted,"Ok") && ((string)IdentityField(faulted,"Detail")).IndexOf("InvalidOperationException")>=0
            && ((Array)IdentityField(faulted,"Inscriptions")).Length==0
            && Kernel.Guilds.Count==projectedRegistries && Kernel.Guilds_adv.Count==projectedAds && Kernel.GamePool.Count==projectedSessions
            && Kernel.Unions.Count==unionsBefore
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null && imageFounder.Guild==null,
            "a projection fault does not publish the guild or change the session");
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id) VALUES(@life,4299,'enemy',4298)","life",imageLife);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction", "REVIEW-358 rejects same-incarnation wrong-id association");
        Query("DELETE FROM revora_guild_association WHERE guild_id=4299 AND subject_id=4298");
        Query("UPDATE guilds SET incarnation=NULL WHERE id=4211");
        Query("UPDATE revora_guild_association SET peer_incarnation=NULL WHERE guild_id=4210 AND kind='ally'");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction", "REVIEW-358 rejects unbound legacy peer");
        Query("DELETE FROM guilds WHERE id=4211");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction", "REVIEW-358 rejects absent peer");
        Query("INSERT INTO guilds(id,name,leadername,bulletin,silverfund,enrollmentdate,bulletindate,incarnation) VALUES(4211,'AllyHarbor','AllyLeader','',1,20260924,20260924,@life)","life",Guid.Empty);
        Query("UPDATE revora_guild_association SET peer_incarnation=@life WHERE guild_id=4210 AND kind='ally'","life",Guid.Empty);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction", "REVIEW-358 rejects empty peer incarnation");
        Query("UPDATE guilds SET incarnation=@life WHERE id=4211","life",allyLife);
        Query("UPDATE revora_guild_association SET peer_incarnation=@life WHERE guild_id=4210 AND kind='ally'","life",allyLife);
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id) VALUES(@life,4299,'arsenal',2)","life",Guid.NewGuid());
        Check(IdentityKind(IdentityRead(4299,stray.Entity.UID))=="Contradiction", "REVIEW-358 rejects orphan association owner");
        Query("UPDATE revora_guild_association SET guild_id=4203 WHERE guild_id=4299");
        Check(IdentityKind(IdentityRead(4203,stray.Entity.UID))=="Contradiction", "REVIEW-358 rejects association on legacy owner");
        Query("DELETE FROM revora_guild_association WHERE guild_id=4203");
        Query("UPDATE items SET entityid=@uid WHERE uid=@item","uid",(long)stray.Entity.UID,"item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction", "REVIEW-358 rejects transferred inscribed item");
        Query("UPDATE items SET entityid=@uid WHERE uid=@item","uid",(long)imageFounder.Entity.UID,"item",(long)imageWeapon.UID);
        Query("UPDATE pt_arsenal_inscribed SET uid=@uid WHERE syn_id=4210","uid",(long)stray.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction", "REVIEW-358 inscription owner must belong to the exact saved roster");
        Query("UPDATE pt_arsenal_inscribed SET uid=@uid WHERE syn_id=4210","uid",(long)imageFounder.Entity.UID);
        Query("INSERT INTO guildenemy(guildid,enemyid) VALUES(4210,4211)");
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id,peer_incarnation) VALUES(@life,4210,'enemy',4211,@peer)","life",imageLife,"peer",allyLife);
        object withEnemy=IdentityRead(4210,imageMember.Entity.UID);
        Check(IdentityKind(withEnemy)=="Consistent" && ((Array)IdentityField(withEnemy,"Enemies")).Length==1,
            "REVIEW-358 saved enemy uses the same durable peer contract");
        Query("DELETE FROM guildenemy WHERE guildid=4210");
        Query("DELETE FROM revora_guild_association WHERE guild_id=4210 AND kind='enemy'");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE pt_arsenal SET arsenal_enhance=8 WHERE syn_id=4210 AND arsenal_id=2");
        });
        object arsenalRace;
        try{arsenalRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(arsenalRace)=="ReadFailure" && ((string)IdentityField(arsenalRace,"Detail")).IndexOf("40001")>=0,
            "REVIEW-358 committed arsenal change during read fails closed without a mixed image");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent",
            "REVIEW-358 fresh read accepts the committed arsenal change");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE items SET plus=3 WHERE uid=@uid","uid",(long)imageWeapon.UID);
        });
        object itemRace;
        try{itemRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(itemRace)=="ReadFailure" && ((string)IdentityField(itemRace,"Detail")).IndexOf("40001")>=0,
            "REVIEW-358 committed item change during read fails closed");
        Check(IdentityKind(IdentityRead(4203,429999))=="Contradiction" && IdentityKind(IdentityRead(4299,429999))=="Contradiction",
            "REVIEW-358 missing requesting entity cannot receive a legacy or absent answer");
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id) VALUES(@life,4210,'enemy',4298)","life",Guid.NewGuid());
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "REVIEW-358 rejects stale association from an old owner incarnation");
        Query("DELETE FROM revora_guild_association WHERE guild_id=4210 AND subject_id=4298");
        bool duplicateLife=false;
        try{Query("UPDATE guilds SET incarnation=@life WHERE id=4211","life",imageLife);}
        catch(Npgsql.PostgresException ex){duplicateLife=ex.SqlState=="23505";}
        Check(duplicateLife,"REVIEW-358 durable guild incarnation uniqueness rejects a second owner");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE revora_guild_association SET peer_incarnation=@life WHERE guild_id=4210 AND kind='ally'","life",Guid.NewGuid());
        });
        object edgeRace;
        try{edgeRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(edgeRace)=="ReadFailure" && ((string)IdentityField(edgeRace,"Detail")).IndexOf("40001")>=0,
            "REVIEW-358 committed association change during read fails closed");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "REVIEW-358 fresh read exposes the committed peer mismatch");
        Query("UPDATE revora_guild_association SET peer_incarnation=@life WHERE guild_id=4210 AND kind='ally'","life",allyLife);
        Query("UPDATE guilds SET incarnation=@life WHERE id=4211","life",replacedAlly);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "a related guild that changes incarnation does not stay attached through the old numeric id");
        Query("UPDATE revora_guild_association SET peer_incarnation=@life WHERE guild_id=4210 AND kind='ally'","life",replacedAlly);
        Query("UPDATE guilds SET silverfund=777777 WHERE id=4210");
        Query("UPDATE pt_arsenal SET arsenal_enhance=9 WHERE syn_id=4210 AND arsenal_id=2");
        object moved=IdentityRead(4210,imageMember.Entity.UID);
        object movedArsenal=((Array)IdentityField(moved,"Arsenal")).GetValue(0);
        Check(IdentityKind(moved)=="Consistent" && (ulong)IdentityField(moved,"SilverFund")==777777
            && (uint)movedArsenal.GetType().GetField("Enhancement").GetValue(movedArsenal)==9
            && (Guid)((Array)IdentityField(moved,"Allies")).GetValue(0).GetType().GetField("Incarnation").GetValue(((Array)IdentityField(moved,"Allies")).GetValue(0))==replacedAlly,
            "a later read sees the committed fund, arsenal, and ally identity together");
        Query("INSERT INTO pt_arsenal(syn_id,arsenal_id,arsenal_unlocked,arsenal_enhance,syn_id_bp) VALUES(4210,8,1,1,1)");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "an arsenal row without this incarnation is not absorbed by the numeric guild id");
        Query("DELETE FROM pt_arsenal WHERE syn_id=4210 AND arsenal_id=8");
        Query("UPDATE entities SET guildid=4210 WHERE uid=@uid","uid",(long)stray.Entity.UID);
        Check(IdentityKind(IdentityRead(4203,stray.Entity.UID))=="Contradiction",
            "a saved member of another managed guild cannot take a legacy answer for a stale guild id");
        Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)stray.Entity.UID);
        Query("ALTER TABLE pt_arsenal RENAME TO review357_unavailable_arsenal");
        object relatedFailure;
        try{relatedFailure=IdentityRead(4210,imageMember.Entity.UID);}
        finally{Query("ALTER TABLE review357_unavailable_arsenal RENAME TO pt_arsenal");}
        Check(IdentityKind(relatedFailure)=="ReadFailure" && ((string)IdentityField(relatedFailure,"Detail")).IndexOf("42P01")>=0
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null,
            "a related-table read failure stays fail-closed and does not publish the guild");
        object denied=Call(GuildIdentityType,null,"Project",deleted);
        Check(!(bool)IdentityField(denied,"Ok") && ((Array)IdentityField(denied,"Inscriptions")).Length==0
            && ((Array)IdentityField(denied,"Arsenal")).Length==0 && ReferenceEquals(Kernel.Guilds[4201],stale),
            "a contradictory snapshot does not project a usable guild");
        Query("UPDATE pt_arsenal_inscribed SET name='NotFounder' WHERE syn_id=4210 AND iten_id=@item","item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "inscription name must match the saved roster member");
        Query("UPDATE pt_arsenal_inscribed SET name=@name WHERE syn_id=4210 AND iten_id=@item","name",imageFounder.Entity.Name,"item",(long)imageWeapon.UID);
        Query("UPDATE items SET plus=13 WHERE uid=@item","item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "unsupported saved plus is not returned as gear");
        Query("UPDATE items SET plus=9 WHERE uid=@item","item",(long)imageWeapon.UID);
        Query("UPDATE items SET socketone=4 WHERE uid=@item","item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "an undefined saved socket is not returned as gear");
        Query("UPDATE items SET socketone=1 WHERE uid=@item","item",(long)imageWeapon.UID);
        Query("UPDATE items SET bound=2 WHERE uid=@item","item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "a saved bound value other than 0 or 1 is not returned as gear");
        Query("UPDATE items SET bound=1 WHERE uid=@item","item",(long)imageWeapon.UID);
        Query("UPDATE items SET inscribed=0 WHERE uid=@item","item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "an inscription row whose item is not marked inscribed is a contradiction");
        Query("UPDATE items SET inscribed=1 WHERE uid=@item","item",(long)imageWeapon.UID);
        Guid rotated=Guid.NewGuid();
        Query("UPDATE items SET revora_item_token=@token WHERE uid=@item","token",rotated,"item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "a replaced item token does not inherit the previous binding");
        Query("UPDATE items SET revora_item_token=@token WHERE uid=@item","token",weaponToken,"item",(long)imageWeapon.UID);
        Query("UPDATE revora_guild_item_binding SET owner_uid=@uid WHERE item_uid=@item","uid",(long)stray.Entity.UID,"item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "the item binding owner must be the saved inscription owner");
        Query("UPDATE revora_guild_item_binding SET owner_uid=@uid WHERE item_uid=@item","uid",(long)imageFounder.Entity.UID,"item",(long)imageWeapon.UID);
        Query("DELETE FROM revora_guild_item_binding WHERE item_uid=@item","item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "a missing direct item binding is not inferred from the item uid");
        Query("INSERT INTO revora_guild_item_binding(item_uid,incarnation,owner_uid,item_token) VALUES(@item,@life,@owner,@token)",
            "item",(long)imageWeapon.UID,"life",imageLife,"owner",(long)imageFounder.Entity.UID,"token",weaponToken);
        long parked=4000000001L;
        Query("UPDATE items SET uid=@park WHERE uid=@item","park",parked,"item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "a missing item row is not reconstructed from the inscription");
        Query("UPDATE items SET uid=@item WHERE uid=@park","item",(long)imageWeapon.UID,"park",parked);
        var secondWeapon=Pack(imageFounder,420009,1);Guid secondToken=Guid.NewGuid();
        // Pack supplies only uid/type/owner/stack; a managed image requires explicit saved fields.
        Query("UPDATE items SET (durability,maximdurability,position,agate,socketprogress,plusprogress,socketone,sockettwo,effect,plus,bless,bound,enchant,locked,unlockend,suspicious,suspiciousstart,color,warehouse,stacksize,refineryitem,refinerytime,inwardrobe,daystamp,perfectionlevel,perfectionowner,perfectionownerguid,perfectionprogress,signature,runesexp,relicattrib1,relicattrib2,relicattrib3,relicattrib4,relicattrib5,days,inscribed,nextsteedcolor)=(SELECT durability,maximdurability,position,agate,socketprogress,plusprogress,socketone,sockettwo,effect,plus,bless,bound,enchant,locked,unlockend,suspicious,suspiciousstart,color,warehouse,stacksize,refineryitem,refinerytime,inwardrobe,daystamp,perfectionlevel,perfectionowner,perfectionownerguid,perfectionprogress,signature,runesexp,relicattrib1,relicattrib2,relicattrib3,relicattrib4,relicattrib5,days,inscribed,nextsteedcolor FROM items WHERE uid=@source) WHERE uid=@item",
            "source",(long)imageWeapon.UID,"item",(long)secondWeapon.UID);
        Query("UPDATE items SET plus=8,socketone=11,sockettwo=1,bound=1,inscribed=1,durability=1800,maximdurability=2000,revora_item_token=@token WHERE uid=@item",
            "token",secondToken,"item",(long)secondWeapon.UID);
        Query("INSERT INTO revora_guild_item_binding(item_uid,incarnation,owner_uid,item_token) VALUES(@item,@life,@owner,@token)",
            "item",(long)secondWeapon.UID,"life",imageLife,"owner",(long)imageFounder.Entity.UID,"token",secondToken);
        Query("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,@name,@item,2,4210,789)",
            "uid",(long)imageFounder.Entity.UID,"name",imageFounder.Entity.Name,"item",(long)secondWeapon.UID);
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id) VALUES(@life,4210,'inscribed',@item)",
            "life",imageLife,"item",(long)secondWeapon.UID);
        object pair=IdentityRead(4210,imageMember.Entity.UID);
        Check(IdentityKind(pair)=="Consistent","REVIEW-363 second complete saved item: "+IdentityKind(pair)+" "+IdentityField(pair,"Detail"));
        Array pairRows=(Array)IdentityField(pair,"Inscriptions");
        object firstSaved=IdentityField(pairRows.GetValue(0),"Item");
        object secondSaved=IdentityField(pairRows.GetValue(1),"Item");
        Check(IdentityKind(pair)=="Consistent" && pairRows.Length==2 && ((Array)IdentityField(pair,"Arsenal")).Length==1
            && (uint)IdentityField(firstSaved,"Id")==410009 && (int)IdentityField(firstSaved,"Plus")==9
            && (uint)IdentityField(secondSaved,"Id")==420009 && (int)IdentityField(secondSaved,"Plus")==8
            && (int)IdentityField(secondSaved,"SocketOne")==11 && (bool)IdentityField(secondSaved,"Bound")
            && (bool)IdentityField(secondSaved,"Inscribed") && IdentityRank(pair,imageMember.Entity.UID)==(int)Enums.GuildMemberRank.Member
            && imageMember.Guild==null,
            "two supplied weapons on one arsenal keep their own saved fields");
        object pairProjected=Call(GuildIdentityType,null,"Project",pair);
        object pairProjectedItem=IdentityField(((Array)IdentityField(pairProjected,"Inscriptions")).GetValue(1),"Item");
        Check((bool)IdentityField(pairProjected,"Ok") && (int)IdentityField(pairProjectedItem,"Plus")==8
            && (uint)IdentityField(pairProjectedItem,"Id")==420009 && (ulong)IdentityField(pairProjected,"SilverFund")==777777
            && ReferenceEquals(Kernel.Guilds[4201],stale),
            "a fresh snapshot projects the committed second weapon without publishing the guild");
        var wrongCategory=Pack(imageFounder,111009,1);Guid headToken=Guid.NewGuid();
        // Pack supplies only uid/type/owner/stack; a managed image requires explicit saved fields.
        Query("UPDATE items SET (durability,maximdurability,position,agate,socketprogress,plusprogress,socketone,sockettwo,effect,plus,bless,bound,enchant,locked,unlockend,suspicious,suspiciousstart,color,warehouse,stacksize,refineryitem,refinerytime,inwardrobe,daystamp,perfectionlevel,perfectionowner,perfectionownerguid,perfectionprogress,signature,runesexp,relicattrib1,relicattrib2,relicattrib3,relicattrib4,relicattrib5,days,inscribed,nextsteedcolor)=(SELECT durability,maximdurability,position,agate,socketprogress,plusprogress,socketone,sockettwo,effect,plus,bless,bound,enchant,locked,unlockend,suspicious,suspiciousstart,color,warehouse,stacksize,refineryitem,refinerytime,inwardrobe,daystamp,perfectionlevel,perfectionowner,perfectionownerguid,perfectionprogress,signature,runesexp,relicattrib1,relicattrib2,relicattrib3,relicattrib4,relicattrib5,days,inscribed,nextsteedcolor FROM items WHERE uid=@source) WHERE uid=@item",
            "source",(long)imageWeapon.UID,"item",(long)wrongCategory.UID);
        Query("UPDATE items SET plus=1,socketone=0,sockettwo=0,bound=0,inscribed=1,revora_item_token=@token WHERE uid=@item",
            "token",headToken,"item",(long)wrongCategory.UID);
        Query("INSERT INTO revora_guild_item_binding(item_uid,incarnation,owner_uid,item_token) VALUES(@item,@life,@owner,@token)",
            "item",(long)wrongCategory.UID,"life",imageLife,"owner",(long)imageFounder.Entity.UID,"token",headToken);
        Query("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,@name,@item,2,4210,1)",
            "uid",(long)imageFounder.Entity.UID,"name",imageFounder.Entity.Name,"item",(long)wrongCategory.UID);
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id) VALUES(@life,4210,'inscribed',@item)",
            "life",imageLife,"item",(long)wrongCategory.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "an item whose category does not match the inscription is not absorbed");
        Query("DELETE FROM revora_guild_association WHERE guild_id=4210 AND kind='inscribed' AND subject_id=@item","item",(long)wrongCategory.UID);
        Query("DELETE FROM pt_arsenal_inscribed WHERE syn_id=4210 AND iten_id=@item","item",(long)wrongCategory.UID);
        Query("DELETE FROM revora_guild_item_binding WHERE item_uid=@item","item",(long)wrongCategory.UID);
        bool duplicateMark=false;
        try{Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id) VALUES(@life,4210,'inscribed',@item)","life",imageLife,"item",(long)imageWeapon.UID);}
        catch(Npgsql.PostgresException ex){duplicateMark=ex.SqlState=="23505";}
        Check(duplicateMark,"a duplicate inscription association is refused by the binding key");
        bool duplicateRow=false;
        try{Query("INSERT INTO pt_arsenal_inscribed(uid,name,iten_id,iten_atype,syn_id,arsenal_donation) VALUES(@uid,@name,@item,2,4210,2)","uid",(long)imageFounder.Entity.UID,"name",imageFounder.Entity.Name,"item",(long)imageWeapon.UID);}
        catch(Npgsql.PostgresException ex){duplicateRow=ex.SqlState=="23505";}
        if(!duplicateRow){
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a duplicate inscribed item row is refused");
            Query("DELETE FROM pt_arsenal_inscribed WHERE syn_id::text='4210' AND arsenal_donation::text='2'");
        }else Check(duplicateRow,"a duplicate inscribed item row is refused by the table");
        Guid racedToken=Guid.NewGuid();
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE revora_guild_item_binding SET item_token=@token WHERE item_uid=@item","token",racedToken,"item",(long)imageWeapon.UID);
        });
        object bindingRace;
        try{bindingRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(bindingRace)=="ReadFailure" && ((string)IdentityField(bindingRace,"Detail")).IndexOf("40001")>=0,
            "a committed item-binding change during the read fails closed");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "the fresh read exposes the committed binding mismatch");
        Query("UPDATE revora_guild_item_binding SET item_token=@token WHERE item_uid=@item","token",weaponToken,"item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent" && imageMember.Guild==null,
            "restoring the supplied binding makes the saved image readable again");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE pt_arsenal_inscribed SET arsenal_donation=457 WHERE syn_id=4210 AND iten_id=@item","item",(long)imageWeapon.UID);
        });
        object inscriptionRace;
        try{inscriptionRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(inscriptionRace)=="ReadFailure" && ((string)IdentityField(inscriptionRace,"Detail")).IndexOf("40001")>=0,
            "REVIEW-363 rejects an inscription committed after the snapshot");
        object freshInscription=IdentityRead(4210,imageMember.Entity.UID);
        Check(IdentityKind(freshInscription)=="Consistent"
            && (uint)IdentityField(((Array)IdentityField(freshInscription,"Inscriptions")).GetValue(0),"Donation")==457,
            "REVIEW-363 fresh snapshot observes the committed inscription donation");
        Query("UPDATE pt_arsenal_inscribed SET arsenal_donation=456 WHERE syn_id=4210 AND iten_id=@item","item",(long)imageWeapon.UID);
        int failureRegistries=Kernel.Guilds.Count;int failureAds=Kernel.Guilds_adv.Count;int failureSessions=Kernel.GamePool.Count;
        Query("ALTER TABLE revora_guild_item_binding RENAME TO review362_unavailable_binding");
        object bindingFailure;
        try{bindingFailure=IdentityRead(4210,imageMember.Entity.UID);}
        finally{Query("ALTER TABLE review362_unavailable_binding RENAME TO revora_guild_item_binding");}
        Check(IdentityKind(bindingFailure)=="ReadFailure" && ((string)IdentityField(bindingFailure,"Detail")).IndexOf("42P01")>=0
            && Kernel.Guilds.Count==failureRegistries && Kernel.Guilds_adv.Count==failureAds && Kernel.GamePool.Count==failureSessions
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null,
            "an item-binding read failure stays fail-closed and does not publish the guild");
        Check(IdentityField(savedItem,"Purification")==null && IdentityField(savedItem,"PermanentRefinery")==null
            && IdentityField(savedItem,"TemporaryRefinery")==null && IdentityField(savedItem,"StabilizationCredit")==null,
            "a managed item with no addition rows and no addition marks has no saved effect");
        long expiredOn=new DateTime(2000,1,1).Ticks;long permanentOn=new DateTime(2020,5,2).Ticks;long temporaryOn=new DateTime(2024,3,4).Ticks;
        long purifyId=910001;long permanentId=910002;long temporaryId=910003;
        Query("INSERT INTO itemadding(iditemadding,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(910001,@uid,0,800721,7,0,60,@expired,NULL)","uid",(long)imageWeapon.UID,"expired",expiredOn);
        Query("INSERT INTO itemadding(iditemadding,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(910002,@uid,1,990001,9,44,0,@permanent,270)","uid",(long)imageWeapon.UID,"permanent",permanentOn);
        Query("INSERT INTO itemadding(iditemadding,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(910003,@uid,1,990002,4,15,604800,@temporary,150)","uid",(long)imageWeapon.UID,"temporary",temporaryOn);
        Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(@id,@item,@token,@life)",
            "id",purifyId,"item",(long)imageWeapon.UID,"token",weaponToken,"life",imageLife);
        Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(@id,@item,@token,@life)",
            "id",permanentId,"item",(long)imageWeapon.UID,"token",weaponToken,"life",imageLife);
        Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(@id,@item,@token,@life)",
            "id",temporaryId,"item",(long)imageWeapon.UID,"token",weaponToken,"life",imageLife);
        Guid purificationToken=BindAdditionFixture(purifyId);
        Guid originalPurificationToken=purificationToken;
        Guid permanentToken=BindAdditionFixture(permanentId);
        Guid temporaryToken=BindAdditionFixture(temporaryId);
        int additionRegistries=Kernel.Guilds.Count;int additionAds=Kernel.Guilds_adv.Count;int additionSessions=Kernel.GamePool.Count;
        object withAdditions=IdentityRead(4210,imageMember.Entity.UID);
        object additionItem=IdentityField(((Array)IdentityField(withAdditions,"Inscriptions")).GetValue(0),"Item");
        Check(IdentityKind(withAdditions)=="Consistent" && IdentityRank(withAdditions,imageMember.Entity.UID)==(int)Enums.GuildMemberRank.Member
            && IdentityRank(withAdditions,imageFounder.Entity.UID)==(int)Enums.GuildMemberRank.GuildLeader
            && (uint)IdentityField(additionItem,"RefineItem")==724440U && (long)IdentityField(additionItem,"RefineryTimeRaw")==86420L
            && (int)IdentityField(additionItem,"StabilizationCredit")==270
            && Scalar("SELECT count(*) FROM itemadding WHERE iditemadding=910001 AND uid=@uid",imageWeapon.UID)==1
            && Kernel.Guilds.Count==additionRegistries && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null,
            "stored additions stay raw and an expired purification row is not deleted");
        CheckAddition(IdentityField(additionItem,"Purification"),purifyId,0,800721U,7U,0U,60U,expiredOn,false,0,"saved purification");
        CheckAddition(IdentityField(additionItem,"PermanentRefinery"),permanentId,1,990001U,9U,44U,0U,permanentOn,true,270,"saved permanent refinery");
        CheckAddition(IdentityField(additionItem,"TemporaryRefinery"),temporaryId,1,990002U,4U,15U,604800U,temporaryOn,true,150,"saved temporary refinery");
        object additionProjected=Call(GuildIdentityType,null,"Project",withAdditions);
        object projectedAddition=IdentityField(((Array)IdentityField(additionProjected,"Inscriptions")).GetValue(0),"Item");
        Check((bool)IdentityField(additionProjected,"Ok") && (int)IdentityField(projectedAddition,"StabilizationCredit")==270
            && !ReferenceEquals(IdentityField(additionItem,"Purification"),IdentityField(projectedAddition,"Purification"))
            && !ReferenceEquals(IdentityField(additionItem,"PermanentRefinery"),IdentityField(projectedAddition,"PermanentRefinery"))
            && !ReferenceEquals(IdentityField(additionItem,"TemporaryRefinery"),IdentityField(projectedAddition,"TemporaryRefinery"))
            && Kernel.Guilds.Count==additionRegistries && imageMember.Guild==null,
            "the partial projection copies each addition without publishing the guild");
        foreach(string additionName in new[]{"Purification","PermanentRefinery","TemporaryRefinery"}){
            object savedAddition=IdentityField(additionItem,additionName);
            object copiedAddition=IdentityField(projectedAddition,additionName);
            foreach(var field in savedAddition.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                Check(object.Equals(field.GetValue(savedAddition),field.GetValue(copiedAddition)),"projected addition field: "+additionName+"."+field.Name);
        }
        Query("INSERT INTO itemadding(uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon) VALUES(@uid,0,800723,1,0,1,@expired)","uid",(long)imageWeapon.UID,"expired",expiredOn);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a null addition row id is not a lifetime");
        Query("DELETE FROM itemadding WHERE uid=@uid AND iditemadding IS NULL","uid",(long)imageWeapon.UID);
        long extraPurify=910004;
        Query("INSERT INTO itemadding(iditemadding,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(910004,@uid,0,800722,1,0,0,@expired,NULL)","uid",(long)imageWeapon.UID,"expired",expiredOn);
        Query("UPDATE itemadding SET revora_addition_token=@token WHERE iditemadding=@id","token",Guid.NewGuid(),"id",extraPurify);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","an addition row without a lifetime binding is refused");
        Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(@id,@item,@token,@life)",
            "id",extraPurify,"item",(long)imageWeapon.UID,"token",weaponToken,"life",imageLife);
        BindAdditionFixture(extraPurify);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a duplicate purification is refused");
        Query("DELETE FROM revora_item_addition_binding WHERE addition_id=@id","id",extraPurify);
        Query("DELETE FROM itemadding WHERE iditemadding=@id","id",extraPurify);
        long badType=910005;
        Query("INSERT INTO itemadding(iditemadding,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(910005,@uid,2,990009,1,1,1,@expired,1)","uid",(long)imageWeapon.UID,"expired",expiredOn);
        Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(@id,@item,@token,@life)",
            "id",badType,"item",(long)imageWeapon.UID,"token",weaponToken,"life",imageLife);
        BindAdditionFixture(badType);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","an unsupported addition type is refused");
        Query("DELETE FROM revora_item_addition_binding WHERE addition_id=@id","id",badType);
        Query("DELETE FROM itemadding WHERE iditemadding=@id","id",badType);
        // REVIEW-367: explicit identity reuse must not inherit an old addition mark.
        Query("DELETE FROM itemadding WHERE iditemadding=@id","id",purifyId);
        Query("INSERT INTO itemadding(iditemadding,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) VALUES(@id,@uid,0,800799,7,0,60,@time,NULL)",
            "id",purifyId,"uid",(long)imageWeapon.UID,"time",expiredOn);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-367 replacement addition cannot inherit stale binding");
        Guid replacementToken=Guid.NewGuid();
        Query("UPDATE itemadding SET revora_addition_token=@token WHERE iditemadding=@id","token",replacementToken,"id",purifyId);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","replacement addition with fresh token cannot inherit old mark");
        Query("UPDATE revora_item_addition_binding SET addition_token=@token WHERE addition_id=@id","token",replacementToken,"id",purifyId);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","explicitly supplied replacement and matching mark form a new saved image");
        purificationToken=replacementToken;
        Query("UPDATE itemadding SET addingid=800721 WHERE iditemadding=@id","id",purifyId);
        Check((Guid)IdentityField(IdentityField(additionItem,"Purification"),"Token")==originalPurificationToken
            && (Guid)IdentityField(IdentityField(additionItem,"PermanentRefinery"),"Token")==permanentToken
            && (Guid)IdentityField(IdentityField(additionItem,"TemporaryRefinery"),"Token")==temporaryToken,
            "saved additions retain their independent row lifetime tokens");
        Query("UPDATE itemadding SET revora_addition_token=@token WHERE iditemadding=@id","token",Guid.Empty,"id",purifyId);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","empty addition row token refused");
        Query("UPDATE itemadding SET revora_addition_token=@token WHERE iditemadding=@id","token",purificationToken,"id",purifyId);
        foreach(object badToken in new object[]{DBNull.Value,Guid.Empty,Guid.NewGuid()}){
            if(badToken==DBNull.Value)Query("UPDATE revora_item_addition_binding SET addition_token=NULL WHERE addition_id=@id","id",purifyId);
            else Query("UPDATE revora_item_addition_binding SET addition_token=@token WHERE addition_id=@id","token",badToken,"id",purifyId);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","absent, empty or mismatched binding addition token refused: "+badToken.GetType().Name);
        }
        Query("UPDATE revora_item_addition_binding SET addition_token=@token WHERE addition_id=@id","token",purificationToken,"id",purifyId);
        bool duplicateAdditionToken=false;
        try{Query("UPDATE itemadding SET revora_addition_token=@token WHERE iditemadding=@id","token",purificationToken,"id",permanentId);}
        catch(Npgsql.PostgresException ex){duplicateAdditionToken=ex.SqlState=="23505";}
        Check(duplicateAdditionToken,"two addition rows cannot share one lifetime token");
        string[] badAdditionSql={
            "UPDATE itemadding SET addingid=0 WHERE iditemadding=@id",
            "UPDATE itemadding SET addinglevel=-1 WHERE iditemadding=@id",
            "UPDATE itemadding SET addingpercent=-1 WHERE iditemadding=@id",
            "UPDATE itemadding SET duration=-1 WHERE iditemadding=@id",
            "UPDATE itemadding SET addedon=NULL WHERE iditemadding=@id",
            "UPDATE itemadding SET stabilizationpoints=NULL WHERE iditemadding=@id",
            "UPDATE itemadding SET stabilizationpoints=-1 WHERE iditemadding=@id"};
        foreach(string sql in badAdditionSql){
            Query(sql,"id",permanentId);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","malformed stored addition refused: "+sql);
            Query("UPDATE itemadding SET addingid=990001,addinglevel=9,addingpercent=44,duration=0,addedon=@time,stabilizationpoints=270 WHERE iditemadding=@id","time",permanentOn,"id",permanentId);
        }
        Query("UPDATE itemadding SET addingpercent=1 WHERE iditemadding=@id","id",purifyId);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","purification with refinery percent refused");
        Query("UPDATE itemadding SET addingpercent=0 WHERE iditemadding=@id","id",purifyId);
        foreach(long existingAddition in new[]{permanentId,temporaryId}){
            Query("INSERT INTO itemadding(iditemadding,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints) SELECT 910006,uid,addingtype,addingid,addinglevel,addingpercent,duration,addedon,stabilizationpoints FROM itemadding WHERE iditemadding=@id","id",existingAddition);
            Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(910006,@item,@token,@life)",
                "item",(long)imageWeapon.UID,"token",weaponToken,"life",imageLife);
            BindAdditionFixture(910006);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","duplicate refinery slot refused: "+existingAddition);
            Query("DELETE FROM revora_item_addition_binding WHERE addition_id=910006");
            Query("DELETE FROM itemadding WHERE iditemadding=910006");
        }
        Query("UPDATE itemadding SET uid=3999999996 WHERE iditemadding=@id","id",purifyId);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","transferred addition leaves an orphan direct mark");
        Query("UPDATE itemadding SET uid=@uid WHERE iditemadding=@id","uid",(long)imageWeapon.UID,"id",purifyId);
        Guid reusedItemToken=Guid.NewGuid();
        Query("UPDATE items SET revora_item_token=@token WHERE uid=@item","token",reusedItemToken,"item",(long)imageWeapon.UID);
        Query("UPDATE revora_guild_item_binding SET item_token=@token WHERE item_uid=@item","token",reusedItemToken,"item",(long)imageWeapon.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","replacement item with matching item binding cannot inherit old addition marks");
        Query("UPDATE items SET revora_item_token=@token WHERE uid=@item","token",weaponToken,"item",(long)imageWeapon.UID);
        Query("UPDATE revora_guild_item_binding SET item_token=@token WHERE item_uid=@item","token",weaponToken,"item",(long)imageWeapon.UID);
        // Preserve the raw saved snapshot if projection faults with populated additions.
        GuildIdentityType.GetField("BeforeProject",Hidden).SetValue(null,(Action)delegate{throw new InvalidOperationException("review367");});
        object additionProjectionFault;
        try{additionProjectionFault=Call(GuildIdentityType,null,"Project",withAdditions);}
        finally{GuildIdentityType.GetField("BeforeProject",Hidden).SetValue(null,null);}
        Check(!(bool)IdentityField(additionProjectionFault,"Ok") && ((Array)IdentityField(additionProjectionFault,"Inscriptions")).Length==0
            && Kernel.Guilds.Count==additionRegistries && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null && imageFounder.Guild==null,
            "populated addition projection failure leaves every registry and both sessions unchanged");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE revora_item_addition_binding SET addition_token=@token WHERE addition_id=@id","token",Guid.NewGuid(),"id",purifyId);
        });
        object additionBindingRace;
        try{additionBindingRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(additionBindingRace)=="ReadFailure" && ((string)IdentityField(additionBindingRace,"Detail")).IndexOf("40001")>=0,
            "committed addition binding change after snapshot returns 40001");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","fresh read observes mismatched addition token");
        Query("UPDATE revora_item_addition_binding SET addition_token=@token WHERE addition_id=@id","token",purificationToken,"id",purifyId);
        Guid rotatedAddition=Guid.NewGuid();
        Query("UPDATE revora_item_addition_binding SET item_token=@token WHERE addition_id=@id","token",rotatedAddition,"id",purifyId);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","an addition binding does not follow a different item token");
        Query("UPDATE revora_item_addition_binding SET item_token=@token WHERE addition_id=@id","token",weaponToken,"id",purifyId);
        Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(3999999998,@item,@token,@life)",
            "item",(long)imageWeapon.UID,"token",weaponToken,"life",imageLife);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","an addition mark without its row is refused");
        Query("DELETE FROM revora_item_addition_binding WHERE addition_id=3999999998");
        Query("INSERT INTO revora_item_addition_binding(addition_id,item_uid,item_token,incarnation) VALUES(3999999997,3999999997,@token,@life)",
            "token",Guid.NewGuid(),"life",imageLife);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","an addition mark for another item number does not attach to this guild");
        Query("DELETE FROM revora_item_addition_binding WHERE addition_id=3999999997");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE itemadding SET stabilizationpoints=400 WHERE iditemadding=@id","id",permanentId);
        });
        object additionRace;
        try{additionRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(additionRace)=="ReadFailure" && ((string)IdentityField(additionRace,"Detail")).IndexOf("40001")>=0,
            "a committed addition change during the read fails closed");
        object freshAddition=IdentityRead(4210,imageMember.Entity.UID);
        Check(IdentityKind(freshAddition)=="Consistent"
            && (int)IdentityField(IdentityField(((Array)IdentityField(freshAddition,"Inscriptions")).GetValue(0),"Item"),"StabilizationCredit")==400,
            "the fresh read observes the committed stabilization credit");
        Query("UPDATE itemadding SET stabilizationpoints=270 WHERE iditemadding=@id","id",permanentId);
        Query("ALTER TABLE itemadding RENAME TO review366_unavailable_adding");
        object additionFailure;
        try{additionFailure=IdentityRead(4210,imageMember.Entity.UID);}
        finally{Query("ALTER TABLE review366_unavailable_adding RENAME TO itemadding");}
        Check(IdentityKind(additionFailure)=="ReadFailure" && ((string)IdentityField(additionFailure,"Detail")).IndexOf("42P01")>=0
            && Kernel.Guilds.Count==additionRegistries && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null,
            "an addition-table read failure stays fail-closed and does not publish the guild");
        Guid rotatedUnion=Guid.NewGuid();
        Query("UPDATE guilds SET unionid=77 WHERE id=4211");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "REVIEW-373 unmarked saved union guild is refused");
        Query("INSERT INTO revora_guild_union_binding(incarnation,guild_id,union_id,union_token) VALUES(@life,4211,77,@token)","life",replacedAlly,"token",unionToken);
        object twoGuilds=IdentityRead(4210,imageMember.Entity.UID);
        Check(IdentityKind(twoGuilds)=="Consistent" && ((Array)IdentityField(IdentityField(twoGuilds,"Union"),"Guilds")).Length==2,
            "REVIEW-373 each saved guild union reference has its own direct incarnation");
        Query("UPDATE guilds SET incarnation=@life WHERE id=4211","life",Guid.NewGuid());
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 reused guild id cannot inherit its union binding");
        Query("UPDATE guilds SET incarnation=@life WHERE id=4211","life",replacedAlly);
        Query("DELETE FROM revora_guild_union_binding WHERE guild_id=4211");
        Query("UPDATE guilds SET unionid=0 WHERE id=4211");
        Query("INSERT INTO revora_guild_union_binding(incarnation,guild_id,union_id,union_token) VALUES(@life,4210,78,@token)","life",Guid.NewGuid(),"token",Guid.NewGuid());
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "REVIEW-373 stale incarnation mark under another union is refused");
        Query("DELETE FROM revora_guild_union_binding WHERE guild_id=4210 AND union_id=78");
        Query("UPDATE unions SET revora_union_token=@token WHERE id=77","token",rotatedUnion);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a replacement union token does not inherit the old guild binding");
        Query("UPDATE unions SET revora_union_token=@token WHERE id=77","token",unionToken);
        Query("CREATE TABLE review373_union_copy AS SELECT * FROM unions WHERE id=77");
        Query("UPDATE review373_union_copy SET id=78");
        bool unionTokenUnique=false;
        try{Query("INSERT INTO unions SELECT * FROM review373_union_copy");}catch(Npgsql.PostgresException ex){unionTokenUnique=ex.SqlState=="23505";}
        Check(unionTokenUnique,"REVIEW-373 two union rows cannot share a non-null token");
        Query("UPDATE review373_union_copy SET id=77");
        Query("DELETE FROM unions WHERE id=77");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 missing union row with marks is refused");
        Query("INSERT INTO unions SELECT * FROM review373_union_copy");
        Query("DROP TABLE review373_union_copy");
        Query("UPDATE unions SET revora_union_token=NULL WHERE id=77");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 reused numeric union id without a token is refused");
        Query("UPDATE unions SET revora_union_token=@token WHERE id=77","token",Guid.Empty);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 empty union lifetime is refused");
        Query("UPDATE unions SET revora_union_token=@token WHERE id=77","token",rotatedUnion);
        Query("UPDATE revora_guild_union_binding SET union_token=@token WHERE union_id=77","token",rotatedUnion);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 replacement union with current guild mark still refuses old member marks");
        Query("UPDATE revora_union_member_binding SET union_token=@token WHERE union_id=77","token",rotatedUnion);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","REVIEW-373 explicit replacement union with all current marks is readable");
        Query("UPDATE unions SET revora_union_token=@token WHERE id=77","token",unionToken);
        Query("UPDATE revora_guild_union_binding SET union_token=@token WHERE union_id=77","token",unionToken);
        Query("UPDATE revora_union_member_binding SET union_token=@token WHERE union_id=77","token",unionToken);
        Query("DELETE FROM revora_union_member_binding WHERE entity_uid=@uid","uid",(long)outsider.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 independent member without a direct mark is refused");
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,77,@token,@entity)","uid",(long)outsider.Entity.UID,"token",unionToken,"entity",outsiderToken);
        Query("UPDATE entities SET unionid=78 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 transferred independent member leaves a contradictory mark");
        Query("UPDATE entities SET unionid=77 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        Query("UPDATE unions SET leadername='Wrong saved leader' WHERE id=77");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 union leader must match a saved member");
        Query("ALTER TABLE unions ALTER COLUMN title DROP NOT NULL, ALTER COLUMN bulletin DROP NOT NULL, ALTER COLUMN declaration DROP NOT NULL");
        Query("UPDATE unions SET leadername=@name,title=NULL,bulletin=NULL,declaration=NULL WHERE id=77","name",founder.Entity.Name);
        object nullUnion=IdentityField(IdentityRead(4210,imageMember.Entity.UID),"Union");
        Check((string)IdentityField(nullUnion,"Title")=="" && (string)IdentityField(nullUnion,"Bulletin")=="" && (string)IdentityField(nullUnion,"Declaration")=="",
            "REVIEW-373 nullable union text is normalized to empty");
        Query("UPDATE unions SET title='HarborTitle',bulletin='Union bulletin',declaration='Union declaration' WHERE id=77");
        Query("ALTER TABLE unions ALTER COLUMN title SET NOT NULL, ALTER COLUMN bulletin SET NOT NULL, ALTER COLUMN declaration SET NOT NULL");
        Query("INSERT INTO revora_guild_union_binding(incarnation,guild_id,union_id,union_token) VALUES(@life,4298,77,@token)","life",Guid.NewGuid(),"token",unionToken);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a union binding for a missing guild is refused");
        Check(IdentityKind(Call(GuildIdentityType,null,"ReadOwner",(uint)4298))=="Contradiction","REVIEW-373 missing guild with only a union mark cannot return None");
        Query("INSERT INTO guilds(id,name,leadername,enrollmentdate,bulletindate,bulletin,silverfund) VALUES(4298,'LegacyUnionGuild','LegacyOwner',20260924,20260924,'',0)");
        Check(IdentityKind(Call(GuildIdentityType,null,"ReadOwner",(uint)4298))=="Contradiction","REVIEW-373 legacy guild with a union mark cannot return None");
        Query("DELETE FROM guilds WHERE id=4298");
        Query("DELETE FROM revora_guild_union_binding WHERE guild_id=4298");
        Query("UPDATE unions SET coreofficals='1#0#0#0#0#0#0' WHERE id=77");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a short official list is refused");
        Query("UPDATE unions SET coreofficals=@core WHERE id=77","core",founder.Entity.UID.ToString()+"#"+founder.Entity.UID.ToString()+"#0#0#0#0#0#0");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a duplicate official assignment is refused");
        Query("UPDATE unions SET coreofficals=@core WHERE id=77","core",officials);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token) VALUES(3999999996,77,@token)","token",unionToken);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a union member mark without a saved member is refused");
        Query("DELETE FROM revora_union_member_binding WHERE entity_uid=3999999996");
        Query("UPDATE unions SET harem=@roles WHERE id=77","roles",outsider.Entity.UID.ToString()+"#"+outsider.Entity.UID.ToString()+"#0#0#0#0#0#0#0#0");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 duplicate harem assignments are refused");
        Query("UPDATE unions SET harem=@roles,guards=@guards WHERE id=77","roles",haremRoles,"guards","x#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 nonnumeric guard assignment is refused");
        Query("UPDATE unions SET guards=@roles WHERE id=77","roles","3999999996#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0#0");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 role without saved union membership is refused");
        Query("UPDATE unions SET guards=@roles WHERE id=77","roles",guardRoles);
        Query("UPDATE entities SET offical=-1 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 malformed independent member value is refused");
        Query("UPDATE entities SET offical=0 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        Query("UPDATE revora_union_member_binding SET union_id=-1 WHERE entity_uid=@uid","uid",(long)outsider.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 malformed binding is a contradiction, not a conversion failure");
        Query("UPDATE revora_union_member_binding SET union_id=77 WHERE entity_uid=@uid","uid",(long)outsider.Entity.UID);
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE entities SET unionexploits=29 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        });
        object memberRace;
        try{memberRace=IdentityRead(4210,imageMember.Entity.UID);}finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(memberRace)=="ReadFailure" && ((string)IdentityField(memberRace,"Detail")).IndexOf("40001")>=0,
            "REVIEW-373 committed independent-member change after snapshot returns 40001");
        object freshMember=IdentityRead(4210,imageMember.Entity.UID);bool changedExploits=false;
        foreach(object member in (Array)IdentityField(IdentityField(freshMember,"Union"),"Members"))
            if((uint)IdentityField(member,"Uid")==outsider.Entity.UID)changedExploits=(ulong)IdentityField(member,"Exploits")==29UL;
        Check(IdentityKind(freshMember)=="Consistent" && changedExploits,"REVIEW-373 fresh read sees member change");
        Query("UPDATE entities SET unionexploits=15 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE revora_union_member_binding SET union_token=@token WHERE entity_uid=@uid","token",Guid.NewGuid(),"uid",(long)outsider.Entity.UID);
        });
        object memberBindingRace;
        try{memberBindingRace=IdentityRead(4210,imageMember.Entity.UID);}finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(memberBindingRace)=="ReadFailure" && ((string)IdentityField(memberBindingRace,"Detail")).IndexOf("40001")>=0
            && IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 member binding race returns 40001 and fresh read refuses stale token");
        Query("UPDATE revora_union_member_binding SET union_token=@token WHERE entity_uid=@uid","token",unionToken,"uid",(long)outsider.Entity.UID);
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE revora_guild_union_binding SET union_token=@token WHERE guild_id=4210","token",Guid.NewGuid());
        });
        object guildBindingRace;
        try{guildBindingRace=IdentityRead(4210,imageMember.Entity.UID);}finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(guildBindingRace)=="ReadFailure" && ((string)IdentityField(guildBindingRace,"Detail")).IndexOf("40001")>=0
            && IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-373 guild binding race returns 40001 and fresh read refuses stale token");
        Query("UPDATE revora_guild_union_binding SET union_token=@token WHERE guild_id=4210","token",unionToken);
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE unions SET unionfund=1 WHERE id=77");
        });
        object unionRace;
        try{unionRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(unionRace)=="ReadFailure" && ((string)IdentityField(unionRace,"Detail")).IndexOf("40001")>=0,
            "a committed union change during the read fails closed");
        object freshUnion=IdentityRead(4210,imageMember.Entity.UID);
        Check(IdentityKind(freshUnion)=="Consistent" && (ulong)IdentityField(IdentityField(freshUnion,"Union"),"Fund")==1UL,
            "the fresh read observes the committed union fund");
        Query("UPDATE unions SET unionfund=880000 WHERE id=77");
        object unionProjected=Call(GuildIdentityType,null,"Project",freshUnion);
        object projectedUnion=IdentityField(unionProjected,"Union");
        object savedUnionNow=IdentityField(freshUnion,"Union");
        Check((bool)IdentityField(unionProjected,"Ok") && !ReferenceEquals(savedUnionNow,projectedUnion)
            && !ReferenceEquals(((Array)IdentityField(savedUnionNow,"Members")).GetValue(0),((Array)IdentityField(projectedUnion,"Members")).GetValue(0))
            && (ulong)IdentityField(projectedUnion,"Fund")==1UL && (Guid)IdentityField(projectedUnion,"Token")==unionToken
            && Kernel.Unions.Count==unionsBefore && Kernel.Guilds.Count==additionRegistries,
            "the partial projection copies the union without publishing it");
        IdentityIndependentCopy(savedUnionNow,projectedUnion,"REVIEW-373 union copy");
        IdentityIndependentCopy(IdentityField(freshUnion,"Members"),IdentityField(unionProjected,"Members"),"REVIEW-373 guild member copy");
        Array corruptMembers=(Array)IdentityField(savedUnionNow,"Members");
        object heldUnionMember=corruptMembers.GetValue(0);corruptMembers.SetValue(null,0);
        object unionCopyFailure;
        try{unionCopyFailure=Call(GuildIdentityType,null,"Project",freshUnion);}finally{corruptMembers.SetValue(heldUnionMember,0);}
        Check(!(bool)IdentityField(unionCopyFailure,"Ok") && IdentityField(unionCopyFailure,"Union")==null
            && ((Array)IdentityField(unionCopyFailure,"Members")).Length==0
            && Kernel.Unions.Count==unionsBefore && Kernel.Guilds.Count==additionRegistries
            && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && ReferenceEquals(Kernel.Guilds[4201],stale) && imageFounder.Guild==null && imageMember.Guild==null,
            "REVIEW-373 populated union-copy fault discards all partial data without changing registries or sessions");
        Query("UPDATE unions SET unionfund=880000 WHERE id=77");
        Guid noneLife=Guid.NewGuid();
        Query("INSERT INTO guilds(id,name,leadername,enrollmentdate,bulletindate,bulletin,silverfund,unionid,incarnation) VALUES(4212,'NoUnion',@leader,20260924,20260924,'',500000,0,@life)","leader",stray.Entity.Name,"life",noneLife);
        Query("INSERT INTO revora_guild_member_identity(entity_uid,guild_id,incarnation,entity_token) VALUES(@uid,4212,@life,@token)","uid",(long)stray.Entity.UID,"life",noneLife,"token",strayToken);
        Query("UPDATE entities SET guildid=4212,guildrank=@rank WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)stray.Entity.UID);
        object noUnion=IdentityRead(4212,stray.Entity.UID);
        Check(IdentityKind(noUnion)=="Consistent" && IdentityField(noUnion,"Union")==null && (uint)IdentityField(noUnion,"UnionId")==0 && Kernel.Unions.Count==unionsBefore,
            "a guild with no stored union id has no union image");
        Query("INSERT INTO revora_guild_union_binding(incarnation,guild_id,union_id,union_token) VALUES(@life,4212,77,@token)","life",noneLife,"token",unionToken);
        Check(IdentityKind(IdentityRead(4212,stray.Entity.UID))=="Contradiction","a union binding on a guild with no union id is refused");
        Query("DELETE FROM revora_guild_union_binding WHERE guild_id=4212");
        Query("INSERT INTO revora_guild_union_binding(incarnation,guild_id,union_id,union_token) VALUES(@life,4212,78,@token)","life",Guid.NewGuid(),"token",Guid.NewGuid());
        Check(IdentityKind(IdentityRead(4212,stray.Entity.UID))=="Contradiction","REVIEW-373 no-union guild refuses an old-incarnation mark");
        Query("DELETE FROM revora_guild_union_binding WHERE guild_id=4212");
        Query("UPDATE entities SET guildid=0 WHERE uid=@uid","uid",(long)stray.Entity.UID);
        Query("ALTER TABLE unions RENAME TO review371_unavailable_union");
        object unionFailure;
        try{unionFailure=IdentityRead(4210,imageMember.Entity.UID);}
        finally{Query("ALTER TABLE review371_unavailable_union RENAME TO unions");}
        Check(IdentityKind(unionFailure)=="ReadFailure" && ((string)IdentityField(unionFailure,"Detail")).IndexOf("42P01")>=0
            && Kernel.Guilds.Count==additionRegistries && Kernel.Unions.Count==unionsBefore && imageMember.Guild==null,
            "a union-table read failure stays fail-closed and does not publish a guild or union");
        Guid replacedFounder=Guid.NewGuid();
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",replacedFounder,"uid",(long)founder.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a replaced character token does not inherit the old membership mark");
        Query("UPDATE revora_guild_member_identity SET entity_token=@token WHERE entity_uid=@uid","token",replacedFounder,"uid",(long)founder.Entity.UID);
        Query("UPDATE revora_union_member_binding SET entity_token=@token WHERE entity_uid=@uid","token",replacedFounder,"uid",(long)founder.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","an explicit character rebind matches the new row token");
        Query("UPDATE entities SET offical=4 WHERE uid=@uid","uid",(long)founder.Entity.UID);
        string cross=outsider.Entity.UID.ToString()+"#0#0#0#0#0#0#0";
        string crossHarem=outsider.Entity.UID.ToString()+"#0#0#0#0#0#0#0#0#0";
        Query("UPDATE unions SET coreofficals=@core,harem=@harem WHERE id=77","core",cross,"harem",crossHarem);
        object derived=IdentityRead(4210,imageMember.Entity.UID);
        object derivedUnion=IdentityField(derived,"Union");
        object derivedOutsider=null;object derivedLeader=null;
        foreach(object member in (Array)IdentityField(derivedUnion,"Members")){
            if((uint)IdentityField(member,"Uid")==outsider.Entity.UID) derivedOutsider=member;
            if((uint)IdentityField(member,"Uid")==founder.Entity.UID) derivedLeader=member;
        }
        Check(IdentityKind(derived)=="Consistent" && derivedOutsider!=null && derivedLeader!=null
            && (int)IdentityField(derivedLeader,"Official")==4 && (int)IdentityField(derivedLeader,"DerivedOfficial")==1
            && (int)IdentityField(derivedOutsider,"Official")==0 && (int)IdentityField(derivedOutsider,"DerivedOfficial")==2
            && (int)IdentityField(derivedOutsider,"DerivedHarem")==1024 && IdentityField(derivedOutsider,"DerivedGuards")==null,
            "leader precedence and cross-list roles stay derived from the arrays without repairing stored columns");
        Query("UPDATE entities SET offical=1 WHERE uid=@uid","uid",(long)founder.Entity.UID);
        Query("UPDATE unions SET coreofficals=@core,harem=@harem WHERE id=77","core",officials,"harem",haremRoles);
        Guid secondUnion=Guid.NewGuid();
        string deputyOfficials=deputy.Entity.UID.ToString()+"#0#0#0#0#0#0#0";
        Query("INSERT INTO unions(id,goldbricks,unionfund,name,title,leadername,leaderuid,bulletin,declaration,coreofficals,harem,guards,revora_union_token) VALUES(78,4,440000,'SecondUnion','SecondTitle',@leader,@uid,'Second bulletin','Second declaration',@core,@harem,@guards,@token)",
            "leader",deputy.Entity.Name,"uid",(long)deputy.Entity.UID,"core",deputyOfficials,"harem",haremRoles,"guards",guardRoles,"token",secondUnion);
        Query("UPDATE entities SET unionid=78,unionexploits=21,offical=1,harem=0,guards=0 WHERE uid=@uid","uid",(long)deputy.Entity.UID);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,78,@token,@entity)","uid",(long)deputy.Entity.UID,"token",secondUnion,"entity",deputyToken);
        Query("UPDATE entities SET unionid=78,unionexploits=9,offical=0,harem=0,guards=0 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,78,@token,@entity)","uid",(long)imageMember.Entity.UID,"token",secondUnion,"entity",imageMemberToken);
        object home=IdentityRead(4210,imageMember.Entity.UID);
        Array otherUnions=(Array)IdentityField(home,"OtherUnions");
        Check(IdentityKind(home)=="Consistent" && (uint)IdentityField(IdentityField(home,"Union"),"Id")==77 && otherUnions.Length==1
            && (uint)IdentityField(otherUnions.GetValue(0),"Id")==78 && (Guid)IdentityField(otherUnions.GetValue(0),"Token")==secondUnion
            && Kernel.Unions.Count==unionsBefore,
            "one guild roster can hold the guild union and a member's different union");
        Query("UPDATE entities SET unionid=0 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","a union mark remains stale after the member union id is cleared");
        Query("DELETE FROM revora_union_member_binding WHERE entity_uid=@uid","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","clearing the stale mark leaves the guild union readable");
        Query("UPDATE entities SET guildid=4212,guildrank=@rank,unionid=78,unionexploits=3,offical=1,harem=0,guards=0 WHERE uid=@uid","rank",(int)Enums.GuildMemberRank.GuildLeader,"uid",(long)stray.Entity.UID);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,78,@token,@entity)","uid",(long)stray.Entity.UID,"token",secondUnion,"entity",strayToken);
        Query("INSERT INTO guilds(id,name,leadername,enrollmentdate,bulletindate,bulletin,silverfund,unionid) VALUES(0,'UnrelatedZero','Unrelated',20260924,20260924,'',0,0)");
        Check(IdentityKind(IdentityRead(4212,stray.Entity.UID))=="Consistent","REVIEW-378 unrelated guild zero does not poison an independent union");
        Query("DELETE FROM guilds WHERE id=0");
        object strayUnion=IdentityRead(4212,stray.Entity.UID);
        Check(IdentityKind(strayUnion)=="Consistent" && IdentityField(strayUnion,"Union")==null
            && ((Array)IdentityField(strayUnion,"OtherUnions")).Length==1
            && (uint)IdentityField(((Array)IdentityField(strayUnion,"OtherUnions")).GetValue(0),"Id")==78,
            "a guild with no union can still record a member's separate union");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",Guid.NewGuid(),"uid",(long)imageMember.Entity.UID);
        });
        object characterRace;
        try{characterRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(characterRace)=="ReadFailure" && ((string)IdentityField(characterRace,"Detail")).IndexOf("40001")>=0,
            "a committed character-token change during the read fails closed");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","the fresh read sees the replaced character token");
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent" && Kernel.Unions.Count==unionsBefore && imageMember.Guild==null,
            "restoring the character token makes the saved image readable again");
        // REVIEW-378: exercise character replacement with actual deletion and reinsertion.
        Query("UPDATE entities SET unionid=78 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,78,@token,@entity)","uid",(long)imageMember.Entity.UID,"token",secondUnion,"entity",imageMemberToken);
        Query("CREATE TABLE review378_character_copy AS SELECT * FROM entities WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Query("DELETE FROM entities WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageFounder.Entity.UID))=="Contradiction","REVIEW-378 deleted character leaves orphan marks");
        Query("UPDATE review378_character_copy SET revora_entity_token=NULL");
        Query("INSERT INTO entities SELECT * FROM review378_character_copy");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 same UID without a new token cannot inherit membership");
        Guid newCharacterToken=Guid.NewGuid();
        foreach(Guid invalidEntityToken in new[]{Guid.Empty,newCharacterToken}){
            Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",invalidEntityToken,"uid",(long)imageMember.Entity.UID);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 empty or new character token refuses old marks");
        }
        Query("UPDATE revora_guild_member_identity SET entity_token=@token WHERE entity_uid=@uid","token",newCharacterToken,"uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 rebind of guild alone cannot inherit union mark");
        Query("UPDATE revora_union_member_binding SET entity_token=@token WHERE entity_uid=@uid","token",newCharacterToken,"uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","REVIEW-378 explicitly rebound replacement has a new complete image");
        imageMemberToken=newCharacterToken;
        Query("DROP TABLE review378_character_copy");
        foreach(string table in new[]{"revora_guild_member_identity","revora_union_member_binding"}){
            foreach(object token in new object[]{DBNull.Value,Guid.Empty,Guid.NewGuid()}){
                // Table names are fixed test constants, never player input.
                if(token==DBNull.Value)Query("UPDATE "+table+" SET entity_token=NULL WHERE entity_uid=@uid","uid",(long)imageMember.Entity.UID);
                else Query("UPDATE "+table+" SET entity_token=@token WHERE entity_uid=@uid","token",token,"uid",(long)imageMember.Entity.UID);
                Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 missing empty mismatched character mark: "+table);
            }
            Query("UPDATE "+table+" SET entity_token=@token WHERE entity_uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        }
        bool duplicateEntityToken=false;
        try{Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",imageMemberToken,"uid",(long)imageFounder.Entity.UID);}
        catch(Npgsql.PostgresException ex){duplicateEntityToken=ex.SqlState=="23505";}
        Check(duplicateEntityToken,"REVIEW-378 distinct character rows cannot share a lifetime");
        Query("UPDATE entities SET revora_entity_token=NULL WHERE uid=@uid","uid",(long)deputy.Entity.UID);
        Call(GuildIdentityType,null,"EnsureSchema");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 schema rerun does not enroll a legacy other-union member");
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",deputyToken,"uid",(long)deputy.Entity.UID);
        foreach(object absentUnion in new object[]{DBNull.Value,0}){
            if(absentUnion==DBNull.Value)Query("UPDATE entities SET unionid=NULL WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
            else Query("UPDATE entities SET unionid=0 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 null or zero union cannot keep a mark");
        }
        Query("UPDATE entities SET unionid=79 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 transfer to missing union cannot inherit a mark");
        Query("UPDATE entities SET unionid=78 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Query("CREATE TABLE review378_union_copy AS SELECT * FROM unions WHERE id=78");
        Query("DELETE FROM unions WHERE id=78");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 missing other union refused");
        Query("UPDATE review378_union_copy SET revora_union_token=NULL");
        Query("INSERT INTO unions SELECT * FROM review378_union_copy");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 legacy replacement other union refused");
        Guid newOtherUnion=Guid.NewGuid();
        Query("UPDATE unions SET revora_union_token=@token WHERE id=78","token",newOtherUnion);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-378 replaced other union cannot inherit marks");
        Query("UPDATE revora_union_member_binding SET union_token=@token WHERE union_id=78","token",newOtherUnion);
        secondUnion=newOtherUnion;
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","REVIEW-378 other union explicitly rebound");
        Query("DROP TABLE review378_union_copy");
        // Two other unions in the same roster, plus the guild's own independent union.
        Guid thirdUnion=Guid.NewGuid();
        Query("INSERT INTO unions(id,goldbricks,unionfund,name,title,leadername,leaderuid,bulletin,declaration,coreofficals,harem,guards,revora_union_token) VALUES(79,5,55,'ThirdUnion','ThirdTitle',@leader,@uid,'','',@core,@harem,@guards,@token)",
            "leader",imageFounder.Entity.Name,"uid",(long)imageFounder.Entity.UID,"core","0#0#0#0#0#0#0#0","harem",haremRoles,"guards",guardRoles,"token",thirdUnion);
        Query("UPDATE entities SET unionid=79,unionexploits=0,offical=0,harem=0,guards=0 WHERE uid=@uid","uid",(long)imageFounder.Entity.UID);
        Query("INSERT INTO revora_union_member_binding(entity_uid,union_id,union_token,entity_token) VALUES(@uid,79,@token,@entity)","uid",(long)imageFounder.Entity.UID,"token",thirdUnion,"entity",imageFounderToken);
        object multiple=IdentityRead(4210,imageMember.Entity.UID);
        Array multipleOthers=(Array)IdentityField(multiple,"OtherUnions");
        Check(IdentityKind(multiple)=="Consistent" && multipleOthers.Length==2
            && (uint)IdentityField(multipleOthers.GetValue(0),"Id")==78 && (uint)IdentityField(multipleOthers.GetValue(1),"Id")==79,
            "REVIEW-378 distinct other unions are deduplicated and ordered");
        object multipleCopy=Call(GuildIdentityType,null,"Project",multiple);
        Check((bool)IdentityField(multipleCopy,"Ok"),"REVIEW-378 multiple-union projection succeeds");
        IdentityIndependentCopy(IdentityField(multiple,"OtherUnions"),IdentityField(multipleCopy,"OtherUnions"),"REVIEW-378 other unions copy");
        IdentityIndependentCopy(IdentityField(multiple,"Members"),IdentityField(multipleCopy,"Members"),"REVIEW-378 character token copy");
        object savedOther=multipleOthers.GetValue(1);multipleOthers.SetValue(null,1);
        object otherCopyFault;
        try{otherCopyFault=Call(GuildIdentityType,null,"Project",multiple);}finally{multipleOthers.SetValue(savedOther,1);}
        Check(!(bool)IdentityField(otherCopyFault,"Ok") && IdentityField(otherCopyFault,"Union")==null
            && ((Array)IdentityField(otherCopyFault,"OtherUnions")).Length==0 && ((Array)IdentityField(otherCopyFault,"Members")).Length==0
            && Kernel.Guilds.Count==additionRegistries && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && Kernel.Unions.Count==unionsBefore && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null && imageFounder.Guild==null,
            "REVIEW-378 later other-union copy failure discards all partial data and preserves registries and sessions");
        // Every slot has an explicit fixture, including leader precedence over a nonzero official slot.
        for(int roleSlot=0;roleSlot<20;roleSlot++){
            string[] coreSlots=new string[8];string[] haremSlots=new string[10];string[] guardSlots=new string[20];
            for(int j=0;j<8;j++)coreSlots[j]=j==roleSlot%8?outsider.Entity.UID.ToString():"0";
            for(int j=0;j<10;j++)haremSlots[j]=j==roleSlot%10?outsider.Entity.UID.ToString():"0";
            for(int j=0;j<20;j++)guardSlots[j]=j==roleSlot?outsider.Entity.UID.ToString():"0";
            Query("UPDATE unions SET coreofficals=@core,harem=@harem,guards=@guards WHERE id=77","core",string.Join("#",coreSlots),"harem",string.Join("#",haremSlots),"guards",string.Join("#",guardSlots));
            object roleImage=IdentityRead(4210,imageMember.Entity.UID);object roleOutsider=null;
            foreach(object member in (Array)IdentityField(IdentityField(roleImage,"Union"),"Members"))
                if((uint)IdentityField(member,"Uid")==outsider.Entity.UID)roleOutsider=member;
            int haremSlot=roleSlot%10;
            Check(IdentityKind(roleImage)=="Consistent" && (int)IdentityField(roleOutsider,"DerivedOfficial")== (2 << (roleSlot%8))
                && (int)IdentityField(roleOutsider,"DerivedHarem")== (haremSlot==0?1024:haremSlot<3?2048:4096)
                && (int)IdentityField(roleOutsider,"DerivedGuards")==512 && (int)IdentityField(roleOutsider,"Official")==0
                && (int)IdentityField(roleOutsider,"Harem")==0 && (int)IdentityField(roleOutsider,"Guards")==0,
                "REVIEW-378 cross-list role derivation preserves saved columns, slot "+roleSlot);
        }
        Query("UPDATE unions SET coreofficals=@core,harem=@harem,guards=@guards WHERE id=77","core","0#0#0#0#0#0#0#"+founder.Entity.UID,"harem",haremRoles,"guards",guardRoles);
        object precedence=IdentityRead(4210,imageMember.Entity.UID);object precedenceLeader=null;
        foreach(object member in (Array)IdentityField(IdentityField(precedence,"Union"),"Members"))
            if((uint)IdentityField(member,"Uid")==founder.Entity.UID)precedenceLeader=member;
        Check((int)IdentityField(precedenceLeader,"DerivedOfficial")==1,"REVIEW-378 leader overrides official slot 7");
        Query("UPDATE unions SET coreofficals=@core WHERE id=77","core",officials);
        Action[] identityChanges={
            delegate{Query("UPDATE revora_guild_member_identity SET entity_token=@token WHERE entity_uid=@uid","token",Guid.NewGuid(),"uid",(long)imageMember.Entity.UID);},
            delegate{Query("UPDATE revora_union_member_binding SET entity_token=@token WHERE entity_uid=@uid","token",Guid.NewGuid(),"uid",(long)imageMember.Entity.UID);},
            delegate{Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",Guid.NewGuid(),"uid",(long)deputy.Entity.UID);},
            delegate{Query("UPDATE unions SET unionfund=1 WHERE id=78");}};
        for(int raceIndex=0;raceIndex<identityChanges.Length;raceIndex++){
            GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,identityChanges[raceIndex]);
            object identityRaced;
            try{identityRaced=IdentityRead(4210,imageMember.Entity.UID);}finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
            Check(IdentityKind(identityRaced)=="ReadFailure" && ((string)IdentityField(identityRaced,"Detail")).IndexOf("40001")>=0,"REVIEW-378 committed other-union/character binding race "+raceIndex);
            object afterRace=IdentityRead(4210,imageMember.Entity.UID);
            Check(IdentityKind(afterRace)==(raceIndex==3?"Consistent":"Contradiction"),"REVIEW-378 fresh snapshot distinguishes race "+raceIndex);
            if(raceIndex==3)Check((ulong)IdentityField(((Array)IdentityField(afterRace,"OtherUnions")).GetValue(0),"Fund")==1UL,"REVIEW-378 fresh other-union fund retained");
            Query("UPDATE revora_guild_member_identity SET entity_token=@token WHERE entity_uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
            Query("UPDATE revora_union_member_binding SET entity_token=@token WHERE entity_uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
            Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",deputyToken,"uid",(long)deputy.Entity.UID);
            Query("UPDATE unions SET unionfund=440000 WHERE id=78");
        }
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","REVIEW-378 final multiple-union image remains consistent");
        Query("UPDATE guilds SET malefemale=1 WHERE id=4210");
        Query("UPDATE entities SET face=37,body=1005 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Query("UPDATE entities SET face=201,body=2002 WHERE uid=@uid","uid",(long)outsider.Entity.UID);
        object absentNoble=IdentityRead(4210,imageMember.Entity.UID);
        object absentRoster=null;object absentFounder=null;
        foreach(object member in (Array)IdentityField(absentNoble,"Members")){
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) absentRoster=member;
            if((uint)IdentityField(member,"Uid")==imageFounder.Entity.UID) absentFounder=member;
        }
        Check(IdentityKind(absentNoble)=="Consistent" && IdentityField(absentNoble,"BlackList")==null
            && (string)IdentityField(absentNoble,"BlackListState")=="unsupported"
            && (uint)IdentityField(absentNoble,"MaleFemale")==1
            && absentRoster!=null && IdentityField(absentRoster,"Nobility")==null
            && (int)IdentityField(absentRoster,"Face")==37 && (int)IdentityField(absentRoster,"Body")==1005
            && (int)IdentityField(absentRoster,"BodyDigit")==5 && (int)IdentityField(absentRoster,"LoginFallbackGender")==0
            && (uint)IdentityField(absentRoster,"ConcatenatedMesh")==371005
            && absentFounder!=null && IdentityField(absentFounder,"Nobility")==null,
            "REVIEW-382 absent nobility stays absent and blacklist is unknown");
        Query("INSERT INTO nobility(entityname,entityuid,donation,gender,mesh,rey,duracionrey,revora_entity_token) VALUES('OldNoble',@uid,30000000,2,371005999,1,'2026-09-24',@token)",
            "uid",(long)imageMember.Entity.UID,"token",imageMemberToken);
        Query("INSERT INTO nobility(entityname,entityuid,donation,gender,mesh,rey,duracionrey,revora_entity_token) VALUES('Outside',@uid,0,4,42,0,'',@token)",
            "uid",(long)outsider.Entity.UID,"token",outsiderToken);
        object presentNoble=IdentityRead(4210,imageMember.Entity.UID);
        object presentRoster=null;object presentUnion=null;object presentOutsider=null;object presentDeputy=null;
        foreach(object member in (Array)IdentityField(presentNoble,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) presentRoster=member;
        foreach(object member in (Array)IdentityField(((Array)IdentityField(presentNoble,"OtherUnions")).GetValue(0),"Members")){
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) presentUnion=member;
            if((uint)IdentityField(member,"Uid")==deputy.Entity.UID) presentDeputy=member;
        }
        foreach(object member in (Array)IdentityField(IdentityField(presentNoble,"Union"),"Members"))
            if((uint)IdentityField(member,"Uid")==outsider.Entity.UID) presentOutsider=member;
        object rosterNoble=IdentityField(presentRoster,"Nobility");
        object unionNoble=IdentityField(presentUnion,"Nobility");
        object outsiderNoble=IdentityField(presentOutsider,"Nobility");
        Check(IdentityKind(presentNoble)=="Consistent" && rosterNoble!=null && unionNoble!=null && !ReferenceEquals(rosterNoble,unionNoble)
            && (uint)IdentityField(rosterNoble,"EntityUid")==imageMember.Entity.UID
            && (Guid)IdentityField(rosterNoble,"EntityToken")==imageMemberToken
            && (string)IdentityField(rosterNoble,"Name")=="OldNoble" && (string)IdentityField(presentRoster,"Name")!= "OldNoble"
            && IdentityField(rosterNoble,"Donation") is ulong && (ulong)IdentityField(rosterNoble,"Donation")==30000000UL
            && (int)IdentityField(rosterNoble,"Gender")==2 && (int)IdentityField(rosterNoble,"PacketGender")==3
            && (uint)IdentityField(rosterNoble,"Mesh")==371005999 && (int)IdentityField(rosterNoble,"Rey")==1
            && (string)IdentityField(rosterNoble,"DurationRaw")=="2026-09-24" && IdentityField(rosterNoble,"DerivedRank")==null
            && (int)IdentityField(presentRoster,"BodyDigit")==5 && (int)IdentityField(presentRoster,"LoginFallbackGender")==0
            && (uint)IdentityField(presentNoble,"MaleFemale")==1
            && presentDeputy!=null && IdentityField(presentDeputy,"Nobility")==null
            && outsiderNoble!=null && (int)IdentityField(outsiderNoble,"Gender")==4 && (int)IdentityField(outsiderNoble,"PacketGender")==5
            && (uint)IdentityField(outsiderNoble,"Mesh")==42 && (int)IdentityField(presentOutsider,"Body")==2002
            && (int)IdentityField(presentOutsider,"BodyDigit")==2 && (int)IdentityField(presentOutsider,"LoginFallbackGender")==1
            && (uint)IdentityField(presentOutsider,"ConcatenatedMesh")==2012002
            && IdentityField(outsiderNoble,"DerivedRank")==null && (ulong)IdentityField(outsiderNoble,"Donation")==0UL,
            "REVIEW-382 saved nobility, body digit, login fallback, packet gender, and mesh stay separate");
        Query("UPDATE nobility SET donation=0,gender=0,rey=1,duracionrey='' WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        object zeroNoble=IdentityRead(4210,imageMember.Entity.UID);object zeroRoster=null;
        foreach(object member in (Array)IdentityField(zeroNoble,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) zeroRoster=member;
        object zeroRow=IdentityField(zeroRoster,"Nobility");
        Check(IdentityKind(zeroNoble)=="Consistent" && zeroRow!=null && (ulong)IdentityField(zeroRow,"Donation")==0UL
            && (int)IdentityField(zeroRow,"Gender")==0 && (int)IdentityField(zeroRow,"PacketGender")==1
            && (int)IdentityField(zeroRow,"Rey")==1 && (string)IdentityField(zeroRow,"DurationRaw")==""
            && IdentityField(zeroRow,"DerivedRank")==null,
            "REVIEW-382 stored zero donation and gender stay present while paid king is not a rank");
        Query("UPDATE nobility SET gender=3 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        object packetThree=IdentityRead(4210,imageMember.Entity.UID);object packetRoster=null;
        foreach(object member in (Array)IdentityField(packetThree,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) packetRoster=member;
        Check(IdentityKind(packetThree)=="Consistent" && (int)IdentityField(IdentityField(packetRoster,"Nobility"),"Gender")==3
            && (int)IdentityField(IdentityField(packetRoster,"Nobility"),"PacketGender")==1,
            "REVIEW-382 guild packet gender maps saved gender 3 to 1");
        Query("UPDATE nobility SET gender=2 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        Query("ALTER TABLE nobility ALTER COLUMN donation DROP NOT NULL, ALTER COLUMN gender DROP NOT NULL, ALTER COLUMN rey DROP NOT NULL");
        Query("UPDATE nobility SET donation=NULL,gender=NULL,rey=NULL WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        object nullNoble=IdentityRead(4210,imageMember.Entity.UID);object nullRoster=null;
        foreach(object member in (Array)IdentityField(nullNoble,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) nullRoster=member;
        object nullRow=IdentityField(nullRoster,"Nobility");
        Check(IdentityKind(nullNoble)=="Consistent" && nullRow!=null && IdentityField(nullRow,"Donation")==null
            && IdentityField(nullRow,"Gender")==null && IdentityField(nullRow,"PacketGender")==null && IdentityField(nullRow,"Rey")==null,
            "REVIEW-382 null nobility columns stay absent inside a present row");
        Query("UPDATE nobility SET donation=30000000,gender=2,rey=1,duracionrey='2026-09-24' WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        Query("ALTER TABLE entities ALTER COLUMN face DROP NOT NULL, ALTER COLUMN body DROP NOT NULL");
        Query("UPDATE entities SET face=NULL,body=NULL WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        object blankAppearance=IdentityRead(4210,imageMember.Entity.UID);object blankRoster=null;
        foreach(object member in (Array)IdentityField(blankAppearance,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) blankRoster=member;
        Check(IdentityKind(blankAppearance)=="Consistent" && IdentityField(blankRoster,"Face")==null && IdentityField(blankRoster,"Body")==null
            && IdentityField(blankRoster,"BodyDigit")==null && IdentityField(blankRoster,"LoginFallbackGender")==null
            && IdentityField(blankRoster,"ConcatenatedMesh")==null && IdentityField(blankRoster,"Nobility")!=null,
            "REVIEW-382 null face and body do not become zeros or a mesh");
        Query("UPDATE entities SET face=65535,body=65535 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        object wideMesh=IdentityRead(4210,imageMember.Entity.UID);object wideRoster=null;
        foreach(object member in (Array)IdentityField(wideMesh,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) wideRoster=member;
        Check(IdentityKind(wideMesh)=="Consistent" && (int)IdentityField(wideRoster,"Face")==65535 && (int)IdentityField(wideRoster,"Body")==65535
            && IdentityField(wideRoster,"ConcatenatedMesh")==null && (int)IdentityField(wideRoster,"BodyDigit")==5,
            "REVIEW-382 a concatenated mesh that does not fit a uint stays unresolved");
        foreach(int badAppearance in new[]{-1,65536}){
            Query("UPDATE entities SET face=@value,body=1005 WHERE uid=@uid","value",badAppearance,"uid",(long)imageMember.Entity.UID);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 malformed face is refused: "+badAppearance);
            Query("UPDATE entities SET face=37,body=@value WHERE uid=@uid","value",badAppearance,"uid",(long)imageMember.Entity.UID);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 malformed body is refused: "+badAppearance);
        }
        Query("UPDATE entities SET face=37,body=1005 WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        foreach(string badNoble in new[]{"gender","mesh","rey","donation"}){
            if(badNoble=="gender") Query("UPDATE nobility SET gender=256 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
            else if(badNoble=="mesh") Query("UPDATE nobility SET mesh=-1 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
            else if(badNoble=="rey") Query("UPDATE nobility SET rey=256 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
            else Query("UPDATE nobility SET donation=-1 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 malformed nobility value is refused: "+badNoble);
            Query("UPDATE nobility SET donation=30000000,gender=2,mesh=371005999,rey=1 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        }
        Query("UPDATE nobility SET mesh=4294967296 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 nobility mesh above uint is refused");
        Query("UPDATE nobility SET mesh=371005999 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        foreach(object badToken in new object[]{DBNull.Value,Guid.Empty,Guid.NewGuid()}){
            if(badToken==DBNull.Value) Query("UPDATE nobility SET revora_entity_token=NULL WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
            else Query("UPDATE nobility SET revora_entity_token=@token WHERE entityuid=@uid","token",badToken,"uid",(long)imageMember.Entity.UID);
            Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 missing empty or mismatched nobility token");
        }
        Query("UPDATE nobility SET revora_entity_token=@token WHERE entityuid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        Query("UPDATE nobility SET revora_entity_token=NULL WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        Call(GuildIdentityType,null,"EnsureSchema");
        Check(Scalar("SELECT count(*) FROM nobility WHERE entityuid=@uid AND revora_entity_token IS NULL",imageMember.Entity.UID)==1
            && IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-384 schema rerun does not enroll a legacy nobility row");
        Query("UPDATE nobility SET revora_entity_token=@token WHERE entityuid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        bool duplicateNoble=false;
        try{Query("INSERT INTO nobility(entityname,entityuid,donation,gender,mesh,rey,duracionrey,revora_entity_token) VALUES('DupNoble',880002,1,0,0,0,'',@token)","token",imageMemberToken);}
        catch(Npgsql.PostgresException ex){duplicateNoble=ex.SqlState=="23505";}
        Check(duplicateNoble,"REVIEW-382 two nobility rows cannot share a character lifetime");
        Query("DELETE FROM nobility WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","REVIEW-382 deleting the nobility row leaves the character without one");
        Query("INSERT INTO nobility(entityname,entityuid,donation,gender,mesh,rey,duracionrey,revora_entity_token) VALUES('Orphan',880001,9,1,8,0,'',@token)","token",imageMemberToken);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 a token on another nobility uid does not attach");
        Query("DELETE FROM nobility WHERE entityuid=880001");
        Query("INSERT INTO nobility(entityname,entityuid,donation,gender,mesh,rey,duracionrey) VALUES('Legacy',@uid,4,1,8,0,'')","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 reinserting the same nobility uid without a token does not inherit one");
        Query("UPDATE nobility SET revora_entity_token=@token,donation=5,entityname='Rebound' WHERE entityuid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        object reboundNoble=IdentityRead(4210,imageMember.Entity.UID);object reboundRoster=null;
        foreach(object member in (Array)IdentityField(reboundNoble,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) reboundRoster=member;
        Check(IdentityKind(reboundNoble)=="Consistent" && (ulong)IdentityField(IdentityField(reboundRoster,"Nobility"),"Donation")==5UL
            && (string)IdentityField(IdentityField(reboundRoster,"Nobility"),"Name")=="Rebound",
            "REVIEW-382 an explicit nobility rebind uses the replacement row");
        Guid replacedCharacter=Guid.NewGuid();
        Query("CREATE TABLE review382_character_copy AS SELECT * FROM entities WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Query("DELETE FROM entities WHERE uid=@uid","uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageFounder.Entity.UID))=="Contradiction","REVIEW-382 deleting the character leaves the nobility row orphaned");
        Query("UPDATE review382_character_copy SET revora_entity_token=@token","token",replacedCharacter);
        Query("INSERT INTO entities SELECT * FROM review382_character_copy");
        Query("UPDATE revora_guild_member_identity SET entity_token=@token WHERE entity_uid=@uid","token",replacedCharacter,"uid",(long)imageMember.Entity.UID);
        Query("UPDATE revora_union_member_binding SET entity_token=@token WHERE entity_uid=@uid","token",replacedCharacter,"uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-382 a replaced character does not inherit the old nobility row");
        Query("UPDATE nobility SET revora_entity_token=@token WHERE entityuid=@uid","token",replacedCharacter,"uid",(long)imageMember.Entity.UID);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","REVIEW-382 nobility follows only an explicit character rebind");
        Query("UPDATE entities SET revora_entity_token=@token WHERE uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        Query("UPDATE revora_guild_member_identity SET entity_token=@token WHERE entity_uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        Query("UPDATE revora_union_member_binding SET entity_token=@token WHERE entity_uid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        Query("UPDATE nobility SET revora_entity_token=@token WHERE entityuid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        Query("DROP TABLE review382_character_copy");
        Query("ALTER TABLE nobility RENAME TO review382_nobility_away");
        object nobleFailure=IdentityRead(4210,imageMember.Entity.UID);
        Check(IdentityKind(nobleFailure)=="ReadFailure" && ((string)IdentityField(nobleFailure,"Detail")).IndexOf("42P01")>=0
            && Kernel.Guilds.Count==additionRegistries && Kernel.Unions.Count==unionsBefore,
            "REVIEW-382 a missing nobility table is a read failure");
        Query("ALTER TABLE review382_nobility_away RENAME TO nobility");
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE nobility SET donation=7 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        });
        object nobleRace;
        try{nobleRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        object nobleAfter=IdentityRead(4210,imageMember.Entity.UID);object nobleAfterRoster=null;
        foreach(object member in (Array)IdentityField(nobleAfter,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) nobleAfterRoster=member;
        Check(IdentityKind(nobleRace)=="ReadFailure" && ((string)IdentityField(nobleRace,"Detail")).IndexOf("40001")>=0
            && IdentityKind(nobleAfter)=="Consistent" && (ulong)IdentityField(IdentityField(nobleAfterRoster,"Nobility"),"Donation")==7UL,
            "REVIEW-382 a committed nobility donation change returns 40001 and the fresh read keeps it");
        Query("UPDATE nobility SET donation=5 WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("UPDATE nobility SET revora_entity_token=@token WHERE entityuid=@uid","token",Guid.NewGuid(),"uid",(long)imageMember.Entity.UID);
        });
        object tokenRace;
        try{tokenRace=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        Check(IdentityKind(tokenRace)=="ReadFailure" && ((string)IdentityField(tokenRace,"Detail")).IndexOf("40001")>=0
            && IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction",
            "REVIEW-382 a committed nobility token change returns 40001 and the fresh read is a contradiction");
        Query("UPDATE nobility SET revora_entity_token=@token WHERE entityuid=@uid","token",imageMemberToken,"uid",(long)imageMember.Entity.UID);
        // Repeatable read preserves absence; row locks do not fence a later insert.
        GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,(Action)delegate{
            Query("INSERT INTO nobility(entityname,entityuid,donation,gender,mesh,rey,duracionrey,revora_entity_token) VALUES('Later',@uid,30,255,0,0,'',@token)",
                "uid",(long)deputy.Entity.UID,"token",deputyToken);
        });
        object insertedNoble;
        try{insertedNoble=IdentityRead(4210,imageMember.Entity.UID);}
        finally{GuildIdentityType.GetField("AfterGuildLock",Hidden).SetValue(null,null);}
        object insertedDeputy=null;
        foreach(object member in (Array)IdentityField(((Array)IdentityField(insertedNoble,"OtherUnions")).GetValue(0),"Members"))
            if((uint)IdentityField(member,"Uid")==deputy.Entity.UID) insertedDeputy=member;
        object freshInsertedNoble=IdentityRead(4210,imageMember.Entity.UID);object freshInsertedDeputy=null;
        foreach(object member in (Array)IdentityField(((Array)IdentityField(freshInsertedNoble,"OtherUnions")).GetValue(0),"Members"))
            if((uint)IdentityField(member,"Uid")==deputy.Entity.UID) freshInsertedDeputy=member;
        Check(IdentityKind(insertedNoble)=="Consistent" && insertedDeputy!=null && IdentityField(insertedDeputy,"Nobility")==null
            && IdentityKind(freshInsertedNoble)=="Consistent" && freshInsertedDeputy!=null
            && (ulong)IdentityField(IdentityField(freshInsertedDeputy,"Nobility"),"Donation")==30UL
            && (int)IdentityField(IdentityField(freshInsertedDeputy,"Nobility"),"PacketGender")==256,
            "REVIEW-384 concurrent insertion preserves snapshot absence; fresh read sees row and uint packet gender");
        Query("DELETE FROM nobility WHERE entityuid=@uid","uid",(long)deputy.Entity.UID);
        Query("ALTER TABLE nobility ALTER COLUMN mesh DROP NOT NULL, ALTER COLUMN duracionrey DROP NOT NULL");
        Query("UPDATE nobility SET mesh=NULL,duracionrey=NULL WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        object nullMeshNoble=IdentityRead(4210,imageMember.Entity.UID);object nullMeshRoster=null;
        foreach(object member in (Array)IdentityField(nullMeshNoble,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) nullMeshRoster=member;
        Check(IdentityKind(nullMeshNoble)=="Consistent" && IdentityField(IdentityField(nullMeshRoster,"Nobility"),"Mesh")==null
            && IdentityField(IdentityField(nullMeshRoster,"Nobility"),"DurationRaw")==null,
            "REVIEW-384 null saved mesh and duration stay distinct from zero and empty text");
        Query("UPDATE nobility SET mesh=0,duracionrey='' WHERE entityuid=@uid","uid",(long)imageMember.Entity.UID);
        object copiedNoble=IdentityRead(4210,imageMember.Entity.UID);
        object copiedProjection=Call(GuildIdentityType,null,"Project",copiedNoble);
        Check((bool)IdentityField(copiedProjection,"Ok"),"REVIEW-382 nobility projection succeeds");
        IdentityIndependentCopy(IdentityField(copiedNoble,"Members"),IdentityField(copiedProjection,"Members"),"REVIEW-382 roster provenance");
        IdentityIndependentCopy(IdentityField(copiedNoble,"Union"),IdentityField(copiedProjection,"Union"),"REVIEW-382 union provenance");
        IdentityIndependentCopy(IdentityField(copiedNoble,"OtherUnions"),IdentityField(copiedProjection,"OtherUnions"),"REVIEW-382 other union provenance");
        object copiedRoster=null;
        foreach(object member in (Array)IdentityField(copiedNoble,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) copiedRoster=member;
        object savedName=IdentityField(IdentityField(copiedRoster,"Nobility"),"Name");
        copiedRoster.GetType().GetField("Nobility").GetValue(copiedRoster).GetType().GetField("Name").SetValue(IdentityField(copiedRoster,"Nobility"),null);
        object nobleFault;
        try{nobleFault=Call(GuildIdentityType,null,"Project",copiedNoble);}
        finally{copiedRoster.GetType().GetField("Nobility").GetValue(copiedRoster).GetType().GetField("Name").SetValue(IdentityField(copiedRoster,"Nobility"),savedName);}
        Check(!(bool)IdentityField(nobleFault,"Ok") && IdentityField(nobleFault,"Union")==null
            && ((Array)IdentityField(nobleFault,"Members")).Length==0 && ((Array)IdentityField(nobleFault,"OtherUnions")).Length==0
            && Kernel.Guilds.Count==additionRegistries && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && Kernel.Unions.Count==unionsBefore && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null && imageFounder.Guild==null,
            "REVIEW-382 a nobility copy fault discards partial data and preserves registries and sessions");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent" && Kernel.Unions.Count==unionsBefore,
            "REVIEW-382 nobility fixtures leave the saved image readable");
        Guid review389Enemy=Guid.NewGuid();
        Query("INSERT INTO guilds(id,name,leadername,bulletin,silverfund,enrollmentdate,bulletindate,incarnation) VALUES(4214,'EnemyHarbor','EnemyLeader','',1,20260924,20260924,@life)","life",review389Enemy);
        Query("INSERT INTO guildenemy(guildid,enemyid) VALUES(4210,4214)");
        Query("INSERT INTO revora_guild_association(incarnation,guild_id,kind,subject_id,peer_incarnation) VALUES(@life,4210,'enemy',4214,@peer)","life",imageLife,"peer",review389Enemy);
        Query("UPDATE guilds SET adend=0,bulletin='' WHERE id=4210");
        object review389Zero=IdentityRead(4210,imageMember.Entity.UID);
        object review389ZeroCopy=Call(GuildIdentityType,null,"Project",review389Zero);
        Check(IdentityKind(review389Zero)=="Consistent" && (bool)IdentityField(review389ZeroCopy,"Ok")
            && (long)IdentityField(review389ZeroCopy,"AdvertisementEndsRaw")==0 && (string)IdentityField(review389ZeroCopy,"Bulletin")==""
            && IdentityField(review389ZeroCopy,"BlackList")==null && (string)IdentityField(review389ZeroCopy,"BlackListState")=="unsupported",
            "REVIEW-389 stored zero advertisement end and empty bulletin stay raw, and blacklist stays unknown");
        Query("UPDATE guilds SET silverfund=666666,conquerpointfund=22222,bulletin='Projected bulletin',bulletindate=20260925,levelrequirement=91,rebornrequirement=2,classrequirement=11,autorecruit=3,wins=12,losts=4,ctfpoints=56,ctfreward=78,maxenchant=7,adv='Projected advertisement',ad_rate=9,adlevelrequirement=15,adrebornrequirement=1,adclassrequirement=41,malefemale=2,adend=13579,ctfdonationcps=101,ctfdonationsilver=102,ctfdonationcpsold=103,ctfdonationsilverold=104 WHERE id=4210");
        object review389=IdentityRead(4210,imageMember.Entity.UID);
        object review389Copy=Call(GuildIdentityType,null,"Project",review389);
        string[] review389Fields=new string[]{"Incarnation","GuildId","Name","LeaderName","LeaderUid","SilverFund","ConquerPointFund","Bulletin","EnrollmentDate","BulletinDate","LevelRequirement","RebornRequirement","ClassRequirement","AdLevelRequirement","AdRebornRequirement","AdClassRequirement","AutoRecruit","MaleFemale","UnionId","Wins","Losses","CtfPoints","CtfReward","CtfDonationCps","CtfDonationSilver","CtfDonationCpsOld","CtfDonationSilverOld","MaxEnchant","Advertisement","AdvertisementRate","AdvertisementEndsRaw","BlackList","BlackListState"};
        Check((bool)IdentityField(review389Copy,"Ok") && (Guid)IdentityField(review389Copy,"Incarnation")==imageLife && imageLife!=Guid.Empty,"REVIEW-389 projection keeps the guild incarnation");
        for(int review389Field=0;review389Field<review389Fields.Length;review389Field++)
            Check(object.Equals(IdentityField(review389,review389Fields[review389Field]),IdentityField(review389Copy,review389Fields[review389Field])),
                "REVIEW-389 projected metadata: "+review389Fields[review389Field]);
        Check((ulong)IdentityField(review389Copy,"SilverFund")==666666UL && (uint)IdentityField(review389Copy,"ConquerPointFund")==22222
            && (string)IdentityField(review389Copy,"Bulletin")=="Projected bulletin" && (uint)IdentityField(review389Copy,"BulletinDate")==20260925
            && (uint)IdentityField(review389Copy,"EnrollmentDate")==20260924 && (uint)IdentityField(review389Copy,"LevelRequirement")==91
            && (uint)IdentityField(review389Copy,"RebornRequirement")==2 && (uint)IdentityField(review389Copy,"ClassRequirement")==11
            && (uint)IdentityField(review389Copy,"AutoRecruit")==3 && (uint)IdentityField(review389Copy,"Wins")==12
            && (uint)IdentityField(review389Copy,"Losses")==4 && (uint)IdentityField(review389Copy,"CtfPoints")==56
            && (uint)IdentityField(review389Copy,"CtfReward")==78 && (uint)IdentityField(review389Copy,"MaxEnchant")==7
            && (string)IdentityField(review389Copy,"Advertisement")=="Projected advertisement" && (uint)IdentityField(review389Copy,"AdvertisementRate")==9
            && (uint)IdentityField(review389Copy,"AdLevelRequirement")==15 && (uint)IdentityField(review389Copy,"AdRebornRequirement")==1
            && (uint)IdentityField(review389Copy,"AdClassRequirement")==41 && (uint)IdentityField(review389Copy,"MaleFemale")==2
            && (long)IdentityField(review389Copy,"AdvertisementEndsRaw")==13579 && (uint)IdentityField(review389Copy,"UnionId")==77
            && (uint)IdentityField(review389Copy,"CtfDonationCps")==101 && (uint)IdentityField(review389Copy,"CtfDonationSilver")==102
            && (uint)IdentityField(review389Copy,"CtfDonationCpsOld")==103 && (uint)IdentityField(review389Copy,"CtfDonationSilverOld")==104,
            "REVIEW-389 every saved guild metadata field is non-default and copied");
        IdentityIndependentCopy(IdentityField(review389,"Allies"),IdentityField(review389Copy,"Allies"),"REVIEW-389 allies");
        IdentityIndependentCopy(IdentityField(review389,"Enemies"),IdentityField(review389Copy,"Enemies"),"REVIEW-389 enemies");
        IdentityIndependentCopy(IdentityField(review389,"Members"),IdentityField(review389Copy,"Members"),"REVIEW-389 members");
        object review389Member=null;
        foreach(object member in (Array)IdentityField(review389Copy,"Members"))
            if((uint)IdentityField(member,"Uid")==imageMember.Entity.UID) review389Member=member;
        Check(review389Member!=null && IdentityField(IdentityField(review389Member,"Nobility"),"DerivedRank")==null
            && IdentityField(review389Copy,"BlackList")==null && (string)IdentityField(review389Copy,"BlackListState")=="unsupported",
            "REVIEW-389 projection keeps unresolved rank and unknown blacklist");
        Guid review389Reused=Guid.NewGuid();
        Query("UPDATE guilds SET incarnation=@life WHERE id=4210","life",review389Reused);
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Contradiction","REVIEW-389 a replaced guild incarnation does not match old marks");
        object review389Held=Call(GuildIdentityType,null,"Project",review389);
        Check((bool)IdentityField(review389Held,"Ok") && (Guid)IdentityField(review389Held,"Incarnation")==imageLife
            && (uint)IdentityField(review389Held,"GuildId")==4210,"REVIEW-389 projection keeps the captured incarnation after the row id is reused");
        Query("UPDATE guilds SET incarnation=@life WHERE id=4210","life",imageLife);
        Guid review389NextAlly=Guid.NewGuid();
        Query("UPDATE guilds SET incarnation=@life WHERE id=4211","life",review389NextAlly);
        Query("UPDATE revora_guild_association SET peer_incarnation=@life WHERE guild_id=4210 AND kind='ally'","life",review389NextAlly);
        object review389AllyRead=IdentityRead(4210,imageMember.Entity.UID);
        object review389AllyCopy=Call(GuildIdentityType,null,"Project",review389AllyRead);
        Check(IdentityKind(review389AllyRead)=="Consistent" && (Guid)IdentityField(((Array)IdentityField(review389AllyCopy,"Allies")).GetValue(0),"Incarnation")==review389NextAlly
            && (Guid)IdentityField(((Array)IdentityField(review389,"Allies")).GetValue(0),"Incarnation")==replacedAlly,
            "REVIEW-389 a replaced ally token is copied from the snapshot that read it");
        Query("UPDATE guilds SET incarnation=@life WHERE id=4211","life",replacedAlly);
        Query("UPDATE revora_guild_association SET peer_incarnation=@life WHERE guild_id=4210 AND kind='ally'","life",replacedAlly);
        GuildIdentityType.GetField("BeforeProject",Hidden).SetValue(null,(Action)delegate{throw new InvalidOperationException("review389 early");});
        object review389Early;
        try{review389Early=Call(GuildIdentityType,null,"Project",review389);}
        finally{GuildIdentityType.GetField("BeforeProject",Hidden).SetValue(null,null);}
        Check(!(bool)IdentityField(review389Early,"Ok") && (Guid)IdentityField(review389Early,"Incarnation")==Guid.Empty
            && IdentityField(review389Early,"BlackList")==null && IdentityField(review389Early,"BlackListState")==null
            && ((Array)IdentityField(review389Early,"Allies")).Length==0 && ((Array)IdentityField(review389Early,"Enemies")).Length==0
            && ((Array)IdentityField(review389Early,"Members")).Length==0 && IdentityField(review389Early,"Union")==null
            && (uint)IdentityField(review389Early,"ConquerPointFund")==0,
            "REVIEW-389 an early projection fault returns no partial metadata");
        Array review389Allies=(Array)IdentityField(review389,"Allies");
        object review389SavedAlly=review389Allies.GetValue(0);
        review389Allies.SetValue(null,0);
        object review389Late;
        try{review389Late=Call(GuildIdentityType,null,"Project",review389);}
        finally{review389Allies.SetValue(review389SavedAlly,0);}
        Check(!(bool)IdentityField(review389Late,"Ok") && (Guid)IdentityField(review389Late,"Incarnation")==Guid.Empty
            && IdentityField(review389Late,"Name")==null && IdentityField(review389Late,"BlackList")==null
            && ((Array)IdentityField(review389Late,"Allies")).Length==0 && ((Array)IdentityField(review389Late,"Enemies")).Length==0
            && ((Array)IdentityField(review389Late,"Members")).Length==0 && ((Array)IdentityField(review389Late,"OtherUnions")).Length==0
            && IdentityField(review389Late,"Union")==null
            && Kernel.Guilds.Count==additionRegistries && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && Kernel.Unions.Count==unionsBefore && ReferenceEquals(Kernel.Guilds[4201],stale) && imageMember.Guild==null && imageFounder.Guild==null,
            "REVIEW-389 a late relation fault discards copied metadata and preserves registries and sessions");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent","REVIEW-389 metadata fixtures leave the saved image readable");
        DateTime review394Clock=new DateTime(2026,6,1,12,0,0,DateTimeKind.Utc);
        long review394Stamp=review394Clock.AddDays(-1).ToBinary();
        long review394Unlock=review394Clock.AddHours(2).ToBinary();
        long review394Purify=review394Clock.AddSeconds(-40).ToBinary();
        long review394Temporary=review394Clock.AddSeconds(-30).ToBinary();
        long review394Rows=Scalar("SELECT count(*) FROM itemadding WHERE uid=@uid",imageWeapon.UID);
        long review394Items=Scalar("SELECT count(*) FROM items WHERE uid=@uid",imageWeapon.UID);
        Query("UPDATE items SET daystamp=@stamp,days=3,locked=2,unlockend=@unlock,refineryitem=724440,refinerytime=@time WHERE uid=@item",
            "stamp",review394Stamp,"unlock",review394Unlock,"time",review394Clock.AddHours(1).ToBinary(),"item",(long)imageWeapon.UID);
        Query("UPDATE itemadding SET duration=100,addedon=@on WHERE iditemadding=910001","on",review394Purify);
        Query("UPDATE itemadding SET addinglevel=5,addingpercent=44,stabilizationpoints=10,duration=0,addedon=@on WHERE iditemadding=910002","on",review394Clock.ToBinary());
        Query("UPDATE itemadding SET duration=120,addedon=@on,addinglevel=4,addingpercent=15,stabilizationpoints=20 WHERE iditemadding=910003","on",review394Temporary);
        string review395ItemSql="SELECT hashtextextended(row_to_json(i)::text,0) FROM items i WHERE uid=@uid";
        string review395AdditionSql="SELECT hashtextextended(string_agg(row_to_json(a)::text,'|' ORDER BY iditemadding),0) FROM itemadding a WHERE uid=@uid";
        long review395ItemHash=Scalar(review395ItemSql,imageWeapon.UID),review395AdditionHash=Scalar(review395AdditionSql,imageWeapon.UID);
        object review394=IdentityRead(4210,imageMember.Entity.UID);
        object review394Item=IdentityField(((Array)IdentityField(review394,"Inscriptions")).GetValue(0),"Item");
        long review394RawStamp=(long)IdentityField(review394Item,"DayStampRaw");
        uint[] review394Ids=new uint[]{990001,990002};
        uint[] review394Levels=new uint[]{5,4};
        uint[] review394Percents=new uint[]{44,15};
        object review394Derived=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        object review394Effect=((Array)IdentityField(review394Derived,"Items")).GetValue(0);
        Type review394Refinery=typeof(Entity).Assembly.GetType("Revora.Game.Features.RefineryProgress");
        Check((bool)IdentityField(review394Derived,"Ok") && (bool)IdentityField(review394Derived,"LoaderDecodesFromBinary")
            && (bool)IdentityField(review394Derived,"WritersStoreTicks") && (bool)IdentityField(review394Derived,"TimeLeftUsesSeconds")
            && (string)IdentityField(review394Derived,"ClockKind")=="Utc"
            && IdentityField(review394Effect,"DayStatus").ToString()=="Active" && (uint)IdentityField(review394Effect,"TimeLeftSeconds")==172800
            && !((bool)IdentityField(review394Effect,"LoaderWouldDelete"))
            && IdentityField(review394Effect,"LockStatus").ToString()=="Active" && !((bool)IdentityField(review394Effect,"LoaderWouldClearLock"))
            && IdentityField(IdentityField(review394Effect,"Purification"),"Status").ToString()=="Active"
            && (long)IdentityField(IdentityField(review394Effect,"Purification"),"SecondsRemaining")==60
            && IdentityField(IdentityField(review394Effect,"PermanentRefinery"),"Status").ToString()=="Permanent"
            && IdentityField(IdentityField(review394Effect,"TemporaryRefinery"),"Status").ToString()=="Active"
            && (long)IdentityField(IdentityField(review394Effect,"TemporaryRefinery"),"SecondsRemaining")==90
            && (uint)IdentityField(review394Effect,"CurrentRefineryId")==990002
            && (int)IdentityField(review394Effect,"DerivedCredit")==270 && (int)IdentityField(review394Item,"StabilizationCredit")==20
            && (int)Call(review394Refinery,null,"Cost",(uint)5)==270
            && (long)IdentityField(review394Item,"DayStampRaw")==review394RawStamp
            && Scalar("SELECT count(*) FROM itemadding WHERE uid=@uid",imageWeapon.UID)==review394Rows
            && Scalar("SELECT count(*) FROM items WHERE uid=@uid",imageWeapon.UID)==review394Items
            && Scalar("SELECT daystamp FROM items WHERE uid=@uid",imageWeapon.UID)==review394Stamp,
            "REVIEW-394 captured clock derives active time, lock, purification, and refinery without writing");
        object review394Again=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(!ReferenceEquals(((Array)IdentityField(review394Derived,"Items")).GetValue(0),((Array)IdentityField(review394Again,"Items")).GetValue(0))
            && !ReferenceEquals(IdentityField(review394Effect,"Purification"),IdentityField(((Array)IdentityField(review394Again,"Items")).GetValue(0),"Purification")),
            "REVIEW-394 derived items and effects are independent objects");
        object review394Boundary=Call(GuildIdentityType,null,"Derive",review394,review394Clock.AddDays(-1).AddDays(3),review394Ids,review394Levels,review394Percents);
        object review394BoundaryItem=((Array)IdentityField(review394Boundary,"Items")).GetValue(0);
        Check(IdentityField(review394BoundaryItem,"DayStatus").ToString()=="Expired" && (bool)IdentityField(review394BoundaryItem,"LoaderWouldDelete")
            && IdentityField(review394BoundaryItem,"TimeLeftSeconds")==null,
            "REVIEW-394 the day boundary is inclusive and does not invent remaining seconds");
        object review394Fallback=Call(GuildIdentityType,null,"Derive",review394,review394Clock.AddSeconds(90),review394Ids,review394Levels,review394Percents);
        object review394FallbackItem=((Array)IdentityField(review394Fallback,"Items")).GetValue(0);
        Check(IdentityField(IdentityField(review394FallbackItem,"TemporaryRefinery"),"Status").ToString()=="Expired"
            && (uint)IdentityField(review394FallbackItem,"CurrentRefineryId")==990001,
            "REVIEW-394 an expired temporary refinery falls back to the permanent effect");
        review394Item.GetType().GetField("Days").SetValue(review394Item,0);
        object review394Undated=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(((Array)IdentityField(review394Undated,"Items")).GetValue(0),"DayStatus").ToString()=="Absent"
            && IdentityField(((Array)IdentityField(review394Undated,"Items")).GetValue(0),"TimeLeftSeconds")==null,
            "REVIEW-394 zero days is an absent time limit, not a zero-second effect");
        review394Item.GetType().GetField("Days").SetValue(review394Item,3);
        review394Item.GetType().GetField("DayStampRaw").SetValue(review394Item,0L);
        object review394ZeroStamp=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check((bool)IdentityField(((Array)IdentityField(review394ZeroStamp,"Items")).GetValue(0),"RawDayStampIsZero")
            && IdentityField(((Array)IdentityField(review394ZeroStamp,"Items")).GetValue(0),"DayStatus").ToString()=="Expired",
            "REVIEW-394 a stored zero day stamp still decodes and is not treated as missing");
        review394Item.GetType().GetField("DayStampRaw").SetValue(review394Item,long.MaxValue);
        object review394BadDate=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(((Array)IdentityField(review394BadDate,"Items")).GetValue(0),"DayStatus").ToString()=="Malformed"
            && !((bool)IdentityField(((Array)IdentityField(review394BadDate,"Items")).GetValue(0),"LoaderWouldDelete")),
            "REVIEW-394 an invalid binary day stamp stays malformed");
        review394Item.GetType().GetField("DayStampRaw").SetValue(review394Item,new DateTime(9999,12,31,0,0,0,DateTimeKind.Utc).ToBinary());
        object review394Overflow=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(((Array)IdentityField(review394Overflow,"Items")).GetValue(0),"DayStatus").ToString()=="Overflow",
            "REVIEW-394 a day stamp that cannot accept its duration stays overflow");
        review394Item.GetType().GetField("DayStampRaw").SetValue(review394Item,review394RawStamp);
        review394Item.GetType().GetField("Lock").SetValue(review394Item,2);
        review394Item.GetType().GetField("UnlockEndRaw").SetValue(review394Item,review394Clock.ToBinary());
        object review394UnlockNow=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check((bool)IdentityField(((Array)IdentityField(review394UnlockNow,"Items")).GetValue(0),"LoaderWouldClearLock"),
            "REVIEW-394 lock 2 clears when the captured clock reaches the unlock instant");
        review394Item.GetType().GetField("Lock").SetValue(review394Item,1);
        review394Item.GetType().GetField("UnlockEndRaw").SetValue(review394Item,review394Clock.AddHours(-1).ToBinary());
        object review394OtherLock=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(((Array)IdentityField(review394OtherLock,"Items")).GetValue(0),"LockStatus").ToString()=="NotTimed"
            && !((bool)IdentityField(((Array)IdentityField(review394OtherLock,"Items")).GetValue(0),"LoaderWouldClearLock")),
            "REVIEW-394 a non-timed lock is not cleared from its unlock column");
        review394Item.GetType().GetField("Lock").SetValue(review394Item,2);
        review394Item.GetType().GetField("UnlockEndRaw").SetValue(review394Item,review394Unlock);
        review394Item.GetType().GetField("RefineryTimeRaw").SetValue(review394Item,0L);
        object review394ZeroRefinery=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(((Array)IdentityField(review394ZeroRefinery,"Items")).GetValue(0),"SavedRefineryStatus").ToString()=="StoredZero"
            && !((bool)IdentityField(((Array)IdentityField(review394ZeroRefinery,"Items")).GetValue(0),"LoaderWouldClearSavedRefinery")),
            "REVIEW-394 a zero refinery time is stored zero and is not cleared");
        review394Item.GetType().GetField("RefineryTimeRaw").SetValue(review394Item,review394Clock.ToBinary());
        object review394ExactRefinery=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        object review394LaterRefinery=Call(GuildIdentityType,null,"Derive",review394,review394Clock.AddSeconds(1),review394Ids,review394Levels,review394Percents);
        Check(IdentityField(((Array)IdentityField(review394ExactRefinery,"Items")).GetValue(0),"SavedRefineryStatus").ToString()=="Active"
            && !((bool)IdentityField(((Array)IdentityField(review394ExactRefinery,"Items")).GetValue(0),"LoaderWouldClearSavedRefinery"))
            && (bool)IdentityField(((Array)IdentityField(review394LaterRefinery,"Items")).GetValue(0),"LoaderWouldClearSavedRefinery")
            && (uint)IdentityField(review394Item,"RefineItem")==724440,
            "REVIEW-394 saved refinery clears only after the decoded instant and the raw id stays");
        object review394Purification=IdentityField(review394Item,"Purification");
        review394Purification.GetType().GetField("Duration").SetValue(review394Purification,(uint)0);
        object review394PermanentPurify=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(IdentityField(((Array)IdentityField(review394PermanentPurify,"Items")).GetValue(0),"Purification"),"Status").ToString()=="Permanent"
            && IdentityField(IdentityField(((Array)IdentityField(review394PermanentPurify,"Items")).GetValue(0),"Purification"),"SecondsRemaining")==null,
            "REVIEW-394 purification duration zero is permanent, not an expired effect");
        review394Purification.GetType().GetField("Duration").SetValue(review394Purification,(uint)100);
        review394Purification.GetType().GetField("AddedOnRaw").SetValue(review394Purification,review394Clock.AddSeconds(-100).ToBinary());
        object review394PurifyEdge=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(IdentityField(((Array)IdentityField(review394PurifyEdge,"Items")).GetValue(0),"Purification"),"Status").ToString()=="Expired"
            && (bool)IdentityField(IdentityField(((Array)IdentityField(review394PurifyEdge,"Items")).GetValue(0),"Purification"),"LoaderWouldDelete"),
            "REVIEW-394 purification at zero seconds remaining is expired and would be deleted by the loader");
        object review394TemporaryEffect=IdentityField(review394Item,"TemporaryRefinery");
        uint review394TemporaryId=(uint)review394TemporaryEffect.GetType().GetField("AddingId").GetValue(review394TemporaryEffect);
        review394TemporaryEffect.GetType().GetField("AddingId").SetValue(review394TemporaryEffect,(uint)990099);
        object review394Unknown=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(IdentityField(((Array)IdentityField(review394Unknown,"Items")).GetValue(0),"TemporaryRefinery"),"Status").ToString()=="UnknownCatalog"
            && IdentityField(((Array)IdentityField(review394Unknown,"Items")).GetValue(0),"CurrentRefineryId")==null,
            "REVIEW-394 an unknown refinery catalog entry does not become an active effect");
        review394TemporaryEffect.GetType().GetField("AddingId").SetValue(review394TemporaryEffect,review394TemporaryId);
        review394TemporaryEffect.GetType().GetField("Percent").SetValue(review394TemporaryEffect,(uint)99);
        object review394Mismatch=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);
        Check(IdentityField(IdentityField(((Array)IdentityField(review394Mismatch,"Items")).GetValue(0),"TemporaryRefinery"),"Status").ToString()=="MismatchedCatalog",
            "REVIEW-394 a catalog row with a different percent stays mismatched");
        review394TemporaryEffect.GetType().GetField("Percent").SetValue(review394TemporaryEffect,(uint)15);
        object review394Missing=Call(GuildIdentityType,null,"Derive",review394,review394Clock,null,null,null);
        Check(!(bool)IdentityField(review394Missing,"Ok") && ((Array)IdentityField(review394Missing,"Items")).Length==0
            && (string)IdentityField(review394Missing,"Detail")=="missing catalog",
            "REVIEW-394 a missing catalog returns no partial derived item");
        GuildIdentityType.GetField("BeforeDerive",Hidden).SetValue(null,(Action)delegate{throw new InvalidOperationException("review394 early");});
        object review394Early;
        try{review394Early=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);}
        finally{GuildIdentityType.GetField("BeforeDerive",Hidden).SetValue(null,null);}
        GuildIdentityType.GetField("AfterDeriveItem",Hidden).SetValue(null,(Action)delegate{throw new InvalidOperationException("review394 late");});
        object review394Late;
        try{review394Late=Call(GuildIdentityType,null,"Derive",review394,review394Clock,review394Ids,review394Levels,review394Percents);}
        finally{GuildIdentityType.GetField("AfterDeriveItem",Hidden).SetValue(null,null);}
        Check(!(bool)IdentityField(review394Early,"Ok") && ((Array)IdentityField(review394Early,"Items")).Length==0
            && !(bool)IdentityField(review394Late,"Ok") && ((Array)IdentityField(review394Late,"Items")).Length==0
            && (long)IdentityField(review394Item,"DayStampRaw")==review394RawStamp
            && Kernel.Guilds.Count==additionRegistries && Kernel.Guilds_adv.Count==additionAds && Kernel.GamePool.Count==additionSessions
            && Kernel.Unions.Count==unionsBefore && imageMember.Guild==null,
            "REVIEW-394 early and late derivation faults discard partial results and preserve the raw image");
        Check(IdentityKind(IdentityRead(4210,imageMember.Entity.UID))=="Consistent"
            && Scalar("SELECT count(*) FROM itemadding WHERE uid=@uid",imageWeapon.UID)==review394Rows,
            "REVIEW-394 effect fixtures leave the saved rows in place");
        Review395DerivedRules(IdentityRead(4210,imageMember.Entity.UID),review394Clock,review394Ids,review394Levels,review394Percents);
        Check(Scalar(review395ItemSql,imageWeapon.UID)==review395ItemHash && Scalar(review395AdditionSql,imageWeapon.UID)==review395AdditionHash,
            "REVIEW-395 all item and addition columns remain unchanged across derivation and failure fixtures");
        Review400Scores(IdentityRead(4210,imageMember.Entity.UID),review394Clock,imageWeapon.UID,additionRegistries,additionAds,additionSessions,unionsBefore,imageMember);
        HeroClaim(founder,1,303);
        Check(HeroRows(founder,303,false)==0 && HeroPacksOf(founder,3005123)==0,
            "reading a guild identity does not pay stage 303");
    }
}
