using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Revora.Database
{
    class AvaterFaces
    {
        public class HairFace_Type
        {
            public bool ISHair;
            public uint ID;
            public string Name;
            public uint Cost;
            public byte Type;
            public List<byte> Class;
            // Preserve the installed restriction columns for the owned service.
            // Their policy meanings must be established before enabling purchases.
            public uint RuleKind, RuleValue, CostKind, Extra;
        }
        public static List<HairFace_Type> HairFace = new List<HairFace_Type>();
        internal static List<HairFace_Type> Parse(IEnumerable<string> rows)
        {
            var result = new List<HairFace_Type>();
            var identities = new HashSet<ulong>();
            foreach (string row in rows)
            {
                if (string.IsNullOrWhiteSpace(row)) continue;
                var data = row.Split(new[] { "@@" }, StringSplitOptions.None);
                if (data.Length != 12 || data[11] != "" || (data[0] != "0" && data[0] != "1"))
                    throw new FormatException("Invalid hair/face catalog row shape.");
                uint id = Number(data[1]);
                if (id == 0 || id > ushort.MaxValue || Number(data[2]) != 0 || string.IsNullOrWhiteSpace(data[3]))
                    throw new FormatException("Invalid hair/face catalog identity.");
                var item = new HairFace_Type {
                    ISHair = data[0] == "0", ID = id, Name = data[3], Cost = Number(data[7]),
                    RuleKind = Number(data[4]), RuleValue = Number(data[5]), CostKind = Number(data[6]),
                    Extra = Number(data[8]), Type = checked((byte)Number(data[10])), Class = new List<byte>()
                };
                if (!identities.Add(((ulong)(item.ISHair ? 0u : 1u) << 32) | id))
                    throw new FormatException("Repeated hair/face catalog identity.");
                if (data[9].Length != 0) {
                    var classes = data[9].Split(',');
                    if (classes.Length < 3 || classes[0] != "" || classes[classes.Length - 1] != "")
                        throw new FormatException("Invalid hair/face class list.");
                    for (int i = 1; i < classes.Length - 1; i++) {
                        byte family = checked((byte)Number(classes[i]));
                        if (family == 0 || item.Class.Contains(family)) throw new FormatException("Invalid hair/face class identity.");
                        item.Class.Add(family);
                    }
                }
                result.Add(item);
                if (result.Count > 1024) throw new FormatException("Hair/face catalog is too large.");
            }
            if (result.Count == 0) throw new FormatException("Hair/face catalog is empty.");
            return result;
        }
        private static uint Number(string value)
        { return uint.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture); }
        public static void Load()
        {
            // Publish only a completely parsed catalog; reload neither appends nor
            // leaves a partially populated list after a malformed row.
            HairFace = Parse(File.ReadLines(Constants.hairface_storage_type));
        }
    }
}
