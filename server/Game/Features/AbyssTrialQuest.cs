using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;
using R=Revora.Game.Features.AbyssTrialRules;
using S=Revora.Game.Features.AbyssTrialStore;
using M=Revora.Game.Features.AbyssTrialMaterials;
using W=Revora.Game.Features.AbyssTrialWorld;
using J=Revora.Game.Features.AbyssTrialJournal;

namespace Revora.Game.Features
{
    internal static class AbyssTrialQuest
    {
        private enum Kind { Entry,Exchange,TurnIn,Summon }
        private sealed class Offer { internal Kind Kind; internal uint Npc,Product; internal Guid Run; internal int Version; internal M.Plan Plan; internal DateTime Until; }
        private static readonly ConditionalWeakTable<GameState,Offer> offers=new ConditionalWeakTable<GameState,Offer>();
        internal static Func<DateTime> Clock=()=>DateTime.UtcNow;
        internal static bool Enabled { get; private set; }
        internal static bool StartupRequested(string setting) {
            if (string.IsNullOrWhiteSpace(setting)) return false;
            setting=setting.Trim();
            if (setting=="1" || string.Equals(setting,"true",StringComparison.OrdinalIgnoreCase)) return true;
            if (setting=="0" || string.Equals(setting,"false",StringComparison.OrdinalIgnoreCase)) return false;
            throw new ArgumentException("Invalid Abyss startup setting.");
        }
        internal static void ConfigureStartup(string setting) {
            if (StartupRequested(setting)) Initialize();
            else if (Enabled) throw new InvalidOperationException("Abyss cannot be disabled during a running process.");
        }
        internal static bool Handles(uint npc) { return Enabled && (npc==HeavenlyStoneCrafting.Sereth || Sites.Any(s=>s[0]==npc)); }
        // Installed map-1700 talkers. Stanley's native merchant remains separate.
        internal static readonly uint[][] Sites={
            new uint[]{3601,607,637},new uint[]{3602,609,633},new uint[]{3603,611,638},new uint[]{3604,601,641},
            new uint[]{3605,444,878},new uint[]{3606,883,447},new uint[]{3607,920,929},new uint[]{3608,331,340},new uint[]{3609,715,744},
            new uint[]{3615,271,721},new uint[]{3616,338,580},new uint[]{3617,286,722},new uint[]{3618,339,592},
            new uint[]{3619,411,596},new uint[]{3620,447,757},new uint[]{3621,525,687},new uint[]{3622,660,441},
            new uint[]{3626,731,478},new uint[]{3627,695,557},new uint[]{3628,651,341},new uint[]{3629,731,313},
            new uint[]{3630,680,919},new uint[]{3631,591,974},new uint[]{3632,790,979},new uint[]{3633,635,340},
            new uint[]{3634,729,298},new uint[]{3635,956,789},new uint[]{3636,923,724},new uint[]{3637,985,593}};
        internal static void Initialize() {
            if(Enabled)return;
            var map=Kernel.Maps[R.Map];
            foreach(var site in Sites) {
                Revora.Interfaces.INpc npc;
                if(!map.Npcs.TryGetValue(site[0],out npc) || npc.MapID!=R.Map || npc.X!=site[1] || npc.Y!=site[2] || npc.Type!=Enums.NpcType.Talker)
                    throw new InvalidOperationException("Abyss keeper differs from the reviewed world.");
            }
            W.Initialize(); Enabled=true;
        }
        private static bool Near(GameState p,uint npc) {
            ushort map=npc==HeavenlyStoneCrafting.Sereth?(ushort)1002:R.Map;
            return Q.Near(p,npc,map) && p.Map.ID==map && p.Map.BaseID==map && p.Map.Npcs[npc].MapID==map && p.Entity.PokerTable==0;
        }
        private static S.Run Read(GameState p) { using(var c=DataHolder.MySqlConnection){c.Open();return S.Read(c,null,p.Entity.UID);} }
        private static S.Encounter Active(GameState p,S.Run r) { if(r==null)return null;using(var c=DataHolder.MySqlConnection){c.Open();return S.Active(c,null,r);} }
        private static uint Family(uint npc,S.Run r) {
            if(npc==3605)return 3632;if(npc==3606)return 3633;if(npc==3607)return 3634;if(npc==3609)return 3635;
            if(npc==3608)return r.Progress.Step==R.Stage.Satan?3644u+(uint)r.Progress.SatanForms:0;
            if(npc==3615 || npc==3616)return 3636;if(npc==3617 || npc==3618)return 3637;
            if(npc>=3619 && npc<=3621)return 3638;if(npc==3622 || npc==3626 || npc==3627)return 3639;
            if(npc==3628 || npc==3629)return 3640;if(npc>=3630 && npc<=3632)return 3641;
            if(npc==3633 || npc==3634)return 3642;if(npc>=3635 && npc<=3637)return 3643;return 0;
        }
        private static string Name(uint family) { return family>=3632 && family<=3646?new[]{"Hill Spirit","Swift Devil","Banshee","Cleansing Devil","Andrew","Peter","Philip","Timothy","Daphne","Victoria","Wayne","Theodore","Satan","Beast Satan","Fury Satan"}[family-3632]:"the spirits"; }
        private static bool Available(S.Run r,uint family) {
            return r!=null && r.Selected && !r.Consumed && (r.Progress.Step==R.Stage.Vigor && family>=3632 && family<=3635
                || r.Progress.Step==R.Stage.Spirits && r.Introduced && family!=0 && family==R.NextSpirit(r.Progress)
                || r.Progress.Step==R.Stage.Satan && family==3644u+r.Progress.SatanForms);
        }
        private static R.Material[] Costs(Kind kind,uint product) {
            if(kind==Kind.Exchange)return R.ExchangeCost(product);
            if(kind==Kind.TurnIn)return new[]{new R.Material(722731,3)};
            return product>=3632 && product<=3635 || product==3644?R.SummonCost(product):null;
        }
        private static string Offering(Kind kind,uint product) {
            if(kind==Kind.Exchange)return product==722721?"Three Ghost Horns become one Evil Tooth.":product==722728?"Three Feather Stones become one Immortal Stone.":"Three Vigor Fragments become one Impure Vigor.";
            if(kind==Kind.TurnIn)return "Offer three Pure Vigors and let Bryan judge your next trial.";
            if(product==3632)return "One Moss, one Dream Grass and one Soul Aroma will awaken the Hill Spirit.";
            if(product==3633)return "Bring one Moss, one Dream Grass, one Soul Aroma and one Evil Tooth to awaken the Swift Devil.";
            if(product==3634)return "One Moss, one Dream Grass, one Soul Aroma and one Immortal Stone will summon the Banshee.";
            if(product==3635)return "One Impure Vigor calls the Cleansing Devil.";
            if(product==3644)return "One Squama Bead breaks Satan's seal.";
            return "Face "+Name(product)+" and break this curse.";
        }
        private static void Prepare(GameState p,Npcs d,Kind kind,uint product) {
            offers.Remove(p); S.Run r; M.Plan plan=null; var costs=Costs(kind,product);
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                r=S.Require(c,t,p);
                if(kind==Kind.Summon && (!Available(r,product) || S.Active(c,t,r)!=null)
                    || kind!=Kind.Summon && r.Progress.Step!=R.Stage.Vigor) {d.Text("Another part of your trial awaits. Speak with me when it is done.");return;}
                if(costs!=null){plan=M.Select(p,costs);if(plan==null){d.Text(Offering(kind,product));return;}M.Bag(c,t,p);}
                if(!Near(p,p.ActiveNpc))throw new InvalidOperationException("Abyss offering moved.");t.Commit();
            }}
            offers.Add(p,new Offer{Kind=kind,Npc=p.ActiveNpc,Product=product,Run=r.Id,Version=r.Version,Plan=plan,Until=Clock().AddMinutes(2)});
            d.Text(Offering(kind,product));if(plan!=null && plan.Bound)d.Text("Bound offerings leave bound spoils.");d.Option("Make the offering.",20);
        }
        private static bool Unchanged(GameState p,M.Plan plan) {
            if(plan==null)return true;
            foreach(var part in plan.Parts) {ConquerItem item;if(!p.Inventory.TryGetItem(part.Uid,out item) || !ReferenceEquals(item,part.Item)
                || !M.Usable(item) || item.ID!=part.Type || item.StackSize!=part.Stack || item.Bound!=part.Bound)return false;}
            return true;
        }
        private static void Confirm(GameState p,Npcs d) {
            Offer offer;if(!offers.TryGetValue(p,out offer)) {d.Text("Speak with me before making an offering.");return;} offers.Remove(p);
            var r=Read(p);
            if(offer.Npc!=p.ActiveNpc || Clock()>=offer.Until || r==null || r.Id!=offer.Run || r.Version!=offer.Version || !Unchanged(p,offer.Plan)) {
                d.Text("Your offering has changed. Let us begin again.");return;
            }
            if(offer.Kind==Kind.Summon) {
                var actor=W.SummonOffered(p,offer.Version,offer.Product,offer.Plan,()=>Clock()<offer.Until && Near(p,offer.Npc));
                d.Text(actor==null?"The seal is crowded. Return shortly; any offering already made will wait.":Name(offer.Product)+" rises at ("+actor.Body.X+","+actor.Body.Y+").");
            } else {
                ConquerItem item=null;bool completed=false;
                using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                    if(offer.Kind==Kind.Exchange)item=S.Exchange(c,t,p,offer.Version,offer.Product,offer.Plan);
                    else if(offer.Kind==Kind.TurnIn)completed=S.TurnIn(c,t,p,offer.Version,p.ActiveNpc,offer.Plan);
                    if(!Near(p,offer.Npc) || Clock()>=offer.Until)throw new InvalidOperationException("Abyss offering context changed.");t.Commit();
                }}
                if(item!=null || completed)M.Apply(p,offer.Plan,item);
                d.Text(item!=null?"It is done. Guard this stone well.":completed?"Your offering is accepted. Speak with Bryan.":"Make room in your bag, or finish the spirit you have already called.");
            }
            J.Refresh(p);
        }
        private static void Claim(GameState p,Npcs d) {
            var r=Read(p);if(r==null || r.Consumed || !r.Selected){d.Text("Return to your Abyss trial before collecting its spoils.");return;}
            int count=0;while(count<40 && W.ClaimOne(p))count++;
            d.Text(count>0?"Your spoils are yours. Make room if more remain.":p.Inventory.Count>=40?"Make room for the spoils I am holding.":"I hold no spoils for you.");J.Refresh(p);
        }
        private static void Enter(GameState p,Npcs d) {
            Offer offer;if(!offers.TryGetValue(p,out offer) || offer.Kind!=Kind.Entry || offer.Npc!=p.ActiveNpc || Clock()>=offer.Until){offers.Remove(p);d.Text("Speak with me before entering the Abyss.");return;}
            offers.Remove(p);ushort x=603,y=641;
            if(!PotencyGuide.FindLanding(Kernel.Maps[R.Map],ref x,ref y)){d.Text("The way is closed for a moment.");return;}
            using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                S.Enroll(c,t,p);if(!Near(p,HeavenlyStoneCrafting.Sereth) || Clock()>=offer.Until)throw new InvalidOperationException("Abyss admission changed.");t.Commit();
            }}
            p.Entity.Teleport(R.Map,x,y);
            if(p.Entity.MapID!=R.Map)throw new InvalidOperationException("Abyss entry must be resumed.");
            J.Refresh(p);d.Text("Arthur awaits you beside the campfire.");
        }
        private static void Sereth(GameState p,NpcRequest request,Npcs d) {
            if(request.OptionID==181) {
                offers.Remove(p);offers.Add(p,new Offer{Kind=Kind.Entry,Npc=p.ActiveNpc,Until=Clock().AddMinutes(2)});
                d.Text("Arthur guards the path to a third life. Prove yourself in the Evil Abyss.");d.Option("Enter the Abyss.",182);
                if(J.Selected(p)){d.Option("Follow the Slayer Cup path.",183);d.Option("Claim my spoils.",184);}
            } else if(request.OptionID==182)Enter(p,d);
            else if(request.OptionID==183) {
                offers.Remove(p);var r=Read(p);
                if(r==null || !r.Selected)d.Text("The Cup's keepers already await you.");
                else if(Active(p,r)!=null)d.Text("Finish the spirit you called before taking another path.");
                else {using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()){S.SelectCup(c,t,p);if(!Near(p,p.ActiveNpc))throw new InvalidOperationException("Abyss route changed.");t.Commit();}}
                    J.Refresh(p);d.Text("Your Abyss trial will wait. The Cup's keepers remember you.");}
            } else if(request.OptionID==184){offers.Remove(p);Claim(p,d);}
        }
        private static void Keeper(GameState p,NpcRequest request,Npcs d,S.Run r) {
            uint npc=p.ActiveNpc;byte option=request.OptionID;
            if(option==9){offers.Remove(p);Claim(p,d);return;}
            if(option==20){Confirm(p,d);return;}
            if(npc==3601) {
                if(option>=1 && option<=3 && r.Progress.Step==R.Stage.Vigor)Prepare(p,d,Kind.Exchange,new uint[]{722721,722728,722730}[option-1]);
                else if(option==4 && r.Progress.Step==R.Stage.Vigor)Prepare(p,d,Kind.TurnIn,3601);
                else if(option==0) {
                    d.Text(r.Progress.Step==R.Stage.Vigor?"Bring me three Pure Vigors. I can bind the lesser stones for your hunt.":"Your offering is remembered. The other keepers will guide you.");
                    if(r.Progress.Step==R.Stage.Vigor){d.Option("Bind three Ghost Horns.",1);d.Option("Bind three Feather Stones.",2);d.Option("Bind three Vigor Fragments.",3);d.Option("Offer three Pure Vigors.",4);}
                }
            } else if(npc==3602 || npc==3603) {
                bool step=npc==3602?r.Progress.Step==R.Stage.Grievance:r.Progress.Step==R.Stage.Spirits;
                if(!step){d.Text(r.Progress.Step==R.Stage.Complete?"Your trial is complete. Return to Sereth.":"Follow the keeper who holds your trial.");return;}
                if(option==1 && !r.Introduced || option==2 && r.Introduced) {
                    using(var c=DataHolder.MySqlConnection){c.Open();using(var t=c.BeginTransaction()) {
                        if(option==1)S.Introduce(c,t,p,r.Version);else S.TurnIn(c,t,p,r.Version,npc,null);
                        if(!Near(p,npc))throw new InvalidOperationException("Abyss keeper moved.");t.Commit();
                    }}
                    J.Refresh(p);r=Read(p);step=npc==3602?r.Progress.Step==R.Stage.Grievance:r.Progress.Step==R.Stage.Spirits;
                    if(!step){d.Text(npc==3602?"The mountain is gone. Speak with Angela.":"The binding curse is broken. Take a Squama Bead to Satan's seal.");return;}
                }
                if(!r.Introduced){d.Text(npc==3602?"Slay the demons until their mountain of grievance is gone.":"Free Andrew, Peter, Philip, Timothy, Daphne, Victoria, Wayne and Theodore, in that order. Break all eight curses nine times.");d.Option("I will begin.",1);}
                else if(npc==3602){d.Text("You have cleared "+r.Progress.Meters.ToString("N0")+" of 40,000 meters of grievance.");if(r.Progress.Meters==40000)d.Option("The mountain is gone.",2);}
                else if(r.Progress.Spirits==72){d.Text("All nine bindings are broken.");d.Option("Open the final trial.",2);}
                else d.Text(Name(R.NextSpirit(r.Progress))+" is next. You have broken "+(r.Progress.Spirits/8)+" of nine bindings.");
            } else {
                uint family=Family(npc,r);var active=Active(p,r);
                if(option==2 && active!=null && active.Family==family) {offers.Remove(p);var actor=W.Summon(p,r.Version,family,null);
                    d.Text(actor==null?"The seal is crowded. Your offering will wait.":Name(family)+" waits at ("+actor.Body.X+","+actor.Body.Y+").");J.Refresh(p);}
                else if(option==1 && Available(r,family))Prepare(p,d,Kind.Summon,family);
                else if(option==0) {
                    if(active!=null && active.Family==family){d.Text("Your offering still binds this spirit.");d.Option("Resume my challenge.",2);}
                    else if(Available(r,family)){d.Text(Offering(Kind.Summon,family));d.Option("Prepare the offering.",1);}
                    else d.Text("This spirit is not your next trial.");
                }
            }
            d.Option("Claim my spoils.",9);
        }
        internal static bool HandleNpc(GameState p,NpcRequest request) {
            if(!Enabled || p==null || request==null || p.ItemSyncRoot==null || request.InteractType==102)return false;
            Offer held;bool goodbye=request.OptionID==255 && offers.TryGetValue(p,out held) && held.Npc==p.ActiveNpc;
            bool home=p.ActiveNpc==HeavenlyStoneCrafting.Sereth;
            bool ours=home && (request.OptionID>=181 && request.OptionID<=184 || goodbye)
                || p.Entity!=null && p.Entity.MapID==R.Map && Sites.Any(s=>s[0]==p.ActiveNpc) && (p.ActiveNpc==3604 || J.Selected(p));
            lock(p.ItemSyncRoot) {
                if(!ours){offers.Remove(p);return false;}
                if(request.NpcID!=0 && request.NpcID!=p.ActiveNpc || !Near(p,p.ActiveNpc)){offers.Remove(p);return true;}
                if(request.OptionID==255){offers.Remove(p);return true;}
                try {
                    if(request.OptionID==0)offers.Remove(p);var d=new Npcs(p);
                    if(p.ActiveNpc==3604) {
                        if(request.OptionID==1){offers.Remove(p);var npc=Kernel.Maps[1002].Npcs[HeavenlyStoneCrafting.Sereth];ushort x=(ushort)(npc.X+2),y=npc.Y;
                            if(!PotencyGuide.FindLanding(Kernel.Maps[1002],ref x,ref y))throw new InvalidOperationException("Abyss return landing blocked.");p.Entity.Teleport(1002,x,y);d.Text("May the keepers remember your courage.");}
                        else if(request.OptionID==2 && R.Eligible(p.Entity.Reborn,p.Entity.Level))Claim(p,d);
                        else {d.Text("The Abyss keeps its debts. I can gather your spoils or send you home.");d.Option("Return to Twin City.",1);d.Option("Claim my spoils.",2);}
                    } else if(!R.Eligible(p.Entity.Reborn,p.Entity.Level)){offers.Remove(p);d.Text("Return in your second life at level 120.");}
                    else if(home)Sereth(p,request,d);
                    else {var r=Read(p);if(r!=null && r.Selected && !r.Consumed)Keeper(p,request,d,r);}
                    d.Option("Farewell.",255);d.Send();
                } catch(Exception ex){offers.Remove(p);Q.Fail(p,"abyss-keeper",ex);}
                return true;
            }
        }
        internal static void OnLogin(GameState p) { if(Enabled)J.Refresh(p); }
    }
}
