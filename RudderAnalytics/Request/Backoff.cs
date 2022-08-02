using System;
using System.Threading.Tasks;

namespace RudderStack.Request
{
    public class Backoff
    {
        private readonly int _min;
        private readonly byte _factor;
        private readonly ushort _jitter;
        private readonly int _max;
        private int _attempt;
        private int _currentAttemptTime;

        private const int IntMax = int.MaxValue - ushort.MaxValue;

        public bool HasReachedMax => _currentAttemptTime >= _max;
        public int CurrentAttempt => _attempt;

        public Backoff(int min = 100, int max = 10000, byte factor = 2, ushort jitter = 10000)
        {
            _min = min;
            _factor = factor;
            _jitter = jitter;
            _max = Math.Min(max, IntMax);
        }

        public Task AttemptAsync()
        {
            return Task.Delay(AttemptTime());
        }

        public Task AttemptForAsync(int attempt)
        {
            return Task.Delay(AttemptTimeFor(attempt));
        }

        public int AttemptTime()
        {
            return _currentAttemptTime = AttemptTimeFor(_attempt++);
        }

        public int AttemptTimeFor(int attempt)
        {
            var jitter = 0;
            if (_jitter > 0) jitter = new Random().Next(_jitter);
            var waitingTime = _min * Math.Pow(_factor, attempt);
            waitingTime = Math.Min(_max, waitingTime);
            return (int)waitingTime + jitter;
        }

        public void Reset()
        {

            _attempt = _currentAttemptTime = 0;
        }
    }
}