namespace StreamRpc
{
    using System.Buffers;
    using System.IO;

    internal static class StreamExtensions
    {
        /// <summary>
        /// Exposes a <see cref="ReadOnlySequence{T}"/> of <see cref="byte"/> as a <see cref="Stream"/>.
        /// </summary>
        /// <param name="readOnlySequence">The sequence of bytes to expose as a stream.</param>
        /// <returns>The readable stream.</returns>
        public static Stream AsStream(this ReadOnlySequence<byte> readOnlySequence) => new ReadOnlySequenceStream(readOnlySequence);

        /// <summary>
        /// Creates a writable <see cref="Stream"/> that can be used to add to a <see cref="IBufferWriter{T}"/> of <see cref="byte"/>.
        /// </summary>
        /// <param name="writer">The buffer writer the stream should write to.</param>
        /// <returns>A <see cref="Stream"/>.</returns>
        public static Stream AsStream(this IBufferWriter<byte> writer) => new BufferWriterStream(writer);
    }
}
