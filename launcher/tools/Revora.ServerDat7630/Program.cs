using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

if (args.Length < 1) return Usage();
try
{
    switch (args[0])
    {
        case "decode" when args.Length == 4:
            byte[] encoded = File.ReadAllBytes(args[2]);
            using (RSA rsa = LoadPublic(args[1]))
            {
                byte[] plain = ServerDatCodec.Decode(encoded, rsa.ExportParameters(false));
                ServerSchema.Parse(plain);
                File.WriteAllBytes(args[3], plain);
                Console.WriteLine($"blocks={encoded.Length / 256} plaintext={plain.Length} sha256={Hash(plain)}");
                CryptographicOperations.ZeroMemory(plain);
            }
            break;
        case "encode" when args.Length == 4:
            byte[] xml = File.ReadAllBytes(args[2]); ServerSchema.Parse(xml);
            using (RSA rsa = LoadPrivate(args[1]))
            {
                byte[] output = ServerDatCodec.Encode(xml, rsa.ExportParameters(true));
                File.WriteAllBytes(args[3], output);
                Console.WriteLine($"blocks={output.Length / 256} sha256={Hash(output)}");
            }
            CryptographicOperations.ZeroMemory(xml);
            break;
        case "create-revora" when args.Length == 4:
            byte[] revora = ServerSchema.CreateRevora();
            File.WriteAllBytes(args[2], revora);
            using (RSA rsa = LoadPrivate(args[1])) File.WriteAllBytes(args[3], ServerDatCodec.Encode(revora, rsa.ExportParameters(true)));
            Console.WriteLine($"plaintextSha256={Hash(revora)} encodedSha256={Hash(File.ReadAllBytes(args[3]))}");
            CryptographicOperations.ZeroMemory(revora);
            break;
        case "inspect" when args.Length == 2:
            foreach (ServerEntry entry in ServerSchema.Parse(File.ReadAllBytes(args[1])))
                Console.WriteLine($"id={entry.Id} name={entry.Name} ip={entry.Ip} port={entry.Port} child={entry.Child} bind={entry.BindIp}:{entry.BindPort}");
            break;
        case "public-fingerprint" when args.Length == 2:
            using (RSA rsa = LoadPublic(args[1])) Console.WriteLine(Hash(rsa.ExportSubjectPublicKeyInfo()));
            break;
        case "scan-key" when args.Length == 3:
            using (RSA rsa = LoadPublic(args[1])) KeyScanner.Scan(File.ReadAllBytes(args[2]), rsa.ExportParameters(false).Modulus!);
            break;
        case "self-test" when args.Length == 1:
            using (RSA testKey = RSA.Create(2048))
            {
                byte[] plain = ServerSchema.CreateRevora();
                byte[] testEncoded = ServerDatCodec.Encode(plain, testKey.ExportParameters(true));
                byte[] decoded = ServerDatCodec.Decode(testEncoded, testKey.ExportParameters(false));
                if (!plain.SequenceEqual(decoded)) throw new InvalidDataException("Round-trip mismatch.");
                byte[] repeated = ServerDatCodec.Encode(plain, testKey.ExportParameters(true));
                if (!testEncoded.SequenceEqual(repeated)) throw new InvalidDataException("Encoding is not deterministic.");
                testEncoded[0] ^= 1;
                bool rejected = false;
                try { _ = ServerDatCodec.Decode(testEncoded, testKey.ExportParameters(false)); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new InvalidDataException("Tamper was accepted.");
                CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(decoded);
                Console.WriteLine("self-test=passed");
            }
            break;
        default: return Usage();
    }
    return 0;
}
catch (Exception ex) when (ex is IOException or InvalidDataException or CryptographicException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex.Message); return 1;
}

static int Usage() { Console.Error.WriteLine("decode <public.pem> <Server.dat> <xml> | encode <private.pem> <xml> <Server.dat> | create-revora <private.pem> <xml> <Server.dat> | inspect <xml> | public-fingerprint <pem> | scan-key <public.pem> <Conquer.exe> | self-test"); return 2; }
static RSA LoadPublic(string path) { RSA rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(path));return rsa; }
static RSA LoadPrivate(string path) { RSA rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(path));if(rsa.ExportParameters(true).D is null)throw new InvalidDataException("Private RSA key required.");return rsa; }
static string Hash(byte[] value)=>Convert.ToHexString(SHA256.HashData(value));

internal sealed record ServerEntry(int Id,string Name,string Ip,int Port,string FlashName,string FlashIcon,string FlashHint,int Child,string PicIp,int PicPort,string BindIp,int BindPort,int Charges);

