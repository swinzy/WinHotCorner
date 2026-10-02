using System;
using System.Collections.Generic;

namespace WinHotCorner
{
    /// <summary>
    /// Decides when pushing against a hot corner is hard enough to trigger it.
    /// </summary>
    /// <remarks>
    /// A port of GNOME Shell's PressureBarrier (js/ui/layout.js), so it feels the same as on GNOME.
    /// The corner has two barriers, one along the left edge and one along the top edge, each as long as GNOME's
    /// top bar is tall. Pushing out through a barrier adds pressure; pressure older than one second is forgotten,
    /// so it takes a push, not a slow drift with pauses. After triggering, it waits until the pointer has left
    /// both barriers before it can trigger again.
    ///
    /// When a barrier counts as hit or left follows mutter (src/backends/native/meta-barrier-native.c): once hit,
    /// a barrier stays held while the pointer is within a 2 px wide hit box along it, so the small recoil after a
    /// push does not count as leaving.
    ///
    /// GNOME's numbers are in logical pixels; they are scaled by the monitor's display scale here because the
    /// mouse hook reports physical pixels.
    /// </remarks>
    internal class PressureBarrier
    {
        /// <summary>
        /// Length of each barrier from the corner, in logical pixels.
        /// GNOME uses the height of its top bar (2.2em, about 32 px)
        /// </summary>
        private const double BARRIER_SIZE = 32;

        /// <summary>
        /// Pressure older than this is forgotten, in milliseconds (HOT_CORNER_PRESSURE_TIMEOUT)
        /// </summary>
        private const uint TIMEOUT = 1000;

        /// <summary>
        /// Most pressure a single event can add, in logical pixels
        /// </summary>
        private const double MAX_EVENT_PRESSURE = 15;

        /// <summary>
        /// Width of the hit box beside a held barrier, in logical pixels (mutter's calculate_barrier_hit_box).
        /// The pointer has to move at least this far away from the barrier to leave it
        /// </summary>
        private const double HIT_BOX = 2;

        private struct PressureEvent
        {
            public uint Time;
            public double Distance;
        }

        public HotCorner Corner { get; }

        /// <summary>
        /// The pointer is currently held by at least one of the barriers
        /// </summary>
        public bool IsHit => _verticalHit || _horizontalHit;

        private readonly int _size;
        private readonly int _hitBox;
        private readonly Queue<PressureEvent> _events = new Queue<PressureEvent>();
        private double _pressure = 0;
        private uint _lastTime = 0;
        private bool _triggered = false;
        private bool _verticalHit = false;
        private bool _horizontalHit = false;

        public PressureBarrier(HotCorner corner)
        {
            Corner = corner;
            _size = (int)Math.Round(BARRIER_SIZE * corner.Scale);
            _hitBox = Math.Max(1, (int)Math.Round(HIT_BOX * corner.Scale));
        }

        /// <summary>
        /// Cheap check whether a pointer position could involve this corner at all
        /// </summary>
        public bool IsNear(POINT pt) =>
            pt.X < Corner.X + _size && pt.Y < Corner.Y + _size && (pt.X < Corner.X || pt.Y < Corner.Y);

        /// <summary>
        /// Handles one pointer movement
        /// </summary>
        /// <param name="prev">where the pointer is</param>
        /// <param name="pt">where the pointer is about to go (can be off the screen)</param>
        /// <param name="time">event time in milliseconds</param>
        /// <param name="threshold">pressure needed to trigger, in logical pixels</param>
        /// <returns>true if the corner should trigger now</returns>
        public bool OnMove(POINT prev, POINT pt, uint time, double threshold)
        {
            // Only a real barrier stops the pointer: if the target is on another monitor, the pointer just goes there.
            // When stopped, Windows keeps the pointer at the edges of the corner
            bool blocked = (pt.X < Corner.X || pt.Y < Corner.Y) && !DisplayLayout.IsOnAnyMonitor(pt);
            var end = blocked ? new POINT { X = Math.Max(pt.X, Corner.X), Y = Math.Max(pt.Y, Corner.Y) } : pt;

            // Pushing out through a barrier hits it
            bool vertical = blocked && pt.X < Corner.X && prev.X >= Corner.X && prev.Y >= Corner.Y && prev.Y <= Corner.Y + _size;
            bool horizontal = blocked && pt.Y < Corner.Y && prev.Y >= Corner.Y && prev.X >= Corner.X && prev.X <= Corner.X + _size;

            // A held barrier keeps the pointer while it stays in the hit box beside the barrier, otherwise it is left
            bool left = false;
            if (!vertical && _verticalHit)
            {
                if (end.X < Corner.X + _hitBox && end.Y >= Corner.Y && end.Y <= Corner.Y + _size)
                {
                    vertical = true;
                }
                else
                {
                    _verticalHit = false;
                    left = true;
                }
            }
            if (!horizontal && _horizontalHit)
            {
                if (end.Y < Corner.Y + _hitBox && end.X >= Corner.X && end.X <= Corner.X + _size)
                {
                    horizontal = true;
                }
                else
                {
                    _horizontalHit = false;
                    left = true;
                }
            }

            // Once the pointer has left both barriers, start over and allow triggering again
            if (left && !IsHit)
            {
                Reset();
                _triggered = false;
            }

            int dx = Math.Abs(pt.X - prev.X);
            int dy = Math.Abs(pt.Y - prev.Y);
            double scaledThreshold = threshold * Corner.Scale;
            if (vertical && Hit(ref _verticalHit, dx, dy, time, scaledThreshold))
                return true;
            if (horizontal && Hit(ref _horizontalHit, dy, dx, time, scaledThreshold))
                return true;
            return false;
        }

        /// <summary>
        /// GNOME's _onBarrierHit
        /// </summary>
        /// <param name="distance">movement through the barrier</param>
        /// <param name="slide">movement along the barrier</param>
        private bool Hit(ref bool isHit, double distance, double slide, uint time, double threshold)
        {
            isHit = true;

            // Once triggered, wait until the pointer has left the barriers before triggering again
            if (_triggered)
                return false;

            if (distance >= threshold)
                return Trigger();

            // Throw out events where the pointer moves more along the barrier than through it
            if (slide > distance)
                return false;

            _lastTime = time;
            TrimEvents();

            distance = Math.Min(MAX_EVENT_PRESSURE * Corner.Scale, distance);
            _events.Enqueue(new PressureEvent { Time = time, Distance = distance });
            _pressure += distance;

            return _pressure >= threshold && Trigger();
        }

        /// <summary>
        /// Forgets pressure older than <see cref="TIMEOUT"/>
        /// </summary>
        private void TrimEvents()
        {
            // Unsigned subtraction also works across the wrap-around of the millisecond counter
            while (_events.Count > 0 && unchecked(_lastTime - _events.Peek().Time) > TIMEOUT)
                _pressure -= _events.Dequeue().Distance;
        }

        private bool Trigger()
        {
            _triggered = true;
            Reset();
            return true;
        }

        private void Reset()
        {
            _events.Clear();
            _pressure = 0;
            _lastTime = 0;
        }
    }
}
