// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace StreamRpc
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A collection of extension methods to support special awaiters.
    /// </summary>
    internal static class AwaitExtensions
    {
        /// <summary>
        /// Consumes a task and doesn't do anything with it.  Useful for fire-and-forget calls to async methods within async methods.
        /// </summary>
        public static void Forget(this Task task)
        {
        }

        /// <summary>
        /// Gets an awaitable that schedules continuations on the specified scheduler.
        /// </summary>
        /// <param name="scheduler">The task scheduler used to execute continuations.</param>
        /// <param name="alwaysYield">A value indicating whether the caller should yield even if
        /// already executing on the desired task scheduler.</param>
        /// <returns>An awaitable.</returns>
        public static TaskSchedulerAwaitable SwitchTo(this TaskScheduler scheduler, bool alwaysYield = false)
        {
            Requires.NotNull(scheduler, nameof(scheduler));
            return new TaskSchedulerAwaitable(scheduler, alwaysYield);
        }

        /// <summary>
        /// Gets an awaiter that switches the caller to execute on the specified <see cref="SynchronizationContext"/>.
        /// </summary>
        /// <param name="synchronizationContext">The <see cref="SynchronizationContext"/> to switch to.</param>
        /// <returns>The value to await on.</returns>
        internal static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext synchronizationContext)
        {
            Requires.NotNull(synchronizationContext, nameof(synchronizationContext));
            return new SynchronizationContextAwaiter(synchronizationContext);
        }

        /// <summary>
        /// The awaiter for <see cref="SynchronizationContext"/>.
        /// </summary>
        internal struct SynchronizationContextAwaiter : INotifyCompletion
        {
            /// <summary>
            /// The <see cref="SynchronizationContext"/> to switch the caller's context to.
            /// </summary>
            private readonly SynchronizationContext synchronizationContext;

            /// <summary>
            /// Initializes a new instance of the <see cref="SynchronizationContextAwaiter"/> struct.
            /// </summary>
            /// <param name="synchronizationContext">The <see cref="SynchronizationContext"/> to switch the caller's context to.</param>
            internal SynchronizationContextAwaiter(SynchronizationContext synchronizationContext)
            {
                Requires.NotNull(synchronizationContext, nameof(synchronizationContext));
                this.synchronizationContext = synchronizationContext;
            }

            /// <summary>
            /// Gets a value indicating whether the caller is already on the desired context.
            /// </summary>
            /// <remarks>
            /// We always return <c>false</c> because we use this to invoke server methods and we *always* want to
            /// yield before invoking them, even if this is the default SynchronizationContext that the caller is on.
            /// </remarks>
            public bool IsCompleted => false;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void GetResult()
            {
            }

            /// <summary>
            /// Schedules a continuation on the <see cref="SynchronizationContext"/> specified in the constructor.
            /// </summary>
            /// <param name="continuation">The delegate to execute on the <see cref="SynchronizationContext"/>.</param>
            public void OnCompleted(Action continuation)
            {
#pragma warning disable VSTHRD001 // Avoid legacy threading switching APIs
                this.synchronizationContext.Post(action => ((Action)action).Invoke(), continuation);
#pragma warning restore VSTHRD001 // Avoid legacy threading switching APIs
            }
        }

        /// <summary>
        /// An awaitable that executes continuations on the specified task scheduler.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "Don't care")]
        public readonly struct TaskSchedulerAwaitable
        {
            /// <summary>
            /// The scheduler for continuations.
            /// </summary>
            private readonly TaskScheduler taskScheduler;

            /// <summary>
            /// A value indicating whether the awaitable will always call the caller to yield.
            /// </summary>
            private readonly bool alwaysYield;

            /// <summary>
            /// Initializes a new instance of the <see cref="TaskSchedulerAwaitable"/> struct.
            /// </summary>
            /// <param name="taskScheduler">The task scheduler used to execute continuations.</param>
            /// <param name="alwaysYield">A value indicating whether the caller should yield even if
            /// already executing on the desired task scheduler.</param>
            public TaskSchedulerAwaitable(TaskScheduler taskScheduler, bool alwaysYield = false)
            {
                Requires.NotNull(taskScheduler, nameof(taskScheduler));

                this.taskScheduler = taskScheduler;
                this.alwaysYield = alwaysYield;
            }

            /// <summary>
            /// Gets an awaitable that schedules continuations on the specified scheduler.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate", Justification = "Don't care")]
            public TaskSchedulerAwaiter GetAwaiter()
            {
                return new TaskSchedulerAwaiter(this.taskScheduler, this.alwaysYield);
            }
        }

        /// <summary>
        /// An awaiter returned from <see cref="TaskSchedulerAwaitable.GetAwaiter"/>.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "Don't care")]
        public readonly struct TaskSchedulerAwaiter : ICriticalNotifyCompletion
        {
            /// <summary>
            /// The scheduler for continuations.
            /// </summary>
            private readonly TaskScheduler scheduler;

            /// <summary>
            /// A value indicating whether <see cref="IsCompleted"/>
            /// should always return false.
            /// </summary>
            private readonly bool alwaysYield;

            /// <summary>
            /// Initializes a new instance of the <see cref="TaskSchedulerAwaiter"/> struct.
            /// </summary>
            /// <param name="scheduler">The scheduler for continuations.</param>
            /// <param name="alwaysYield">A value indicating whether the caller should yield even if
            /// already executing on the desired task scheduler.</param>
            public TaskSchedulerAwaiter(TaskScheduler scheduler, bool alwaysYield = false)
            {
                this.scheduler = scheduler;
                this.alwaysYield = alwaysYield;
            }

            /// <summary>
            /// Gets a value indicating whether no yield is necessary.
            /// </summary>
            /// <value><c>true</c> if the caller is already running on that TaskScheduler.</value>
            public bool IsCompleted
            {
                get
                {
                    if (this.alwaysYield)
                    {
                        return false;
                    }

                    // We special case the TaskScheduler.Default since that is semantically equivalent to being
                    // on a ThreadPool thread, and there are various ways to get on those threads.
                    // TaskScheduler.Current is never null.  Even if no scheduler is really active and the current
                    // thread is not a threadpool thread, TaskScheduler.Current == TaskScheduler.Default, so we have
                    // to protect against that case too.
                    bool isThreadPoolThread = Thread.CurrentThread.IsThreadPoolThread;
                    return (this.scheduler == TaskScheduler.Default && isThreadPoolThread)
                        || (this.scheduler == TaskScheduler.Current && TaskScheduler.Current != TaskScheduler.Default);
                }
            }

            /// <summary>
            /// Schedules a continuation to execute using the specified task scheduler.
            /// </summary>
            /// <param name="continuation">The delegate to invoke.</param>
            public void OnCompleted(Action continuation)
            {
                if (this.scheduler == TaskScheduler.Default)
                {
                    ThreadPool.QueueUserWorkItem(state => ((Action)state)(), continuation);
                }
                else
                {
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.None, this.scheduler).Forget();
                }
            }

            /// <summary>
            /// Schedules a continuation to execute using the specified task scheduler
            /// without capturing the ExecutionContext.
            /// </summary>
            /// <param name="continuation">The action.</param>
            public void UnsafeOnCompleted(Action continuation)
            {
                if (this.scheduler == TaskScheduler.Default)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(state => ((Action)state)(), continuation);
                }
                else
                {
                    // There is no API for scheduling a Task without capturing the ExecutionContext.
                    Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.None, this.scheduler).Forget();
                }
            }

            /// <summary>
            /// Does nothing.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Don't care")]
            public void GetResult()
            {
            }
        }
    }
}
