using System;

namespace Revora.Game.Features
{
    // Staged rules only: no dispatcher, rewards or achievement are enabled here.
    // Installed identities and explicit Revora choices: ANCIENT_DEVIL_RESEARCH.md.
    internal static class AncientDevilRules
    {
        internal const int RequiredAmulets = 9;
        private static readonly byte[] professions = {20,10,140,130,40,50,60,70,80,160};
        private static readonly uint[] certificates = {710016,710017,710018,710019,710020,710634,711335,711599,3004946,3301711};
        private static readonly uint[] amulets = {710011,710012,710013,710014,710015,710635,711336,711600,3004947,3301712};
        private static readonly uint[] trialFamilies = {7001,7002,7004,7005,7003};
        private static readonly string[] schools={"Warrior","Trojan","Fire Taoist","Water Taoist","Archer","Ninja","Monk","Pirate","Dragon Warrior","Windwalker"};
        internal static string AmuletName(uint type) {int index=Array.IndexOf(amulets,type);return index<0?null:schools[index];}

        private static int School(byte profession)
        {
            for (int i=0;i<professions.Length;i++)
                if (profession>=professions[i] && profession<=professions[i]+5) return i;
            return -1;
        }

        internal sealed class Trial
        {
            internal readonly byte Profession;
            internal readonly uint Guard, Family, Certificate, Amulet;
            internal Trial(byte profession,uint guard,uint family,uint certificate,uint amulet)
            { Profession=profession;Guard=guard;Family=family;Certificate=certificate;Amulet=amulet; }
        }

        internal static bool TryTrial(byte profession,ushort level,uint guard,out Trial trial)
        {
            trial=null;
            int school=School(profession);
            if (level<50 || school<0 || guard<300011 || guard>300015) return false;
            int guardIndex=(int)(guard-300011);
            if (school<5 && school!=guardIndex) return false;
            // Reward follows the participant's class, never the chosen guard.
            // Windwalker any-guard admission is an explicit Revora extension.
            trial=new Trial(profession,guard,trialFamilies[guardIndex],certificates[school],amulets[school]);
            return true;
        }

        internal static bool TryExchange(byte profession,uint certificate,out uint amulet)
        {
            amulet=0;
            int school=School(profession);
            if (school<0 || certificates[school]!=certificate) return false;
            amulet=amulets[school];return true;
        }

        internal sealed class Offering
        {
            internal readonly uint Uid, Type;
            internal Offering(uint uid,uint type) { Uid=uid;Type=type; }
        }

        // Checks an exact selection, not the whole inventory. The caller must
        // lock and revalidate ownership, item state, provenance and eligibility
        // inside the summon transaction before consuming anything.
        internal static bool ValidOffering(Offering[] selected)
        {
            if (selected==null || selected.Length!=RequiredAmulets) return false;
            int mask=0;
            for (int i=0;i<selected.Length;i++)
            {
                var item=selected[i];
                if (item==null || item.Uid==0) return false;
                int school=Array.IndexOf(amulets,item.Type);
                if (school<0 || (mask & (1<<school))!=0) return false;
                for (int j=0;j<i;j++) if (selected[j].Uid==item.Uid) return false;
                mask|=1<<school;
            }
            return true;
        }
    }
}
