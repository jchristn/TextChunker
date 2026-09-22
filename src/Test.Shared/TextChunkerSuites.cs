namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Suites;
    using Touchstone.Core;

    /// <summary>
    /// Registry of all TextChunker test suite descriptors, consumed by every runner.
    /// </summary>
    public static class TextChunkerSuites
    {
        /// <summary>
        /// All registered test suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    StrategySuite.Build(),
                    RecursiveSuite.Build(),
                    DegradationSuite.Build(),
                    OverlapSuite.Build(),
                    TokenizerSuite.Build(),
                    TokenizerIntegrationSuite.Build(),
                    BudgetResolverSuite.Build(),
                    InputModeSuite.Build(),
                    HierarchySuite.Build(),
                    RequestHierarchySuite.Build(),
                    MetadataSuite.Build(),
                    SmallChunkSuite.Build(),
                    UnicodeSuite.Build(),
                    PresetSuite.Build(),
                    DiagnosticsSuite.Build(),
                    DependencyInjectionSuite.Build(),
                    ConcurrencySuite.Build(),
                    LargeInputSuite.Build(),
                    FuzzSuite.Build(),
                    NegativeArgumentSuite.Build(),
                    InvariantSuite.Build(),
                    SerializationSuite.Build(),
                    OptionsValidationSuite.Build(),
                    ParitySuite.Build(),
                    GoldenSuite.Build()
                };
            }
        }
    }
}
