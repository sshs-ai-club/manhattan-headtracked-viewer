using System;
using UnityEngine;
namespace WebShooter.Swing
{
    [Serializable]
    public sealed class GripMotorSettings
    {
        public bool enabled;
        [Min(0)] public float acceleration = 55f;
        [Min(0)] public float speed = 18f;
        [Min(0)] public float specificPower = 1200f;
        [Min(0)] public float clearance = 4f;
        [Min(0)] public float tensionAcceleration = 90f, stiffness = 35f, damping = 8f;
    }
    /// <summary>A powered web winch, not a pose target. Work is joules per kg.</summary>
    public static class GripRopeMotor
    {
        public static Vector3 Apply(Vector3 velocity, Vector3 toAnchor, float dt,
            GripMotorSettings settings, out float work, out float acceleration)
        {
            work = acceleration = 0;
            if (settings == null || !settings.enabled || !float.IsFinite(dt) || dt <= 0 ||
                !Finite(velocity) || !Finite(toAnchor)) return velocity;
            float distance = toAnchor.magnitude;
            float remaining = distance - Safe(settings.clearance);
            float a = Safe(settings.acceleration), power = Safe(settings.specificPower);
            if (distance <= .001f || a <= 0 || power <= 0) return velocity;
            Vector3 direction = toAnchor / distance;
            float radial = Vector3.Dot(velocity, direction);
            // Reduce added tension near the surface. A tensile web cannot brake inward motion.
            float target = Mathf.Min(Safe(settings.speed), Mathf.Sqrt(2f * a * Mathf.Max(0,remaining)));
            float impulse = Mathf.Min(a * dt, Mathf.Max(0, target - radial));
            // Solve radial*dv + dv^2/2 <= power*dt, including outward motion.
            float powerLimit = -radial + Mathf.Sqrt(radial * radial + 2f * power * dt);
            if (impulse > 0) impulse = Mathf.Min(impulse, powerLimit);
            work = radial * impulse + .5f * impulse * impulse;
            acceleration = impulse / dt;
            return velocity + direction * impulse;
        }
        public static float Tension(Vector3 velocity,Vector3 toAnchor,float length,GripMotorSettings settings)
        {
            float distance=toAnchor.magnitude,extension=distance-length;
            if(distance<.001f || extension<0)return 0;
            float outward=-Vector3.Dot(velocity,toAnchor/distance);
            return Mathf.Clamp(Safe(settings.stiffness)*extension+Safe(settings.damping)*outward,0,Safe(settings.tensionAcceleration));
        }
        private static float Safe(float value) => float.IsFinite(value) ? Mathf.Max(0, value) : 0;
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
    /// <summary>Neutral must be observed after pause/disconnect before a grip can fire.</summary>
    public sealed class GripEdges
    {
        private bool armed, held;
        public bool Pressed { get; private set; }
        public bool Held { get; private set; }
        public bool Released { get; private set; }
        public void Update(bool eligible, bool grip)
        {
            Pressed = Released = false;
            if (!eligible) { Released = held; held = Held = armed = false; return; }
            if (!grip) { armed = true; Released = held; held = Held = false; return; }
            Held = armed;
            Pressed = Held && !held;
            held = Held;
        }
    }
}
