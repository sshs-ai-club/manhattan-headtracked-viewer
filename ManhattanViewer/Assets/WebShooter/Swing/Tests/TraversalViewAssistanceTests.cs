using NUnit.Framework;
using UnityEngine;

namespace WebShooter.Swing.Tests
{
    public sealed class TraversalViewAssistanceTests
    {
        private static Vector2 Settle(TraversalViewFilter filter, Vector3 velocity, bool wall, bool airborne,
            bool active = true, float pitch = 1, float roll = 1)
        {
            for (int i = 0; i < 1200; i++) filter.Step(1f / 60, velocity, wall, airborne, active, false, pitch, roll);
            return filter.Angles;
        }
        [Test] public void ClimbFallAndLandingHaveDistinctGradualTargets()
        {
            var f = new TraversalViewFilter();
            Assert.That(f.Step(1f / 60, Vector3.up * 14, true, true, true, false, 1, 1).x, Is.InRange(-.02f, 0));
            Assert.That(Settle(f, Vector3.up * 14, true, true).x, Is.EqualTo(-55).Within(.001));
            Assert.That(Settle(f, Vector3.down * 24, false, true).x, Is.EqualTo(35).Within(.001));
            Assert.That(Settle(f, Vector3.down * 24, false, false).x, Is.EqualTo(0).Within(.001));
            Assert.That(Settle(f, Vector3.up * 24, false, true).x, Is.EqualTo(0).Within(.001));
        }
        [Test] public void OffZeroStrengthAndInvalidTelemetryAreNeutral()
        {
            var f = new TraversalViewFilter();
            Assert.That(Settle(f, Vector3.down * 30, false, true, false), Is.EqualTo(Vector2.zero));
            Assert.That(Settle(f, Vector3.up * 20, true, true, true, 0, 0), Is.EqualTo(Vector2.zero));
            Assert.That(Settle(f, new Vector3(float.NaN, 0, 0), true, true), Is.EqualTo(Vector2.zero));
            Assert.That(Settle(f, Vector3.up * 20, true, true, true, float.NaN, float.PositiveInfinity), Is.EqualTo(Vector2.zero));
            Assert.That(f.Step(float.NaN, Vector3.up, true, true, true, false, 1, 1), Is.EqualTo(Vector2.zero));
        }
        [Test] public void LeftAndRightCornersBankWithOppositeSignsAndNeverBankAtRest()
        {
            foreach (float sign in new[] { -1f, 1f })
            {
                var f = new TraversalViewFilter();
                for (int i = 0; i < 600; i++) f.Step(1f / 60, Quaternion.Euler(0, sign * i, 0) * Vector3.forward * 20,
                    false, true, true, false, 1, 1);
                Assert.That(f.Angles.y * sign, Is.LessThan(-4.7f));
                Assert.That(Settle(f, Vector3.zero, false, false).y, Is.EqualTo(0).Within(.001));
            }
        }
        [Test] public void ManualViewSuppressesTargetsAndWaitsBeforeResuming()
        {
            var f = new TraversalViewFilter();
            for (int i = 0; i < 60; i++) f.Step(1f / 60, Vector3.up * 20, true, true, true, true, 1, 1);
            Assert.That(f.Angles, Is.EqualTo(Vector2.zero));
            for (int i = 0; i < 40; i++) f.Step(1f / 60, Vector3.up * 20, true, true, true, false, 1, 1);
            Assert.That(f.Angles, Is.EqualTo(Vector2.zero));
            Assert.That(Settle(f, Vector3.up * 20, true, true).x, Is.LessThan(-54));
            Assert.That(Settle(f, Vector3.up * 20, true, true, false).magnitude, Is.LessThan(.001));
        }
        [Test] public void ResetRemovesOldFallAndTurnHistoryAndStallsCannotJump()
        {
            var f = new TraversalViewFilter(); Settle(f, Vector3.down * 30, false, true);
            var before = f.Angles;
            Assert.That((f.Step(10, Vector3.up * 30, true, true, true, false, 1, 1) - before).magnitude, Is.LessThan(.2));
            f.Reset(); Assert.That(f.Angles, Is.EqualTo(Vector2.zero));
            Assert.That(f.Step(1f / 60, Vector3.left * 50, false, true, true, false, 1, 1).y, Is.Zero);
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void ReversalsHaveBoundedAnglesRatesAndAccelerations(int hz)
        {
            var f = new TraversalViewFilter(); Vector2 last = Vector2.zero, rate = Vector2.zero;
            float dt = 1f / hz;
            for (int i = 0; i < hz * 30; i++)
            {
                bool wall = (i / (hz * 2)) % 2 == 0;
                Vector3 velocity = Quaternion.Euler(0, i * dt * (wall ? 90 : -90), 0) * Vector3.forward * 30;
                velocity.y = wall ? 20 : -30;
                var angle = f.Step(dt, velocity, wall, true, i < hz * 25, false, 1, 1);
                var nextRate = (angle - last) / dt; var acceleration = (nextRate - rate) / dt;
                Assert.That(angle.x, Is.InRange(-55f, 35f)); Assert.That(angle.y, Is.InRange(-8f, 8f));
                Assert.That(Mathf.Abs(nextRate.x), Is.LessThan(27)); Assert.That(Mathf.Abs(nextRate.y), Is.LessThan(12));
                Assert.That(Mathf.Abs(acceleration.x), Is.LessThan(67)); Assert.That(Mathf.Abs(acceleration.y), Is.LessThan(74));
                last = angle; rate = nextRate;
            }
        }
        [Test] public void ExactFilterAgreesAcrossFrameRatesAndManualPolesRemainUpright()
        {
            var a = new TraversalViewFilter(); var b = new TraversalViewFilter();
            for (int i = 0; i < 60; i++) a.Step(1f / 30, Vector3.up * 20, true, true, true, false, 1, 0);
            for (int i = 0; i < 288; i++) b.Step(1f / 144, Vector3.up * 20, true, true, true, false, 1, 0);
            Assert.That((a.Angles - b.Angles).magnitude, Is.LessThan(.001));
            Assert.That(Quaternion.Angle(a.Rotation(-85), Quaternion.identity), Is.LessThan(.001));
        }
        [Test] public void CommonPivotPreservesScreenSpaceAndObserverAndRestoresCalibrationEdits()
        {
            var body = new GameObject("observer"); var screen = new GameObject("screen").transform;
            var eye = new GameObject("camera").transform;
            screen.SetParent(body.transform, false); eye.SetParent(body.transform, false);
            screen.localPosition = Vector3.forward * .6f; eye.localPosition = new Vector3(.1f, .03f, 0);
            var camera = eye.gameObject.AddComponent<Camera>(); camera.enabled = false;
            camera.projectionMatrix = Matrix4x4.Frustum(-.02f, .03f, -.01f, .02f, .03f, 1000);
            var projection = camera.projectionMatrix; var eyeInScreen = screen.InverseTransformPoint(eye.position);
            var before = camera.WorldToViewportPoint(screen.TransformPoint(new Vector3(.2f, .1f, 0)));
            var pivot = new TraversalViewPivot(body.transform, screen, eye);
            try
            {
                pivot.Transform.localRotation = Quaternion.Euler(-40, 0, 7);
                Assert.That(body.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(body.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(Vector3.Distance(screen.InverseTransformPoint(eye.position), eyeInScreen), Is.LessThan(.00001));
                Assert.That(Vector3.Distance(camera.WorldToViewportPoint(screen.TransformPoint(new Vector3(.2f, .1f, 0))), before), Is.LessThan(.00001));
                Assert.That(camera.projectionMatrix, Is.EqualTo(projection));
                screen.localPosition = Vector3.forward * .8f;
                pivot.Restore(); pivot.Restore();
                Assert.That(screen.parent, Is.EqualTo(body.transform)); Assert.That(eye.parent, Is.EqualTo(body.transform));
                Assert.That(screen.localPosition.z, Is.EqualTo(.8f));
                Assert.That(eye.localPosition, Is.EqualTo(new Vector3(.1f, .03f, 0)));
                Assert.That(body.transform.childCount, Is.EqualTo(2));
            }
            finally { pivot.Restore(); Object.DestroyImmediate(body); }
        }
        [Test] public void AnotherOwnersHierarchyCannotBeClaimed()
        {
            var body = new GameObject("body"); var screen = new GameObject("screen"); var eye = new GameObject("eye");
            try { Assert.Throws<System.ArgumentException>(() => new TraversalViewPivot(body.transform, screen.transform, eye.transform)); }
            finally { Object.DestroyImmediate(body); Object.DestroyImmediate(screen); Object.DestroyImmediate(eye); }
        }
    }
}
