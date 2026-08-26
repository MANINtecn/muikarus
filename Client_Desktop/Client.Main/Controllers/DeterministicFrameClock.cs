namespace Client.Main.Controllers
{
    /// <summary>
    /// Generates a stable fixed-step timeline for frame budgeting and catch-up decisions.
    /// </summary>
    public sealed class DeterministicFrameClock
    {
        private readonly TimeSpan _fixedStep;
        private readonly int _maxStepsPerFrame;
        private readonly TimeSpan _maxAcceptedElapsed;
        private TimeSpan _accumulator;

        public readonly struct StepInfo
        {
            public StepInfo(int stepCount, TimeSpan rawElapsed, TimeSpan acceptedElapsed, TimeSpan simulatedElapsed, float interpolationAlpha)
            {
                StepCount = stepCount;
                RawElapsed = rawElapsed;
                AcceptedElapsed = acceptedElapsed;
                SimulatedElapsed = simulatedElapsed;
                InterpolationAlpha = interpolationAlpha;
            }

            public int StepCount { get; }
            public TimeSpan RawElapsed { get; }
            public TimeSpan AcceptedElapsed { get; }
            public TimeSpan SimulatedElapsed { get; }
            public float InterpolationAlpha { get; }
        }

        public DeterministicFrameClock(TimeSpan fixedStep, int maxStepsPerFrame, TimeSpan maxAcceptedElapsed)
        {
            _fixedStep = fixedStep <= TimeSpan.Zero ? TimeSpan.FromSeconds(1.0 / 60.0) : fixedStep;
            _maxStepsPerFrame = Math.Max(1, maxStepsPerFrame);
            _maxAcceptedElapsed = maxAcceptedElapsed <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(250) : maxAcceptedElapsed;
        }

        public StepInfo Advance(TimeSpan rawElapsed)
        {
            if (rawElapsed < TimeSpan.Zero)
                rawElapsed = TimeSpan.Zero;

            TimeSpan acceptedElapsed = rawElapsed;
            if (acceptedElapsed > _maxAcceptedElapsed)
                acceptedElapsed = _maxAcceptedElapsed;

            _accumulator += acceptedElapsed;

            int steps = 0;
            while (_accumulator >= _fixedStep && steps < _maxStepsPerFrame)
            {
                _accumulator -= _fixedStep;
                steps++;
            }

            if (steps == _maxStepsPerFrame && _accumulator >= _fixedStep)
            {
                // Drop excessive backlog to keep runtime responsive after long stalls.
                _accumulator = TimeSpan.Zero;
            }

            TimeSpan simulatedElapsed = TimeSpan.FromTicks(_fixedStep.Ticks * steps);
            float alpha = _fixedStep.Ticks > 0
                ? (float)(_accumulator.Ticks / (double)_fixedStep.Ticks)
                : 0f;

            return new StepInfo(steps, rawElapsed, acceptedElapsed, simulatedElapsed, alpha);
        }
    }
}
