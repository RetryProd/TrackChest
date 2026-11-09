namespace ChestCounter;

internal enum ReplayState { Unavailable, Invalid, Live, NoKey, Finished }

internal static class ReplayHeaderProbe
{
    public static ReplayState Read(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt32() != 0x1CA2E27F) return ReplayState.Invalid;
            var version = reader.ReadUInt32();
            if (version >= 7)
            {
                var versions = reader.ReadInt32();
                if (versions is < 0 or > 64) return ReplayState.Invalid;
                stream.Seek(versions * 20L, SeekOrigin.Current);
            }

            stream.Seek(12, SeekOrigin.Current); // Duration, network version, changelist.
            var nameLength = reader.ReadInt32();
            stream.Seek(nameLength < 0 ? -2L * nameLength : nameLength, SeekOrigin.Current);
            if (reader.ReadUInt32() != 0) return ReplayState.Live;
            if (version < 6) return ReplayState.Finished;

            stream.Seek(12, SeekOrigin.Current); // Timestamp and compression flag.
            if (reader.ReadUInt32() == 0) return ReplayState.Finished;
            var keyLength = reader.ReadInt32();
            if (keyLength is < 0 or > 256) return ReplayState.Invalid;
            var key = reader.ReadBytes(keyLength);
            if (key.Length != keyLength) return ReplayState.Invalid;
            return key.All(b => b == 0) ? ReplayState.NoKey : ReplayState.Finished;
        }
        catch (EndOfStreamException) { return ReplayState.Invalid; }
        catch (IOException) { return ReplayState.Unavailable; }
        catch (UnauthorizedAccessException) { return ReplayState.Invalid; }
    }
}