internal static class ServerSchema
{
    internal static IReadOnlyList<ServerEntry> Parse(byte[] bytes)
    {
        if(bytes.Length is 0 or > 1024*1024)throw new InvalidDataException("Server XML size is invalid.");
        XDocument doc;try{doc=XDocument.Parse(new UTF8Encoding(false,true).GetString(bytes),LoadOptions.None);}catch(Exception e)when(e is System.Xml.XmlException or DecoderFallbackException){throw new InvalidDataException("Server XML is invalid.",e);}
        XElement table=doc.Descendants("table_data").SingleOrDefault(x=>(string?)x.Attribute("name")=="outenserver")??throw new InvalidDataException("outenserver table is missing.");
        var result=new List<ServerEntry>();
        foreach(XElement row in table.Elements("row"))
        {
            string F(string n)=>row.Elements("field").SingleOrDefault(x=>(string?)x.Attribute("name")==n)?.Value??throw new InvalidDataException($"Required field {n} is missing.");
            int I(string n){if(!int.TryParse(F(n),out int value))throw new InvalidDataException($"Field {n} is invalid.");return value;}
            var entry=new ServerEntry(I("id"),F("ServerName"),F("ServerIP"),I("ServerPort"),F("FlashName"),F("FlashIcon"),F("FlashHint"),I("Child"),F("PicServerIP"),I("PicServerPort"),F("BindServerIP"),I("BindServerPort"),I("Charges"));
            bool groupRow = entry.Port == 0 && (entry.Ip.Length == 0 || entry.Ip == "0");
            bool playableRow = entry.Name.Length is > 0 and <= 50
                && System.Net.IPAddress.TryParse(entry.Ip,out _)
                && entry.Port is >= 1 and <= 65535;
            if(!groupRow && !playableRow)throw new InvalidDataException("Server entry bounds are invalid.");
            result.Add(entry);
        }
        if(result.Count is 0 or > 512)throw new InvalidDataException("Server entry count is invalid.");
        return result;
    }

    internal static byte[] CreateRevora()
    {
        const string xml="""
<?xml version="1.0" encoding="utf-8"?>
<mysqldump xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <database name="serverdat">
    <table_structure name="outenserver">
      <field Field="id" Type="int(10) unsigned" Null="NO" Key="PRI" Default="" Extra="" />
      <field Field="ServerName" Type="varchar(50)" Null="NO" Key="" Default="0" Extra="" />
      <field Field="ServerIP" Type="varchar(128)" Null="NO" Key="" Default="0" Extra="" />
      <field Field="ServerPort" Type="int(10) unsigned" Null="NO" Key="" Default="0" Extra="" />
      <field Field="FlashName" Type="varchar(50)" Null="NO" Key="" Default="0" Extra="" />
      <field Field="FlashIcon" Type="varchar(50)" Null="NO" Key="" Default="0" Extra="" />
      <field Field="FlashHint" Type="varchar(50)" Null="NO" Key="" Default="0" Extra="" />
      <field Field="Child" Type="int(10) unsigned" Null="NO" Key="" Default="0" Extra="" />
      <field Field="PicServerIP" Type="varchar(50)" Null="NO" Key="" Default="0" Extra="" />
      <field Field="PicServerPort" Type="int(10) unsigned" Null="NO" Key="" Default="0" Extra="" />
      <field Field="BindServerIP" Type="varchar(50)" Null="NO" Key="" Default="0" Extra="" />
      <field Field="BindServerPort" Type="int(10) unsigned" Null="NO" Key="" Default="0" Extra="" />
      <field Field="Charges" Type="int(10) unsigned" Null="NO" Key="" Default="0" Extra="" />
      <key Table="outenserver" Non_unique="0" Key_name="PRIMARY" Seq_in_index="1" Column_name="id" Collation="A" Cardinality="213" Null="" Index_type="BTREE" Comment="" />
      <options Name="outenserver" Engine="MyISAM" Version="10" Row_format="Dynamic" Rows="3" Avg_row_length="62" Data_length="186" Max_data_length="281474976710655" Index_length="1024" Data_free="0" Create_time="2016-05-09 06:42:48" Update_time="2023-08-23 08:09:57" Collation="utf8_general_ci" Create_options="" Comment="" />
    </table_structure>
    <table_data name="outenserver">
      <row>
        <field name="id">0</field><field name="ServerName"></field><field name="ServerIP">0</field><field name="ServerPort">0</field>
        <field name="FlashName"></field><field name="FlashIcon"></field><field name="FlashHint"></field><field name="Child">1</field>
        <field name="PicServerIP">0</field><field name="PicServerPort">0</field><field name="BindServerIP">0</field><field name="BindServerPort">0</field><field name="Charges">0</field>
      </row>
      <row>
        <field name="id">1</field><field name="ServerName"></field><field name="ServerIP">0</field><field name="ServerPort">0</field>
        <field name="FlashName">Revora</field><field name="FlashIcon">Treasure Land</field><field name="FlashHint"></field><field name="Child">1</field>
        <field name="PicServerIP">0</field><field name="PicServerPort">0</field><field name="BindServerIP">0</field><field name="BindServerPort">0</field><field name="Charges">0</field>
      </row>
      <row>
      <field name="id">101</field><field name="ServerName">Revora</field><field name="ServerIP">79.110.49.34</field><field name="ServerPort">9958</field>
      <field name="FlashName">Revora</field><field name="FlashIcon">Group2_server13</field><field name="FlashHint"></field><field name="Child">0</field>
      <field name="PicServerIP">0</field><field name="PicServerPort">0</field><field name="BindServerIP">0</field><field name="BindServerPort">0</field><field name="Charges">0</field>
      </row>
    </table_data>
  </database>
</mysqldump>
""";
        byte[] bytes=new UTF8Encoding(false).GetBytes(xml.Replace("\r\n","\n"));_ = Parse(bytes);return bytes;
    }
}

