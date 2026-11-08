namespace Revora.Database
{
    public unsafe class PremioEventos
    {
        public static void Load()
        {
            using (var cmd = new MySqlCommand(MySqlCommandType.SELECT).Select("premioeventos"))
            using (var reader = new MySqlReader(cmd))
            {
                Console.WriteLine("Loading Events table!");
                while (reader.Read())
                {
                    Config.LoadPrizes(
                        reader.ReadUInt32("VIPcps"),
                        reader.ReadUInt32("VIPoro"),
                        reader.ReadUInt32("DropCpsMax"),
                        reader.ReadUInt32("DropCpsMin"),
                        reader.ReadUInt32("EventosPKOro"),
                        reader.ReadUInt32("EventosPKCps"),
                        reader.ReadUInt32("EGCOro"),
                        reader.ReadUInt32("EGCCps"),
                        reader.ReadUInt32("GClanesOro"),
                        reader.ReadUInt32("GClanesCps"),
                        reader.ReadUInt32("Hombreloco"),
                        reader.ReadUInt32("PKTorneosOro"),
                        reader.ReadUInt32("PKTorneosCps"),
                        reader.ReadUInt32("MestroMarcial"),
                        reader.ReadUInt32("HombreOro"),
                        reader.ReadUInt32("MujerOro"));
                    return;
                }
            }
        }

        public static uint VIPoro
        {
            get { return Config.PremioVipGold; }
            set { Config.PremioVipGold = value; }
        }
        public static uint VIPcps
        {
            get { return Config.PremioVipCp; }
            set { Config.PremioVipCp = value; }
        }
        public static uint DropCpsMax
        {
            get { return Config.PremioDropCpMax; }
            set { Config.PremioDropCpMax = value; }
        }
        public static uint DropCpsMin
        {
            get { return Config.PremioDropCpMin; }
            set { Config.PremioDropCpMin = value; }
        }
        public static uint EventosPKOro
        {
            get { return Config.PremioEventosPkGold; }
            set { Config.PremioEventosPkGold = value; }
        }
        public static uint EventosPKCps
        {
            get { return Config.PremioEventosPkCp; }
            set { Config.PremioEventosPkCp = value; }
        }
        public static uint EGCOro
        {
            get { return (uint)System.Math.Min(Config.EliteGuildWarGold, uint.MaxValue); }
            set { Config.EliteGuildWarGold = value; }
        }
        public static uint EGCCps
        {
            get { return Config.EliteGuildWarCp; }
            set { Config.EliteGuildWarCp = value; }
        }
        public static uint GClanesOro
        {
            get { return (uint)System.Math.Min(Config.GuildWarGold, uint.MaxValue); }
            set { Config.GuildWarGold = value; }
        }
        public static uint GClanesCps
        {
            get { return Config.GuildWarCp; }
            set { Config.GuildWarCp = value; }
        }
        public static uint Hombreloco
        {
            get { return Config.PremioHombreloco; }
            set { Config.PremioHombreloco = value; }
        }
        public static uint PKTorneosCps
        {
            get { return Config.PkTournamentCp; }
            set { Config.PkTournamentCp = value; }
        }
        public static uint PKTorneosOro
        {
            get { return (uint)System.Math.Min(Config.PkTournamentGold, uint.MaxValue); }
            set { Config.PkTournamentGold = value; }
        }
        public static uint MestroMarcial
        {
            get { return Config.PremioMestroMarcial; }
            set { Config.PremioMestroMarcial = value; }
        }
        public static uint HombreOro
        {
            get { return Config.PremioHombreOro; }
            set { Config.PremioHombreOro = value; }
        }
        public static uint MujerOro
        {
            get { return Config.PremioMujerOro; }
            set { Config.PremioMujerOro = value; }
        }
    }
}
