using System;
using System.Collections.Generic;
using NUnit.Framework;
using Manhattan.Viewer;
using UnityEngine;
namespace WebShooter.Swing.Tests
{
    public sealed class MinimalGripRouteTests
    {
        // A restricted, state-aware scripted pilot, not a learned policy or evidence of human usability.
        [TestCase(12,30,0)][TestCase(12,60,0)][TestCase(12,120,0)]
        [TestCase(20,30,0)][TestCase(20,60,0)][TestCase(20,120,0)]
        [TestCase(12,30,1)][TestCase(12,60,1)][TestCase(12,120,1)]
        [TestCase(20,30,1)][TestCase(20,60,1)][TestCase(20,120,1)]
        public void DistinctBuildingsWithRepeatedAlternatingCorners(int count,int fps,int seed)
        { RunCourse(count, fps, seed, false); }

        // Acceptance gate, not a claim that the existing pilot can already pass it.
        // Unlike the legacy thin-pole course these are full 24 m wide facades.
        [TestCase(12,30,0)][TestCase(12,60,0)][TestCase(12,120,0)]
        [TestCase(20,30,1)][TestCase(20,60,1)][TestCase(20,120,1)]
        public void FullFacadesRequireZeroContactsAndObservedCornerPassage(int count,int fps,int seed)
        { RunCourse(count, fps, seed, true); }

        private static void RunCourse(int count,int fps,int seed,bool fullFacades)
        {
            var objects=new List<GameObject>();var goals=new List<Vector3>();var seen=new HashSet<int>();var waypoints=new List<Vector3>();var directions=new List<Vector3>();
            var rig=new GameObject("Restricted grip pilot");var flight=rig.AddComponent<FreeFlightController>();flight.enabled=false;
            var start=new Vector3(-30000,2050,-30000);var point=start;double clock=0;long sequence=0;
            var controls=new BufferedGripInput(()=>clock);var settings=MinimalGripInput.Settings();settings.groundHeight=2000;
            var motion=new WebSwingMotionInput(controls,flight,null,settings);float dt=1f/fps,maxCorrection=0,maxForce=0;
            float maxTension=0,maxTotalAcceleration=0;var previousVelocity=Vector3.zero;
            motion.PhysicsStepped+=sample=>{maxTension=Mathf.Max(maxTension,motion.Body.TensionAcceleration);maxTotalAcceleration=Mathf.Max(maxTotalAcceleration,(sample.velocity-previousVelocity).magnitude/sample.dt);previousVelocity=sample.velocity;};
            int presses=0,releases=0,corners=0,observedCorners=0,passedGates=0,actualAttachments=0,actualReleases=0;bool commandHeld=false;
            motion.WebAttached+=_=>actualAttachments++;motion.WebReleased+=()=>{actualReleases++;Assert.That(commandHeld,Is.False,"Automatic detach while held is not a successful grip link.");};float travel=0,minHeight=start.y;
            var auditedPosition=start;
            motion.PhysicsStepped+=sample=>
            {
                // Audit every physical substep rather than only the rendered frame.
                Assert.That(sample.collided,Is.False,$"Physical collision at {sample.time:F4}: {sample.collider}");
                Assert.That(CapsulePathBlocked(auditedPosition,sample.position,settings.eyeHeight),Is.False,
                    $"Independent capsule audit failed at {sample.time:F4}: {auditedPosition} -> {sample.position}");
                auditedPosition=sample.position;
            };
            try
            {
                for(int i=0;i<count;i++)
                {
                    bool right=(i/2)%2!=0;point+=(right?Vector3.right:Vector3.forward)*(45+seed*5);
                    if(i>0 && i%2==0)corners++;
                    var direction=right?Vector3.right:Vector3.forward;waypoints.Add(point);directions.Add(direction);
                    var facade=point+Vector3.Cross(Vector3.up,direction)*(i%2==0?25:-25);
                    var tower=GameObject.CreatePrimitive(PrimitiveType.Cube);tower.name="Grip building "+i;tower.layer=30;
                    tower.transform.position=new Vector3(facade.x,2030,facade.z);tower.transform.localScale=new Vector3(fullFacades?24:5,100,fullFacades?24:5);
                    objects.Add(tower);goals.Add(new Vector3(facade.x,2075,facade.z));
                }
                Physics.SyncTransforms();flight.SetPose(start,Quaternion.identity);motion.Activate();
                void Step(bool held,Vector3 target)
                {
                    commandHeld=held;clock+=dt;var ray=new Ray(motion.Body.Position,target-motion.Body.Position);
                    Assert.That(controls.Submit(new GripFrame{sequence=++sequence,receivedAtSeconds=clock,held=held,aim=ray}),Is.True);
                    var before=motion.Body.Position;var c=motion.Sample(dt);flight.SetPose(c.cityPosition,c.cityRotation);
                    travel+=Vector3.Distance(before,c.cityPosition);minHeight=Mathf.Min(minHeight,c.cityPosition.y);
                    maxCorrection=Mathf.Max(maxCorrection,motion.MaxConstraintCorrection);maxForce=Mathf.Max(maxForce,Mathf.Abs(motion.Body.GripMotorAcceleration));
                    Assert.That(motion.ReelInput,Is.Zero);Assert.That(motion.Body.ResetSequence,Is.EqualTo(1));
                    Assert.That(motion.Body.ContactSequence,Is.Zero,$"Contact at t={clock:F2} presses={presses} p={motion.Body.Position} v={motion.Body.Velocity} target={target} collider={motion.Body.LastCollisionName}");
                    Assert.That(motion.Body.FloorContactSequence,Is.Zero);
                    Assert.That(motion.ConstraintSnapPreventions+motion.PreventiveRopeReleases+motion.CollisionReleaseCount,Is.Zero);
                }
                Step(false,goals[0]);
                for(int i=0;i<count;i++)
                {
                    var target=goals[i];bool attached=false;bool crossed=false;presses++;
                    float priorAlong=Vector3.Dot(motion.Body.Position-waypoints[i],directions[i]);
                    bool approachedGate=priorAlong<0;
                    for(int f=0;f<fps*8;f++)
                    {
                        Step(true,target);
                        if(motion.Body.Attached)
                        {
                            attached=true;Assert.That(objects[i].GetComponent<Collider>().bounds.SqrDistance(motion.ShotTarget),Is.LessThan(.01f));
                            seen.Add(i);
                        }
                        float along=Vector3.Dot(motion.Body.Position-waypoints[i],directions[i]);
                        approachedGate|=along<0;
                        if(attached && f>fps/2 && along >= (fullFacades?0:-Mathf.Max(22,Vector3.Dot(motion.Body.Velocity,directions[i])*.7f)))
                        {
                            if(fullFacades)
                            {
                                var horizontal=Vector3.ProjectOnPlane(motion.Body.Velocity,Vector3.up);
                                // Merely attaching around a corner is not traversing it.
                                if(!approachedGate || horizontal.magnitude<1 || Vector3.Dot(horizontal.normalized,directions[i])<.7f)continue;
                                if(Vector3.Cross(motion.Body.Position-waypoints[i],directions[i]).magnitude>35)continue;
                                crossed=true;passedGates++;if(i>0 && i%2==0)observedCorners++;
                            }
                            break;
                        }
                        if(f==fps*8-1)Assert.Fail("Timed out approaching building "+i+" at "+motion.Body.Position);
                    }
                    if(fullFacades)Assert.That(crossed,Is.True,"Did not pass gate "+i);
                    Step(false,target);releases++;Assert.That(motion.Body.Attached || motion.Flying,Is.False);
                }
                if(fullFacades)
                {
                    Assert.That(passedGates,Is.EqualTo(count));
                    Assert.That(observedCorners,Is.EqualTo(corners));
                }
                Assert.That(actualAttachments,Is.EqualTo(count));Assert.That(actualReleases,Is.EqualTo(count));
                Assert.That(maxCorrection,Is.Zero);Assert.That(maxTension,Is.LessThanOrEqualTo(settings.gripMotor.tensionAcceleration+.01f));
                Assert.That(maxTotalAcceleration,Is.LessThan(171),"Motor plus cable tension, gravity and drag must stay bounded.");
                Assert.That(seen.Count,Is.EqualTo(count));Assert.That(corners,Is.GreaterThanOrEqualTo(5));
                Assert.That(travel,Is.GreaterThan(count*20));Assert.That(minHeight,Is.GreaterThan(settings.groundHeight+settings.eyeHeight));
                Assert.That(maxForce,Is.LessThanOrEqualTo(settings.gripMotor.acceleration+.01f));
                TestContext.WriteLine($"GRIP fullFacades={fullFacades} buildings={seen.Count} layoutCorners={corners} observedCorners={observedCorners} passedGates={passedGates} fps={fps} seed={seed} presses={presses} releases={releases} travel={travel:F2} minY={minHeight:F2} maxMotorAcceleration={maxForce:F2} maxTension={maxTension:F2} maxTotalAcceleration={maxTotalAcceleration:F2} correction={maxCorrection:F4} workJPerKg={motion.Body.GripMotorWork:F2}");
            }
            finally{motion.Collision.Dispose();foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rig);}
        }

