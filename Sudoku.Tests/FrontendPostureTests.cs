using System.Text.RegularExpressions;

namespace Sudoku.Tests;

// Findings from the frontend review. These read the shipped markup and CSS as
// text rather than booting a browser: they cannot prove something renders, but
// they do catch the specific regressions found - a dead asset reference, an
// inline handler the CSP blocks, or a class name colliding across roles - which
// otherwise only surface as "the feature is broken" from a player.
public class FrontendPostureTests
{
    private static readonly string WebRoot = FindWebProject();

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

    // A literal onclick="..." attribute is script the Content-Security-Policy
    // blocks (script-src-attr). One in the nav menu meant the mobile menu never
    // closed after a tap, silently, on the deployed site only.
    [Fact]
    public void Markup_contains_no_inline_event_handler_attributes()
    {
        // Blazor's own @onclick/@onchange are fine - they compile to delegates.
        var inlineHandler = new Regex("""(?<!@)\bon(click|change|input|submit|load|error|keydown|keyup)\s*=\s*["']""");

        var offenders = RazorFiles()
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(f => inlineHandler.IsMatch(f.text))
            .Select(f => Path.GetFileName(f.path))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Inline event handlers are blocked by the CSP: {string.Join(", ", offenders)}");
    }

    // The template's Bootstrap <link> survived without the library ever being
    // vendored, so every page load fetched a 404.
    [Fact]
    public void Every_referenced_static_asset_exists()
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

        Assert.True(missing.Count == 0,
            $"Referenced assets that do not exist: {string.Join(", ", missing)}");
    }

    // A bare .notes rule with pointer-events:none once matched the Notes BUTTON
    // as well as the pencil-mark overlay, making the button click-transparent.
    // Scoped CSS isolates per component, not per role within one.
    [Fact]
    public void No_rule_makes_an_element_click_transparent_by_a_generic_class_name()
    {
        var risky = new List<string>();

        foreach (var path in CssFiles())
        {
            var text = File.ReadAllText(path);
            foreach (Match rule in Regex.Matches(text, @"(?<selector>[^{}]+)\{(?<body>[^}]*)\}"))
            {
                if (!rule.Groups["body"].Value.Contains("pointer-events:none", StringComparison.OrdinalIgnoreCase)
                    && !rule.Groups["body"].Value.Contains("pointer-events: none", StringComparison.OrdinalIgnoreCase))
                    continue;

                var selector = rule.Groups["selector"].Value.Trim();
                // A single bare class (".notes") is the dangerous shape; anything
                // qualified by an element, a parent, or a second class is fine.
                if (Regex.IsMatch(selector, @"^\.[a-z][a-z0-9-]*$", RegexOptions.IgnoreCase))
                    risky.Add($"{Path.GetFileName(path)}: {selector}");
            }
        }

        Assert.True(risky.Count == 0,
            "A bare single-class pointer-events:none rule can disable unrelated elements " +
            $"sharing that class name: {string.Join(", ", risky)}");
    }

    // Guards the specific collision that shipped: the pencil-mark overlay and
    // the Notes button must not share a class.
    [Fact]
    public void The_pencil_mark_overlay_does_not_share_a_class_with_the_notes_button()
    {
        var board = File.ReadAllText(Path.Combine(WebRoot, "Components", "Pages", "SudokuBoard.razor"));

        Assert.Contains("class=\"pencil-marks\"", board);
        Assert.DoesNotContain("<div class=\"notes\"", board);
    }
}
