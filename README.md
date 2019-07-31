# StreamJsonRpc

[![NuGet package](https://img.shields.io/nuget/v/StreamRpc.svg)](https://nuget.org/packages/StreamRpc)
[![Build Status](https://ci.appveyor.com/api/projects/status/github/shana/StreamRpc)](https://ci.appveyor.com/project/shana/streamrpc)

StreamRpc is fork of [StreamJsonRpc](https://github.com/microsoft/vs-streamjsonrpc), a cross-platform, .NET portable library that implements the
[JSON-RPC][JSONRPC] wire protocol.

StreamRpc's goal is to be fast and minimize dependencies, so it uses [MessagePack-CSharp](https://github.com/neuecc/MessagePack-CSharp)
as the default message serializer, removes Newtonsoft.Json entirely, and internalizes the essentials of Nerdbank.Streams and other utility
libraries that StreamJsonRpc relies on, so it's small and as self-contained as possible.

It works over [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) or System.IO.Pipelines pipes, independent of the underlying transport.

Bonus features beyond the JSON-RPC spec include:

1. Request cancellation
1. .NET Events as notifications
1. Dynamic client proxy generation
1. Support for [compact binary serialization](doc/extensibility.md) (e.g. MessagePack)

Learn about the use cases for JSON-RPC and how to use this library from our [documentation](doc/index.md).

## Supported platforms

* .NET 4.7+
* .NET Core
