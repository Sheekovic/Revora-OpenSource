using System;

namespace Revora.Game.Features
{
    // Confirmed quest identities; not yet connected to live item/NPC dispatch.
    internal static class PirateEpicRules
    {
        internal const uint Heart=3307449,Water=3307450,Bottle=3307448;
        internal const uint FirstMap=3307443,LastMap=3307447;
        internal const uint PaidMapCp=27;
        internal const int FlowersPerColor=3;
        // Explicit Revora exchange price, not a verified original price.
        internal const int WaterPerScroll=10;
        internal static uint Scroll(byte choice) {
            if(choice<1 || choice>3)throw new ArgumentOutOfRangeException("choice");return 3307431u+choice;
        }
        internal static ushort ScrollSpell(uint scroll) {
            switch(scroll){case 3307432:return 14680;case 3307433:return 14720;case 3307434:return 14710;default:return 0;}
        }


        internal static bool Eligible(byte profession,byte reborn,byte level) {
            return profession>=70 && profession<=75 && reborn==2 && level>=70;
        }

        internal static uint EpicWeapon(uint id,Func<uint,bool> catalog) {
            if(catalog==null)throw new ArgumentNullException("catalog");
            uint family=id/1000,suffix=id%1000;
            if((family!=611 && family!=612) || suffix>439 || !catalog(id))return 0;
            uint target=(family==611?671000u:670000u)+suffix;
            return catalog(target)?target:0;
        }

        // Return a slot, not an item: the transactional caller must revalidate
        // ownership, durable preimages, class/equip rules and both active slots.
        // A missing mapping never silently skips an ordinary priority rapier.
        internal static byte ConversionSlot(uint right,uint left,Func<uint,bool> catalog) {
            if(catalog==null)throw new ArgumentNullException("catalog");
            if(right/1000==611)return EpicWeapon(right,catalog)!=0?(byte)4:(byte)0;
            if(right/1000==671 && right%1000<=439 && catalog(right)
                && left/1000==612 && EpicWeapon(left,catalog)!=0)return 5;
            return 0;
        }

        internal static int MapStars(uint item) {
            return item>=FirstMap && item<=LastMap?(int)(item-FirstMap+1):0;
        }

        // These are map-reward chances, not guaranteed upgrades: the published
        // output ranges can include the same star. Revora uses uniform selection within each allowed range.
        internal static int MapRewardPercent(int stars) {
            switch(stars) {
                case 1:return 30;
                case 2:return 25;
                case 3:case 4:return 20;
                default:throw new ArgumentOutOfRangeException("stars");
            }
        }
        internal static uint[] SearchRewards(uint input,Func<int,int> draw) {
            if(draw==null)throw new ArgumentNullException("draw");
            if(input==Bottle)return new[]{FirstMap+(uint)CheckedDraw(draw,3)};
            int stars=MapStars(input);if(stars==0)throw new ArgumentOutOfRangeException("input");
            // One bound Water is Revora's explicit consolation/final bonus quantity.
            if(stars==5)return new[]{Heart,Water};
            if(CheckedDraw(draw,100)>=MapRewardPercent(stars))return new[]{Water};
            int maximum=stars==1?4:5;
            return new[]{FirstMap+(uint)(stars-1+CheckedDraw(draw,maximum-stars+1))};
        }
        private static int CheckedDraw(Func<int,int> draw,int maximum) {
            int value=draw(maximum);if(value<0 || value>=maximum)throw new InvalidOperationException("Pirate draw outside its bound.");return value;
        }
    }
}
