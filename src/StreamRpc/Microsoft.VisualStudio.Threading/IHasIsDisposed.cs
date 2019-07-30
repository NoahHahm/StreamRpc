// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace StreamRpc
{
    using System;

    /// <summary>
    /// A disposable object that also provides a safe way to query its disposed status.
    /// </summary>
    public interface IHasIsDisposed : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this instance has been disposed.
        /// </summary>
        bool IsDisposed { get; }
    }
}
