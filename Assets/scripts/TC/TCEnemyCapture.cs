using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Enemy screenshot + jump-clearance harness. Only active with "-tcenemy <dir>" (optional "-tcphase before|after").
/// Captures gameplay-view and close-up shots of the squirrel, the bat and (when present) the snake, then runs a
/// logged jump-over test for the snake at 1x / 1.75x / 2.5x climb speed.
public class TCEnemyCapture : MonoBehaviour
{
    string dir, phase = "after"; bool noJump; int n; const float JumpStep = 0.1f; const int Reps = 2; int curVariant;
    readonly List<string> log = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, "-tcenemy"); if (i < 0) return;
        PlayerPrefs.DeleteAll(); PlayerPrefs.SetInt("consent", 1); TCSave.TutorialDone = true;
        var g = new GameObject("TCEnemyCapture"); DontDestroyOnLoad(g);
        var c = g.AddComponent<TCEnemyCapture>(); c.dir = i + 1 < a.Length ? a[i + 1] : "/tmp";
        int p = System.Array.IndexOf(a, "-tcphase"); if (p >= 0 && p + 1 < a.Length) c.phase = a[p + 1];
        c.noJump = System.Array.IndexOf(a, "-tcnojump") >= 0;
    }

    void L(string s) { Debug.Log("[TCEnemyCapture] " + s); log.Add(s); System.IO.File.WriteAllLines(dir + "/enemy_log.txt", log); }
    IEnumerator Shot(string name) { yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot($"{dir}/{++n:00}_{name}.png"); yield return null; }
    static IEnumerator W(float s) { yield return new WaitForSecondsRealtime(s); }

    /// Close-up from an extra camera looking at the trunk surface point from outside.
    void CloseUp(string name, Vector3 target, float angleDeg, float dist = 1.25f, float up = 0.35f, float fov = 34f, float side = 0f)
    {
        var main = Camera.main; if (!main) return;
        var go = new GameObject("TCCloseCam"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(main);
        float r = angleDeg * Mathf.Deg2Rad; var outward = new Vector3(Mathf.Sin(r), 0, Mathf.Cos(r)); var tangent = new Vector3(Mathf.Cos(r), 0, -Mathf.Sin(r));
        go.transform.position = target + outward * dist + Vector3.up * up + tangent * side; go.transform.LookAt(target);
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f;
        var rt = new RenderTexture(720, 720, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(720, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 720, 720), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes($"{dir}/{++n:00}_{name}.png", tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null; Destroy(rt); Destroy(tex); Destroy(go);
    }

    static void SetPhase(int idx)
    {
        var gm = gameManager.Instance; if (!gm) return;
        var t = typeof(gameManager);
        t.GetField("currentPhaseIndex", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gm, idx);
        t.GetField("nextPhaseIndex", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gm, idx);
        t.GetField("isTransitioning", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(gm, false);
        t.GetMethod("ApplyPhaseImmediate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(gm, new object[] { idx });
        gameManager.OnBatPhaseChanged?.Invoke(gm.IsBatPhase);
    }

    playerInteract sp; player pl; TCTrack track;
    float PlayerAngle() { var p = sp.transform.position; return Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg; }
    float PY => sp.transform.position.y;
    float HitRW() { var t = sp.HitCollider.transform.lossyScale; return sp.HitCollider.radius * Mathf.Max(Mathf.Abs(t.x), Mathf.Abs(t.z)); }
    void Clear() { track.ClearHazardsInRange(PY - 30f, PY + 60f); track.ReserveClearRun(5000f); }
    static void SetField(object o, string f, object v) => o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(o, v);

    IEnumerator Start()
    {
        yield return W(2.5f);
        SceneManager.LoadScene("gameplay");
        yield return W(3f);
        GameObject.Find("TC_GameUI/Safe/Start")?.GetComponent<Button>()?.onClick.Invoke();
        var ui = FindObjectOfType<TCGameUI>(); if (ui) ui.StartRun();
        pl = FindObjectOfType<player>(); sp = FindObjectOfType<playerInteract>(); track = FindObjectOfType<TCTrack>();
        if (pl) pl.paused = false;
        SetPhase(1);
        yield return W(1.5f);
        Clear(); sp.GrantGrace(9999f);
        yield return W(0.6f);

        // ---------- spider reference ----------
        float a = PlayerAngle();
        { Time.timeScale = 0f; yield return W(0.05f);
          var spT = sp.transform; Vector3 c = spT.position + new Vector3(spT.position.x, 0, spT.position.z).normalized * 0.05f;
          CloseUp("spider_ref_close", c, a, 0.75f, 0.25f, 32f);
          CloseUp("spider_ref_side", c, a, 0.75f, 0.1f, 32f, 0.55f);
          Time.timeScale = 1f; }
        yield return W(0.3f);

        // ---------- wasp (day) ----------
        a = PlayerAngle();
        var wasp = track.SpawnTutorialWasp(PY + 1.5f, a + 10f);
        if (wasp) { wasp.UpdateReskin(); yield return W(0.5f); }
        if (wasp)
        {
            Time.timeScale = 0f; yield return W(0.05f);
            yield return Shot(phase + "_wasp_game");
            var m = wasp.transform.Find("Model");
            float wa = Mathf.Atan2(m.position.x, m.position.z) * Mathf.Rad2Deg;
            CloseUp(phase + "_wasp_close", m.position, wa, 0.9f, 0.15f, 32f);
            CloseUp(phase + "_wasp_close_side", m.position, wa, 0.9f, 0.1f, 32f, 0.6f);
            Time.timeScale = 1f;
        }
        yield return W(1.2f); Clear();

        // ---------- squirrel ----------
        a = PlayerAngle();
        var sq = track.SpawnTutorialSquirrel(PY + 3.0f, a + 18f);
        yield return W(0.45f);
        if (sq)
        {
            Time.timeScale = 0f; yield return W(0.05f);
            yield return Shot(phase + "_squirrel_game");
            var m = sq.transform.Find("Model");
            CloseUp(phase + "_squirrel_close", m.position, a + 18f, 1.7f, 0.35f, 32f);
            CloseUp(phase + "_squirrel_close_side", m.position, a + 18f, 1.5f, 0.2f, 32f, 1.0f);
            Time.timeScale = 1f;
        }
        yield return W(1.5f); Clear();
        a = PlayerAngle();
        var sq2 = track.SpawnTutorialSquirrel(PY + 1.6f, a + 8f);
        if (sq2) { yield return W(0.35f); Time.timeScale = 0f; yield return W(0.05f);
            var m2 = sq2.transform.Find("Model"); Vector3 mid = (m2.position + sp.transform.position) * 0.5f;
            CloseUp(phase + "_squirrel_with_spider", mid, a + 4f, 2.0f, 0.4f, 34f);
            Time.timeScale = 1f;
            // gait strip: 4 side-on frames a quarter stride apart
            for (int g = 0; g < 4; g++) { yield return W(0.11f); Time.timeScale = 0f; yield return null;
                var gm = sq2 ? sq2.transform.Find("Model") : null; if (gm) CloseUp(phase + "_squirrel_gait_" + g, gm.position, a + 8f, 1.1f, 0.0f, 32f, 0.9f);
                Time.timeScale = 1f; } }
        yield return W(1.5f); Clear();

        // ---------- bat (sunset) ----------
        SetPhase(3); yield return W(0.8f);
        a = PlayerAngle();
        var bat = track.SpawnTutorialWasp(PY + 2.4f, a - 14f);
        if (bat) { bat.UpdateReskin(); yield return W(0.5f); }
        if (bat)
        {
            Time.timeScale = 0f; yield return W(0.05f);
            yield return Shot(phase + "_bat_game");
            var m = bat.transform.Find("Model");
            CloseUp(phase + "_bat_close", m.position, a - 14f, 1.0f, 0.1f, 32f);
            Time.timeScale = 1f; yield return W(0.09f); Time.timeScale = 0f; yield return W(0.05f);
            CloseUp(phase + "_bat_close_flap", m.position, a - 14f, 1.0f, 0.1f, 32f, 0.4f);
            Time.timeScale = 1f;
        }
        yield return W(1.2f); Clear();
        SetPhase(1); yield return W(0.8f);

        // ---------- snake ----------
        var spawnSnake = typeof(TCTrack).GetMethod("SpawnDebugSnake");
        if (spawnSnake != null)
        {
            a = PlayerAngle();
            var snake = spawnSnake.Invoke(track, new object[] { PY + 3.8f, a - 35f, 1, 0 }) as TCEnemy;
            for (int f = 0; f < 4; f++) { yield return W(0.55f); yield return Shot("snake_spiral_" + f); }
            if (snake)
            {
                Time.timeScale = 0f; yield return W(0.05f);
                var head = snake.transform.Find("Head");
                { var bodyR = snake.transform.Find("Body").GetComponent<Renderer>(); var mf = snake.transform.Find("Body").GetComponent<MeshFilter>();
                  var hd0 = head ? head.position : Vector3.zero; var mv = mf && mf.sharedMesh ? mf.sharedMesh.vertices : new Vector3[0];
                  L($"snake dbg: root pos {snake.transform.position} rot {snake.transform.eulerAngles} scale {snake.transform.lossyScale}; body tr pos {bodyR.transform.position} scale {bodyR.transform.lossyScale}; head {hd0} (r={new Vector2(hd0.x, hd0.z).magnitude:0.000}); body bounds {bodyR.bounds.center} size {bodyR.bounds.size}; v0 {(mv.Length > 0 ? mv[0] : Vector3.zero)} (r={(mv.Length > 0 ? new Vector2(mv[0].x, mv[0].z).magnitude : 0):0.000}) vmid {(mv.Length > 500 ? mv[500] : Vector3.zero)} n={mv.Length} mat={bodyR.sharedMaterial.name} tex={(bodyR.sharedMaterial.mainTexture ? bodyR.sharedMaterial.mainTexture.name : "-")}"); }
                Vector3 hp = head ? head.position : snake.transform.position;
                float ha = Mathf.Atan2(hp.x, hp.z) * Mathf.Rad2Deg;
                CloseUp("snake_close", hp + Vector3.up * 0.25f, ha, 1.5f, 0.35f, 36f);
                CloseUp("snake_close_head", hp, ha, 0.75f, 0.15f, 30f);
                CloseUp("snake_close_wrap", hp + Vector3.down * 0.35f, ha - 40f, 2.4f, 0.4f, 40f);
                // tongue-out frame: step until the flick loop has the tongue fully out
                var tg = head ? head.Find("Tongue") : null;
                Time.timeScale = 1f; float guard = 0f;
                while (tg && (!tg.gameObject.activeSelf || tg.localScale.z < 0.7f) && guard < 3f) { guard += Time.unscaledDeltaTime; yield return null; }
                Time.timeScale = 0f; yield return W(0.05f);
                hp = head ? head.position : hp; ha = Mathf.Atan2(hp.x, hp.z) * Mathf.Rad2Deg;
                CloseUp("snake_close_tongue_out", hp, ha + 8f, 0.6f, 0.12f, 28f);
                Time.timeScale = 1f; guard = 0f;
                while (tg && tg.gameObject.activeSelf && guard < 2f) { guard += Time.unscaledDeltaTime; yield return null; }
                while (tg && (!tg.gameObject.activeSelf || tg.localScale.z < 0.6f) && guard < 4f) { guard += Time.unscaledDeltaTime; yield return null; }
                Time.timeScale = 0f; yield return W(0.03f);
                hp = head ? head.position : hp; ha = Mathf.Atan2(hp.x, hp.z) * Mathf.Rad2Deg;
                CloseUp("snake_close_tongue_flex", hp, ha + 8f, 0.6f, 0.12f, 28f);
                Time.timeScale = 1f;
            }
            yield return W(0.5f); Clear();
            // variant line-up: green / yellow / red near the spider, then a head close-up of each
            a = PlayerAngle(); var vs = new TCEnemy[3];
            for (int v = 0; v < 3; v++) vs[v] = spawnSnake.Invoke(track, new object[] { PY + 1.6f + v * 0.9f, a - 40f + v * 25f, 1, v }) as TCEnemy;
            yield return W(0.35f); Time.timeScale = 0f; yield return W(0.05f);
            yield return Shot("snake_variants_game");
            CloseUp("snake_variants_with_spider", sp.transform.position + Vector3.up * 1.4f, a - 10f, 3.0f, 0.6f, 40f);
            string[] vn = { "green", "yellow", "red" };
            for (int v = 0; v < 3; v++) { var hd = vs[v] ? vs[v].transform.Find("Head") : null; if (!hd) continue;
                float hha = Mathf.Atan2(hd.position.x, hd.position.z) * Mathf.Rad2Deg; CloseUp("snake_head_" + vn[v], hd.position, hha, 0.7f, 0.18f, 30f); }
            Time.timeScale = 1f; yield return W(0.3f);
            foreach (var e in vs) if (e && e.pooled) e.pooled.Release();
            Clear();
            if (!noJump) yield return JumpTests();
        }
        Application.Quit();
    }

    int hits; float hitT, hitAir, trialStart; TCCuteSpider cuteRef;
    void OnHit(string e, Vector3 p) { if (e == "hit" || e == "magnet_break") { hits++; hitT = Time.time - trialStart; hitAir = cuteRef ? cuteRef.CurrentAirHeight : -1; } }

    /// Sets climb speed by shifting the run start so ClimbSpeedAtDistance(currentScore) hits the target multiplier.
    void SetSpeedMult(float mult)
    {
        var C = TCConfig.I;
        float metres = Mathf.InverseLerp(C.startSpeed, C.EffectiveMaxSpeed, C.startSpeed * mult) * C.metresToMaxSpeed;
        float startY = PY - metres / C.metresPerUnit;
        SetField(pl, "startY", startY);
    }

    IEnumerator JumpTests()
    {
        playerInteract.OnEvent += OnHit;
        var cute = sp.GetComponentInChildren<TCCuteSpider>(); cuteRef = cute;
        var combos = new (int v, float mult)[] { (2, 1f), (2, 1.75f), (2, 2.5f), (0, 1f), (0, 2.5f), (1, 1f), (1, 2.5f) };
        foreach (var combo in combos)
        {
            float mult = combo.mult; curVariant = combo.v; var clean = new SortedDictionary<float, bool>();
            L($"=== {(TCSnake.Variant)curVariant} snake (speed x{TCSnake.SpeedMulFor((TCSnake.Variant)curVariant)}) vs spider {mult:0.00}x ===");
            SetSpeedMult(mult); yield return W(0.6f);
            // 1) measure the airborne-clear window of a plain jump at this speed
            SetField(sp, "invulnTimer", 0.001f); Clear();
            yield return W(0.4f); var bc0 = sp.HitCollider.bounds.center; float rest = new Vector2(bc0.x, bc0.z).magnitude; L($"hit capsule world size {sp.HitCollider.bounds.size}, centre radius {rest:0.000}"); pl.jump(); if (!sp.IsJumping) { yield return W(0.3f); pl.jump(); } float t0 = Time.time, clearStart = -1, clearEnd = -1, peak = 0;
            while (Time.time - t0 < 1.6f)
            {
                var bc = sp.HitCollider.bounds.center; float h = new Vector2(bc.x, bc.z).magnitude - rest; peak = Mathf.Max(peak, h);
                if (sp.IsAirborneClear || h > 0.10f) { if (clearStart < 0) clearStart = Time.time - t0; clearEnd = Time.time - t0; }
                yield return null;
            }
            float v = pl.calcSpeed;
            L($"speed {mult:0.00}x: climb {v:0.00} u/s, jump peak radial lift {peak:0.00} u, lifted>0.10u {clearStart:0.00}s..{clearEnd:0.00}s (window {clearEnd - clearStart:0.00}s, covers {(clearEnd - clearStart) * v:0.00} u of climb)");

            // 2) snake crossing trials: jump at various lead times before reaching the band; plus a no-jump control
            var leads = new List<float> { -1f }; for (float q = 0f; q <= 0.751f; q += JumpStep) leads.Add(q);
            foreach (var lead in leads)
            {
                Clear(); SetField(sp, "invulnTimer", 0.001f); sp.life = Mathf.Max(sp.life, 3); yield return W(0.25f);
                SetField(sp, "invulnTimer", 0.001f);
                for (int rep = 0; rep < Reps; rep++) {
                Clear(); SetField(sp, "invulnTimer", 0.001f); sp.life = 3; yield return W(0.15f);
                { float g = 0f; while (TCWebBonus.Instance && TCWebBonus.Instance.IsInvincible && g < 8f) { g += Time.deltaTime; yield return null; } }
                SetField(sp, "invulnTimer", 0.001f);
                string state = $"web={(TCWebBonus.Instance && TCWebBonus.Instance.IsInvincible)} magnet={(TCCoinMagnet.Instance && TCCoinMagnet.Instance.IsActive)} dead={sp.IsDead} life={sp.life}";
                float T = 1.6f; // seconds until the spider's hit capsule reaches the band
                float bandY;
                var snake = SpawnCrossing(T, out bandY);
                if (!snake) { L("no snake"); break; }
                hits = 0; float start = Time.time; trialStart = start; bool jumped = false;
                Vector3 hc = sp.HitCollider.bounds.center;
                float jumpAt = T - lead;
                float minGap = 9f, maxAir = 0f, airAtBand = -1f; var cols = snake.GetComponentsInChildren<SphereCollider>();
                while (Time.time - start < T + 1.4f)
                {
                    if (!jumped && lead >= 0 && Time.time - start >= jumpAt) { pl.jump(); jumped = true; }
                    var c = sp.HitCollider.bounds.center;
                    foreach (var sc in cols) { if (!sc || !sc.enabled) continue; float g = Vector3.Distance(c, sc.bounds.center) - sc.bounds.extents.x - HitRW(); if (g < minGap) minGap = g; }
                    float air = cute ? cute.CurrentAirHeight : 0; maxAir = Mathf.Max(maxAir, air);
                    if (airAtBand < 0 && Time.time - start >= T) airAtBand = air;
                    yield return null;
                }
                L($"  {mult:0.00}x lead {(lead < 0 ? "no-jump" : lead.ToString("0.00") + "s")}: hits={hits} minGap={minGap:0.00} maxAir={maxAir:0.00} airAtBand={airAtBand:0.00} jumping={sp.IsJumping} [{state}]" + (hits > 0 ? $" (t={hitT - T:+0.00;-0.00}s vs band, air={hitAir:0.00})" : ""));
                if (snake && snake.gameObject.activeSelf && snake.pooled) snake.pooled.Release();
                if (lead >= 0) clean[lead] = (!clean.TryGetValue(lead, out var cprev) || cprev) && hits == 0;
                }
            }
            var ok = new List<string>(); foreach (var kv in clean) if (kv.Value) ok.Add(kv.Key.ToString("0.0"));
            L($"  SUMMARY {(TCSnake.Variant)curVariant} @ {mult:0.00}x: clean leads (all reps, 0 hits) = [{string.Join(", ", ok)}]s");
        }
        curVariant = 0;
        playerInteract.OnEvent -= OnHit;
        // Visual: jump over + hit frames at 1x
        SetPhase(1); SetSpeedMult(1f); yield return W(0.5f);
        Clear(); SetField(sp, "invulnTimer", 0.001f); sp.life = 3;
        { float by; var s = SpawnCrossing(1.6f, out by); float st = Time.time; bool j = false, shot1 = false, shot2 = false;
          while (Time.time - st < 2.6f) { float e = Time.time - st;
            if (!j && e >= 1.6f - 0.45f) { pl.jump(); j = true; }
            if (!shot1 && e >= 1.42f) { shot1 = true; yield return Shot("snake_jump_over_a"); }
            if (!shot2 && e >= 1.62f) { shot2 = true; yield return Shot("snake_jump_over_b"); }
            yield return null; } if (s && s.pooled) s.pooled.Release(); }
        Clear(); SetField(sp, "invulnTimer", 0.001f);
        hits = 0; playerInteract.OnEvent += OnHit;
        for (int tries = 0; tries < 4 && hits == 0; tries++)
        { Clear(); SetField(sp, "invulnTimer", 0.001f); sp.life = 3; yield return W(0.3f); float by; var s = SpawnCrossing(1.4f, out by); float st = Time.time; bool shot = false;
          while (Time.time - st < 2.4f) { if (!shot && hits > 0) { shot = true; yield return W(0.05f); yield return Shot("snake_player_hit"); } yield return null; }
          if (s && s.pooled) s.pooled.Release(); }
        playerInteract.OnEvent -= OnHit; L($"visual hit run: hits={hits}");
    }

    TCEnemy SpawnCrossing(float T, out float bandY)
    {
        bandY = 0;
        var m = typeof(TCTrack).GetMethod("SpawnDebugSnakeCrossing");
        if (m == null) return null;
        float a = PlayerAngle();
        float yHit = sp.HitCollider.bounds.center.y;
        object[] args = { a, yHit, pl.calcSpeed, T, curVariant, 0f };
        var e = m.Invoke(track, args) as TCEnemy; bandY = (float)args[5];
        return e;
    }
}
