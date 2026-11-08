using System;
using System.Text;
using Revora.Game.Features;
using System.Collections.Generic;
using Revora.Game.ConquerStructures;

namespace Revora.Database
{
    public unsafe class ArsenalTable
    {
        public static void UpdateArsenal(ulong Donation, Arsenal_ID id, uint GID)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("pt_arsenal")
                .Set("arsenal_donation", Donation.ToString())
                .Where("arsenal_id", (byte)id)
                .Execute();
        }
        public static void CreateArsenal(uint s_id, Arsenal_ID id)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("pt_arsenal")
                    .Insert("syn_id", s_id)
                    .Insert("arsenal_id", (byte)id)
                    .Insert("arsenal_unlocked", 1)
                    .Insert("arsenal_enhance", 0)
                    .Insert("syn_id_bp", 0)
                    .Execute();
        }
        public static void UpdateEch(Arsenal_ID id, uint ech, uint bp)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("pt_arsenal")
                .Set("arsenal_enhance", ech)
                .Set("syn_id_bp", bp)
                .Where("arsenal_id", (byte)id)
                .Execute();
        }  
        public static void CreateArsenalItem(uint uid, string name, uint i_uid, uint sid, Arsenal_ID aid, uint donation)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("pt_arsenal_inscribed")
                 .Insert("uid", uid)
                 .Insert("name", name)
                 .Insert("iten_id", i_uid)
                 .Insert("iten_atype", (byte)aid)
                 .Insert("syn_id", sid)
                 .Insert("arsenal_donation", donation)
                 .Execute();
        }
        public static void DeleteArsenalItem(uint uid, uint i_uid, Arsenal_ID aid)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE))
                cmd.Delete("pt_arsenal_inscribed", "uid", uid).And("iten_id", i_uid).Execute();
        }
        public static void UpdateArsenalItem(uint i_uid, Arsenal_ID aid)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE))
                cmd.Update("pt_arsenal_inscribed")
                    .Set("iten_atype", (byte)aid)
                    .Where("iten_id", i_uid).Execute();
        }
        public static void DeleteArsenal(uint s_id, Arsenal_ID id)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.DELETE))
                cmd.Delete("pt_arsenal", "syn_id", s_id).And("arsenal_id", (byte)id).Execute();
        }
        public static void LoadArsenal(Game.ConquerStructures.Society.Guild Syn)
        {
            Syn.Arsenal = new Arsenal((ushort)Syn.ID);
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("pt_arsenal").Where("syn_id", Syn.ID))
            using (var Reader = new MySqlReader(cmd))
            {
                while (Reader.Read())
                {
                    Game.Features.Arsenal_State Arsenal = new Game.Features.Arsenal_State();
                    Arsenal.ID = (Game.Features.Arsenal_ID)Reader.ReadByte("arsenal_id");
                    Arsenal.Unlocked = (Reader.ReadByte("arsenal_unlocked") == 1);
                    Arsenal.Enhancement = Reader.ReadUInt32("arsenal_enhance");
                    Arsenal.ars_bp = Reader.ReadUInt32("syn_id_bp");
                    long expires = Reader.ReadNullableInt64Strict("enhancement_expires_utc_ticks") ?? throw new InvalidOperationException("Arsenal deadline is missing.");
                    // Zero denotes legacy purchases whose original deadline was never saved.
                    // Do not silently renew them on every load.
                    Arsenal.EnhancementExpDate = expires == 0 ? default(DateTime) : new DateTime(expires, DateTimeKind.Utc);
                    if (!Syn.Arsenal.Arsenals.ContainsKey(Arsenal.ID))
                        Syn.Arsenal.Arsenals.Add(Arsenal.ID, Arsenal);
                }
            }
        }  
        public static void LoadArsenal2(Game.ConquerStructures.Society.Guild Syn)
        {
            if (Syn.Arsenal.Arsenals.Count > 0)
            {
                using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("pt_arsenal_inscribed").Where("syn_id", Syn.ID))
                using (var Reader = new MySqlReader(cmd))
                {
                    while (Reader.Read())
                    {
                        uint OWNER = Reader.ReadUInt32("uid");
                        string Name = Reader.ReadString("name");
                        uint UID = Reader.ReadUInt32("iten_id");
                        uint arsenal_donation = Reader.ReadUInt32("arsenal_donation");
                        Game.Features.Arsenal_ID ID = (Game.Features.Arsenal_ID)Reader.ReadByte("iten_atype");
                        if (Syn.Arsenal.Arsenals.ContainsKey(ID))
                        {
                            if (!Syn.Arsenal.Arsenals[ID].Inscribed.ContainsKey(UID))
                            {
                                Syn.Arsenal.Arsenals[ID].Inscribed.Add(UID, new Game.Features.Arsenal_Client() { UID = OWNER, Name = Name, iUID = UID, donation = arsenal_donation });
                            }
                        }
                    }
                }
            }
            foreach (Game.Features.Arsenal_State astate in Syn.Arsenal.Arsenals.Values)
            {
                foreach (Game.Features.Arsenal_Client ac in astate.Inscribed.Values)
                {
                    if (ac.Item == null)
                    {
                        ac.Item = ConquerItemTable.LoadItem(ac.iUID);
                    }
                }
            }
            GuildArsenalPurchases.Rebuild(Syn);
        }
    }
}