using Manhattan.Viewer;
using UnityEngine;

namespace WebShooter.Swing
{
    /// <summary>Read-only motion cues. Two cascaded low-pass stages bound angle, rate and acceleration.</summary>
    public sealed class TraversalViewFilter
    {
        private Vector2 first;
        private Vector3 previousHorizontal;
        private float manualHold;
        public Vector2 Angles { get; private set; } // x: pitch down, y: roll left
        public void Reset() { first = Angles = Vector2.zero; previousHorizontal = Vector3.zero; manualHold = 0; }
        public Vector2 Step(float dt, Vector3 velocity, bool wall, bool airborne, bool active,
            bool manualLook, float pitchStrength, float rollStrength)
        {
            if (!float.IsFinite(dt) || dt <= 0) return Angles;
            if (dt > .1f) { active = false; previousHorizontal = Vector3.zero; }
            dt = Mathf.Min(dt, .1f); // Never catch up a stalled frame with a large camera rotation.
            bool finite = float.IsFinite(velocity.x) && float.IsFinite(velocity.y) && float.IsFinite(velocity.z);
            active &= finite && float.IsFinite(velocity.sqrMagnitude);
            if (manualLook) manualHold = .8f;
            bool suppressed = manualHold > 0;
            manualHold = Mathf.Max(0, manualHold - dt);
            Vector3 horizontal = active ? new Vector3(velocity.x, 0, velocity.z) : Vector3.zero;
            float turn = horizontal.sqrMagnitude > 9 && previousHorizontal.sqrMagnitude > 9
                ? Vector3.SignedAngle(previousHorizontal, horizontal, Vector3.up) / dt : 0;
            previousHorizontal = horizontal;
            float pitch = wall ? -55 * Mathf.InverseLerp(2, 14, velocity.y)
                : airborne ? 35 * Mathf.InverseLerp(6, 24, -velocity.y) : 0;
            var target = active && !suppressed ? new Vector2(pitch * Strength(pitchStrength),
                Mathf.Clamp(-turn * .08f, -8, 8) * Strength(rollStrength)) : Vector2.zero;
            // Exact integration for a constant target over this sample; no Euler overshoot.
            Angles = new Vector2(Filter(target.x, ref first.x, Angles.x, .8f, dt),
                Filter(target.y, ref first.y, Angles.y, 2f, dt));
            return Angles;
        }
        public static bool WallAscentCue(TraversalState state, bool attached, Vector3 velocity, Vector3 normal, Vector3 toAnchor)
        {
            // Minimal-grip winching can climb a wall without requesting the WASD WallRunning state.
            if (state == TraversalState.WallRunning) return true;
            return attached && float.IsFinite(velocity.sqrMagnitude) && float.IsFinite(normal.sqrMagnitude) &&
                float.IsFinite(toAnchor.sqrMagnitude) && normal.sqrMagnitude > .5f &&
                Mathf.Abs(normal.normalized.y) < .3f && velocity.y > 2 && toAnchor.y > 2 &&
                Vector3.Dot(velocity, toAnchor) > 0;
        }
        private static float Strength(float value) => float.IsFinite(value) ? Mathf.Clamp01(value) : 0;
        private static float Filter(float target, ref float stage, float angle, float frequency, float dt)
        {
            float decay = Mathf.Exp(-frequency * dt), old = stage;
            stage = target + (old - target) * decay;
            return target + (angle - target + (old - target) * frequency * dt) * decay;
        }
        public Quaternion Rotation(float manualPitch)
        {
            // Manual input owns the poles. Assistance cannot tip the combined view past them.
            float low = Mathf.Min(0, -80 - manualPitch), high = Mathf.Max(0, 80 - manualPitch);
            float applied = Mathf.Clamp(Angles.x, low, high);
            if (applied != Angles.x)
            {
                // Discard hidden tilt at a pole; it must not cancel manual movement away from the pole.
                first.x = applied; Angles = new Vector2(applied, Angles.y);
            }
            return Quaternion.Euler(applied, 0, Angles.y);
        }
    }

