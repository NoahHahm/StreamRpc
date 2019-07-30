// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace StreamRpc
{
    using System;

    internal class Verify
    {
        public static void Operation(bool condition, string message = null)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        public static void NotNull<T>(T obj)
            where T : class
        {
            if (obj == null)
                throw new InvalidOperationException();
        }

        public static void NotDisposed(IHasIsDisposed instance, string message = null)
        {
            if (!instance.IsDisposed)
                return;
            throw new ObjectDisposedException(instance.GetType().FullName, message);
        }
    }
}