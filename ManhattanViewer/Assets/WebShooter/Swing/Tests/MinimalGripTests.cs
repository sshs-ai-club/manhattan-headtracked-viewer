using NUnit.Framework;
using UnityEngine;
namespace WebShooter.Swing.Tests
{
    public sealed class MinimalGripTests
    {
        [Test] public void GripRequiresNeutralAndRearmsAfterLoss()
        {
            var g=new GripEdges();g.Update(true,true);Assert.That(g.Held,Is.False);
            g.Update(true,false);g.Update(true,true);Assert.That(g.Pressed && g.Held,Is.True);
            g.Update(true,true);Assert.That(g.Pressed,Is.False);
            g.Update(false,true);Assert.That(g.Released && !g.Held,Is.True);
            g.Update(true,true);Assert.That(g.Pressed || g.Held,Is.False);
            g.Update(true,false);g.Update(true,true);Assert.That(g.Pressed,Is.True);
            g.Update(true,false);Assert.That(g.Released,Is.True);
        }
        [Test] public void GloveRejectsReorderingAndStaleHeldCannotResume()
        {
            double now=10;var input=new BufferedGripInput(()=>now);
            GripFrame Frame(long n,bool held)=>new GripFrame{sequence=n,receivedAtSeconds=now,held=held,aim=new Ray(Vector3.zero,Vector3.forward)};
            Assert.That(input.Submit(Frame(1,false)),Is.True);input.ReadActions();
            Assert.That(input.Submit(Frame(2,true)),Is.True);Assert.That(input.ReadActions().firePressed,Is.True);
            Assert.That(input.Submit(Frame(1,false)),Is.False);
            now+=.3;var expired=input.ReadActions();Assert.That(expired.active,Is.False);Assert.That(expired.release,Is.True);
            Assert.That(input.Submit(Frame(3,true)),Is.True);Assert.That(input.ReadActions().fireHeld,Is.False);
            input.Submit(Frame(4,false));input.ReadActions();input.Submit(Frame(5,true));Assert.That(input.ReadActions().firePressed,Is.True);
            var command=input.Sample(.02f);Assert.That(command.localMovement,Is.EqualTo(Vector3.zero));
            Assert.That(command.lookDegrees,Is.EqualTo(Vector2.zero));
        }
        [TestCase(false)][TestCase(true)] public void GloveCannotHideLossBetweenReads(bool gap)
        {
            double now=10;var input=new BufferedGripInput(()=>now);long seq=0;
            void Send(bool held)=>input.Submit(new GripFrame{sequence=++seq,receivedAtSeconds=now,held=held,aim=new Ray(Vector3.zero,Vector3.forward)});
            Send(false);input.ReadActions();Send(true);Assert.That(input.ReadActions().firePressed,Is.True);
            if(gap)now+=.3;else Send(false);
            Send(true);var action=input.ReadActions();Assert.That(action.release,Is.True);
            Assert.That(action.fireHeld || action.firePressed,Is.False);Assert.That(input.ReadActions().fireHeld,Is.False);
            Send(false);input.ReadActions();Send(true);Assert.That(input.ReadActions().firePressed,Is.True);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)] public void MotorHasMeasuredForceSpeedPowerAndNoTangentialForce(int fps)
        {
            var s=new GripMotorSettings{enabled=true};var v=new Vector3(8,0,0);
            for(int i=0;i<fps;i++)
            {
                var next=GripRopeMotor.Apply(v,Vector3.up*100,1f/fps,s,out float work,out float acceleration);
                Assert.That(next.x,Is.EqualTo(8));Assert.That(next.z,Is.Zero);
                Assert.That(acceleration,Is.InRange(0,s.acceleration+.001f));
                Assert.That(next.y,Is.LessThanOrEqualTo(s.speed+.001f));
                Assert.That(work,Is.EqualTo((next.sqrMagnitude-v.sqrMagnitude)*.5f).Within(.0001f));
                Assert.That(work,Is.LessThanOrEqualTo(s.specificPower/fps+.001f));v=next;
            }
        }
        [Test] public void TensileWebNeverPushesAwayToBrakeInwardMotion()
        {
            var s=new GripMotorSettings{enabled=true};var v=Vector3.up*18;
            var next=GripRopeMotor.Apply(v,Vector3.up*4.1f,.02f,s,out float work,out float acceleration);
            Assert.That(next,Is.EqualTo(v),"A web cannot push the player away from its anchor.");Assert.That(work,Is.Zero);
            Assert.That(Mathf.Abs(acceleration),Is.LessThanOrEqualTo(s.acceleration+.001f));
        }
        [TestCase(30)][TestCase(60)][TestCase(120)] public void GroundTakeoffUsesOnlyHeldGripAndReleaseAddsNoImpulse(int fps)
        {
            var s=MinimalGripInput.Settings();var b=new SwingPhysics(s);b.Reset(Vector3.up*b.FloorY);
            b.Attach(b.Position+new Vector3(0,60,25));b.GripHeld=true;
            for(int i=0;i<fps;i++)b.Step(1f/fps,Vector3.right,1);
            Assert.That(b.Position.y,Is.GreaterThan(b.FloorY+3));Assert.That(b.Position.x,Is.EqualTo(0).Within(.001));
            Assert.That(b.RopeCorrectionMetres,Is.LessThan(.02f));Assert.That(b.ResetSequence,Is.EqualTo(1));
            var v=b.Velocity;b.Release(true);Assert.That(b.Velocity,Is.EqualTo(v));Assert.That(b.GripMotorWork,Is.GreaterThan(0));
            double work=b.GripMotorWork;b.Step(1f/fps);Assert.That(b.GripMotorWork,Is.EqualTo(work));
        }
        [Test] public void CompliantWebBoundsTensionWithoutPositionProjection()
        {
            var settings=MinimalGripInput.Settings();settings.gravity=settings.airDrag=0;
            var body=new SwingPhysics(settings);body.Reset(Vector3.up*100);body.Velocity=Vector3.forward*18;
            body.Attach(body.Position-Vector3.forward*50);body.Step(1f/120);
            Assert.That(body.RopeCorrectionMetres,Is.LessThan(.005f));
            Assert.That(body.Velocity.z,Is.GreaterThanOrEqualTo(18-90f/120-.001f));
            Assert.That(body.TensionAcceleration,Is.InRange(0,90));
            Assert.That(body.Position.z,Is.EqualTo(body.Velocity.z/120).Within(.001));
        }
        [Test] public void InvalidMotorInputCannotAddEnergy()
        {
            var s=new GripMotorSettings{enabled=true};var v=Vector3.one;
            Assert.That(GripRopeMotor.Apply(v,Vector3.up,float.NaN,s,out _,out _),Is.EqualTo(v));
            s.acceleration=float.NaN;Assert.That(GripRopeMotor.Apply(v,Vector3.up*20,.1f,s,out _,out _),Is.EqualTo(v));
        }
    }
}
