// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace StreamRpc
{
    using System;

    /// <summary>
    /// Describes the reason behind a disconnection with the remote party.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class JsonRpcDisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcDisconnectedEventArgs"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="reason">The reason for disconnection.</param>
        public JsonRpcDisconnectedEventArgs(string description, DisconnectedReason reason)
            : this(description, reason, exception: null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcDisconnectedEventArgs"/> class.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <param name="reason">The reason for disconnection.</param>
        /// <param name="exception">The exception.</param>
        public JsonRpcDisconnectedEventArgs(string description, DisconnectedReason reason, Exception exception)
        {
            Requires.NotNullOrEmpty(description, nameof(description));

            this.Description = description;
            this.Reason = reason;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets the reason.
        /// </summary>
        public DisconnectedReason Reason { get; }

        /// <summary>
        /// Gets the exception.
        /// </summary>
        public Exception Exception { get; }
    }
}
