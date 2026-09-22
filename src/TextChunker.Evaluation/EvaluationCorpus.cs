namespace TextChunker.Evaluation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A small evaluation corpus. Each document declares golden answer phrases, which are converted to character
    /// spans by locating them in the text. The metric then measures how tightly a chunker bounds those spans.
    /// </summary>
    internal static class EvaluationCorpus
    {
        internal static List<EvalDocument> Build()
        {
            List<EvalDocument> documents = new List<EvalDocument>();

            string doc1 =
                "The Apollo program was a series of crewed spaceflight missions undertaken to land humans on the Moon. "
                + "Apollo 11 was the first mission to land astronauts on the lunar surface in July 1969. "
                + "Neil Armstrong became the first person to step onto the Moon, followed by Buzz Aldrin. "
                + "Michael Collins remained in orbit aboard the command module. "
                + "The program ended in 1972 after Apollo 17, the final crewed lunar landing.";
            documents.Add(Build("apollo", doc1, new[]
            {
                "Apollo 11 was the first mission to land astronauts on the lunar surface in July 1969.",
                "Neil Armstrong became the first person to step onto the Moon, followed by Buzz Aldrin.",
                "The program ended in 1972 after Apollo 17, the final crewed lunar landing."
            }));

            string doc2 =
                "Photosynthesis is the process by which green plants convert light energy into chemical energy. "
                + "It occurs mainly in the chloroplasts, which contain the pigment chlorophyll. "
                + "During the light dependent reactions, water is split and oxygen is released as a byproduct. "
                + "The Calvin cycle then uses the energy captured to fix carbon dioxide into glucose. "
                + "This process is the foundation of nearly all food chains on Earth.";
            documents.Add(Build("photosynthesis", doc2, new[]
            {
                "It occurs mainly in the chloroplasts, which contain the pigment chlorophyll.",
                "During the light dependent reactions, water is split and oxygen is released as a byproduct.",
                "The Calvin cycle then uses the energy captured to fix carbon dioxide into glucose."
            }));

            string doc3 =
                "A hash table is a data structure that maps keys to values using a hash function. "
                + "The hash function computes an index into an array of buckets from which the value can be found. "
                + "Collisions occur when two keys hash to the same bucket, and they are resolved by chaining or open addressing. "
                + "A well designed hash table offers average constant time lookups, insertions, and deletions. "
                + "The load factor, the ratio of entries to buckets, determines when the table should be resized.";
            documents.Add(Build("hashtable", doc3, new[]
            {
                "The hash function computes an index into an array of buckets from which the value can be found.",
                "Collisions occur when two keys hash to the same bucket, and they are resolved by chaining or open addressing.",
                "The load factor, the ratio of entries to buckets, determines when the table should be resized."
            }));

            return documents;
        }

        private static EvalDocument Build(string id, string text, string[] answers)
        {
            List<EvalSpan> spans = new List<EvalSpan>();
            foreach (string answer in answers)
            {
                int start = text.IndexOf(answer, StringComparison.Ordinal);
                if (start >= 0)
                    spans.Add(new EvalSpan(answer, start, answer.Length));
            }
            return new EvalDocument(id, text, spans);
        }
    }
}
