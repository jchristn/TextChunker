namespace Test.Shared.Suites
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using TextChunker.Chunking;
    using TextChunker.DependencyInjection;
    using TextChunker.Tokenization;
    using Touchstone.Core;

    /// <summary>
    /// Verifies the AddTextChunker registrations.
    /// </summary>
    public static class DependencyInjectionSuite
    {
        private static void AssertRegistersChunker(FakeServiceCollection services)
        {
            bool found = false;
            foreach (ServiceDescriptor descriptor in services)
            {
                if (descriptor.ServiceType == typeof(IChunker))
                {
                    found = true;
                    TestSupport.Assert(descriptor.Lifetime == ServiceLifetime.Singleton, "IChunker should be registered as a singleton");
                }
            }
            TestSupport.Assert(found, "IChunker was not registered");
        }

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "DependencyInjection",
                displayName: "Dependency Injection",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("DependencyInjection", "AddTextChunkerDefault", "AddTextChunker registers IChunker as a singleton",
                        executeAsync: ct =>
                        {
                            FakeServiceCollection services = new FakeServiceCollection();
                            services.AddTextChunker();
                            AssertRegistersChunker(services);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DependencyInjection", "AddTextChunkerWithProbe", "AddTextChunker with a probe registers IChunker",
                        executeAsync: ct =>
                        {
                            FakeServiceCollection services = new FakeServiceCollection();
                            services.AddTextChunker(new FakeCalibrationProbe(256));
                            AssertRegistersChunker(services);
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("DependencyInjection", "AddTextChunkerWithTokenizer", "AddTextChunker with an explicit tokenizer registers IChunker",
                        executeAsync: ct =>
                        {
                            FakeServiceCollection services = new FakeServiceCollection();
                            services.AddTextChunker(new SharpTokenTokenizerAdapter("cl100k_base"));
                            AssertRegistersChunker(services);
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
