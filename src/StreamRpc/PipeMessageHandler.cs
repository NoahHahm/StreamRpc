﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace StreamRpc
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Pipelines;
    using System.Threading;
    using System.Threading.Tasks;
    using Protocol;

    /// <summary>
    /// An abstract base class for for sending and receiving messages
    /// using <see cref="PipeReader"/> and <see cref="PipeWriter"/>.
    /// </summary>
    public abstract class PipeMessageHandler : MessageHandlerBase
    {
        /// <summary>
        /// The largest size of a message to buffer completely before deserialization begins
        /// when we have an async deserializing alternative from the formatter.
        /// </summary>
        /// <remarks>
        /// This value is chosen to match the default buffer size for the <see cref="PipeOptions"/> class
        /// since exceeding the <see cref="PipeOptions.PauseWriterThreshold"/> would cause an exception
        /// when we call <see cref="PipeReader.AdvanceTo(SequencePosition, SequencePosition)"/> to wait for more data.
        /// </remarks>
        private static readonly long LargeMessageThreshold = new PipeOptions().PauseWriterThreshold;

        /// <summary>
        /// Objects that we should dispose when we are disposed. May be null.
        /// </summary>
        private List<IDisposable> disposables;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeMessageHandler"/> class.
        /// </summary>
        /// <param name="pipe">The reader and writer to use for receiving/transmitting messages.</param>
        /// <param name="formatter">The formatter used to serialize messages.</param>
        public PipeMessageHandler(IDuplexPipe pipe, IJsonRpcMessageFormatter formatter)
            : this(Requires.NotNull(pipe, nameof(pipe)).Output, Requires.NotNull(pipe, nameof(pipe)).Input, formatter)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeMessageHandler"/> class.
        /// </summary>
        /// <param name="writer">The writer to use for transmitting messages.</param>
        /// <param name="reader">The reader to use for receiving messages.</param>
        /// <param name="formatter">The formatter used to serialize messages.</param>
        public PipeMessageHandler(PipeWriter writer, PipeReader reader, IJsonRpcMessageFormatter formatter)
            : base(formatter)
        {
            this.Reader = reader;
            this.Writer = writer;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeMessageHandler"/> class.
        /// </summary>
        /// <param name="writer">The stream to use for transmitting messages.</param>
        /// <param name="reader">The stream to use for receiving messages.</param>
        /// <param name="formatter">The formatter used to serialize messages.</param>
        public PipeMessageHandler(Stream writer, Stream reader, IJsonRpcMessageFormatter formatter)
            : base(formatter)
        {
            // We use Strict reader to avoid max buffer size issues in Pipe (https://github.com/dotnet/corefx/issues/30689)
            // since it's just stream semantics.
            this.Reader = reader?.UseStrictPipeReader();
            this.Writer = writer?.UsePipeWriter();

            this.disposables = new List<IDisposable>();
            if (reader != null)
            {
                this.disposables.Add(reader);
            }

            if (writer != null && writer != reader)
            {
                this.disposables.Add(writer);
            }
        }

        /// <inheritdoc/>
        public override bool CanRead => this.Reader != null;

        /// <inheritdoc/>
        public override bool CanWrite => this.Writer != null;

        /// <summary>
        /// Gets the reader to use for receiving messages.
        /// </summary>
        protected PipeReader Reader { get; }

        /// <summary>
        /// Gets the writer to use for transmitting messages.
        /// </summary>
        protected PipeWriter Writer { get; }

#pragma warning disable AvoidAsyncSuffix // Avoid Async suffix
        /// <inheritdoc/>
        protected sealed override ValueTask WriteCoreAsync(JsonRpcMessage content, CancellationToken cancellationToken)
        {
            this.Write(content, cancellationToken);
            return default;
        }
#pragma warning restore AvoidAsyncSuffix // Avoid Async suffix

        /// <summary>
        /// Writes a message to the pipe.
        /// </summary>
        /// <param name="content">The message to write.</param>
        /// <param name="cancellationToken">A token to cancel the transmission.</param>
        /// <remarks>
        /// Implementations may assume the method is never called before the previous call has completed.
        /// They can assume their caller will invoke <see cref="PipeWriter.FlushAsync(CancellationToken)"/> on their behalf
        /// after writing is completed.
        /// </remarks>
        protected abstract void Write(JsonRpcMessage content, CancellationToken cancellationToken);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Reader?.Complete();
                this.Writer?.Complete();

                if (this.disposables != null)
                {
                    // Only dispose the underlying streams (if any) *after* our writer's work has been fully read.
                    // Otherwise we risk cutting of data that we claimed to have transmitted.
                    if (this.Writer != null && this.disposables != null)
                    {
                        this.Writer.OnReaderCompleted((ex, s) => this.DisposeDisposables(), null);
                    }
                    else
                    {
                        this.DisposeDisposables();
                    }
                }

                base.Dispose(disposing);
            }
        }

        /// <inheritdoc />
        protected override async ValueTask FlushAsync(CancellationToken cancellationToken) => await this.Writer.FlushAsync(cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Reads from the <see cref="Reader"/> until at least a specified number of bytes are available.
        /// </summary>
        /// <param name="requiredBytes">The number of bytes that must be available.</param>
        /// <param name="allowEmpty"><c>true</c> to allow returning 0 bytes if the end of the stream is encountered before any bytes are read.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The <see cref="ReadResult"/> containing at least <paramref name="requiredBytes"/> bytes.</returns>
        /// <exception cref="OperationCanceledException">Thrown if <see cref="ReadResult.IsCanceled"/>.</exception>
        /// <exception cref="EndOfStreamException">
        /// Thrown if <see cref="ReadResult.IsCompleted"/> before we have <paramref name="requiredBytes"/> bytes.
        /// Not thrown if 0 bytes were read and <paramref name="allowEmpty"/> is <c>true</c>.
        /// </exception>
        protected async ValueTask<ReadResult> ReadAtLeastAsync(int requiredBytes, bool allowEmpty, CancellationToken cancellationToken)
        {
            var readResult = await this.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            while (readResult.Buffer.Length < requiredBytes && !readResult.IsCompleted && !readResult.IsCanceled)
            {
                this.Reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
                readResult = await this.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }

            if (allowEmpty && readResult.Buffer.Length == 0)
            {
                return readResult;
            }

            if (readResult.Buffer.Length < requiredBytes)
            {
                throw readResult.IsCompleted ? new EndOfStreamException() :
                    readResult.IsCanceled ? new OperationCanceledException() :
                    (Exception)new InvalidOperationException();
            }

            return readResult;
        }

        /// <summary>
        /// Deserializes a JSON-RPC message using the <see cref="MessageHandlerBase.Formatter"/>.
        /// </summary>
        /// <param name="contentLength">The length of the JSON-RPC message.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The deserialized message.</returns>
        private protected ValueTask<JsonRpcMessage> DeserializeMessageAsync(int contentLength, CancellationToken cancellationToken) => this.DeserializeMessageAsync(contentLength, null, null, cancellationToken);

        /// <summary>
        /// Deserializes a JSON-RPC message using the <see cref="MessageHandlerBase.Formatter"/>.
        /// </summary>
        /// <param name="contentLength">The length of the JSON-RPC message.</param>
        /// <param name="specificEncoding">The encoding to use during deserialization, as specified in a header for this particular message.</param>
        /// <param name="defaultEncoding">The encoding to use when <paramref name="specificEncoding"/> is <c>null</c> if the <see cref="MessageHandlerBase.Formatter"/> supports encoding.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The deserialized message.</returns>
        /// <exception cref="NotSupportedException">Thrown if <paramref name="specificEncoding"/> is non-null and the formatter does not implement the appropriate interface to supply the encoding.</exception>
        private protected async ValueTask<JsonRpcMessage> DeserializeMessageAsync(int contentLength, Encoding specificEncoding, Encoding defaultEncoding, CancellationToken cancellationToken)
        {
            Requires.Range(contentLength > 0, nameof(contentLength));
            Encoding contentEncoding = specificEncoding ?? defaultEncoding;

            // Being async during deserialization increases GC pressure,
            // so prefer getting all bytes into a buffer first if the message is a reasonably small size.
            if (contentLength >= LargeMessageThreshold && this.Formatter is IJsonRpcAsyncMessageFormatter asyncFormatter)
            {
                var slice = this.Reader.ReadSlice(contentLength);
                if (contentEncoding != null && asyncFormatter is IJsonRpcAsyncMessageTextFormatter asyncTextFormatter)
                {
                    return await asyncTextFormatter.DeserializeAsync(slice, contentEncoding, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    if (specificEncoding != null)
                    {
                        this.ThrowNoTextEncoder();
                    }

                    return await asyncFormatter.DeserializeAsync(slice, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                var readResult = await this.ReadAtLeastAsync(contentLength, allowEmpty: false, cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> contentBuffer = readResult.Buffer.Slice(0, contentLength);
                try
                {
                    if (contentEncoding != null && this.Formatter is IJsonRpcMessageTextFormatter textFormatter)
                    {
                        return textFormatter.Deserialize(contentBuffer, contentEncoding);
                    }
                    else
                    {
                        if (specificEncoding != null)
                        {
                            this.ThrowNoTextEncoder();
                        }

                        return this.Formatter.Deserialize(contentBuffer);
                    }
                }
                finally
                {
                    // We're now done reading from the pipe's buffer. We can release it now.
                    this.Reader.AdvanceTo(contentBuffer.End);
                }
            }
        }

        private protected Exception ThrowNoTextEncoder()
        {
            throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resources.TextEncoderNotApplicable, this.Formatter.GetType().FullName, typeof(IJsonRpcMessageTextFormatter).FullName));
        }

        private void DisposeDisposables()
        {
            if (this.disposables != null)
            {
                foreach (IDisposable disposable in this.disposables)
                {
                    disposable?.Dispose();
                }

                this.disposables = null;
            }
        }
    }
}
