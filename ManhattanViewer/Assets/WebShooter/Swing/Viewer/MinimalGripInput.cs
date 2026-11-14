using System;
using Manhattan.Viewer;
using UnityEngine;
using UnityEngine.InputSystem;
namespace WebShooter.Swing
{
    /// <summary>One grip plus aim, with optional R debug flight and Q two-hand launch.</summary>
    public sealed class MinimalGripInput : ITraversalInputSource
    {
        private readonly IMotionInputSource look;
        private readonly Func<bool> eligible, freeFlight;
        private readonly GripEdges edges = new GripEdges();
        private readonly GripEdges launchEdges = new GripEdges();
        private readonly GripEdges flightEdges = new GripEdges();
        public MinimalGripInput(IMotionInputSource source, Func<bool> canRead, Func<bool> isFreeFlight = null)
        { look = source; eligible = canRead; freeFlight = isFreeFlight ?? (() => false); }
        public MotionCommand Sample(float dt)
        {
            var input = look != null ? look.Sample(dt) : default;
            if (!eligible()) return default;
            // Debug flight alone gets the existing WASD/QE/speed controls. Normal grip never steers.
            return freeFlight() ? input : new MotionCommand { lookDegrees = input.lookDegrees };
        }
        public TraversalActions ReadActions()
        {
            var keyboard = Keyboard.current;
            return ReadControls(eligible(), Mouse.current != null && Mouse.current.leftButton.isPressed,
                keyboard != null && keyboard.qKey.isPressed, keyboard != null && keyboard.rKey.isPressed,
                freeFlight(), Camera.main != null ? Camera.main.ViewportPointToRay(new Vector3(.5f,.5f,0)) : default);
        }
        // Explicit control snapshot makes neutral/rearm and debug-mode transitions testable.
        public TraversalActions ReadControls(bool active, bool gripHeld, bool launchHeld, bool flightHeld, bool flying, Ray aim)
        {
            flightEdges.Update(active, flightHeld);
            bool toggle = flightEdges.Pressed;
            // Never carry a held grip or Q charge through pause or either flight transition.
            bool traversalActive = active && !flying && !toggle;
            edges.Update(traversalActive, gripHeld);
            launchEdges.Update(traversalActive, launchHeld);
            return new TraversalActions { active = active, toggleFlight = toggle,
                firePressed = edges.Pressed, fireHeld = edges.Held, release = edges.Released,
                launchPressed = launchEdges.Pressed, launchHeld = launchEdges.Held,
                launchReleased = launchEdges.Released, viewportX = .7f, aim = aim };
        }
        public static SwingSettings Settings()
        {
            var s = WebSwingMotionInput.TraversalSettings();
            s.airControl = s.cruiseAcceleration = s.attachPull = 0;
            s.chain.enabled = false;
            s.gripMotor.enabled = true;
            s.features = new FlowFeatures { tensionOnlyRope = true, assistAnchor = true,
                anchorAssistStrength = 1f, anchorConeDegrees = 12f, fixedAttachmentDelay = true };
            return s;
        }
    }
    public struct GripFrame
    {
        public long sequence;
        public double receivedAtSeconds;
        public bool held;
        public Ray aim;
    }
    /// <summary>Transport-neutral mock for a future stateful glove; v1 wrist events stay unchanged.</summary>
    public sealed class BufferedGripInput : ITraversalInputSource
    {
        private readonly GripEdges edges = new GripEdges();
        private readonly Func<double> clock;
        private GripFrame latest;
        private bool hasFrame, cancelPending, lossPending;
        public bool Enabled = true;
        public double StaleSeconds = .2;
        public BufferedGripInput(Func<double> now) { clock = now ?? throw new ArgumentNullException(nameof(now)); }
        public bool Submit(GripFrame frame)
        {
            double now = clock();
            if (!double.IsFinite(now) || !double.IsFinite(StaleSeconds) || StaleSeconds <= 0 ||
                hasFrame && frame.sequence <= latest.sequence || !double.IsFinite(frame.receivedAtSeconds) ||
                frame.receivedAtSeconds > now || now - frame.receivedAtSeconds > StaleSeconds ||
                !Finite(frame.aim.origin) || !Finite(frame.aim.direction) || frame.aim.direction.sqrMagnitude < .01f) return false;
            // Do not let a fresh packet erase a loss or release that occurred between reads.
            if(hasFrame && now-latest.receivedAtSeconds>StaleSeconds)lossPending=cancelPending=true;
            if(hasFrame && latest.held && !frame.held)cancelPending=true;
            latest = frame; hasFrame = true; return true;
        }
        public MotionCommand Sample(float dt) => default;
        public TraversalActions ReadActions()
        {
            if(cancelPending){bool fresh=Enabled && clock()>=latest.receivedAtSeconds && clock()-latest.receivedAtSeconds<=StaleSeconds;
                bool activeRelease=fresh && !lossPending;cancelPending=lossPending=false;edges.Update(false,false);
                if(fresh && !latest.held)edges.Update(true,false);
                return new TraversalActions{active=activeRelease,release=true};}
            double age = clock() - latest.receivedAtSeconds;
            bool active = Enabled && hasFrame && age >= 0 && age <= StaleSeconds;
            edges.Update(active, latest.held);
            return new TraversalActions { active = active, firePressed = edges.Pressed, fireHeld = edges.Held,
                release = edges.Released, viewportX = .7f, aim = active ? latest.aim : default };
        }
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