    /// <summary>Velocity tangent in the observer frame, without rotating the physics body.</summary>
    public sealed class TraversalTangentFilter
    {
        public Vector2 Angles { get; private set; } // x: local pitch, y: continuous local yaw
        private Vector2 rate;
        private float manualHold;
        public void Reset() { Angles = rate = Vector2.zero; manualHold = 0; }
        public Vector2 Step(float dt, Vector3 worldVelocity, Quaternion observer, bool active, bool manualLook, float fallbackPitch = 0)
        {
            if (!float.IsFinite(dt) || dt <= 0) return Angles;
            if (dt > .1f) active = false;
            dt = Mathf.Min(dt, .1f);
            if (manualLook) manualHold = .8f;
            bool suppressed = manualHold > 0;
            manualHold = Mathf.Max(0, manualHold - dt);
            bool finite = float.IsFinite(worldVelocity.sqrMagnitude) &&
                float.IsFinite(observer.x) && float.IsFinite(observer.y) &&
                float.IsFinite(observer.z) && float.IsFinite(observer.w);
            var v = finite ? Quaternion.Inverse(observer) * worldVelocity : Vector3.zero;
            active &= finite && worldVelocity.sqrMagnitude > 9 && !suppressed;
            float horizontal = new Vector2(v.x, v.z).magnitude;
            // A nearly vertical trajectory has no stable heading. Retain yaw, rather than jitter.
            float yaw = active ? (horizontal > 1 ? Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg : Angles.y) : 0;
            float pitch = active ? Mathf.Clamp(-Mathf.Atan2(v.y, horizontal) * Mathf.Rad2Deg, -65, 65)
                : float.IsFinite(fallbackPitch) ? Mathf.Clamp(fallbackPitch, -65, 65) : 0;
            var current = Angles;
            current.x = Advance(current.x, pitch, ref rate.x, 40, 100, dt, false);
            current.y = Advance(current.y, yaw, ref rate.y, 55, 120, dt, true);
            Angles = current;
            return Angles;
        }
        private static float Advance(float angle, float target, ref float speed, float limit, float acceleration, float dt, bool wrap)
        {
            float error = wrap ? Mathf.DeltaAngle(angle, target) : target - angle;
            // Proportional velocity servo with independently bounded angular velocity/acceleration.
            float desired = Mathf.Clamp(error * 2, -limit, limit);
            speed = Mathf.MoveTowards(speed, desired, acceleration * dt);
            return angle + speed * dt;
        }
        public Quaternion Rotation(float manualPitch) => Rotation(Quaternion.Euler(manualPitch, 0, 0));
        public Quaternion Rotation(Quaternion observer)
        {
            var local = Quaternion.Euler(Angles.x, Angles.y, 0);
            var forward = observer * local * Vector3.forward;
            float elevation = Mathf.Asin(Mathf.Clamp(forward.y, -1, 1)) * Mathf.Rad2Deg;
            float manualElevation = Mathf.Asin(Mathf.Clamp((observer * Vector3.forward).y, -1, 1)) * Mathf.Rad2Deg;
            float limit = Mathf.Max(80, Mathf.Abs(manualElevation));
            bool inverted = (observer * local * Vector3.up).y < 0;
            if (Mathf.Abs(elevation) > limit || inverted)
            {
                float yaw = Mathf.Atan2(inverted ? -forward.x : forward.x, inverted ? -forward.z : forward.z) * Mathf.Rad2Deg;
                var boundedForward = Quaternion.Euler(-Mathf.Sign(elevation) * limit, yaw, 0) * Vector3.forward;
                var boundedLocal = Quaternion.Inverse(observer) * boundedForward;
                float pitch = -Mathf.Atan2(boundedLocal.y, new Vector2(boundedLocal.x, boundedLocal.z).magnitude) * Mathf.Rad2Deg;
                float localYaw = Mathf.Atan2(boundedLocal.x, boundedLocal.z) * Mathf.Rad2Deg;
                // Discard hidden orientation at a pole, including yaw/pitch coupling.
                Angles = new Vector2(pitch, Angles.y + Mathf.DeltaAngle(Angles.y, localYaw));
                rate = Vector2.zero;
                local = Quaternion.Euler(Angles.x, Angles.y, 0);
            }
            // Cancel incidental roll from composing observer pitch with tangent yaw. Only the bank cue rolls.
            return Quaternion.Inverse(observer) * Quaternion.LookRotation(observer * local * Vector3.forward, Vector3.up);
        }
        public bool Neutral => Mathf.Abs(Angles.x) < .001f && Mathf.Abs(Mathf.DeltaAngle(0, Angles.y)) < .001f;
    }