        private static bool CapsulePathBlocked(Vector3 from,Vector3 to,float eyeHeight)
        {
            Vector3 bottom=Vector3.up*(eyeHeight-TraversalCollision.Radius);
            // Check both ends: sweeps alone miss a starting overlap.
            if(Physics.CheckCapsule(from-bottom,from,TraversalCollision.Radius,TraversalCollision.Mask,QueryTriggerInteraction.Ignore) ||
                Physics.CheckCapsule(to-bottom,to,TraversalCollision.Radius,TraversalCollision.Mask,QueryTriggerInteraction.Ignore))return true;
            var delta=to-from;
            return delta.sqrMagnitude>1e-10f && Physics.CapsuleCast(from-bottom,from,TraversalCollision.Radius,
                delta.normalized,delta.magnitude,TraversalCollision.Mask,QueryTriggerInteraction.Ignore);
        }

        [Test]
        public void IndependentAuditDetectsWallCrossingAndStartingOverlap()
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.layer=30;wall.transform.position=new Vector3(-41000,2000,-41000);
                wall.transform.localScale=new Vector3(1,100,30);Physics.SyncTransforms();
                var center=wall.transform.position;
                Assert.That(CapsulePathBlocked(center+Vector3.left*10,center+Vector3.right*10,1.7f),Is.True,
                    "A high-speed step must not tunnel through a facade undetected.");
                Assert.That(CapsulePathBlocked(center,center+Vector3.right*10,1.7f),Is.True,
                    "Starting inside a wall must not evade the sweep.");
                Assert.That(CapsulePathBlocked(center+Vector3.left*10,center+Vector3.left*10+Vector3.forward*5,1.7f),Is.False,
                    "A genuinely clear path must remain clear.");
            }
            finally{UnityEngine.Object.DestroyImmediate(wall);}
        }
    }
}
