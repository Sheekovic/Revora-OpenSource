namespace Revora.Database
{
    using Revora;
    using System;

    internal class helpdesk
    {
        public static string ChangePass = "";
        public static string ChatBox = "";
        public static string Facebook = "";
        public static string Purchase = "";
        public static string Register = "";
        public static string Vote = "";
        public static string Vote2 = "";
        public static string Vote3 = "";

        public static void LoadRates()
        {
            MySqlReader reader = new MySqlReader(new MySqlCommand(MySqlCommandType.SELECT).Select("helpdesk"));
            if (reader.Read())
            {
                Register = reader.ReadString("Register");
                Vote = reader.ReadString("Vote");
                Vote2 = reader.ReadString("Vote2");
                Vote3 = reader.ReadString("Vote3");
                ChatBox = reader.ReadString("ChatBox");
                Purchase = reader.ReadString("Purchase");
                Facebook = reader.ReadString("Facebook");
                ChangePass = reader.ReadString("ChangePass");
            }
        }
    }
}

