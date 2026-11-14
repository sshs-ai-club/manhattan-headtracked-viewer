using NUnit.Framework;
using UnityEngine;
namespace WebShooter.Swing.Tests
{
    public sealed class DualGloveInputTests
    {
        private double now;
        private DualGloveInput input;
        [SetUp] public void Setup() { now = 10; input = new DualGloveInput(() => now); }
        private GloveFrame Frame(GloveHand hand, long sequence, bool held) => new GloveFrame {
            hand = hand, sequence = sequence, held = held, receivedAtSeconds = now,
            calibrated = true, aimBasis = GloveAimBasis.CalibratedOrientation,
            aim = new Ray(Vector3.zero, hand == GloveHand.Left ? Vector3.left : Vector3.right) };
        private void Neutral()
        { input.Submit(Frame(GloveHand.Left, 0, false)); input.Submit(Frame(GloveHand.Right, 0, false)); input.ReadActions(); }
        [Test] public void IndependentHandsKeepTheirOwnAimAndTransferOnPressOnly()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 1, true));
            var l = input.ReadActions(); Assert.That(l.firePressed && l.fireHeld, Is.True);
            Assert.That(l.aim.direction, Is.EqualTo(Vector3.left));
            input.Submit(Frame(GloveHand.Right, 1, true)); var r = input.ReadActions();
            Assert.That(r.firePressed && !r.release, Is.True); Assert.That(r.aim.direction, Is.EqualTo(Vector3.right));
            input.Submit(Frame(GloveHand.Right, 2, false));
            Assert.That(input.ReadActions().release, Is.True); Assert.That(input.ReadActions().fireHeld, Is.False);
            Assert.That(input.RopeOwner, Is.Null); // Still-held left hand never silently reattaches.
        }
        [Test] public void SimultaneousPressUsesLastReceiptNotSequenceAcrossDevices()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 100, true)); input.Submit(Frame(GloveHand.Right, 1, true));
            Assert.That(input.ReadActions().aim.direction, Is.EqualTo(Vector3.right));
        }
        [Test] public void SensorLossReleasesWithoutPausingGravityAndRequiresNeutral()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 1, true)); input.ReadActions(); now += .3;
            var lost = input.ReadActions(); Assert.That(lost.active && lost.release && !lost.fireHeld, Is.True);
            input.Submit(Frame(GloveHand.Left, 2, true)); Assert.That(input.ReadActions().firePressed, Is.False);
            input.Submit(Frame(GloveHand.Left, 3, false)); input.ReadActions();
            input.Submit(Frame(GloveHand.Left, 4, true)); Assert.That(input.ReadActions().firePressed, Is.True);
        }
        [Test] public void AccelerometerOnlyAndCalibrationLossCannotContinuePulling()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 1, true)); input.ReadActions();
            var invalid = Frame(GloveHand.Left, 2, true); invalid.aimBasis = GloveAimBasis.AccelerometerOnly;
            Assert.That(input.Submit(invalid), Is.False); Assert.That(input.ReadActions().release, Is.True);
            var restored = Frame(GloveHand.Left, 3, true); input.Submit(restored);
            Assert.That(input.ReadActions().fireHeld, Is.False);
            restored = Frame(GloveHand.Right, 2, true); restored.calibrated = false;
            Assert.That(input.Submit(restored), Is.False);
        }
        [Test] public void ReleaseBetweenReadsCannotBeOverwrittenByHeldPacket()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 1, true)); input.ReadActions();
            input.Submit(Frame(GloveHand.Left, 2, false)); input.Submit(Frame(GloveHand.Left, 3, true));
            Assert.That(input.ReadActions().release, Is.True); Assert.That(input.ReadActions().firePressed, Is.False);
        }
        [Test] public void ReorderedInvalidPacketDoesNotCancelFreshOwner()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 2, true)); input.ReadActions();
            var invalid = Frame(GloveHand.Left, 1, false); invalid.calibrated = false;
            Assert.That(input.Submit(invalid), Is.False); Assert.That(input.ReadActions().fireHeld, Is.True);
        }
        [Test] public void ExplicitSessionResetReleasesAndRejectsHeldReconnect()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 1, true)); input.ReadActions(); input.ResetSession(GloveHand.Left);
            Assert.That(input.ReadActions().release, Is.True);
            input.Submit(Frame(GloveHand.Left, 0, true)); Assert.That(input.ReadActions().fireHeld, Is.False);
        }
        [Test] public void DisableRequiresNeutralAndNeverReadsMouseMovement()
        {
            Neutral(); input.Submit(Frame(GloveHand.Left, 1, true)); input.ReadActions();
            input.Enabled = false; Assert.That(input.ReadActions().release, Is.True);
            input.Enabled = true; Assert.That(input.ReadActions().fireHeld, Is.False);
            Assert.That(input.Sample(.1f).localMovement, Is.EqualTo(Vector3.zero));
            Assert.That(input.Sample(.1f).lookDegrees, Is.EqualTo(Vector2.zero));
        }
        [Test] public void InvalidSequenceTimestampAndRayAreRejected()
        {
            var f = Frame(GloveHand.Left, -1, false); Assert.That(input.Submit(f), Is.False);
            f = Frame(GloveHand.Left, 1, false); f.receivedAtSeconds = now + 1; Assert.That(input.Submit(f), Is.False);
            f = Frame(GloveHand.Left, 1, false); f.aim = new Ray(Vector3.zero, Vector3.zero); Assert.That(input.Submit(f), Is.False);
            f = Frame(GloveHand.Left, 2, false); f.aim = new Ray(new Vector3(float.NaN, 0, 0), Vector3.forward); Assert.That(input.Submit(f), Is.False);
            f = Frame(GloveHand.Left, 3, false); Assert.That(input.Submit(f), Is.True);
            Assert.That(input.Submit(f), Is.False);
        }
    }
}
