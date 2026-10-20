using System;

namespace Isko.Protection
{
    // The native interceptor becomes invalid after either terminal operation.
    public interface IRequestCompletion
    {
        string Url { get; }
        bool Complete(bool block);
    }

    public readonly struct CompletionResult
    {
        public bool BlockRequested { get; }
        public bool Completed { get; }
        public CompletionResult(bool blockRequested, bool completed)
        { BlockRequested = blockRequested; Completed = completed; }
    }

    public static class RequestCompletion
    {
        public static CompletionResult Apply<T>(T request, ProtectionPolicy policy, string site) where T : IRequestCompletion
        {
            if (request == null) throw new ArgumentNullException("request");
            if (policy == null) throw new ArgumentNullException("policy");
            bool block, completed;
            try { block = !policy.EvaluateRequest(site, request.Url, true).Allowed; }
            catch { block = false; }
            // Even a false return or exception must not trigger a second terminal call.
            try { completed = request.Complete(block); }
            catch { completed = false; }
            return new CompletionResult(block, completed);
        }
    }
}
