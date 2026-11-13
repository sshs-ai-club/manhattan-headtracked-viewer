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
        public bool AssistanceEnabled { get; set; } // OFF until explicitly enabled each session.
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
                new GameObject("Traversal View Assistance").AddComponent<TraversalViewAssistance>();
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
            Filter.Step(Time.unscaledDeltaTime, motion.Body.Velocity, motion.Body.State == TraversalState.WallRunning,
                !motion.Body.Grounded, active, manual, PitchStrength, RollStrength);
            pivot.Transform.localRotation = Filter.Rotation(Mathf.DeltaAngle(0, rotation.eulerAngles.x));
            if (!AssistanceEnabled && Filter.Angles.sqrMagnitude < .000001f) Detach();
        }
        private void ResetView()
        {
            Filter.Reset(); sampled = false;
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
            GUILayout.BeginArea(new Rect(12, 170, 308, 170), GUI.skin.box);
            AssistanceEnabled = GUILayout.Toggle(AssistanceEnabled, "Experimental traversal view (default OFF)");
            GUILayout.Label("Look up / falling look down: " + Mathf.RoundToInt(PitchStrength * 100) + "%");
            PitchStrength = GUILayout.HorizontalSlider(PitchStrength, 0, 1);
            GUILayout.Label("Corner bank: " + Mathf.RoundToInt(RollStrength * 100) + "%");
            RollStrength = GUILayout.HorizontalSlider(RollStrength, 0, 1);
            GUILayout.Label("Manual look takes priority. Stop if uncomfortable.");
            GUILayout.EndArea();
        }
    }
}
