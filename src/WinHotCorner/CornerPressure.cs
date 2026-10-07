using System;
using System.Collections.Generic;

namespace WinHotCorner
{
    /// <summary>
    /// Measures how hard the pointer pushes into a hot corner, and decides when that is enough to trigger it.
    /// </summary>
    /// <remarks>
    /// Nothing here stops the pointer: the edges of the screen do. This only measures how far the pointer would
    /// have gone past them, and applies GNOME's rules for that pressure (GNOME Shell's PressureBarrier in
    /// js/ui/layout.js), so it feels the same as on GNOME. Two edges count: the left one and the top one, each up to
    /// <see cref="EDGE_LENGTH"/> from the corner. Pushing out past an edge adds pressure; pressure older than one
    /// second is forgotten, so it takes a push, not a slow drift with pauses. After triggering, it waits until the
    /// pointer has left both edges before it can trigger again.
    ///
    /// When the pointer counts as held by an edge and when it has left follows mutter
    /// (src/backends/native/meta-barrier-native.c): once held, it stays held while it is within
    /// <see cref="LEAVE_DISTANCE"/> of the edge, so the small recoil after a push does not count as leaving.
    ///
    /// GNOME's numbers are in logical pixels; they are scaled by the monitor's display scale here because the
    /// mouse hook reports physical pixels.
    ///
    /// Where another monitor is beyond an edge, the pointer usually just moves on to it, but near the corner Windows
    /// may hold it for a few pixels (its sticky corners, MouseCornerClipLength). Whether it did shows only at the
    /// next event, so such a movement is kept until then and counts only if the pointer stayed.
    /// </remarks>
    internal class CornerPressure
    {
        /// <summary>
        /// How far along each edge from the corner a push counts, in logical pixels.
        /// GNOME's barriers are as long as its top bar is tall (2.2em, about 32 px)
        /// </summary>
        private const double EDGE_LENGTH = 32;

        /// <summary>
        /// Pressure older than this is forgotten, in milliseconds (HOT_CORNER_PRESSURE_TIMEOUT)
        /// </summary>
        private const uint TIMEOUT = 1000;

        /// <summary>
        /// Most pressure a single event can add, in logical pixels
        /// </summary>
        private const double MAX_EVENT_PRESSURE = 15;

        /// <summary>
        /// How far the pointer has to move away from an edge to leave it, in logical pixels
        /// (the width of mutter's hit box, calculate_barrier_hit_box)
        /// </summary>
        private const double LEAVE_DISTANCE = 2;

        private struct PressureEvent
        {
            public uint Time;
            public double Distance;
        }

        public HotCorner Corner { get; }

        /// <summary>
        /// The pointer is currently held by at least one of the edges
        /// </summary>
        public bool IsHeld => _leftEdgeHeld || _topEdgeHeld;

        /// <summary>
        /// Needs the next pointer movement even if it is not near the corner
        /// </summary>
        public bool IsWatching => IsHeld || _pending.HasValue;

        private readonly int _edgeLength;
        private readonly int _leaveDistance;
        private readonly Queue<PressureEvent> _events = new Queue<PressureEvent>();
        private double _pressure = 0;
        private uint _lastTime = 0;
        private bool _triggered = false;
        private bool _leftEdgeHeld = false;
        private bool _topEdgeHeld = false;

        private struct Move
        {
            public POINT Prev;
            public POINT Pt;
            public uint Time;
        }

        /// <summary>
        /// A movement towards another monitor, until the next one shows whether the pointer went there
        /// </summary>
        private Move? _pending;

        public CornerPressure(HotCorner corner)
        {
            Corner = corner;
            _edgeLength = (int)Math.Round(EDGE_LENGTH * corner.Scale);
            _leaveDistance = Math.Max(1, (int)Math.Round(LEAVE_DISTANCE * corner.Scale));
        }

        /// <summary>
        /// Cheap check whether a pointer position could involve this corner at all
        /// </summary>
        public bool IsNear(POINT pt) =>
            pt.X < Corner.X + _edgeLength && pt.Y < Corner.Y + _edgeLength && (pt.X < Corner.X || pt.Y < Corner.Y);

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
            bool trigger = false;

            // The pointer is now where the kept movement ended: it stayed if it is still on this side of both edges
            if (_pending.HasValue)
            {
                Move move = _pending.Value;
                _pending = null;
                bool stayed = prev.X >= Corner.X && prev.Y >= Corner.Y;
                trigger = Apply(move.Prev, move.Pt, move.Time, stayed, threshold);
            }

            bool outside = pt.X < Corner.X || pt.Y < Corner.Y;
            if (outside && DisplayLayout.IsOnAnyMonitor(pt))
            {
                _pending = new Move { Prev = prev, Pt = pt, Time = time };
                return trigger;
            }

            // Off every monitor: the edges of the screens stop the pointer
            return Apply(prev, pt, time, outside, threshold) || trigger;
        }

        /// <summary>
        /// Applies one pointer movement, once it is known whether the pointer was stopped
        /// </summary>
        /// <param name="stopped">the pointer did not go past the edges of the corner</param>
        private bool Apply(POINT prev, POINT pt, uint time, bool stopped, double threshold)
        {
            // When stopped, the pointer stays at the edges of the corner
            var end = stopped ? new POINT { X = Math.Max(pt.X, Corner.X), Y = Math.Max(pt.Y, Corner.Y) } : pt;

            // Pushing out past an edge, near the corner
            bool pushLeft = stopped && pt.X < Corner.X && prev.X >= Corner.X && prev.Y >= Corner.Y && prev.Y <= Corner.Y + _edgeLength;
            bool pushUp = stopped && pt.Y < Corner.Y && prev.Y >= Corner.Y && prev.X >= Corner.X && prev.X <= Corner.X + _edgeLength;

            // A held pointer stays held while it is close to the edge, otherwise it has left the edge
            bool left = false;
            if (!pushLeft && _leftEdgeHeld)
            {
                if (end.X < Corner.X + _leaveDistance && end.Y >= Corner.Y && end.Y <= Corner.Y + _edgeLength)
                {
                    pushLeft = true;
                }
                else
                {
                    _leftEdgeHeld = false;
                    left = true;
                }
            }
            if (!pushUp && _topEdgeHeld)
            {
                if (end.Y < Corner.Y + _leaveDistance && end.X >= Corner.X && end.X <= Corner.X + _edgeLength)
                {
                    pushUp = true;
                }
                else
                {
                    _topEdgeHeld = false;
                    left = true;
                }
            }

            // Once the pointer has left both edges, start over and allow triggering again
            if (left && !IsHeld)
            {
                Reset();
                _triggered = false;
            }

            int dx = Math.Abs(pt.X - prev.X);
            int dy = Math.Abs(pt.Y - prev.Y);
            double scaledThreshold = threshold * Corner.Scale;
            if (pushLeft && Push(ref _leftEdgeHeld, dx, dy, time, scaledThreshold))
                return true;
            if (pushUp && Push(ref _topEdgeHeld, dy, dx, time, scaledThreshold))
                return true;
            return false;
        }

        /// <summary>
        /// One push against an edge (GNOME's _onBarrierHit)
        /// </summary>
        /// <param name="distance">movement out past the edge</param>
        /// <param name="slide">movement along the edge</param>
        private bool Push(ref bool held, double distance, double slide, uint time, double threshold)
        {
            held = true;

            // Once triggered, wait until the pointer has left the edges before triggering again
            if (_triggered)
                return false;

            if (distance >= threshold)
                return Trigger();

            // Throw out events where the pointer moves more along the edge than out past it
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
