using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Revora.ClientEndpoint;

public sealed record ServerEntry(uint Id, string Name, string Address, int Port, bool Group)
{
    public override string ToString() => $"{Id}: {Name} ({Address}:{Port})";
}

internal sealed class ServerListDocument
{
    private readonly XDocument document;
    private readonly Dictionary<uint, XElement> rows;
    internal IReadOnlyList<ServerEntry> Entries { get; }

    internal ServerListDocument(byte[] xml)
    {
        if (xml.Length is 0 or > ServerDatCodec.MaximumXml) throw new InvalidDataException("XML size is out of range.");
        using var input = new MemoryStream(xml);
        using var reader = XmlReader.Create(input, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = ServerDatCodec.MaximumXml, IgnoreWhitespace = false
        });
        document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new InvalidDataException("Missing server-list root.");
        XElement? database = root.Element("database");
        XElement? table = database?.Element("table_data");
        if (root.Name != "mysqldump" || root.Elements("database").Count() != 1 || database?.Attribute("name")?.Value != "serverdat" ||
            database.Elements("table_data").Count() != 1 || table?.Attribute("name")?.Value != "outenserver")
            throw new InvalidDataException("Expected the reviewed 6711 serverdat.outenserver XML table.");
        rows = [];
        var entries = new List<ServerEntry>();
        foreach (XElement row in table.Elements("row"))
        {
            if (rows.Count >= 4096) throw new InvalidDataException("Too many server rows.");
            var fields = Fields(row);
            if (!uint.TryParse(Required(fields, "id").Value, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) || !rows.TryAdd(id, row))
                throw new InvalidDataException("Invalid or duplicate server ID.");
            if (!int.TryParse(Required(fields, "ServerPort").Value, NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port is < 0 or > 65535 ||
                !uint.TryParse(Required(fields, "Child").Value, NumberStyles.None, CultureInfo.InvariantCulture, out uint child))
                throw new InvalidDataException("Invalid server port or group marker.");
            entries.Add(new(id, Required(fields, "ServerName").Value, Required(fields, "ServerIP").Value, port, child != 0));
        }
        if (entries.Count == 0 || entries.All(e => e.Group)) throw new InvalidDataException("The list contains no server entries.");
        Entries = entries;
    }

    internal byte[] Edit(uint id, string name, string address, int port)
    {
        EndpointPatch.ValidateAddress(address);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Account port must be 1–65535.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50 || name.Any(char.IsControl))
            throw new ArgumentException("Server name must be 1–50 characters without control characters.");
        XmlConvert.VerifyXmlChars(name);
        if (!rows.TryGetValue(id, out XElement? row) || Entries.Single(e => e.Id == id).Group)
            throw new InvalidDataException("Choose a server row, not a group/category.");
        var fields = Fields(row);
        string oldName = fields["ServerName"].Value;
        SetText(fields["ServerName"], name); SetText(fields["ServerIP"], address);
        SetText(fields["ServerPort"], port.ToString(CultureInfo.InvariantCulture));
        // Some private lists use FlashName as the same label; leave asset references alone.
        if (fields.TryGetValue("FlashName", out XElement? flash) && flash.Value == oldName) SetText(flash, name);
        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, NewLineHandling = NewLineHandling.None }))
            document.Save(writer);
        byte[] result = output.ToArray();
        _ = new ServerListDocument(result);
        return result;
    }

    private static Dictionary<string, XElement> Fields(XElement row)
    {
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (XElement field in row.Elements())
        {
            string name = field.Attribute("name")?.Value ?? throw new InvalidDataException("Unnamed server field.");
            if (field.Name != "field" || !result.TryAdd(name, field)) throw new InvalidDataException("Invalid or duplicate server field.");
        }
        return result;
    }

    private static XElement Required(Dictionary<string, XElement> fields, string name) => fields.TryGetValue(name, out XElement? value) ? value : throw new InvalidDataException("Missing server field: " + name);

    private static void SetText(XElement field, string value)
    {
        // Remove xsi:nil only on edited fields; preserve every other field/attribute.
        field.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"))?.Remove();
        field.Value = value;
    }
}
