namespace Revoras.Database
{
    using Revoras;
    using System;

    internal class rates
    {
        public static uint Rey;
        public static uint Principe;
        public static uint Marquez;
        
        

        public static void LoadRates()
        {

            Revora.Database.MySqlReader reader = new Revora.Database.MySqlReader(new Revora.Database.MySqlCommand(Revora.Database.MySqlCommandType.SELECT).Select("rates").Where("Coder", "slayer"));
            
            if (reader.Read())
            {
                Rey = reader.ReadUInt32("Rey");
                //Program.EntityUID = new ServerBase.Counter(r.ReadUInt32("LastEntity"));
                Principe = reader.ReadUInt32("Principe");
                Marquez = reader.ReadUInt32("Marquez");
                
            }
            Console.WriteLine("Configuracion de Nobleza cargada.");
            
            
        }
     }
}


