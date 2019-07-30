// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using StreamRpc;
using StreamRpc.Protocol;

public class DirectMessageHandler : MessageHandlerBase
{
    public DirectMessageHandler()
        : base(new MessagePackFormatter())
    {
    }

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public new MessagePackFormatter Formatter => (MessagePackFormatter)base.Formatter;

    internal AsyncQueue<ReadOnlySequence<byte>> MessagesToRead { get; } = new AsyncQueue<ReadOnlySequence<byte>>();

    internal AsyncQueue<ReadOnlySequence<byte>> WrittenMessages { get; } = new AsyncQueue<ReadOnlySequence<byte>>();

    protected override async ValueTask<JsonRpcMessage> ReadCoreAsync(CancellationToken cancellationToken)
    {
        return this.Formatter.Deserialize(await this.MessagesToRead.DequeueAsync(cancellationToken));
    }

    protected override ValueTask WriteCoreAsync(JsonRpcMessage content, CancellationToken cancellationToken)
    {
        var builder = new Sequence<byte>();
        this.Formatter.Serialize(builder, content);
        this.WrittenMessages.Enqueue(builder);
        return default;
    }

    protected override ValueTask FlushAsync(CancellationToken cancellationToken) => default;
}

public class DirectJsonMessageHandler : MessageHandlerBase
{
    public DirectJsonMessageHandler()
        : base(new JsonMessageFormatter())
    {
    }

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public new JsonMessageFormatter Formatter => (JsonMessageFormatter)base.Formatter;

    internal AsyncQueue<JToken> MessagesToRead { get; } = new AsyncQueue<JToken>();

    internal AsyncQueue<JToken> WrittenMessages { get; } = new AsyncQueue<JToken>();

    protected override async ValueTask<JsonRpcMessage> ReadCoreAsync(CancellationToken cancellationToken)
    {
        return this.Formatter.Deserialize(await this.MessagesToRead.DequeueAsync(cancellationToken));
    }

    protected override ValueTask WriteCoreAsync(JsonRpcMessage content, CancellationToken cancellationToken)
    {
        this.WrittenMessages.Enqueue(this.Formatter.Serialize(content));
        return default;
    }

    protected override ValueTask FlushAsync(CancellationToken cancellationToken) => default;
}