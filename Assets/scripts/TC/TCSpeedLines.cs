using UnityEngine;

/// polish2: camera-space speed streaks around the screen edges (centre stays clear). Subtle at normal
/// climbing speed, ramps with distance speed-up, strong and fiery while the chilli boost is on, off in slow-mo.
/// Only adds a particle child to the main camera; never moves, rotates or zooms the camera.
public class TCSpeedLines : MonoBehaviour
{
    ParticleSystem lines, embers; player pl; playerInteract pi; Camera cam;
    float level;

    void Start()
    {
        pl = FindObjectOfType<player>(); pi = FindObjectOfType<playerInteract>();
        cam = pl && pl.maincam ? pl.maincam : Camera.main;
        if (!cam) { enabled = false; return; }
        var root = new GameObject("TC_SpeedLines").transform;
        root.SetParent(cam.transform, false);
        root.localPosition = new Vector3(0, 0, 2.6f);
        var ls = cam.transform.lossyScale; root.localScale = new Vector3(1f / Mathf.Max(0.01f, ls.x), 1f / Mathf.Max(0.01f, ls.y), 1f / Mathf.Max(0.01f, ls.z)); // camera rig is scaled
        root.localPosition = new Vector3(0, 0, 2.6f / Mathf.Max(0.01f, ls.z));
        float halfH = 2.6f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), halfW = halfH * Mathf.Max(0.45f, cam.aspect);

        lines = TCFxLib.PS(root, "Streaks", "Spark", Vector3.zero, 120);
        {
            var m = lines.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f); m.startSpeed = 0;
            m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f); m.startColor = new Color(1, 1, 1, 0.5f);
            var sh = lines.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1f; sh.radiusThickness = 0.32f;
            sh.scale = new Vector3(halfW * 1.25f, halfH * 1.15f, 1f);
            var v = lines.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(0, 0); v.y = new ParticleSystem.MinMaxCurve(-9f, -6f); v.z = new ParticleSystem.MinMaxCurve(-1.2f, -0.6f);
            var r = lines.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.045f; r.lengthScale = 2f;
            TCFxLib.ColorLife(lines, TCFxLib.Fade(Color.white, Color.white, 0.15f, 0.6f));
        }
        embers = TCFxLib.PS(root, "BoostEmbers", "Spark", Vector3.zero, 60);
        {
            var m = embers.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f); m.startSpeed = 0;
            m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f); m.startColor = new ParticleSystem.MinMaxGradient(TCFxLib.Hex("#FFE28A"), TCFxLib.Hex("#FF7A2A"));
            var sh = embers.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1f; sh.radiusThickness = 0.45f;
            sh.scale = new Vector3(halfW * 1.2f, halfH * 1.1f, 1f);
            var v = embers.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f); v.y = new ParticleSystem.MinMaxCurve(-6f, -3.5f); v.z = new ParticleSystem.MinMaxCurve(-0.8f, -0.3f);
            var nz = embers.noise; nz.enabled = true; nz.strength = 0.5f; nz.frequency = 1.5f; nz.quality = ParticleSystemNoiseQuality.Low;
            var r = embers.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.03f; r.lengthScale = 1.2f;
            TCFxLib.ColorLife(embers, TCFxLib.Fade(Color.white, TCFxLib.Hex("#FF5A1A"), 0.1f, 0.5f));
        }
    }

    void Update()
    {
        if (lines && !lines.isPlaying) { lines.Play(true); embers.Play(true); }
        if (!lines || !pl) return;
        var C = TCConfig.I;
        bool running = !pl.paused && !pl.GameOver && Time.timeScale > 0.05f;
        bool slow = TCTimeDilation.Instance != null && TCTimeDilation.Instance.IsActive;
        bool boost = pl.boost;
        bool webSwing = TCWebBonus.Instance && TCWebBonus.Instance.IsSwinging;
        float mult = C ? C.SpeedMultiplierAtDistance(pl.currentScore) : 1f;
        float target = webSwing ? 0.95f : !running || slow ? 0f : boost ? 1f : Mathf.Clamp01((mult - 1.15f) / 1.2f) * 0.3f;
        level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime * (target > level ? 4f : 2f));

        var em = lines.emission; em.rateOverTime = level * (webSwing ? 140f : 95f);
        var m = lines.main;
        m.startColor = webSwing ? new Color(0.85f, 0.95f, 1f, 0.8f)
                     : boost ? new Color(1f, 0.9f, 0.7f, 0.75f)
                     : new Color(1f, 1f, 1f, 0.45f);
        var ee = embers.emission; ee.rateOverTime = (boost && running) || webSwing ? (webSwing ? 28f : 45f) : 0f;
    }
}
