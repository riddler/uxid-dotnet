using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Riddler.Tests;

// The vendored conformance corpus: where it is, how its case files split into
// lines, and a strict reader for one case. The reader hands a case's members
// out by name and fails on a member it was not asked for, on a missing
// member and on a member of the wrong JSON type, so a corpus that grows a key
// this runner does not know is a failure rather than a half-read case.
internal static class Corpus
{
    /// <summary>The corpus directory: <c>conformance/</c> at the repository root.</summary>
    public static string Directory { get; } = Locate();

    /// <summary>The bytes of one corpus file, <paramref name="name"/> relative to the corpus directory.</summary>
    public static byte[] ReadBytes(string name) => File.ReadAllBytes(Path.Combine(Directory, name));

    /// <summary>
    /// The lines of one case file, by the corpus README's rule: the file is
    /// split on newlines and every line, the last one included, ends in one.
    /// </summary>
    public static IReadOnlyList<string> Lines(string name)
    {
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(ReadBytes(name));
        if (text.Length == 0 || text[text.Length - 1] != '\n')
        {
            throw new InvalidDataException(name + " does not end in a newline");
        }

        var lines = text.Substring(0, text.Length - 1).Split('\n');
        if (lines.Any(line => line.Length == 0))
        {
            throw new InvalidDataException(name + " holds an empty line");
        }

        return lines;
    }

    /// <summary>The theory data of one case file: each case's id and its line.</summary>
    public static TheoryData<string, string> Cases(string name)
    {
        var data = new TheoryData<string, string>();
        foreach (var line in Lines(name))
        {
            data.Add(Fields.Parse(line).Peek("id"), line);
        }

        return data;
    }

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "conformance");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("conformance/manifest.json is not above " + AppContext.BaseDirectory);
    }
}

/// <summary>
/// One JSON object read member by member. <see cref="Done"/> fails on a member
/// no getter asked for.
/// </summary>
internal sealed class Fields
{
    private readonly Dictionary<string, JsonElement> _members = [];
    private readonly HashSet<string> _read = [];

    private Fields(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("expected a JSON object, found " + element.ValueKind);
        }

        foreach (var member in element.EnumerateObject())
        {
            if (_members.ContainsKey(member.Name))
            {
                throw new InvalidDataException("duplicate member " + member.Name);
            }

            _members.Add(member.Name, member.Value.Clone());
        }
    }

    public static Fields Of(JsonElement element) => new(element);

    /// <summary>Parses one case line; the result owns copies of its members.</summary>
    public static Fields Parse(string line)
    {
        using var document = JsonDocument.Parse(line);
        return new Fields(document.RootElement);
    }

    /// <summary>Reads one string member without marking the object as read.</summary>
    public string Peek(string name) => Get(name, JsonValueKind.String, null, markRead: false).GetString()!;

    public bool Has(string name) => _members.ContainsKey(name);

    public string String(string name) => Get(name, JsonValueKind.String).GetString()!;

    public bool Bool(string name) => Get(name, JsonValueKind.True, JsonValueKind.False).GetBoolean();

    public long Long(string name) => ToLong(name, Get(name, JsonValueKind.Number));

    public Fields Object(string name) => Of(Get(name, JsonValueKind.Object));

    public string? NullableString(string name) =>
        Get(name, JsonValueKind.String, JsonValueKind.Null) is { ValueKind: JsonValueKind.String } element ? element.GetString() : null;

    public long? NullableLong(string name) =>
        Get(name, JsonValueKind.Number, JsonValueKind.Null) is { ValueKind: JsonValueKind.Number } element ? ToLong(name, element) : null;

    public string? OptionalNullableString(string name) => Has(name) ? NullableString(name) : null;

    public string? OptionalString(string name) => Has(name) ? String(name) : null;

    public bool? OptionalBool(string name) => Has(name) ? Bool(name) : null;

    public long? OptionalLong(string name) => Has(name) ? Long(name) : null;

    public Fields? OptionalObject(string name) => Has(name) ? Object(name) : null;

    public IReadOnlyList<Fields> ObjectArray(string name) =>
        Items(name).Select(Of).ToList();

    public long[] LongArray(string name) =>
        Items(name).Select(item => ToLong(name, item)).ToArray();

    public string[] StringArray(string name) =>
        Items(name).Select(item => item.ValueKind == JsonValueKind.String
            ? item.GetString()!
            : throw new InvalidDataException(name + " holds a " + item.ValueKind)).ToArray();

    /// <summary>Fails when the object holds a member no getter read.</summary>
    public void Done()
    {
        var unknown = _members.Keys.Where(name => !_read.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidDataException("unknown member " + string.Join(", ", unknown));
        }
    }

    private JsonElement.ArrayEnumerator Items(string name) => Get(name, JsonValueKind.Array).EnumerateArray();

    private static long ToLong(string name, JsonElement element) =>
        element.TryGetInt64(out var value) ? value : throw new InvalidDataException(name + " is not an integer");

    private JsonElement Get(string name, JsonValueKind kind, JsonValueKind? alternative = null, bool markRead = true)
    {
        if (!_members.TryGetValue(name, out var element))
        {
            throw new InvalidDataException("missing member " + name);
        }

        if (element.ValueKind != kind && element.ValueKind != alternative)
        {
            throw new InvalidDataException(name + " is a " + element.ValueKind);
        }

        if (markRead)
        {
            _read.Add(name);
        }

        return element;
    }
}

internal static class Hex
{
    /// <summary>Lowercase hex to bytes; anything else is refused.</summary>
    public static byte[] Bytes(string text)
    {
        if (text.Length % 2 != 0 || text.Any(c => !Uri.IsHexDigit(c) || char.IsUpper(c)))
        {
            throw new InvalidDataException("not lowercase hex: " + text);
        }

        return Enumerable.Range(0, text.Length / 2)
            .Select(index => (byte)((Nibble(text[index * 2]) << 4) | Nibble(text[(index * 2) + 1])))
            .ToArray();
    }

    private static int Nibble(char digit) => digit <= '9' ? digit - '0' : digit - 'a' + 10;
}