    /// <summary>A disposable view-only pivot; never reparents the observer or writes projection/calibration.</summary>
    public sealed class TraversalViewPivot
    {
        public Transform Transform { get; private set; }
        private readonly Transform observer, screen, camera;
        public bool OwnsChildren => Transform != null && screen != null && camera != null &&
            screen.parent == Transform && camera.parent == Transform;
        public TraversalViewPivot(Transform body, Transform plane, Transform eye)
        {
            if (body == null || plane == null || eye == null || plane == eye ||
                plane.parent != body || eye.parent != body)
                throw new System.ArgumentException("The screen and camera must be distinct direct observer children.");
            observer = body; screen = plane; camera = eye;
            Transform = new GameObject("Traversal View Pivot").transform;
            Transform.SetParent(observer, false);
            screen.SetParent(Transform, false); camera.SetParent(Transform, false);
        }
        public void Restore()
        {
            if (Transform == null) return;
            // Keep any calibration/head-pose changes made while installed. Never reclaim a reparented child.
            Transform.localRotation = Quaternion.identity;
            if (screen != null && screen.parent == Transform) screen.SetParent(observer, false);
            if (camera != null && camera.parent == Transform) camera.SetParent(observer, false);
            if (Transform.childCount == 0)
            {
                if (Application.isPlaying) Object.Destroy(Transform.gameObject);
                else Object.DestroyImmediate(Transform.gameObject);
            }
            Transform = null;
        }
    }

