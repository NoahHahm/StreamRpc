// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft;
using Newtonsoft.Json.Linq;
using StreamRpc;
using Xunit.Abstractions;

public class InteropTestBase : TestBase
{
    protected readonly DirectJsonMessageHandler messageHandler;
    protected readonly JsonRpc rpc;

    public InteropTestBase(ITestOutputHelper logger, bool serverTest)
        : base(logger)
    {
        this.messageHandler = new DirectJsonMessageHandler();
    }

    protected ValueTask<JToken> RequestAsync(object request)
    {
        this.Send(request);
        return this.ReceiveAsync();
    }

    protected void Send(dynamic message)
    {
        Requires.NotNull(message, nameof(message));

        var json = JToken.FromObject(message);
        this.messageHandler.MessagesToRead.Enqueue(json);
    }

    protected async ValueTask<JToken> ReceiveAsync()
    {
        JToken json = await this.messageHandler.WrittenMessages.DequeueAsync(this.TimeoutToken);
        return json;
    }
}
