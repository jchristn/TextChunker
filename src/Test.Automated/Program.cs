using System;
using System.IO;
using System.Threading.Tasks;
using Test.Shared;
using Test.Shared.Suites;
using Touchstone.Cli;

string? resultsPath = null;
string? parityGoldenPath = null;

for (int i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--results") resultsPath = args[i + 1];
    if (args[i] == "--write-parity-golden") parityGoldenPath = args[i + 1];
}

if (parityGoldenPath != null)
{
    // Re approval path for an intended boundary change: write the recomputed fixture, then review the diff.
    File.WriteAllText(parityGoldenPath, ParitySuite.ComputeGoldenJson() + Environment.NewLine);
    Console.WriteLine("Wrote parity golden fixture to " + parityGoldenPath);
    return 0;
}

return await ConsoleRunner.RunAsync(TextChunkerSuites.All, resultsPath: resultsPath);
