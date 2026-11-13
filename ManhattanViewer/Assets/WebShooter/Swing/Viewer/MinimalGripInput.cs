using System;
using Manhattan.Viewer;
using UnityEngine;
using UnityEngine.InputSystem;
namespace WebShooter.Swing
{
    /// <summary>One grip plus aim. No movement, jump, launch, flight or separate reel channels.</summary>
    public sealed class MinimalGripInput : ITraversalInputSource
    {
        private readonly IMotionInputSource look;
        private readonly Func<bool> eligible;
        private readonly GripEdges edges = new GripEdges();
        public MinimalGripInput(IMotionInputSource source, Func<bool> canRead) { look = source; eligible = canRead; }
        public MotionCommand Sample(float dt)
        {
            var input = look != null ? look.Sample(dt) : default;
            return new MotionCommand { lookDegrees = input.lookDegrees };
        }
        public TraversalActions ReadActions()
        {
            bool active = eligible();
            edges.Update(active, Mouse.current != null && Mouse.current.leftButton.isPressed);
            return new TraversalActions { active = active, firePressed = edges.Pressed, fireHeld = edges.Held,
                release = edges.Released, viewportX = .7f,
                aim = Camera.main != null ? Camera.main.ViewportPointToRay(new Vector3(.5f,.5f,0)) : default };
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
        private bool hasFrame, cancelPending;
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
            if(hasFrame && (now-latest.receivedAtSeconds>StaleSeconds || latest.held && !frame.held))cancelPending=true;
            latest = frame; hasFrame = true; return true;
        }
        public MotionCommand Sample(float dt) => default;
        public TraversalActions ReadActions()
        {
            if(cancelPending){cancelPending=false;edges.Update(false,false);
                if(Enabled && !latest.held && clock()-latest.receivedAtSeconds<=StaleSeconds)edges.Update(true,false);
                return new TraversalActions{release=true};}
            double age = clock() - latest.receivedAtSeconds;
            bool active = Enabled && hasFrame && age >= 0 && age <= StaleSeconds;
            edges.Update(active, latest.held);
            return new TraversalActions { active = active, firePressed = edges.Pressed, fireHeld = edges.Held,
                release = edges.Released, viewportX = .7f, aim = active ? latest.aim : default };
        }
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
