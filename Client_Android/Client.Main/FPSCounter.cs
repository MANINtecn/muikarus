using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Org.BouncyCastle.Asn1.Cmp.Challenge;

namespace Client.Main
{
    public class FPSCounter
    {
        public static FPSCounter Instance { get; private set; } = new FPSCounter();

        private bool _timeInit = false;
        private double _startTime = 0;
        private double _lastTime = 0;
        private int _frameCount = 0;

        public double WorldTime { get; private set; }
        public double FPS { get; private set; }
        public double FPS_AVG { get; private set; }
        public float FPS_ANIMATION_FACTOR { get; private set; }

        // --- NEW METRICS FOR PROFILING ---
        public static int DrawCalls { get; set; } = 0;
        public int LastFrameDrawCalls { get; private set; } = 0;
        public long GCMemory { get; private set; } = 0;

        private const float REFERENCE_FPS = 25f;

        public bool RandFPSCheck(int referenceFrames)
        {
            double animationFactor = Math.Min(1.0, (double)FPS_ANIMATION_FACTOR);

            double randValue = MuGame.Random.NextDouble();

            double chance = referenceFrames == 1
                ? animationFactor
                : (1.0 / referenceFrames) * animationFactor;
            
            return randValue <= chance;
        }

        public void CalcFPS(GameTime gameTime)
        {
            if (!_timeInit)
            {
                _startTime = gameTime.TotalGameTime.TotalMilliseconds;
                _timeInit = true;
            }

            _frameCount++;
            WorldTime = gameTime.TotalGameTime.TotalMilliseconds;

            double differenceMs = WorldTime - _lastTime;
            if (differenceMs <= 0)
            {
                FPS = 0.01;
            }
            else
            {
                FPS = 1000 / differenceMs;
            }

            FPS_ANIMATION_FACTOR = Math.Min((float)(REFERENCE_FPS / FPS), 2.5f);

            double diffSinceStart = WorldTime - _startTime;
            if (diffSinceStart > 2000.0 || _frameCount > 25)
            {
                FPS_AVG = (1000.0 * _frameCount) / diffSinceStart;
                _startTime = WorldTime;
                _frameCount = 0;
            }

            _lastTime = WorldTime;

            // Profile metrics
            LastFrameDrawCalls = DrawCalls;
            DrawCalls = 0; // Reset for the next frame
            GCMemory = GC.GetTotalMemory(false) / 1024; // KB
        }
    }

}
