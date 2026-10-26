using System.Collections.Generic;

namespace XelLauncher.Helpers
{
    public sealed class SignBatchResult
    {
        public List<string> Messages { get; } = new();
        public int Succeeded { get; private set; }
        public int Failed { get; private set; }
        public bool Success => Succeeded > 0 && Failed == 0;

        public void Add(bool success, string message)
        {
            if (success) Succeeded++;
            else Failed++;
            Messages.Add(message);
        }

        public void Merge(SignBatchResult result, string prefix)
        {
            Succeeded += result.Succeeded;
            Failed += result.Failed;
            foreach (var message in result.Messages) Messages.Add(prefix + message);
        }

        public static SignBatchResult Single(bool success, string message)
        {
            var result = new SignBatchResult();
            result.Add(success, message);
            return result;
        }
    }
}
