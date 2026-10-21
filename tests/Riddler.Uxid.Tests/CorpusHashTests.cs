using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Riddler.Tests;

// The vendored corpus is the one its manifest and conformance/SOURCE.json
// describe. The hash is recomputed by the reference's rule (the corpus
// README, "The format" and "Running the cases"): the string sha256: and the
// lowercase hex SHA-256 of the case files' bytes concatenated in the order of
// the manifest's files; each case_count is the number of lines of its file.
// A mismatch means the vendored copy is not the one recorded; it is never
// repaired by rewriting a hash.
public class CorpusHashTests
{
    private static readonly string[] _caseFiles =
        ["encode.json", "deterministic.json", "decode.json", "invalid.json", "monotonic.json"];

    private static readonly string[] _kinds =
        ["encode", "deterministic", "decode", "invalid", "monotonic"];

    [Fact] // (sabotage: files hashed in reverse order -> Assert.Equal() Failure: Strings differ)
    public void CaseFilesHashToTheManifestsCorpusHash()
    {
        var manifest = ReadManifest();

        Assert.Equal(manifest.CorpusHash, CorpusHash(manifest.Files.Select(file => file.File)));
    }

    [Fact] // (sabotage: last line dropped from the count -> Assert.Equal() Failure: Values differ)
    public void EachCaseCountIsTheNumberOfLinesOfItsFile()
    {
        foreach (var (_, file, count) in ReadManifest().Files)
        {
            Assert.Equal(count, Corpus.Lines(file).Count);
        }
    }

    [Fact] // (sabotage: expected file order swapped -> Assert.Equal() Failure: Collections differ)
    public void ManifestListsExactlyTheFilesTheRunnerRuns()
    {
        var manifest = ReadManifest();

        Assert.Equal(1, manifest.Version);
        Assert.Equal(_caseFiles, manifest.Files.Select(file => file.File).ToArray());
        Assert.Equal(_kinds, manifest.Files.Select(file => file.Kind).ToArray());
    }

    [Fact] // (sabotage: one changed byte in a case file -> Assert.Equal() Failure: Strings differ)
    public void SourceJsonRecordsTheSameHashAndTheTagItCameFrom()
    {
        var source = Fields.Parse(Encoding.UTF8.GetString(Corpus.ReadBytes("SOURCE.json")));
        var repo = source.String("repo");
        var tag = source.String("tag");
        var sha = source.String("sha");
        var recorded = source.String("corpus_hash");
        source.Done();

        Assert.Equal("riddler/uxid-ex", repo);
        Assert.StartsWith("v", tag, StringComparison.Ordinal);
        Assert.Equal(40, sha.Length);
        Assert.All(sha, c => Assert.True(c is (>= '0' and <= '9') or (>= 'a' and <= 'f'), sha));
        Assert.Equal(ReadManifest().CorpusHash, recorded);
        Assert.Equal(recorded, CorpusHash(_caseFiles));
    }

    [Fact] // (sabotage: a carriage return added to a case file -> Assert.DoesNotContain() Failure: Found item in collection)
    public void CaseFilesAreCheckedInWithNewlineLineEndings()
    {
        foreach (var file in _caseFiles)
        {
            Assert.DoesNotContain((byte)'\r', Corpus.ReadBytes(file));
        }
    }

    private static string CorpusHash(IEnumerable<string> files)
    {
        using var sha256 = SHA256.Create();
        foreach (var file in files)
        {
            var bytes = Corpus.ReadBytes(file);
            sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);
        var hex = new StringBuilder("sha256:");
        foreach (var value in sha256.Hash!)
        {
            hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
        }

        return hex.ToString();
    }

    private static Manifest ReadManifest()
    {
        var root = Fields.Parse(Encoding.UTF8.GetString(Corpus.ReadBytes("manifest.json")));
        var version = root.Long("corpus_version");
        var hash = root.String("corpus_hash");
        var files = new List<(string Kind, string File, int Count)>();
        foreach (var entry in root.ObjectArray("files"))
        {
            var kind = entry.String("kind");
            var file = entry.String("file");
            var count = checked((int)entry.Long("case_count"));
            entry.Done();
            files.Add((kind, file, count));
        }

        root.Done();
        return new Manifest(version, hash, files);
    }

    private sealed class Manifest(long version, string corpusHash, IReadOnlyList<(string Kind, string File, int Count)> files)
    {
        public long Version { get; } = version;

        public string CorpusHash { get; } = corpusHash;

        public IReadOnlyList<(string Kind, string File, int Count)> Files { get; } = files;
    }
}
