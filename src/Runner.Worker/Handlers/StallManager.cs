using System;
using System.Timers;
using GitHub.Runner.Common;
using GitHub.Runner.Sdk;

namespace GitHub.Runner.Worker.Handlers
{
    [ServiceLocator(Default = typeof(TimerAdapter))]
    public interface ITimer 
    {
        void Start();
        void Stop();
        double Interval { get; set; }
        event ElapsedEventHandler Elapsed;
        bool AutoReset { get; set; }
        void Dispose();
    }

    public class TimerAdapter : Timer, ITimer {}

    public sealed class StallManager: IDisposable
    {
        public static TimeSpan DefaultStallInterval = TimeSpan.FromMinutes(30); 
     
        private readonly IExecutionContext _executionContext;
        private readonly double _interval;

        private ITimer _timer { get; set; }
        private int _intervalsElapsedWhileStalled = 0;
        private bool _stallDetectEnabled = false;

        public StallManager(IExecutionContext executionContext, double interval, ITimer timer)
        {
            _stallDetectEnabled = FeatureManager.IsStallDetectEnabled(_executionContext?.Global?.Variables);
            if (_stallDetectEnabled) {
                _executionContext = executionContext;
                _interval = interval;
                _timer = timer;

                _timer.Interval = _interval;
                _timer.Elapsed += TriggerWarning;
            }
        }
        public StallManager(IExecutionContext executionContext, double interval): this(executionContext, interval, new TimerAdapter()) {}
        public StallManager(IExecutionContext executionContext): this(executionContext, StallManager.DefaultStallInterval.TotalMilliseconds) {}

        public void Initialize() {
            if (_stallDetectEnabled)
            {
                this.OnDataReceived(null, null);
            }
        }

        public void Dispose() {
            if (!_stallDetectEnabled) return;

            try 
            {
                _timer.Dispose();
            } 
            catch {}
        }

        public void OnDataReceived(object sender, ProcessDataReceivedEventArgs e)
        {
            if (!_stallDetectEnabled) return;

            _intervalsElapsedWhileStalled = 0;
            _timer.Stop();
            _timer.Start();
        }

        private void TriggerWarning(object source, ElapsedEventArgs e)
        {
            if (!_stallDetectEnabled) return;

            _intervalsElapsedWhileStalled++;
            _executionContext.Warning($"No output has been detected in the last {TimeSpan.FromMilliseconds(_intervalsElapsedWhileStalled * _interval).TotalMinutes} minutes and the process has not yet exited. This step may have stalled and might require some investigation.");
        }
    }
}