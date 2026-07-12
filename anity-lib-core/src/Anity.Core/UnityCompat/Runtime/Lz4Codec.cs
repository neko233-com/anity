using System;

namespace UnityEngine;

/// <summary>
/// Pure C# LZ4 block codec (LZ4 block format, not frame).
/// Used by AssetBundle ChunkBasedCompression — real LZ4 sequences, not Deflate.
/// Spec-compatible with lz4_block (https://github.com/lz4/lz4).
/// </summary>
public static class Lz4Codec
{
    private const int MinMatch = 4;
    private const int HashLog = 12;
    private const int HashSize = 1 << HashLog;
    private const int MaxDistance = 65535;
    private const int LastLiterals = 5;
    private const int Mflimit = 12;

    /// <summary>Compress src into a new LZ4 block (may be larger than src on incompressible data).</summary>
    public static byte[] Encode(byte[] src)
    {
        if (src == null || src.Length == 0) return Array.Empty<byte>();
        // Worst-case bound: src + src/255 + 16
        int maxOut = src.Length + (src.Length / 255) + 16;
        var dst = new byte[maxOut];
        int written = Encode(src, 0, src.Length, dst, 0, dst.Length);
        if (written < 0)
        {
            // Fallback: store as single literal run (always valid LZ4)
            return EncodeLiteralsOnly(src);
        }
        var result = new byte[written];
        Buffer.BlockCopy(dst, 0, result, 0, written);
        return result;
    }

    public static int Encode(byte[] src, int srcOff, int srcLen, byte[] dst, int dstOff, int dstCap)
    {
        if (src == null || dst == null) return -1;
        if (srcLen == 0) return 0;
        if (srcLen + srcOff > src.Length) return -1;

        var hashTable = new int[HashSize];
        for (int i = 0; i < HashSize; i++) hashTable[i] = -1;

        int s = srcOff;
        int sEnd = srcOff + srcLen;
        int anchor = s;
        int d = dstOff;
        int dEnd = dstOff + dstCap;

        // leave room for last literals
        int mflimit = sEnd - Mflimit;
        int matchLimit = sEnd - LastLiterals;

        if (srcLen < Mflimit)
            return EncodeLiteralsOnly(src, srcOff, srcLen, dst, dstOff, dstCap);

        s++;
        while (s < mflimit)
        {
            uint seq = ReadU32(src, s);
            int h = Hash(seq);
            int refPos = hashTable[h];
            hashTable[h] = s;

            if (refPos < srcOff || s - refPos > MaxDistance || ReadU32(src, refPos) != seq)
            {
                s++;
                continue;
            }

            // catch up
            int match = refPos;
            int litLen = s - anchor;
            // extend match backward
            while (match > srcOff && s > anchor && src[match - 1] == src[s - 1])
            {
                match--;
                s--;
                litLen = s - anchor;
            }

            int matchLen = MinMatch;
            int sMatch = s + MinMatch;
            int rMatch = match + MinMatch;
            while (sMatch < matchLimit && src[sMatch] == src[rMatch])
            {
                sMatch++;
                rMatch++;
                matchLen++;
            }

            // write sequence
            int tokenPos = d;
            if (d >= dEnd) return -1;
            d++; // token placeholder

            // literal length
            int ll = litLen;
            if (ll >= 15)
            {
                dst[tokenPos] = 0xF0;
                int remain = ll - 15;
                while (remain >= 255)
                {
                    if (d >= dEnd) return -1;
                    dst[d++] = 255;
                    remain -= 255;
                }
                if (d >= dEnd) return -1;
                dst[d++] = (byte)remain;
            }
            else
            {
                dst[tokenPos] = (byte)(ll << 4);
            }

            // literals
            if (d + litLen > dEnd) return -1;
            if (litLen > 0)
            {
                Buffer.BlockCopy(src, anchor, dst, d, litLen);
                d += litLen;
            }

            // offset
            int offset = s - match;
            if (d + 2 > dEnd) return -1;
            dst[d++] = (byte)offset;
            dst[d++] = (byte)(offset >> 8);

            // match length (excluding MinMatch already in token low nibble)
            int ml = matchLen - MinMatch;
            if (ml >= 15)
            {
                dst[tokenPos] = (byte)((dst[tokenPos] & 0xF0) | 0x0F);
                int remain = ml - 15;
                while (remain >= 255)
                {
                    if (d >= dEnd) return -1;
                    dst[d++] = 255;
                    remain -= 255;
                }
                if (d >= dEnd) return -1;
                dst[d++] = (byte)remain;
            }
            else
            {
                dst[tokenPos] = (byte)((dst[tokenPos] & 0xF0) | (ml & 0x0F));
            }

            s = sMatch;
            anchor = s;
            if (s >= mflimit) break;
            // fill hash at s-2
            if (s - 2 >= srcOff)
            {
                uint seq2 = ReadU32(src, s - 2);
                hashTable[Hash(seq2)] = s - 2;
            }
        }

        // last literals
        int lastLit = sEnd - anchor;
        int need = 1 + lastLit + (lastLit >= 15 ? (lastLit - 15) / 255 + 1 : 0);
        if (d + need > dEnd) return -1;
        int tokenLast = d++;
        if (lastLit >= 15)
        {
            dst[tokenLast] = 0xF0;
            int remain = lastLit - 15;
            while (remain >= 255)
            {
                dst[d++] = 255;
                remain -= 255;
            }
            dst[d++] = (byte)remain;
        }
        else
        {
            dst[tokenLast] = (byte)(lastLit << 4);
        }
        if (lastLit > 0)
        {
            Buffer.BlockCopy(src, anchor, dst, d, lastLit);
            d += lastLit;
        }
        return d - dstOff;
    }

