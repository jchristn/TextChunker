namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// Runs each non skipped Touchstone case as its own xUnit theory row.
    /// </summary>
    public sealed class TextChunkerTheoryTests
    {
        /// <summary>
        /// One theory row per non skipped case.
        /// </summary>
        /// <returns>Theory data of test cases.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();
            foreach (TestSuiteDescriptor suite in TextChunkerSuites.All)
                foreach (TestCaseDescriptor testCase in suite.Cases)
                    if (!testCase.Skip)
                        data.Add(testCase);
            return data;
        }

        /// <summary>
        /// Execute a single case.
        /// </summary>
        /// <param name="testCase">Case to execute.</param>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
