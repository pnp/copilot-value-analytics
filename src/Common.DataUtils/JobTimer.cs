using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Common.DataUtils
{
    /// <summary>
    /// For tracking the time something takes
    /// </summary>
    public class JobTimer
    {
        private readonly ILogger _tracer;
        private readonly string _operationName;
        private readonly Stopwatch _sw;

        public JobTimer(ILogger tracer, string operationName)
        {
            _tracer = tracer;
            _operationName = operationName;
            _sw = new Stopwatch();
        }
        public void Start()
        {
            _tracer.LogDebug($"Starting {_operationName}");
            _sw.Start();
        }

        public TimeSpan Elapsed => _sw.Elapsed;
        public string OperationName => _operationName;

        public override string ToString()
        {
            var timeTaken = TimeSpan.FromMilliseconds(_sw.ElapsedMilliseconds);
            return $"{_operationName}: {timeTaken.Hours} hours, {timeTaken.Minutes} mins, and {timeTaken.Seconds} seconds.";
        }

        public string PrintElapsed()
        {
            var s = ToString();
            _tracer.LogInformation(s);
            return s;
        }
        public string StopAndPrintElapsed()
        {
            _sw.Stop();

            var s = ToString();
            _tracer.LogInformation(s);
            _sw.Reset();
            return s;
        }

        public void TrackFinishedEventAndStopTimer(AnalyticsEvent analyticsEvent)
        {
            var context = new Dictionary<string, string>
            {
                { "context", StopAndPrintElapsed() }
            };
            _tracer.LogTrace(Enum.GetName<AnalyticsEvent>(analyticsEvent), context);
        }
    }



    public enum AnalyticsEvent
    {
        Unknown,
        AzureAIQuery,
        FinishedSectionImport,
        FinishedImportCycle
    }
}