    /// <summary>Decompress LZ4 block into a buffer of known original size.</summary>
    public static byte[] Decode(byte[] src, int destLen)
    {
        if (src == null) return Array.Empty<byte>();
        if (destLen <= 0) return Array.Empty<byte>();
        var dst = new byte[destLen];
        int n = Decode(src, 0, src.Length, dst, 0, destLen);
        if (n != destLen)
            throw new InvalidOperationException($"LZ4 decode size mismatch: got {n}, expected {destLen}");
        return dst;
    }

    public static int Decode(byte[] src, int srcOff, int srcLen, byte[] dst, int dstOff, int dstLen)
    {
        if (src == null || dst == null) return -1;
        int s = srcOff;
        int sEnd = srcOff + srcLen;
        int d = dstOff;
        int dEnd = dstOff + dstLen;

        while (s < sEnd)
        {
            if (s >= sEnd) break;
            byte token = src[s++];
            int litLen = token >> 4;
            if (litLen == 15)
            {
                byte b;
                do
                {
                    if (s >= sEnd) return -1;
                    b = src[s++];
                    litLen += b;
                } while (b == 255);
            }

            if (d + litLen > dEnd || s + litLen > sEnd) return -1;
            if (litLen > 0)
            {
                Buffer.BlockCopy(src, s, dst, d, litLen);
                s += litLen;
                d += litLen;
            }

            if (s >= sEnd) break; // last sequence: literals only

            if (s + 2 > sEnd) return -1;
            int offset = src[s] | (src[s + 1] << 8);
            s += 2;
            if (offset == 0 || d - offset < dstOff) return -1;

            int matchLen = (token & 0x0F) + MinMatch;
            if ((token & 0x0F) == 15)
            {
                byte b;
                do
                {
                    if (s >= sEnd) return -1;
                    b = src[s++];
                    matchLen += b;
                } while (b == 255);
            }

            int matchPos = d - offset;
            if (d + matchLen > dEnd) return -1;
            // may overlap — byte-by-byte
            for (int i = 0; i < matchLen; i++)
                dst[d + i] = dst[matchPos + i];
            d += matchLen;
        }

        return d - dstOff;
    }

    private static byte[] EncodeLiteralsOnly(byte[] src)
    {
        var dst = new byte[src.Length + 16 + src.Length / 255];
        int n = EncodeLiteralsOnly(src, 0, src.Length, dst, 0, dst.Length);
        var r = new byte[n];
        Buffer.BlockCopy(dst, 0, r, 0, n);
        return r;
    }

    private static int EncodeLiteralsOnly(byte[] src, int srcOff, int srcLen, byte[] dst, int dstOff, int dstCap)
    {
        int d = dstOff;
        int dEnd = dstOff + dstCap;
        int need = 1 + srcLen + (srcLen >= 15 ? (srcLen - 15) / 255 + 1 : 0);
        if (d + need > dEnd) return -1;
        int tokenPos = d++;
        if (srcLen >= 15)
        {
            dst[tokenPos] = 0xF0;
            int remain = srcLen - 15;
            while (remain >= 255)
            {
                dst[d++] = 255;
                remain -= 255;
            }
            dst[d++] = (byte)remain;
        }
        else
        {
            dst[tokenPos] = (byte)(srcLen << 4);
        }
        if (srcLen > 0)
        {
            Buffer.BlockCopy(src, srcOff, dst, d, srcLen);
            d += srcLen;
        }
        return d - dstOff;
    }

    private static int Hash(uint seq) => (int)((seq * 2654435761u) >> (32 - HashLog));

    private static uint ReadU32(byte[] b, int i) =>
        (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
}
