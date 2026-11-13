using System.Diagnostics;
using Jime;

var restartTest = args.Contains("--restart-test");
var inputs = args.Where(arg => arg != "--restart-test").ToArray();
if (inputs.Length == 0) inputs = new[] { "ochitsuiteiru" };
var converter = new MozcConverter();
foreach (var romaji in inputs)
{
    Console.WriteLine($"{romaji}:");
    var conversion = await converter.GetConversionAsync(romaji);
    Console.WriteLine($"Segments: {string.Join(" | ", conversion.SegmentReadings)}");
    Console.WriteLine($"Values: {string.Join(" | ", conversion.SegmentValues)}");
    Console.WriteLine($"Romaji: {string.Join("'", RomajiConverter.SplitByKanaSegments(romaji, conversion.SegmentReadings))}");
    for (var i = 0; i < conversion.Candidates.Count; i++)
        Console.WriteLine($"{i + 1}. {conversion.Candidates[i]}");
    if (!restartTest) continue;
    foreach (var process in Process.GetProcessesByName("mozc_server")) process.Kill();
}
