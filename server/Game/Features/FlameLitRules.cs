using System;

namespace Revora.Game.Features
{
    // Installed rune/site identities. Timing is relative to a confirmed Guild War;
    // the future coordinator must independently prove its active battle ownership.
    internal static class FlameLitRules
    {
        internal const uint FirstRune=729960,DragonRune=729970,Taoist=4452;
        private static readonly ushort[] Maps={1002,1038,1038,1038,1038,1038,1038,1038,1002,1038};
        private static readonly ushort[] Xs={366,317,236,194,115,316,136,38,370,62};
        private static readonly ushort[] Ys={346,270,291,168,53,378,182,94,342,59};
        internal sealed class Step {
            internal readonly uint Npc,Rune,Next;internal readonly ushort Map,X,Y;internal readonly int Completed;
            internal Step(int index){Completed=index;Npc=(uint)(4453+index);Rune=FirstRune+(uint)index;Next=Rune+1;Map=Maps[index];X=Xs[index];Y=Ys[index];}
        }
        internal static bool TryStep(uint rune,out Step step) {
            step=null;if(rune<FirstRune || rune>=DragonRune)return false;step=new Step((int)(rune-FirstRune));return true;
        }
        internal static bool CanLight(uint rune,ushort map,int x,int y,DateTime utc,DateTime starts,DateTime ends,bool battleActive) {
            Step step;
            if(!battleActive || utc.Kind!=DateTimeKind.Utc || starts.Kind!=DateTimeKind.Utc || ends.Kind!=DateTimeKind.Utc
                || ends<=starts || !TryStep(rune,out step) || map!=step.Map
                || Math.Max(Math.Abs((long)x-step.X),Math.Abs((long)y-step.Y))>6)return false;
            var duration=ends-starts;
            DateTime opens=ends-TimeSpan.FromTicks(Math.Min(duration.Ticks,TimeSpan.FromHours(2).Ticks));
            DateTime final=ends-TimeSpan.FromTicks(Math.Min(duration.Ticks,TimeSpan.FromMinutes(30).Ticks));
            return utc>=opens && utc<ends && (step.Completed!=9 || utc>=final);
        }
        // Preserve the existing Revora EXP-ball-unit policy, not claimed retail odds.
        internal static int RewardUnits(int completed) {
            if(completed<0 || completed>10)throw new ArgumentOutOfRangeException("completed");
            return completed==10?12:completed;
        }
    }
}
