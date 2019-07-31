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
        /// Enables reading and writing to a <see cref="Stream"/> using <see cref="PipeWriter"/> and <see cref="PipeReader"/>.
        /// </summary>
        /// <param name="stream">The stream to access using a pipe.</param>
        /// <param name="sizeHint">A hint at the size of messages that are commonly transferred. Use 0 for a commonly reasonable default.</param>
        /// <param name="pipeOptions">Optional pipe options to use.</param>
        /// <param name="cancellationToken">A token that may cancel async processes to read from and write to the <paramref name="stream"/>.</param>
        /// <returns>An <see cref="IDuplexPipe"/> instance.</returns>
        public static IDuplexPipe UsePipe(this Stream stream, int sizeHint = 0, PipeOptions pipeOptions = null, CancellationToken cancellationToken = default)
        {
            PipeReader input = stream.UsePipeReader(sizeHint, pipeOptions, cancellationToken);
            PipeWriter output = stream.UsePipeWriter(pipeOptions, cancellationToken);

            // Arrange for closing the stream when *both* input/output are completed.
            Task ioCompleted = Task.WhenAll(input.WaitForWriterCompletionAsync(), output.WaitForReaderCompletionAsync());
            ioCompleted.ContinueWith((_, state) => ((Stream)state).Dispose(), stream, cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default).Forget();

            return new DuplexPipe(input, output);
        }

        /// <summary>
        /// Enables efficiently reading a stream using <see cref="PipeReader"/>.
        /// </summary>
        /// <param name="stream">The stream to read from using a pipe.</param>
        /// <param name="sizeHint">A hint at the size of messages that are commonly transferred. Use 0 for a commonly reasonable default.</param>
        /// <param name="pipeOptions">Optional pipe options to use.</param>
        /// <param name="cancellationToken">A cancellation token that aborts reading from the <paramref name="stream"/>.</param>
        /// <returns>A <see cref="PipeReader"/>.</returns>
        /// <remarks>
        /// When the caller invokes <see cref="PipeReader.Complete(Exception)"/> on the result value,
        /// this leads to the associated <see cref="PipeWriter.Complete(Exception)"/> to be automatically called as well.
        /// </remarks>
        public static PipeReader UsePipeReader(this Stream stream, int sizeHint = 0, PipeOptions pipeOptions = null, CancellationToken cancellationToken = default)
        {
            Requires.NotNull(stream, nameof(stream));
            Requires.Argument(stream.CanRead, nameof(stream), "Stream must be readable.");

            var pipe = new Pipe(pipeOptions ?? PipeOptions.Default);

            // Notice when the pipe reader isn't listening any more, and terminate our loop that reads from the stream.
            var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pipe.Writer.OnReaderCompleted((ex, state) => ((CancellationTokenSource)state).Cancel(), combinedTokenSource);

            Task.Run(async delegate
            {
                while (!combinedTokenSource.Token.IsCancellationRequested)
                {
                    Memory<byte> memory = pipe.Writer.GetMemory(sizeHint);
                    try
                    {
                        int bytesRead = await stream.ReadAsync(memory, combinedTokenSource.Token).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        pipe.Writer.Advance(bytesRead);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Propagate the exception to the reader.
                        pipe.Writer.Complete(ex);
                        return;
                    }

                    FlushResult result = await pipe.Writer.FlushAsync().ConfigureAwait(false);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                // Tell the PipeReader that there's no more data coming
                pipe.Writer.Complete();
            }).Forget();
            return pipe.Reader;
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

        internal static Task WaitForReaderCompletionAsync(this PipeWriter writer)
        {
            Requires.NotNull(writer, nameof(writer));

            var readerDone = new TaskCompletionSource<object>();
            writer.OnReaderCompleted(
                (ex, tcsObject) =>
                {
                    var tcs = (TaskCompletionSource<object>)tcsObject;
                    if (ex != null)
                    {
                        tcs.SetException(ex);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                },
                readerDone);
            return readerDone.Task;
        }

        internal static Task WaitForWriterCompletionAsync(this PipeReader reader)
        {
            Requires.NotNull(reader, nameof(reader));

            var writerDone = new TaskCompletionSource<object>();
            reader.OnWriterCompleted(
                (ex, wdObject) =>
                {
                    var wd = (TaskCompletionSource<object>)wdObject;
                    if (ex != null)
                    {
                        wd.SetException(ex);
                    }
                    else
                    {
                        wd.SetResult(null);
                    }
                },
                writerDone);
            return writerDone.Task;
        }
    }
}
