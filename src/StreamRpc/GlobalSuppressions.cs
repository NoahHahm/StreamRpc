// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0016:Add public types and members to the declared API", Justification = "Localization strings", Scope = "type", Target = "~T:StreamJsonRpc.Resources")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "-", Scope = "type", Target = "~T:StreamRpc.AwaitExtensions.TaskSchedulerAwaitable")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD103:Call async methods when in an async method", Justification = "-", Scope = "member", Target = "~M:StreamRpc.AsyncSemaphore.LockWaitingHelper(System.Threading.Tasks.Task{System.Boolean})~System.Threading.Tasks.Task{StreamRpc.AsyncSemaphore.Releaser}")]

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "-", Scope = "member", Target = "~M:StreamRpc.AsyncSemaphore.EnterAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task{StreamRpc.AsyncSemaphore.Releaser}")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "-", Scope = "member", Target = "~M:StreamRpc.AsyncSemaphore.EnterAsync(System.TimeSpan,System.Threading.CancellationToken)~System.Threading.Tasks.Task{StreamRpc.AsyncSemaphore.Releaser}")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003:Avoid awaiting foreign Tasks", Justification = "-", Scope = "member", Target = "~M:StreamRpc.AsyncSemaphore.EnterAsync(System.Int32,System.Threading.CancellationToken)~System.Threading.Tasks.Task{StreamRpc.AsyncSemaphore.Releaser}")]