using System;
using Manhattan.Viewer;
using UnityEngine;
namespace WebShooter.Swing
{
    public enum GloveHand { Left, Right }
    public enum GloveAimBasis { Unavailable, AccelerometerOnly, CalibratedOrientation }
    public struct GloveFrame
    {
        public GloveHand hand;
        public long sequence;
        public double receivedAtSeconds;
        public bool held, calibrated;
        public GloveAimBasis aimBasis;
        // Render-space ray produced by a calibrated bridge, not inferred from acceleration.
        public Ray aim;
    }
    /// <summary>
    /// Main-thread, transport-neutral independent glove state. Legacy wrist v1 is untouched.
    /// One physical rope: newest deliberate hand press owns it; no silent hand takeover.
    /// Does not synthesize two anchors or Q launch from simultaneous grips.
    /// </summary>
    public sealed class DualGloveInput : ITraversalInputSource
    {
        private sealed class Channel
        {
            public BufferedGripInput input;
            public long sequence = -1, order;
            public bool valid;
            public Channel(Func<double> clock) { input = new BufferedGripInput(clock); }
        }
        private readonly Func<double> clock;
        private readonly Channel left, right;
        private long order;
        private GloveHand? owner;
        public bool Enabled = true;
        public double StaleSeconds = .2;
        public GloveHand? RopeOwner => owner;
        public DualGloveInput(Func<double> now)
        {
            clock = now ?? throw new ArgumentNullException(nameof(now));
            left = new Channel(clock); right = new Channel(clock);
        }
        public bool Submit(GloveFrame frame)
        {
            if (frame.hand != GloveHand.Left && frame.hand != GloveHand.Right) return false;
            Channel c = frame.hand == GloveHand.Left ? left : right;
            double now = clock();
            if (!double.IsFinite(now) || !double.IsFinite(StaleSeconds) || StaleSeconds <= 0 ||
                frame.sequence < 0 || frame.sequence <= c.sequence || !double.IsFinite(frame.receivedAtSeconds) ||
                frame.receivedAtSeconds > now || now - frame.receivedAtSeconds > StaleSeconds) return false;
            c.sequence = frame.sequence;
            // A fresh loss-of-calibration packet invalidates the hand immediately. Rejecting it
            // without clearing state would leave an old held sample pulling until timeout.
            if (!frame.calibrated || frame.aimBasis != GloveAimBasis.CalibratedOrientation ||
                !Finite(frame.aim.origin) || !Finite(frame.aim.direction) ||
                !float.IsFinite(frame.aim.direction.sqrMagnitude) || frame.aim.direction.sqrMagnitude < .01f)
            {
                c.valid = false; c.input.Enabled = false; c.input.ReadActions();
                return false;
            }
            c.input.StaleSeconds = StaleSeconds;
            bool accepted = c.input.Submit(new GripFrame { sequence = frame.sequence,
                receivedAtSeconds = frame.receivedAtSeconds, held = frame.held, aim = frame.aim });
            if (accepted) { c.valid = true; c.order = ++order; }
            return accepted;
        }
        public MotionCommand Sample(float dt) => default; // No hidden keyboard, mouse or steering.
        public TraversalActions ReadActions()
        {
            left.input.StaleSeconds = right.input.StaleSeconds = StaleSeconds;
            left.input.Enabled = Enabled && left.valid;
            right.input.Enabled = Enabled && right.valid;
            var l = left.input.ReadActions(); var r = right.input.ReadActions();
            if (!Enabled) { owner = null; return new TraversalActions { active = true, release = true }; }
            // Only a NEW press can transfer rope ownership. A surviving held hand cannot
            // resurrect a rope when the owner disconnects or releases.
            bool lp = l.active && l.firePressed, rp = r.active && r.firePressed;
            if (lp || rp)
            {
                bool chooseLeft = lp && (!rp || left.order > right.order);
                owner = chooseLeft ? GloveHand.Left : GloveHand.Right;
                var a = chooseLeft ? l : r;
                a.active = true; a.release = false; a.viewportX = chooseLeft ? .3f : .7f;
                return a;
            }
            if (owner.HasValue)
            {
                bool isLeft = owner.Value == GloveHand.Left;
                var a = isLeft ? l : r;
                if (!a.active || !a.fireHeld || a.release)
                { owner = null; return new TraversalActions { active = true, release = true }; }
                a.active = true; a.viewportX = isLeft ? .3f : .7f; return a;
            }
            // Sensor loss releases the rope but MUST NOT pause gravity/game simulation.
            return new TraversalActions { active = true };
        }
        // Explicit connection/session reset only: the caller must invalidate pending bridge
        // packets before reconnecting. Never infer a reboot from an out-of-order sequence.
        public void ResetSession(GloveHand hand)
        {
            if (hand != GloveHand.Left && hand != GloveHand.Right) throw new ArgumentOutOfRangeException(nameof(hand));
            Channel c = hand == GloveHand.Left ? left : right;
            c.input = new BufferedGripInput(clock); c.sequence = -1; c.valid = false;
        }
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
