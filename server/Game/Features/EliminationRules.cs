using System;

namespace Revora.Game.Features
{
    // Recovered expansion guide plus installed item identities. These rules do
    // not authenticate a run, create monsters, debit items or award progress.
    internal static class EliminationRules
    {
        internal const uint BeautyHerb=711042,FirePill=711041,FancySpice=723019;
        internal const int HerbsRequired=12,PillsRequired=10,ExperienceBalls=1;
        internal const ushort StudyPoints=50,Forest=1011,GeorgiaX=423,GeorgiaY=55,SpiceX=311,SpiceY=256;
        internal const uint Georgia=300020,FirstHerb=300021,Landy=300033;
        internal sealed class HerbSite {
            internal readonly ushort X,Y;
            internal HerbSite(ushort x,ushort y){X=x;Y=y;}
        }
        // Revora placements: the recovered guide does not publish herb sites.
        // Keep private so a caller cannot change the authoritative gathering sites.
        private static readonly HerbSite[] Herbs={
            new HerbSite(399,65),new HerbSite(417,70),new HerbSite(433,70),
            new HerbSite(439,65),new HerbSite(405,85),new HerbSite(419,87),
            new HerbSite(433,87),new HerbSite(447,85),new HerbSite(407,105),
            new HerbSite(420,108),new HerbSite(435,106),new HerbSite(449,103)
        };
        internal static HerbSite Herb(uint npc){
            return npc>=FirstHerb && npc<FirstHerb+Herbs.Length ? Herbs[npc-FirstHerb] : null;
        }
        // The caller must authenticate the player and lock the owned daily run.
        // A bit records each distinct plant; repeating one plant cannot yield 12 herbs.
        internal static bool TryGather(uint npc,ushort map,int x,int y,ushort gathered,out ushort next){
            next=gathered;
            var site=Herb(npc);
            if(site==null || map!=Forest || (gathered & ~4095)!=0 ||
                Math.Max(Math.Abs((long)x-site.X),Math.Abs((long)y-site.Y))>6)return false;
            var bit=1<<(int)(npc-FirstHerb);
            if((gathered & bit)!=0)return false;
            next=(ushort)(gathered | bit);return true;
        }
        // Revora den adaptation: six distinct guards in the installed mine scene.
        // The recovered guide gives neither a lackey count nor a usable map binding.
        internal const ushort DenBase=1021,DenLandingX=32,DenLandingY=40,DenExitX=30,DenExitY=40,DenLeaderX=60,DenLeaderY=75;
        internal const byte DenLackeys=6,DenAllLackeys=63;
        private static readonly HerbSite[] DenSites={new HerbSite(30,30),new HerbSite(40,40),new HerbSite(50,50),
            new HerbSite(60,60),new HerbSite(70,70),new HerbSite(70,50)};
        internal static HerbSite DenLackey(int slot){return slot>=0 && slot<DenLackeys?DenSites[slot]:null;}
        // World/store callers must prove the run, current scene generation and
        // canonical slain actor before committing this distinct-slot transition.
        internal static bool TryDenLackey(byte defeated,int slot,out byte next){
            next=defeated;if((defeated & ~DenAllLackeys)!=0 || slot<0 || slot>=DenLackeys)return false;
            int bit=1<<slot;if((defeated & bit)!=0)return false;next=(byte)(defeated|bit);return true;
        }
        internal static bool DenLeaderReady(byte defeated){return defeated==DenAllLackeys;}
        internal static bool Eligible(byte level,byte rebirths){return level>=70 && rebirths>=1 && rebirths<=2;}
        // Six-cell interaction reach is Revora's service policy, not a recovered
        // retail range. The future handler must check owned spice and terrain.
        internal static bool AtSpiceSite(ushort map,int x,int y) {
            return map==Forest && Math.Max(Math.Abs((long)x-SpiceX),Math.Abs((long)y-SpiceY))<=6;
        }
    }
}
