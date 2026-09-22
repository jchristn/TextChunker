namespace TextChunker.DependencyInjection
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using TextChunker.Chunking;
    using TextChunker.Tokenization;

    /// <summary>
    /// Dependency injection helpers for registering the chunker.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register a chunker that resolves its tokenizer from options.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
        public static IServiceCollection AddTextChunker(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            services.AddSingleton<IChunker, Chunker>();
            return services;
        }

        /// <summary>
        /// Register a chunker with a calibration probe used during tokenizer budget resolution.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="calibrationProbe">Calibration probe implementation.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or calibrationProbe is null.</exception>
        public static IServiceCollection AddTextChunker(this IServiceCollection services, ITokenizerCalibrationProbe calibrationProbe)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (calibrationProbe == null) throw new ArgumentNullException(nameof(calibrationProbe));
            services.AddSingleton<IChunker>(_ => new Chunker(null, calibrationProbe));
            return services;
        }

        /// <summary>
        /// Register a chunker that always uses the supplied tokenizer.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="tokenizer">Tokenizer adapter to use for all operations.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when services or tokenizer is null.</exception>
        public static IServiceCollection AddTextChunker(this IServiceCollection services, ITokenizerAdapter tokenizer)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (tokenizer == null) throw new ArgumentNullException(nameof(tokenizer));
            services.AddSingleton<IChunker>(_ => new Chunker(tokenizer));
            return services;
        }
    }
}
