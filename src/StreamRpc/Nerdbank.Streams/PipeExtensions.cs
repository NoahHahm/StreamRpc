// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace StreamRpc
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Pipelines;
    using System.Threading;
    using System.Threading.Tasks;
    using Protocol;

    internal static class PipeExtensions
    {
        /// <summary>
        /// The default buffer size to use for pipe readers.
        /// </summary>
        private const int DefaultReadBufferSize = 4 * 1024;

        /// <summary>
        /// Creates a <see cref="PipeReader"/> that reads from the specified <see cref="Stream"/> exactly as told to do so.
        /// </summary>
        /// <param name="stream">The stream to read from using a pipe.</param>
        /// <param name="sizeHint">A hint at the size of messages that are commonly transferred. Use 0 for a commonly reasonable default.</param>
        /// <returns>A <see cref="PipeReader"/>.</returns>
        public static PipeReader UseStrictPipeReader(this Stream stream, int sizeHint = DefaultReadBufferSize)
        {
            Requires.NotNull(stream, nameof(stream));
            Requires.Argument(stream.CanRead, nameof(stream), "Stream must be readable.");

            return new StreamPipeReader(stream, sizeHint);
        }

        /// <summary>
        /// Enables writing to a stream using <see cref="PipeWriter"/>.
        /// </summary>
        /// <param name="stream">The stream to write to using a pipe.</param>
        /// <param name="pipeOptions">Optional pipe options to use.</param>
        /// <param name="cancellationToken">A cancellation token that aborts writing to the <paramref name="stream"/>.</param>
        /// <returns>A <see cref="PipeWriter"/>.</returns>
        public static PipeWriter UsePipeWriter(this Stream stream, PipeOptions pipeOptions = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(stream, nameof(stream));
            Requires.Argument(stream.CanWrite, nameof(stream), "Stream must be writable.");

            var pipe = new Pipe(pipeOptions ?? PipeOptions.Default);
            Task.Run(async delegate
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        ReadResult readResult = await pipe.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                        if (readResult.Buffer.Length > 0)
                        {
                            foreach (ReadOnlyMemory<byte> segment in readResult.Buffer)
                            {
                                await stream.WriteAsync(segment, cancellationToken).ConfigureAwait(false);
                            }

                            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        }

                        pipe.Reader.AdvanceTo(readResult.Buffer.End);

                        if (readResult.IsCompleted)
                        {
                            break;
                        }
                    }

                    pipe.Reader.Complete();
                }
                catch (Exception ex)
                {
                    // Propagate the exception to the writer.
                    pipe.Reader.Complete(ex);
                    return;
                }
            }).Forget();
            return pipe.Writer;
        }
    }
}
