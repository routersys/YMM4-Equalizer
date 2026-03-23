using Equalizer.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace Equalizer.Infrastructure;

internal static class PresetFileFormat
{
    private static readonly byte[] Magic = "EQPR"u8.ToArray();
    private const ushort FormatVersion = 1;

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        Formatting = Formatting.None
    };

    public static byte[] Serialize(IEnumerable<EQBand> bands)
    {
        string json = JsonConvert.SerializeObject(bands, JsonSettings);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        uint checksum = Crc32.Compute(jsonBytes);

        using var output = new MemoryStream(Magic.Length + sizeof(ushort) + sizeof(int) + sizeof(uint) + jsonBytes.Length);
        output.Write(Magic);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(FormatVersion);
        writer.Write(jsonBytes.Length);
        writer.Write(checksum);
        output.Write(jsonBytes);

        return output.ToArray();
    }

    public static ObservableCollection<EQBand>? Deserialize(byte[] fileData)
    {
        if (fileData is null || fileData.Length == 0) return null;

        return IsNewFormat(fileData)
            ? DeserializeNewFormat(fileData)
            : DeserializeLegacyJson(fileData);
    }

    private static bool IsNewFormat(byte[] data) =>
        data.Length >= Magic.Length &&
        data.AsSpan(0, Magic.Length).SequenceEqual(Magic);

    private static ObservableCollection<EQBand>? DeserializeNewFormat(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            reader.ReadBytes(Magic.Length);

            ushort version = reader.ReadUInt16();
            if (version > FormatVersion) return null;

            int jsonLength = reader.ReadInt32();
            uint storedChecksum = reader.ReadUInt32();

            if (ms.Position + jsonLength > data.Length) return null;

            byte[] jsonBytes = reader.ReadBytes(jsonLength);

            if (Crc32.Compute(jsonBytes) != storedChecksum) return null;

            string json = Encoding.UTF8.GetString(jsonBytes);
            return JsonConvert.DeserializeObject<ObservableCollection<EQBand>>(json, JsonSettings);
        }
        catch
        {
            return null;
        }
    }

    private static ObservableCollection<EQBand>? DeserializeLegacyJson(byte[] data)
    {
        try
        {
            string json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<ObservableCollection<EQBand>>(json, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto
            });
        }
        catch
        {
            return null;
        }
    }
}