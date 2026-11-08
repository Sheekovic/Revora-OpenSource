using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revora.Database
{
    public class NameChange
    {
        public static void UpdateNames()
        {
            Dictionary<String, NameChangeC> UPDATE = new Dictionary<string, NameChangeC>();
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("entities"))
            using (var r = new MySqlReader(cmd))
            {
                String newname = "", name = "";
                UInt32 uid = 0;
                while (r.Read())
                {
                    newname = r.ReadString("namechange");
                    name = r.ReadString("name");
                    if (newname != "")
                    {
                        uid = r.ReadUInt32("uid");

                        MySqlCommand cmdupdate = null;
                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("apprentice").Set("MentorName", newname).Where("MentorID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("apprentice").Set("ApprenticeName", newname).Where("ApprenticeID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("arena").Set("EntityName", newname).Where("EntityID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("claimitems").Set("OwnerName", newname).Where("OwnerUID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("claimitems").Set("GainerName", newname).Where("GainerUID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("detaineditems").Set("OwnerName", newname).Where("OwnerUID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("detaineditems").Set("GainerName", newname).Where("GainerUID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("enemy").Set("EnemyName", newname).Where("EnemyID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("friends").Set("FriendName", newname).Where("FriendID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("guilds").Set("Name", newname).Where("Name", name).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("guilds").Set("LeaderName", newname).Where("LeaderName", name).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("nobility").Set("EntityName", newname).Where("EntityUID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("partners").Set("PartnerName", newname).Where("PartnerID", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("chi").Set("name", newname).Where("uid", uid).Execute();

                        cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE);
                        cmdupdate.Update("teamarena").Set("EntityName", newname).Where("EntityID", uid).Execute();

                        if (!UPDATE.ContainsKey(name))
                            UPDATE.Add(name, new NameChangeC() { NewName = newname, OldName = name });
                    }
                }
            }
            if (UPDATE.Count > 0)
            {
                //Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(" [Names Changed]");
                //Console.ForegroundColor = ConsoleColor.Blue;
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate2 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate2.Update("entities").Set("name", names.NewName).Set("namechange", "").Where("name", names.OldName).Execute();
                    
                Console.WriteLine(" -[" + names.OldName + "] : -[" + names.NewName + "]");
                //Console.ForegroundColor = ConsoleColor.DarkCyan;
            }
            #region Remaining name changes
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                cmdupdate3.Update("entities").Set("Spouse", names.NewName).Where("Spouse", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate.Update("apprentice").Set("MentorName", names.NewName).Where("MentorID", names.OldName).Execute();
                     
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("pkexploits").Set("Name", names.NewName).Where("killedUID", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("clans").Set("LeaderName", names.NewName).Where("LeaderName", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("achievement").Set("Name", names.NewName).Where("Name", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("championarena").Set("EntityName", names.NewName).Where("EntityName", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("quests").Set("Name", names.NewName).Where("UID", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("furniture").Set("NameOwner", names.NewName).Where("NameOwner", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("pt_arsenal_inscribed").Set("name", names.NewName).Where("uid", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("prizes").Set("name", names.NewName).Where("uid", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("reward").Set("Name", names.NewName).Where("EntityID", names.OldName).Execute();
            }
            foreach (NameChangeC names in UPDATE.Values)
            {
                using (var cmdupdate3 = new MySqlCommand(MySqlCommandType.UPDATE))
                    cmdupdate3.Update("house").Set("Name", names.NewName).Where("ID", names.OldName).Execute();
            }
            #endregion
            UPDATE.Clear();
        }
    }
    public class NameChangeC
    {
        public String NewName;
        public String OldName;
    }
}
