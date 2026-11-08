using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Database
{
    public class RuedaDeSuerteTable
    {
        public class Stage
        {
            public int ID;
            public int[] Objetos;
            public uint Costos;
        }
        const string Path = "database\\task_reward_type.ini";
        public static Dictionary<int, Stage> Etapas = null;
        public static void Load()
        {
            Etapas = new Dictionary<int, Stage>();
            using (var _streamReader = new StreamReader(Path))
            {
                var Line = string.Empty;
                while (!string.IsNullOrEmpty(Line = _streamReader.ReadLine()))
                {
                    var Split = Line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToArray();
                    var _stage = new Stage();
                    var Pos = 0;
                    _stage.ID = Convert.ToInt32(Split[Pos++]);
                    Pos += sizeof(ushort);
                    _stage.Costos = Convert.ToByte(Split[Pos]);
                    Pos = 0;
                    _stage.Objetos = new int[8];
                    for (int i = 14; i < 22; i++)
                    {
                        _stage.Objetos[Pos++] = Convert.ToInt32(Split[i]);
                    }
                    Etapas.Add(_stage.ID, _stage);
                }
            }
        }
    }
}
