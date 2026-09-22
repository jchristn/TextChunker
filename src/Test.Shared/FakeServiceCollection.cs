namespace Test.Shared
{
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// A minimal in memory service collection used to inspect what AddTextChunker registers, without pulling in
    /// the dependency injection runtime.
    /// </summary>
    public sealed class FakeServiceCollection : IServiceCollection
    {
        private readonly List<ServiceDescriptor> _Descriptors = new List<ServiceDescriptor>();

        /// <inheritdoc />
        public ServiceDescriptor this[int index]
        {
            get => _Descriptors[index];
            set => _Descriptors[index] = value;
        }

        /// <inheritdoc />
        public int Count => _Descriptors.Count;

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public void Add(ServiceDescriptor item) => _Descriptors.Add(item);

        /// <inheritdoc />
        public void Clear() => _Descriptors.Clear();

        /// <inheritdoc />
        public bool Contains(ServiceDescriptor item) => _Descriptors.Contains(item);

        /// <inheritdoc />
        public void CopyTo(ServiceDescriptor[] array, int arrayIndex) => _Descriptors.CopyTo(array, arrayIndex);

        /// <inheritdoc />
        public IEnumerator<ServiceDescriptor> GetEnumerator() => _Descriptors.GetEnumerator();

        /// <inheritdoc />
        public int IndexOf(ServiceDescriptor item) => _Descriptors.IndexOf(item);

        /// <inheritdoc />
        public void Insert(int index, ServiceDescriptor item) => _Descriptors.Insert(index, item);

        /// <inheritdoc />
        public bool Remove(ServiceDescriptor item) => _Descriptors.Remove(item);

        /// <inheritdoc />
        public void RemoveAt(int index) => _Descriptors.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => _Descriptors.GetEnumerator();
    }
}
