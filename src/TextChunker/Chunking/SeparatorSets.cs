namespace TextChunker.Chunking
{
    using System.Collections.Generic;
    using TextChunker.Enums;

    /// <summary>
    /// Structure aware separator ladders for the Recursive strategy. Each ladder is tried from first to last:
    /// the splitter descends to a finer separator only when a piece still exceeds the token budget. The empty
    /// string at the end signals a final token span fallback.
    /// </summary>
    internal static class SeparatorSets
    {
        internal static List<string> For(ContentFormatEnum format)
        {
            switch (format)
            {
                case ContentFormatEnum.Markdown:
                    return new List<string>
                    {
                        "\n# ", "\n## ", "\n### ", "\n#### ", "\n##### ", "\n###### ",
                        "\n```", "\n\n", "\n", ". ", " ", ""
                    };
                case ContentFormatEnum.CSharp:
                    return new List<string>
                    {
                        "\nnamespace ", "\nclass ", "\ninterface ", "\nstruct ", "\nenum ", "\nrecord ",
                        "\npublic ", "\nprivate ", "\nprotected ", "\ninternal ", "\nstatic ",
                        "\n\n", "\n", " ", ""
                    };
                case ContentFormatEnum.Python:
                    return new List<string>
                    {
                        "\nclass ", "\ndef ", "\n\tdef ", "\n    def ", "\n\n", "\n", " ", ""
                    };
                case ContentFormatEnum.JavaScript:
                    return new List<string>
                    {
                        "\nfunction ", "\nexport ", "\nconst ", "\nlet ", "\nvar ", "\nclass ",
                        "\nif ", "\nfor ", "\nwhile ", "\n\n", "\n", " ", ""
                    };
                case ContentFormatEnum.Java:
                    return new List<string>
                    {
                        "\nclass ", "\ninterface ", "\nenum ", "\nrecord ", "\npublic ", "\nprivate ",
                        "\nprotected ", "\nstatic ", "\n\n", "\n", " ", ""
                    };
                default:
                    return DefaultLadder();
            }
        }

        internal static List<string> DefaultLadder()
        {
            return new List<string> { "\n\n", "\n", ". ", " ", "" };
        }
    }
}
