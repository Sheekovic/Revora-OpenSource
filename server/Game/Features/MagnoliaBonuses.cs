using System;
using System.Linq;
using Npgsql;
using Revora.Client;
using Revora.Database;
using Revora.Network.GamePackets;
using Q=Revora.Game.Features.QuestInventory;

namespace Revora.Game.Features
{
    internal static partial class MagnoliaQuest
    {
        // Owner-approved 20%; equal slots are Revora policy, not retail odds.
        internal static Func<int,int> BonusRoll=SeedRandom;
        private static readonly uint[][] BonusPools={new uint[0],
            new uint[]{1088001,700001,700011,700021,700031,700041,700051,700061,700071,700101,700121},
            new uint[]{723017,753003,754001,756001,723903},
            new uint[]{720128,723714,753009,754003,756003,700003,700013,700023,700033,700043,700053,700063,700073,700103,700123},
            new uint[]{1088000,720027,720578,721259,754009,756009}};
        private static uint[] BonusPool(int grade,int version) {
            // Reuse the existing Moon Box quest's promotion-compatible identity.
            if(version>=4 && grade==3)return BonusPools[grade].Concat(new[]{721090u,721269u}).ToArray();
            if(version>=4 && grade==4)return BonusPools[grade].Concat(new[]{710212u,720957u}).ToArray();
            if(version>=3 && grade==4)return BonusPools[grade].Concat(new[]{710212u}).ToArray();
            return version>=2 && grade==3?BonusPools[grade].Concat(new[]{721090u}).ToArray():BonusPools[grade];
        }
        private static void InitializeBonuses(NpgsqlConnection c) {
            foreach(uint id in BonusPools.SelectMany(pool=>pool))
                if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Magnolia bonus catalog missing.");
            if(!ConquerItemInformation.BaseInformations.ContainsKey(721090))throw new InvalidOperationException("Magnolia Moon Box missing.");
            if(!ConquerItemInformation.BaseInformations.ContainsKey(710212))throw new InvalidOperationException("Magnolia Lottery Ticket missing.");
            Q.Execute(c,null,@"ALTER TABLE revora_magnolia_runs ADD COLUMN IF NOT EXISTS bonus_version integer NOT NULL DEFAULT 0 CHECK(bonus_version IN(0,1));
CREATE TABLE IF NOT EXISTS revora_magnolia_bonuses(
uid bigint NOT NULL,day varchar(8) NOT NULL,grade integer NOT NULL CHECK(grade BETWEEN 0 AND 4),
roll integer NOT NULL CHECK(roll BETWEEN -1 AND 99),item_type bigint NOT NULL,item_uids bigint[] NOT NULL,
PRIMARY KEY(uid,day),FOREIGN KEY(uid,day) REFERENCES revora_magnolia_runs(uid,day),
CHECK((item_type=0 AND cardinality(item_uids)=0) OR (item_type>0 AND cardinality(item_uids) BETWEEN 1 AND 5)),
CHECK((grade=0 AND roll=-1 AND item_type=0) OR (grade>0 AND ((roll BETWEEN 0 AND 19 AND item_type>0) OR (roll BETWEEN 20 AND 99 AND item_type=0)))));");
            foreach(uint id in new uint[]{721269,720957})if(!ConquerItemInformation.BaseInformations.ContainsKey(id))throw new InvalidOperationException("Magnolia custom pack missing.");
            Q.Execute(c,null,@"ALTER TABLE revora_magnolia_runs DROP CONSTRAINT IF EXISTS revora_magnolia_runs_bonus_version_check;
ALTER TABLE revora_magnolia_runs ADD CONSTRAINT revora_magnolia_runs_bonus_version_check CHECK(bonus_version IN(0,1,2,3,4));");
        }
        private static int BonusDraw(int max) {
            int roll=BonusRoll(max);if(roll<0 || roll>=max)throw new InvalidOperationException("Magnolia bonus draw outside range.");return roll;
        }
        private static int BonusCapacity(Run run) {return run.BonusVersion==0 || run.Type==729306?0:run.Type==729308?5:1;}
        private static ConquerItem[] PersistBonus(NpgsqlConnection c,NpgsqlTransaction t,GameState p,Run run,bool bound) {
            if(run.BonusVersion==0)return new ConquerItem[0];
            int grade=checked((int)(run.Type-729306)),roll=grade==0?-1:BonusDraw(100);
            var pool=BonusPool(grade,run.BonusVersion);
            uint id=roll>=0 && roll<20?pool[BonusDraw(pool.Length)]:0;
            var items=id==0?new ConquerItem[0]:Enumerable.Range(0,id==723903?5:1).Select(_=>Q.New(id,bound)).ToArray();
            foreach(var item in items)InsertItem(c,t,p,item);
            if(Q.Execute(c,t,@"INSERT INTO revora_magnolia_bonuses(uid,day,grade,roll,item_type,item_uids)
VALUES(@uid,@day,@grade,@roll,@type,@items)","uid",(long)p.Entity.UID,"day",run.Day,"grade",grade,"roll",roll,
                "type",(long)id,"items",items.Select(item=>(long)item.UID).ToArray())!=1)throw new InvalidOperationException("Magnolia bonus receipt was not saved.");
            return items;
        }
    }
}
