using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
namespace Revora.Game.Features {
    public static partial class RevoraMasterSystem {
        private sealed class PeakOffer {internal Guid Id=Guid.NewGuid();internal byte Target;internal uint Price,Cp;internal DateTime Until;internal string Cache,Items,Skills,Profs;}
        private static readonly ConditionalWeakTable<GameState,PeakOffer> PeakOffers=new ConditionalWeakTable<GameState,PeakOffer>();
        private static void InitializePeak(){using(var c=DataHolder.MySqlConnection){c.Open();Q.Execute(c,null,@"CREATE TABLE IF NOT EXISTS revora_peak_purchases(
id uuid PRIMARY KEY,uid bigint NOT NULL,price bigint NOT NULL CHECK(price>=0),target smallint NOT NULL,
balance_before bigint NOT NULL,balance_after bigint NOT NULL CHECK(balance_after>=0),purchased_at timestamptz NOT NULL,
CHECK(balance_before-balance_after=price));");}}
        private static ushort[] PeakSkillIds(byte target){switch(target/10){
            case 1:return new ushort[]{1110, 1015, 1115, 1270, 1190, 3050, 10315};
            case 2:return new ushort[]{1020, 1025, 1040, 1050, 1051, 1260, 10470, 3060, 10311};
            case 4:return new ushort[]{8000, 8001, 8002, 8003, 8030, 9000, 10313};
            case 5:return new ushort[]{6000, 6001, 6002, 6003, 6004, 6010, 6011};
            case 6:return new ushort[]{10490, 10390, 10415, 10381, 10400, 10425, 10430, 10395, 10410, 10420, 10421, 10422, 10423, 10424, 10405};
            case 7:return new ushort[]{11030, 11040, 11050, 11060, 11070, 11100, 11110, 11120, 11130, 11140, 11170, 11180, 11230, 3050};
            case 8:return new ushort[]{1020, 1025, 1040, 1050, 1051, 1260, 10470, 3060, 10311};
            case 13:return new ushort[]{1000, 1010, 1050, 1055, 1075, 1085, 1090, 1095, 1100, 1170, 1175, 1195, 4000, 4010, 4020, 4050, 4060, 4070, 10309, 30000, 3090, 5001};
            case 14:return new ushort[]{1000, 1002, 1005, 1010, 1120, 1125, 1150, 1160, 1165, 1180, 5001, 10309, 10310, 3080};
            case 16:return new ushort[]{};
            default:return new ushort[0];}}
        private static ushort[] PeakProfIds(byte target){switch(target/10){
            case 1:return new ushort[]{410,420};
            case 2:return new ushort[]{480,900};
            case 4:return new ushort[]{500};
            case 5:return new ushort[]{601};
            case 6:return new ushort[]{610};
            case 7:return new ushort[]{611,612};
            case 8:return new ushort[]{624};
            case 13:return new ushort[]{421};
            case 14:return new ushort[]{561};
            case 16:return new ushort[]{626};
            default:return new ushort[0];}}
        private static bool PreparePeak(GameState p,byte target){
            if(!MountReady(p) || !CharacterProgression.Managed(p) || p.Spells==null || p.Proficiencies==null
                || (NeedsBranch(p.Entity.Class)?target!=135 && target!=145:target!=HighestClass(p.Entity.Class)))return false;
            lock(p.ItemSyncRoot)try{
                PeakOffers.Remove(p);var offer=new PeakOffer{Target=target,Price=PeakPrice,Cp=p.Entity.ConquerPoints,Until=MountClock().AddMinutes(1),Cache=FirstRebirth.Cache(p)};
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){SkillTable.LockOwner(c,t,p);FirstRebirth.AssertLoaded(c,t,p);
                    offer.Items=FirstRebirth.Rows(c,t,p,"items");offer.Skills=FirstRebirth.Rows(c,t,p,"skills");offer.Profs=FirstRebirth.Rows(c,t,p,"profs");t.Commit();}}
                PeakOffers.Add(p,offer);return true;
            }catch(Exception ex){Q.Fail(p,"peak-offer",ex);return false;}
        }
        private static bool PeakReady(GameState p,PeakOffer o){return MountReady(p) && MountClock()<o.Until && PeakPrice==o.Price && p.Entity.ConquerPoints==o.Cp && FirstRebirth.Cache(p)==o.Cache;}
        private static void ApplyPeak(GameState p,Npcs dialog){
            if(!Q.Player(p) || !SessionInstall.TryEnter(p.Entity.UID))return;
            try{lock(p.ItemSyncRoot){
                PeakOffer offer;if(!PeakOffers.TryGetValue(p,out offer))return;PeakOffers.Remove(p);
                if(!PeakReady(p,offer))return;if(p.Entity.ConquerPoints<offer.Price){dialog.Text("Bring the quoted CPs first.");dialog.Option("I see.",255);dialog.Send();return;}
                var values=(long[])CharacterProgression.Capture(p).Values.Clone();bool raised=values[0]<PeakLevel;byte previousClass=(byte)values[7];values[7]=offer.Target;
                if(raised){int start=(int)values[0];if(values[8]==0 && start<120){var stats=DataHolder.StatsFor(offer.Target,120);CharacterProgression.AssignBaseStats(values,stats,previousClass,(byte)start);start=120;}
                    values[6]=checked((ushort)(values[6]+3*(PeakLevel-start)));values[0]=PeakLevel;values[1]=0;}
                var profile=new CharacterProgression.Profile(values);var skills=new List<Spell>();
                foreach(ushort id in PeakSkillIds(offer.Target).Concat(new ushort[]{9876}).Distinct()){
                    if(!SpellTable.SpellInformations.ContainsKey(id))continue;byte level=SpellTable.SpellInformations[id].Keys.Max();Revora.Interfaces.ISkill old;p.Spells.TryGetValue(id,out old);if(old!=null && old.Level>=level)continue;
                    var skill=new Spell(true){ID=id,Level=level,Experience=0,Available=old!=null && old.Available};
                    if(old!=null){skill.PreviousLevel=old.PreviousLevel;skill.Souls=old.Souls;skill.LevelHu2=old.LevelHu2;}
                    skills.Add(skill);
                }
                var profs=new List<Proficiency>();foreach(ushort id in PeakProfIds(offer.Target)){Revora.Interfaces.IProf old;p.Proficiencies.TryGetValue(id,out old);if(old!=null && old.Level>=20)continue;profs.Add(new Proficiency(true){ID=id,Level=20,PreviousLevel=old==null?(byte)0:old.PreviousLevel,Experience=0,Available=old!=null && old.Available});}
                var weapons=new[]{p.Equipment.TryGetItem(ConquerItem.RightWeapon),p.Equipment.TryGetItem(ConquerItem.LeftWeapon)}.Where(i=>i!=null && !ItemHandler.IsArrow(i.ID) && i.Plus<12).ToArray();
                if(profile.Values.SequenceEqual(CharacterProgression.Capture(p).Values) && skills.Count==0 && profs.Count==0 && weapons.Length==0){dialog.Text("Your training is already complete. I will not charge you again.");dialog.Option("Thank you.",255);dialog.Send();return;}
                uint after=offer.Cp-offer.Price;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){
                    SkillTable.LockOwner(c,t,p);if(!PeakReady(p,offer) || FirstRebirth.Rows(c,t,p,"items")!=offer.Items || FirstRebirth.Rows(c,t,p,"skills")!=offer.Skills || FirstRebirth.Rows(c,t,p,"profs")!=offer.Profs)throw new InvalidOperationException("Peak training preimage changed.");
                    CharacterProgression.Persist(c,t,p,profile);
                    if(Q.Execute(c,t,"UPDATE entities SET conquerpoints=@after WHERE uid=@uid AND conquerpoints=@before","after",(long)after,"uid",(long)p.Entity.UID,"before",(long)offer.Cp)!=1)throw new InvalidOperationException("Peak payment changed.");
                    foreach(var skill in skills)SkillTable.Persist(c,t,p,skill);
                    foreach(var prof in profs){string sql=prof.Available?"UPDATE profs SET level=20,experience=0,previouslevel=@previous WHERE entityid=@uid AND id=@id":"INSERT INTO profs(entityid,id,level,experience,previouslevel) VALUES(@uid,@id,20,0,@previous)";if(Q.Execute(c,t,sql,"uid",(long)p.Entity.UID,"id",(int)prof.ID,"previous",(int)prof.PreviousLevel)!=1)throw new InvalidOperationException("Peak proficiency changed.");}
                    foreach(var item in weapons)if(Q.Execute(c,t,"UPDATE items SET plus=12 WHERE uid=@item AND entityid=@uid AND id=@type AND position=@slot AND warehouse=0 AND plus=@before","item",(long)item.UID,"uid",(long)p.Entity.UID,"type",(int)item.ID,"slot",(int)item.Position,"before",(int)item.Plus)!=1)throw new InvalidOperationException("Peak weapon changed.");
                    if(Q.Execute(c,t,"INSERT INTO revora_peak_purchases(id,uid,price,target,balance_before,balance_after,purchased_at) VALUES(@id,@uid,@price,@target,@before,@after,@now)","id",offer.Id,"uid",(long)p.Entity.UID,"price",(long)offer.Price,"target",(int)offer.Target,"before",(long)offer.Cp,"after",(long)after,"now",MountClock())!=1)throw new InvalidOperationException("Peak receipt was not saved.");
                    if(!PeakReady(p,offer))throw new InvalidOperationException("Peak training context changed.");t.Commit();
                }}
                p.Entity.ApplyCommittedConquerPoints(after);CharacterProgression.ApplyCommitted(p,profile);
                foreach(var skill in skills){Revora.Interfaces.ISkill existing;if(p.Spells.TryGetValue(skill.ID,out existing)){existing.Level=skill.Level;existing.Experience=skill.Experience;existing.Available=true;if(skill.ID!=3060)existing.Send(p);}else{skill.Available=true;p.Spells[skill.ID]=skill;if(skill.ID!=3060)skill.Send(p);}}
                foreach(var prof in profs){prof.Available=true;p.Proficiencies[prof.ID]=prof;prof.Send(p);}
                foreach(var item in weapons){item.Plus=12;item.Mode=Enums.ItemMode.Update;item.Send(p);item.Mode=Enums.ItemMode.Default;}
                ClearPending(p);p.LoadItemStats();p.CalculateStatBonus();p.CalculateHPBonus();p.GemAlgorithm();if(raised)p.Entity.Hitpoints=p.Entity.MaxHitpoints;p.Equipment.UpdateEntityPacket();Q.Refresh(p);
                dialog.Text("Your training is complete. Level 140, "+ClassTitle(p.Entity.Class)+", class arts and composed weapons.");dialog.Option("Thank you.",255);dialog.Send();
            }}catch(Exception ex){Q.Fail(p,"peak-purchase",ex);}finally{SessionInstall.Exit(p.Entity.UID);}
        }
    }
}
