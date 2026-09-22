using System.Threading.Tasks;
using Test.Shared;
using Touchstone.Cli;

string? resultsPath = null;

for (int i = 0; i + 1 < args.Length; i++)
{
    if (args[i] == "--results")
    {
        resultsPath = args[i + 1];
        break;
    }
}

return await ConsoleRunner.RunAsync(TextChunkerSuites.All, resultsPath: resultsPath);
