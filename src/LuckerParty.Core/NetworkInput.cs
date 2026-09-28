using System.Text;

namespace LuckerParty.Core;

public readonly record struct NetworkInput(int Sequence, float X, float Y, float Yaw,
    float Pitch, bool Sprint, bool Jump, bool Reset);

// Small redundant batches recover ordinary UDP loss without a reliable movement queue.
public static class InputCodec
{
    public const int MaxBatch = 32;
    private const int CommandBytes = 21;
    public static byte[] Encode(IEnumerable<NetworkInput> commands)
    {
        var batch = commands.TakeLast(MaxBatch).ToArray();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)batch.Length);
        foreach (var c in batch)
        {
            writer.Write(c.Sequence); writer.Write(c.X); writer.Write(c.Y);
            writer.Write(c.Yaw); writer.Write(c.Pitch);
            writer.Write((byte)((c.Sprint ? 1 : 0) | (c.Jump ? 2 : 0) | (c.Reset ? 4 : 0)));
        }
        return stream.ToArray();
    }

    public static NetworkInput[] Decode(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0 || bytes[0] > MaxBatch || bytes.Length != 1 + bytes[0] * CommandBytes)
            throw new InvalidDataException("Invalid movement batch size.");
        using var reader = new BinaryReader(new MemoryStream(bytes));
        var result = new NetworkInput[reader.ReadByte()];
        var previous = 0;
        for (var i = 0; i < result.Length; i++)
        {
            int sequence = reader.ReadInt32();
            float x = reader.ReadSingle(), y = reader.ReadSingle(), yaw = reader.ReadSingle(), pitch = reader.ReadSingle();
            byte flags = reader.ReadByte();
            if (sequence <= previous || !float.IsFinite(x) || !float.IsFinite(y) || Math.Abs(x) > 1 || Math.Abs(y) > 1
                || !float.IsFinite(yaw) || Math.Abs(yaw) > MathF.PI + .01f
                || !float.IsFinite(pitch) || Math.Abs(pitch) > 1.49f || flags > 7)
                throw new InvalidDataException("Invalid movement command.");
            previous = sequence;
            result[i] = new(sequence, x, y, yaw, pitch, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
        }
        return result;
    }
}

public static class PlayerNames
{
    public const int MaxLength = 24;
    public static string Clean(string? value)
    {
        var text = new StringBuilder();
        var count = 0;
        foreach (var rune in (value ?? "").EnumerateRunes())
        {
            if (!Rune.IsLetterOrDigit(rune) && rune.Value is not (' ' or '-' or '_' or '.')) continue;
            if (count++ == MaxLength) break;
            text.Append(rune.ToString());
        }
        var name = text.ToString().Trim();
        return name.Length == 0 ? "Player" : name;
    }
}
