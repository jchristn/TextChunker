namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;

    /// <summary>
    /// Reports every TextChunker Touchstone case as an individual NUnit test via TestCaseSource.
    /// </summary>
    [TestFixture]
    public sealed class TextChunkerNunitCaseTests
    {
        /// <summary>
        /// One entry per registered Touchstone case.
        /// </summary>
        /// <returns>NUnit test case data of suite and case identifiers.</returns>
        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (TestSuiteDescriptor suite in TextChunkerSuites.All)
                foreach (TestCaseDescriptor testCase in suite.Cases)
                    yield return new TestCaseData(suite.SuiteId, testCase.CaseId).SetName(suite.SuiteId + "." + testCase.CaseId);
        }

        /// <summary>
        /// Execute a single Touchstone case.
        /// </summary>
        /// <param name="suiteId">Suite identifier.</param>
        /// <param name="caseId">Case identifier.</param>
        [TestCaseSource(nameof(Cases))]
        public async Task Case(string suiteId, string caseId)
        {
            TestCaseDescriptor? testCase = Resolve(suiteId, caseId);
            Assert.That(testCase, Is.Not.Null);
            if (testCase!.Skip)
            {
                Assert.Ignore(testCase.SkipReason ?? "Skipped.");
                return;
            }
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }

        private static TestCaseDescriptor? Resolve(string suiteId, string caseId)
        {
            foreach (TestSuiteDescriptor suite in TextChunkerSuites.All)
            {
                if (suite.SuiteId != suiteId) continue;
                foreach (TestCaseDescriptor testCase in suite.Cases)
                    if (testCase.CaseId == caseId)
                        return testCase;
            }
            return null;
        }
    }
}
