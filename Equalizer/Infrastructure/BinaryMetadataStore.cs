using Equalizer.Models;
using System.IO;
using System.Text;

namespace Equalizer.Infrastructure;

internal static class BinaryMetadataStore
{
    private static readonly byte[] Magic = "EQMD"u8.ToArray();
    private const ushort FormatVersion = 1;

    public static byte[] Serialize(IReadOnlyDictionary<string, PresetMetadata> metadata)
    {
        using var dataStream = new MemoryStream();
        using var dataWriter = new BinaryWriter(dataStream, Encoding.UTF8, leaveOpen: true);

        dataWriter.Write(metadata.Count);
        foreach (var (name, meta) in metadata)
        {
            dataWriter.Write(name);
            dataWriter.Write(meta.Group ?? "other");
            dataWriter.Write(meta.IsFavorite);
        }
        dataWriter.Flush();

        byte[] dataSection = dataStream.ToArray();
        uint checksum = Crc32.Compute(dataSection);

        using var output = new MemoryStream(Magic.Length + sizeof(ushort) + sizeof(int) + sizeof(uint) + dataSection.Length);
        output.Write(Magic);
        using var headerWriter = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        headerWriter.Write(FormatVersion);
        headerWriter.Write(dataSection.Length);
        headerWriter.Write(checksum);
        output.Write(dataSection);

        return output.ToArray();
    }

    public static Dictionary<string, PresetMetadata>? Deserialize(byte[] data)
    {
        if (data is null || data.Length < Magic.Length + sizeof(ushort) + sizeof(int) + sizeof(uint))
            return null;

        try
        {
            using var ms = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            byte[] magic = reader.ReadBytes(Magic.Length);
            if (!magic.SequenceEqual(Magic)) return null;

            ushort version = reader.ReadUInt16();
            if (version > FormatVersion) return null;

            int dataLength = reader.ReadInt32();
            uint storedChecksum = reader.ReadUInt32();

            if (ms.Position + dataLength > data.Length) return null;

            byte[] dataSection = reader.ReadBytes(dataLength);

            if (Crc32.Compute(dataSection) != storedChecksum) return null;

            using var dataMs = new MemoryStream(dataSection, writable: false);
            using var dataReader = new BinaryReader(dataMs, Encoding.UTF8, leaveOpen: true);

            int count = dataReader.ReadInt32();
            var result = new Dictionary<string, PresetMetadata>(count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                string name = dataReader.ReadString();
                string group = dataReader.ReadString();
                bool isFavorite = dataReader.ReadBoolean();
                result[name] = new PresetMetadata { Group = group, IsFavorite = isFavorite };
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, PresetMetadata>? DeserializeLegacyJson(string jsonPath)
    {
        if (!File.Exists(jsonPath)) return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, PresetMetadata>>(json);
        }
        catch
        {
            return null;
        }
    }
}