    [DefaultExecutionOrder(-100)] // After flight/origin Update; before the package's LateUpdate projection.
    public sealed class TraversalViewAssistance : MonoBehaviour
    {
        public bool AssistanceEnabled { get; set; } // OFF unless explicitly selected in Settings or command line.
        public bool TangentEnabled { get; set; } = true;
        public readonly TraversalTangentFilter Tangent = new TraversalTangentFilter();
        public float PitchStrength { get; set; } = .6f;
        public float RollStrength { get; set; } = .35f;
        public readonly TraversalViewFilter Filter = new TraversalViewFilter();
        private ManhattanBootstrap app;
        private TraversalViewPivot pivot;
        private Quaternion lastRotation;
        private bool sampled;
        private float nextSearch;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (ManhattanBootstrap.HasArgument("-runTests") || ManhattanBootstrap.HasArgument("--benchmark") ||
                ManhattanBootstrap.HasArgument("--traversal-agent") || ManhattanBootstrap.HasArgument("--scripted-flow") ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "TraversalLab") return;
            if (FindFirstObjectByType<TraversalViewAssistance>() == null)
                {
                var assistance = new GameObject("Traversal View Assistance").AddComponent<TraversalViewAssistance>();
                assistance.AssistanceEnabled = ManhattanBootstrap.HasArgument("--traversal-view");
            }
        }
        public static bool ManualLookChanged(Quaternion previous, Quaternion current)
        {
            // Quaternion.Angle rounds small rotations to zero (~0.16 degrees), losing slow mouse look.
            var delta = Quaternion.Inverse(previous) * current;
            return new Vector3(delta.x, delta.y, delta.z).sqrMagnitude > 1e-10f;
        }
        private void Update()
        {
            if (pivot != null && !pivot.OwnsChildren) { AssistanceEnabled = false; Detach(); }
            if (app == null)
            {
                if (Time.unscaledTime < nextSearch) return;
                nextSearch = Time.unscaledTime + 1;
                app = FindFirstObjectByType<ManhattanBootstrap>();
            }
            if (app == null || app.Flight == null || app.Display == null) return;
            var motion = app.Flight.InputSource as WebSwingMotionInput;
            if (motion == null) { Detach(); return; }
            // OFF leaves the original hierarchy completely untouched until the first opt-in.
            if (pivot == null && AssistanceEnabled)
            {
                var plane = app.Display.ScreenPlane; var eye = app.Display.transform;
                if (plane == null || plane.parent != app.Flight.transform || eye.parent != app.Flight.transform) return;
                pivot = new TraversalViewPivot(app.Flight.transform, plane, eye);
                app.Flight.Teleported += ResetView;
                ResetView();
            }
            if (pivot == null) return;
            var rotation = app.Flight.transform.rotation;
            // Observe applied manual look from any input adapter; do not sample/consume input a second time.
            bool manual = sampled && ManualLookChanged(lastRotation, rotation);
            lastRotation = rotation; sampled = true;
            bool active = AssistanceEnabled && Application.isFocused && motion.Enabled && !motion.Paused &&
                app.KeyboardInput != null && app.KeyboardInput.InputEnabled && app.KeyboardInput.Captured;
            bool ascendingWall = TraversalViewFilter.WallAscentCue(motion.Body.State, motion.Body.Attached,
                motion.Body.Velocity, motion.ShotSurfaceNormal, motion.Body.Anchor - motion.Body.Position);
            Filter.Step(Time.unscaledDeltaTime, motion.Body.Velocity, ascendingWall,
                !motion.Body.Grounded, active, manual, PitchStrength, RollStrength);
            float manualPitch = Mathf.DeltaAngle(0, rotation.eulerAngles.x);
            Filter.Rotation(manualPitch); // Maintain legacy manual-pole clipping even while its cue is hidden.
            Tangent.Step(Time.unscaledDeltaTime, motion.Body.Velocity, rotation,
                active && TangentEnabled && !motion.Body.Grounded, manual,
                active && !TangentEnabled ? Filter.Angles.x : 0);
            // A single output servo also handles mode changes; never switch to a hidden legacy tilt.
            pivot.Transform.localRotation = Tangent.Rotation(rotation) * Quaternion.Euler(0, 0, Filter.Angles.y);
            if (!AssistanceEnabled && Filter.Angles.sqrMagnitude < .000001f && Tangent.Neutral) Detach();
        }
        private void ResetView()
        {
            Filter.Reset(); Tangent.Reset(); sampled = false;
            if (pivot != null && pivot.Transform != null) pivot.Transform.localRotation = Quaternion.identity;
        }
        private void Detach()
        {
            if (app != null && app.Flight != null) app.Flight.Teleported -= ResetView;
            pivot?.Restore(); pivot = null; ResetView();
        }
        private void OnDisable() => Detach();
        private void OnGUI()
        {
            if (app == null || app.KeyboardInput == null || app.KeyboardInput.InputEnabled || app.Flight == null ||
                !(app.Flight.InputSource is WebSwingMotionInput)) return;
            GUILayout.BeginArea(new Rect(12, 170, 340, 210), GUI.skin.box);
            AssistanceEnabled = GUILayout.Toggle(AssistanceEnabled, "Experimental traversal view (default OFF)");
            TangentEnabled = GUILayout.Toggle(TangentEnabled, "Follow travel tangent (yaw + pitch)");
            GUILayout.Label("Legacy climb/fall (when tangent OFF): " + Mathf.RoundToInt(PitchStrength * 100) + "%");
            PitchStrength = GUILayout.HorizontalSlider(PitchStrength, 0, 1);
            GUILayout.Label("Corner bank: " + Mathf.RoundToInt(RollStrength * 100) + "%");
            RollStrength = GUILayout.HorizontalSlider(RollStrength, 0, 1);
            GUILayout.Label("Manual look takes priority. Stop if uncomfortable.");
            GUILayout.EndArea();
        }
    }
}
