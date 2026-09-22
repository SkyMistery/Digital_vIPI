using System.Text;
using Vipi.Sectorfile.IO;
using Xunit;

namespace Vipi.Sectorfile.IO.Tests;

/// <summary>Encoding/BOM detection — DEVELOPMENT_PLAN §33.1 … §33.3 (§33.4 in orchestrator tests).</summary>
public class SectorFileReaderTests
{
    // §33.1 — UTF-8 without BOM → Encoding == UTF8, HasByteOrderMark == false.
    [Fact]
    public void Read_Utf8_NoBom()
    {
        byte[] raw = Encoding.UTF8.GetBytes("café\r\nline2\r\n");   // multi-byte char, no preamble
        var result = SectorFileReader.Decode(raw);

        Assert.Equal(Encoding.UTF8.CodePage, result.Encoding.CodePage);
        Assert.False(result.HasByteOrderMark);
        Assert.Equal(new[] { "café", "line2" }, result.Lines);
    }

    // §33.2 — UTF-8 with BOM → HasByteOrderMark == true; BOM absent from chunks.
    [Fact]
    public void Read_Utf8_WithBom()
    {
        byte[] bom = { 0xEF, 0xBB, 0xBF };
        byte[] body = Encoding.UTF8.GetBytes("a\r\nb\r\n");
        byte[] raw = bom.Concat(body).ToArray();

        var result = SectorFileReader.Decode(raw);

        Assert.True(result.HasByteOrderMark);
        Assert.Equal(Encoding.UTF8.CodePage, result.Encoding.CodePage);
        Assert.Equal(new[] { "a", "b" }, result.Lines);
        Assert.DoesNotContain(result.Lines, l => l.Contains('﻿'));
    }

    // §33.3 — invalid UTF-8 bytes → fallback to Windows-1252.
    [Fact]
    public void Read_InvalidUtf8_FallsBackToWindows1252()
    {
        // 0xE0 ("à" in 1252) followed by an ASCII byte is an invalid UTF-8 sequence.
        byte[] raw = { 0xE0, (byte)'b', (byte)'c' };
        var result = SectorFileReader.Decode(raw);

        Assert.Equal(1252, result.Encoding.CodePage);
        Assert.False(result.HasByteOrderMark);
        Assert.Equal(new[] { "àbc" }, result.Lines);
    }

    [Fact]
    public void Read_DetectsLfNewline()
    {
        var result = SectorFileReader.Decode(Encoding.UTF8.GetBytes("a\nb\nc"));
        Assert.Equal("\n", result.NewLine);
        Assert.False(result.HasFinalNewLine);
        Assert.Equal(new[] { "a", "b", "c" }, result.Lines);
    }

    // Mixed newlines: the presence of any CRLF makes CRLF the terminator; the minority LF stays
    // embedded in the line content, which is what lets the orchestrator reproduce the file
    // byte-for-byte (see FileSaverOrchestratorTests.RoundTrip_MixedNewlines_ByteForByte).
    [Fact]
    public void Read_MixedNewlines_PrefersCrlf_KeepsLfEmbedded()
    {
        var result = SectorFileReader.Decode(Encoding.UTF8.GetBytes("a\r\nb\nc\r\n"));
        Assert.Equal("\r\n", result.NewLine);
        Assert.True(result.HasFinalNewLine);
        Assert.Equal(new[] { "a", "b\nc" }, result.Lines);
    }
}
