using System.Text.RegularExpressions;

namespace Sudoku.Tests;

// Findings from the frontend review. These read the shipped markup and CSS as
// text rather than booting a browser: they cannot prove something renders, but
// they do catch the specific regressions found - a dead asset reference, an
// inline handler the CSP blocks, or a class name colliding across roles - which
// otherwise only surface as "the feature is broken" from a player.
//
// Each test body is a scan call plus an assertion; the file walking and regex
// matching live in the private scanners below.
public class FrontendPostureTests
{
    private static readonly string WebRoot = FindWebProject();

    // A literal onclick="..." attribute is script the Content-Security-Policy
    // blocks (script-src-attr). One in the nav menu meant the mobile menu never
    // closed after a tap, silently, on the deployed site only.
    [Fact]
    public void Markup_InlineEventHandlerAttributes_AreAbsent()
    {
        var offenders = RazorFilesWithInlineHandlers();

        Assert.Empty(offenders);
    }

    // The template's Bootstrap <link> survived without the library ever being
    // vendored, so every page load fetched a 404.
    [Fact]
    public void Markup_EveryReferencedStaticAsset_ExistsOnDisk()
    {
        var missing = ReferencedAssetsThatDoNotExist();

        Assert.Empty(missing);
    }

    // A bare .notes rule with pointer-events:none once matched the Notes BUTTON
    // as well as the pencil-mark overlay, making the button click-transparent.
    // Scoped CSS isolates per component, not per role within one.
    [Fact]
    public void Css_BareSingleClassPointerEventsNoneRules_AreAbsent()
    {
        var risky = BareClassPointerEventsRules();

        Assert.Empty(risky);
    }

    // A Windows-1252 save once turned every non-ASCII character in About.razor
    // into mojibake ("3x3" became "3Ã—3"). The bytes are still valid UTF-8 after
    // that kind of corruption, so decoding cannot detect it - the giveaway is
    // the specific sequences a UTF-8 pair produces when read as Latin-1.
    [Fact]
    public void SourceFiles_MojibakeFromABadEncodingSave_IsAbsent()
    {
        var offenders = FilesContainingMojibake();

        Assert.Empty(offenders);
    }

    // Guards the specific collision that shipped: the pencil-mark overlay and
    // the Notes button must not share a class.
    [Fact]
    public void SudokuBoardMarkup_PencilMarkOverlay_DoesNotShareAClassWithTheNotesButton()
    {
        var board = File.ReadAllText(Path.Combine(WebRoot, "Components", "Pages", "SudokuBoard.razor"));

        Assert.Contains("class=\"pencil-marks\"", board);
        Assert.DoesNotContain("<div class=\"notes\"", board);
    }

    // ----- scanners ------------------------------------------------------

    private static List<string> RazorFilesWithInlineHandlers()
    {
        // Blazor's own @onclick/@onchange are fine - they compile to delegates.
        var inlineHandler = new Regex("""(?<!@)\bon(click|change|input|submit|load|error|keydown|keyup)\s*=\s*["']""");

        return RazorFiles()
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(f => inlineHandler.IsMatch(f.text))
            .Select(f => $"{Path.GetFileName(f.path)} (inline handlers are blocked by the CSP)")
            .ToList();
    }

    private static List<string> ReferencedAssetsThatDoNotExist()
    {
        var assetRef = new Regex("""@Assets\["(?<path>[^"]+)"\]""");
        var missing = new List<string>();

        foreach (var path in RazorFiles())
        {
            foreach (Match match in assetRef.Matches(File.ReadAllText(path)))
            {
                var asset = match.Groups["path"].Value;
                // _framework assets are produced by the runtime, not on disk.
                if (asset.StartsWith("_framework/", StringComparison.Ordinal)) continue;
                // Scoped-CSS bundles are generated at build time.
                if (asset.EndsWith(".styles.css", StringComparison.Ordinal)) continue;

                var onDisk = Path.Combine(WebRoot, "wwwroot", asset.Replace('/', Path.DirectorySeparatorChar));
                var asComponentAsset = Path.Combine(WebRoot, asset.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(onDisk) && !File.Exists(asComponentAsset))
                    missing.Add($"{asset} (from {Path.GetFileName(path)})");
            }
        }

        return missing;
    }

    private static List<string> BareClassPointerEventsRules()
    {
        var risky = new List<string>();

        foreach (var path in CssFiles())
        {
            foreach (Match rule in Regex.Matches(File.ReadAllText(path), @"(?<selector>[^{}]+)\{(?<body>[^}]*)\}"))
            {
                var body = rule.Groups["body"].Value;
                if (!body.Contains("pointer-events:none", StringComparison.OrdinalIgnoreCase)
                    && !body.Contains("pointer-events: none", StringComparison.OrdinalIgnoreCase))
                    continue;

                var selector = rule.Groups["selector"].Value.Trim();
                // A single bare class (".notes") is the dangerous shape; anything
                // qualified by an element, a parent, or a second class is fine.
                if (Regex.IsMatch(selector, @"^\.[a-z][a-z0-9-]*$", RegexOptions.IgnoreCase))
                    risky.Add($"{Path.GetFileName(path)}: {selector} can disable unrelated elements sharing that class");
            }
        }

        return risky;
    }

    private static List<string> FilesContainingMojibake()
    {
        // Â followed by punctuation, and the Ã pairs, are the common signatures.
        var mojibake = new Regex("Â[\\u0080-\\u00BF]|Ã[\\u0080-\\u00BF]|â€[\\u0080-\\u00BF]|ï»¿(?!\\uFEFF)");
        var offenders = new List<string>();

        foreach (var path in RazorFiles().Concat(CssFiles()))
        {
            var match = mojibake.Match(File.ReadAllText(path));
            if (match.Success)
                offenders.Add($"{Path.GetFileName(path)} at offset {match.Index}: '{match.Value}' - re-save as UTF-8");
        }

        return offenders;
    }

    private static string FindWebProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Sudoku", "Components")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Sudoku");
    }

    private static IEnumerable<string> RazorFiles() =>
        Directory.EnumerateFiles(Path.Combine(WebRoot, "Components"), "*.razor", SearchOption.AllDirectories);

    private static IEnumerable<string> CssFiles() =>
        Directory.EnumerateFiles(WebRoot, "*.css", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
