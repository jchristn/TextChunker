namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every TextChunker suite descriptor through the Touchstone executor as a single NUnit test.
    /// </summary>
    [TestFixture]
    public sealed class TextChunkerNunitFactTests : TouchstoneNunitBase
    {
        /// <summary>
        /// Suites under test.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TextChunkerSuites.All; }
        }

        /// <summary>
        /// Execute all suites.
        /// </summary>
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