internal static class ServerDatCodec
{
    internal static byte[] Decode(byte[] input,RSAParameters key)
    {
        if(input.Length is 0 or > 1024*1024 || input.Length%256!=0)throw new InvalidDataException("Server.dat block framing is invalid.");
        BigInteger n=new(key.Modulus!,true,true),e=new(key.Exponent!,true,true);using var compressed=new MemoryStream();
        for(int p=0;p<input.Length;p+=256){byte[] block=Fixed(BigInteger.ModPow(new BigInteger(input.AsSpan(p,256),true,true),e,n),256);if(block[0]!=0||block[1]!=1)throw new InvalidDataException("RSA type-1 header is invalid.");int i=2;while(i<block.Length&&block[i]==0xff)i++;if(i<10||i>=block.Length||block[i++]!=0)throw new InvalidDataException("RSA type-1 padding is invalid.");compressed.Write(block,i,block.Length-i);}
        compressed.Position=0;using var gzip=new GZipStream(compressed,CompressionMode.Decompress);using var plain=new MemoryStream();gzip.CopyTo(plain);if(plain.Length>1024*1024)throw new InvalidDataException("Expanded Server.dat exceeds limit.");return plain.ToArray();
    }
    internal static byte[] Encode(byte[] input,RSAParameters key)
    {
        if(key.D is null||key.Modulus?.Length!=256)throw new InvalidDataException("2048-bit RSA private key required.");using var compressed=new MemoryStream();using(var gzip=new GZipStream(compressed,CompressionLevel.SmallestSize,true))gzip.Write(input);byte[] source=compressed.ToArray();BigInteger n=new(key.Modulus,true,true),d=new(key.D,true,true);using var output=new MemoryStream();
        for(int p=0;p<source.Length;p+=245){int count=Math.Min(245,source.Length-p);byte[] block=new byte[256];block[1]=1;Array.Fill(block,(byte)0xff,2,253-count);source.AsSpan(p,count).CopyTo(block.AsSpan(256-count));output.Write(Fixed(BigInteger.ModPow(new BigInteger(block,true,true),d,n),256));}return output.ToArray();
    }
    private static byte[] Fixed(BigInteger value,int size){byte[] b=value.ToByteArray(true,true);if(b.Length>size)throw new InvalidDataException("RSA value overflow.");byte[] r=new byte[size];b.CopyTo(r,size-b.Length);return r;}
}

internal static class KeyScanner
{
    internal static void Scan(byte[] image,byte[] modulus)
    {
        foreach((string name,byte[] bytes) in new[]{("big-endian",modulus),("little-endian",modulus.Reverse().ToArray())}){var hits=Find(image,bytes);Console.WriteLine($"{name}={string.Join(',',hits)}");}
        uint[] words=Enumerable.Range(0,64).Select(i => (uint)((modulus[i*4]<<24)|(modulus[i*4+1]<<16)|(modulus[i*4+2]<<8)|modulus[i*4+3])).ToArray();
        for(int start=0;start<64;start++){uint[] chain=new uint[64];chain[63]=words[(start+63)%64];for(int j=62;j>=0;j--)chain[j]=words[(start+j)%64]^chain[j+1];byte[] pattern=chain.SelectMany(BitConverter.GetBytes).ToArray();var hits=Find(image,pattern);if(hits.Count>0)Console.WriteLine($"rolling-xor-start={start} contiguous={string.Join(',',hits)}");}
    }
    private static List<int> Find(byte[] haystack,byte[] needle){var hits=new List<int>();for(int i=0;i<=haystack.Length-needle.Length;i++){if(haystack.AsSpan(i,needle.Length).SequenceEqual(needle))hits.Add(i);}return hits;}
}
