namespace TextChunkerConsole
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Bundled sample corpora so a user can drive the library without supplying their own input.
    /// </summary>
    internal static class SampleCorpora
    {
        internal static string Prose()
        {
            return "The quick brown fox jumps over the lazy dog. The dog was not amused by the fox. "
                + "It had been sleeping soundly for hours before the interruption occurred.\n\n"
                + "Meanwhile, a second paragraph describes an entirely different scene. Rain fell on the quiet town. "
                + "Streets emptied as people retreated indoors to wait out the storm.\n\n"
                + "A final paragraph closes the sample. Chunking must preserve every meaningful word. "
                + "Determinism matters more than anything else in this pipeline.";
        }

        internal static string Markdown()
        {
            return "# Getting Started\nInstall the package from NuGet.\n\n"
                + "## Configuration\nSet your options before chunking.\n\n"
                + "### Windows\nRun the installer and restart your shell.\n\n"
                + "### Linux\nUse the package manager for your distribution.\n\n"
                + "## Usage\nCall ChunkText and enumerate the results.";
        }

        internal static string LongWordList()
        {
            return string.Join(" ", Enumerable.Range(1, 200).Select(i => "word" + i));
        }

        internal static List<string> ListItems()
        {
            return new List<string>
            {
                "Preheat the oven to 220 degrees.",
                "Combine the dry ingredients in a bowl.",
                "Fold in the wet ingredients until smooth.",
                "Bake for twenty five minutes.",
                "Cool on a wire rack before serving."
            };
        }

        internal static List<List<string>> Table()
        {
            return new List<List<string>>
            {
                new List<string> { "Name", "Role", "Location" },
                new List<string> { "Ada", "Engineer", "London" },
                new List<string> { "Bjarne", "Architect", "Aarhus" },
                new List<string> { "Grace", "Admiral", "Arlington" }
            };
        }
    }
}
