using System;
using Revora.Game.ConquerStructures;

namespace Revora.Database
{
    public unsafe class NobilityTable
    {
        public static void Load()
        {
            Revora.Game.Features.NobilityDonations.Initialize();
            Console.WriteLine("Loading Nobility information!");
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("nobility"))
            using (var reader = cmd.CreateReader())
            {
                while (reader.Read())
                {
                    //System.Console.Write("\b{0}", Cargando.NextChar());
                    NobilityInformation nobilityinfo = new NobilityInformation();
                    nobilityinfo.EntityUID = reader.ReadUInt32("EntityUID");
                    nobilityinfo.Name = reader.ReadString("EntityName");
                    nobilityinfo.Donation = reader.ReadUInt64("Donation");
                    nobilityinfo.Gender = reader.ReadByte("Gender");
                    nobilityinfo.Mesh = reader.ReadUInt32("Mesh");
                    nobilityinfo.Rey = reader.ReadByte("Rey");
                    
                    Game.ConquerStructures.Nobility.Board.Add(nobilityinfo.EntityUID, nobilityinfo);
                }
                Game.ConquerStructures.Nobility.Sort(0);
            }
            //Console.WriteLine("Done!");
        }
        public static void InsertNobilityInformation(NobilityInformation information)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.INSERT))
                cmd.Insert("nobility").Insert("entityname", information.Name)
                    .Insert("entityuid", information.EntityUID).Insert("donation", information.Donation)
                    .Insert("gender", information.Gender).Insert("Rey", information.Rey).Insert("mesh", information.Mesh)
                    .Insert("DuracionRey", "")
                    .Execute();
                    
        }
        public static void UpdateNobilityInformation(NobilityInformation information)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("nobility"))
                cmd.Set("donation", information.Donation).Where("entityuid", information.EntityUID)
                    .Execute();
          
        }

        public static void AgregarRey(NobilityInformation information)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("nobility"))
                cmd.Set("Rey", 1).Where("entityuid", information.EntityUID)
                    .Execute();
            Console.WriteLine("Loading Paid King!");
        }
        public static void RemoverRey(NobilityInformation information)
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("nobility"))
                cmd.Set("Rey", 0).Where("entityuid", information.EntityUID)
                    .Execute();
            Console.WriteLine("Paid King ended!");
        }

        public static void ClearDonations()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.UPDATE).Update("nobility"))
                cmd.Set("donation", 0UL).Set("Rey", 0L).Execute();
        }
    }
}