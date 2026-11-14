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
        [Test] public void FreshReleaseImmediatelyCoastsWithoutPausingSimulation()
        {
            var rig=new GameObject();var flight=rig.AddComponent<Manhattan.Viewer.FreeFlightController>();flight.enabled=false;
            var input=new BufferedGripInput(()=>10);var motion=new WebSwingMotionInput(input,flight,null,MinimalGripInput.Settings());
            try{
                flight.SetPose(Vector3.up*100,Quaternion.identity);motion.Activate();
                void Send(long seq,bool held)=>input.Submit(new GripFrame{sequence=seq,receivedAtSeconds=10,held=held,aim=new Ray(Vector3.up*100,Vector3.forward)});
                Send(1,false);input.ReadActions();Send(2,true);input.ReadActions();motion.Body.Attach(Vector3.up*150);motion.Body.Velocity=Vector3.forward*10;
                Send(3,false);var command=motion.Sample(.02f);
                Assert.That(motion.Paused || motion.Body.Attached,Is.False);Assert.That(motion.SimulatedTime,Is.EqualTo(.02f).Within(.0001));Assert.That(command.cityPosition.z,Is.GreaterThan(.1f));
            }finally{motion.Collision.Dispose();Object.DestroyImmediate(rig);}
        }
        [Test] public void InvalidMotorInputCannotAddEnergy()
        {
            var s=new GripMotorSettings{enabled=true};var v=Vector3.one;
            Assert.That(GripRopeMotor.Apply(v,Vector3.up,float.NaN,s,out _,out _),Is.EqualTo(v));
            s.acceleration=float.NaN;Assert.That(GripRopeMotor.Apply(v,Vector3.up*20,.1f,s,out _,out _),Is.EqualTo(v));
        }
        private sealed class DebugMovement : Manhattan.Viewer.IMotionInputSource
        {
            public Manhattan.Viewer.MotionCommand Sample(float dt) => new Manhattan.Viewer.MotionCommand
            { localMovement = new Vector3(1, -1, 1), lookDegrees = new Vector2(2, 3) };
        }
        [Test] public void DebugFlightAlonePassesExistingMovementAndPauseBlocksIt()
        {
            bool flying=false, active=true;
            var input=new MinimalGripInput(new DebugMovement(),()=>active,()=>flying);
            Assert.That(input.Sample(.02f).localMovement,Is.EqualTo(Vector3.zero));
            Assert.That(input.Sample(.02f).lookDegrees,Is.EqualTo(new Vector2(2,3)));
            flying=true;Assert.That(input.Sample(.02f).localMovement,Is.EqualTo(new Vector3(1,-1,1)));
            active=false;Assert.That(input.Sample(.02f).localMovement,Is.EqualTo(Vector3.zero));
            Assert.That(input.Sample(.02f).lookDegrees,Is.EqualTo(Vector2.zero));
            active=true;flying=false;Assert.That(input.Sample(.02f).localMovement,Is.EqualTo(Vector3.zero));
        }
        [Test] public void QChargeReleaseRequiresNeutralAndDoesNotRepeat()
        {
            var input=new MinimalGripInput(null,()=>true);var ray=new Ray(Vector3.up*10,Vector3.forward);
            Assert.That(input.ReadControls(true,false,true,false,false,ray).launchHeld,Is.False);
            input.ReadControls(true,false,false,false,false,ray);
            var press=input.ReadControls(true,false,true,false,false,ray);
            Assert.That(press.launchPressed && press.launchHeld,Is.True);
            var hold=input.ReadControls(true,false,true,false,false,ray);
            Assert.That(hold.launchHeld && !hold.launchPressed,Is.True);
            Assert.That(input.ReadControls(true,false,false,false,false,ray).launchReleased,Is.True);
            Assert.That(input.ReadControls(true,false,false,false,false,ray).launchReleased,Is.False);
            input.ReadControls(false,false,true,false,false,ray);
            Assert.That(input.ReadControls(true,false,true,false,false,ray).launchHeld,Is.False);
        }
        [Test] public void FlightToggleCancelsGripAndQAndRequiresNeutralOnReturn()
        {
            var input=new MinimalGripInput(null,()=>true);var ray=new Ray(Vector3.up*10,Vector3.forward);
            input.ReadControls(true,false,false,false,false,ray);
            input.ReadControls(true,true,true,false,false,ray);
            var toggle=input.ReadControls(true,true,true,true,false,ray);
            Assert.That(toggle.toggleFlight && toggle.release && toggle.launchReleased,Is.True);
            Assert.That(toggle.fireHeld || toggle.launchHeld,Is.False);
            Assert.That(input.ReadControls(true,true,true,true,true,ray).toggleFlight,Is.False);
            input.ReadControls(true,true,true,false,true,ray);
            Assert.That(input.ReadControls(true,true,true,true,true,ray).toggleFlight,Is.True);
            var returned=input.ReadControls(true,true,true,false,false,ray);
            Assert.That(returned.fireHeld || returned.launchHeld,Is.False);
            input.ReadControls(true,false,false,false,false,ray);
            var fresh=input.ReadControls(true,true,true,false,false,ray);
            Assert.That(fresh.firePressed && fresh.launchPressed,Is.True);
        }
        [Test] public void QInFlightCannotChargeAndFocusLossCannotToggle()
        {
            var input=new MinimalGripInput(null,()=>true);
            input.ReadControls(true,false,false,false,true,default);
            var inFlight=input.ReadControls(true,false,true,false,true,default);
            Assert.That(inFlight.launchPressed || inFlight.launchHeld || inFlight.launchReleased,Is.False);
            input.ReadControls(false,false,false,true,true,default);
            Assert.That(input.ReadControls(true,false,false,true,true,default).toggleFlight,Is.False);
        }
        private sealed class DebugControls : ITraversalInputSource
        {
            public MinimalGripInput Input; public bool Active=true, Q, R; public System.Func<bool> Flying;
            public Ray Aim;
            public Manhattan.Viewer.MotionCommand Sample(float dt)=>Input.Sample(dt);
            public TraversalActions ReadActions()=>Input.ReadControls(Active,false,Q,R,Flying(),Aim);
        }
        [Test] public void MinimalAdapterActuallyChargesLaunchAndRReturnsFreeFlightMovement()
        {
            var rig=new GameObject();var left=GameObject.CreatePrimitive(PrimitiveType.Cube);var right=GameObject.CreatePrimitive(PrimitiveType.Cube);
            WebSwingMotionInput motion=null;
            try
            {
                var start=new Vector3(-50000,100,-50000);
                var flight=rig.AddComponent<Manhattan.Viewer.FreeFlightController>();flight.enabled=false;flight.SetPose(start,Quaternion.identity);
                foreach(var pair in new[]{(left,-20f),(right,20f)}){pair.Item1.layer=30;pair.Item1.transform.position=start+new Vector3(pair.Item2,40,25);pair.Item1.transform.localScale=new Vector3(5,100,150);}
                Physics.SyncTransforms();
                var controls=new DebugControls{Aim=new Ray(start,Vector3.forward)};
                controls.Flying=()=>motion!=null && !motion.Enabled;
                controls.Input=new MinimalGripInput(new DebugMovement(),()=>controls.Active,controls.Flying);
                var settings=MinimalGripInput.Settings();settings.gravity=settings.airDrag=0;
                motion=new WebSwingMotionInput(controls,flight,null,settings);motion.Activate();
                void Step(){var c=motion.Sample(1f/60);if(c.hasAbsolutePose)flight.SetPose(c.cityPosition,c.cityRotation);}
                Step();controls.Q=true;Step();Assert.That(motion.Launch.Charging,Is.True);
                for(int n=0;n<90;n++)Step();
                controls.Q=false;Step();Assert.That(motion.Launch.Launches,Is.EqualTo(1));Assert.That(motion.Body.Velocity.magnitude,Is.GreaterThan(20));
                controls.R=true;Step();Assert.That(motion.Enabled,Is.False);Assert.That(motion.Launch.Charging,Is.False);
                var free=motion.Sample(.02f);Assert.That(free.hasAbsolutePose,Is.False);Assert.That(free.localMovement,Is.EqualTo(new Vector3(1,-1,1)));
                controls.R=false;Step();controls.Q=true;Step();Assert.That(motion.Launch.Charging,Is.False);
                controls.R=true;Step();Assert.That(motion.Enabled,Is.True);Assert.That(motion.Launch.Charging,Is.False);
                controls.R=false;controls.Q=false;Step();controls.Q=true;Step();Assert.That(motion.Launch.Charging,Is.True);
                var velocity=motion.Body.Velocity;controls.R=true;Step();Assert.That(motion.Enabled,Is.False);
                Assert.That(motion.Launch.Launches,Is.EqualTo(1));Assert.That(motion.Body.Velocity,Is.EqualTo(velocity));
            }
            finally{motion?.Collision.Dispose();Object.DestroyImmediate(rig);Object.DestroyImmediate(left);Object.DestroyImmediate(right);}
        }
        [Test] public void RealInputSystemQAndRReachMinimalAdapter()
        {
            var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                var input=new MinimalGripInput(null,()=>true);
                void Keys(params UnityEngine.InputSystem.Key[] keys){UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));UnityEngine.InputSystem.InputSystem.Update();}
                Keys();input.ReadActions();Keys(UnityEngine.InputSystem.Key.Q);Assert.That(input.ReadActions().launchPressed,Is.True);
                Keys();Assert.That(input.ReadActions().launchReleased,Is.True);
                Keys(UnityEngine.InputSystem.Key.R);Assert.That(input.ReadActions().toggleFlight,Is.True);
                Assert.That(input.ReadActions().toggleFlight,Is.False);
            }
            finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);}
        }
    }
}
