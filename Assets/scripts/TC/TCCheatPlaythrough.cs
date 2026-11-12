using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Drives the settings cheat with real EventSystem pointer events (no Feed calls).
/// Also boots a short recorded playthrough when the player is launched with "-tccheat <dir>".
public class TCCheatPlaythrough : MonoBehaviour
{
    string dir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-tccheat");
        if (i < 0) return;
        var g = new GameObject("TCCheatPlaythrough");
        Object.DontDestroyOnLoad(g);
        var p = g.AddComponent<TCCheatPlaythrough>();
        p.dir = (i + 1 < args.Length) ? args[i + 1] : "/tmp";
    }

    IEnumerator Start()
    {
        if (string.IsNullOrEmpty(dir)) yield break;
        yield return new WaitForSecondsRealtime(3f);
        foreach (var cg in Resources.FindObjectsOfTypeAll<CanvasGroup>())
            if (cg && cg.name == "Daily" && cg.gameObject.scene.IsValid() && cg.gameObject.activeSelf)
            {
                var menu = Object.FindObjectOfType<TCMenuUI>();
                if (menu) TCUI.Hide(menu, cg);
            }
        yield return new WaitForSecondsRealtime(0.4f);
        var ui = Object.FindObjectOfType<TCMenuUI>();
        if (!ui)
        {
            Debug.LogError("[TCCheatPlaythrough] TCMenuUI not found");
            Application.Quit();
            yield break;
        }
        bool ok = false;
        yield return Run(ui, v => ok = v);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(dir.TrimEnd('/') + "/cheat_real_events.png");
        yield return null;
        Debug.Log(ok
            ? "[TCCheatPlaythrough] SUCCESS toast fired after real pointer events"
            : "[TCCheatPlaythrough] FAIL toast did not fire");
        Application.Quit();
    }

    public static IEnumerator Run(TCMenuUI menu, System.Action<bool> done = null)
    {
        if (!menu)
        {
            done?.Invoke(false);
            yield break;
        }

        var settings = menu.GetPanel("Settings");
        if (settings) TCUI.Show(menu, settings);
        // Wait until the panel's show animation enables interactable (it stays
        // false for the whole jelly pop — ~0.8s — and Button clicks are ignored until then).
        float wait = 0f;
        while (settings && !settings.interactable && wait < 1.5f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.05f);

        var sound = FindNamed(settings, "Sound", "T");
        var vibe = FindNamed(settings, "Vibration", "T");
        var slider = settings ? settings.GetComponentInChildren<Slider>(true) : null;
        if (!sound || !vibe || !slider)
        {
            Debug.LogError("[TCCheatPlaythrough] missing Sound/Vibration/Slider in baked settings");
            done?.Invoke(false);
            yield break;
        }

        // Case 1: ONE continuous drag — finger down, sweep L-R-L-R, finger up.
        Debug.Log("[TCCheatPlaythrough] CASE continuous: Sound x2, Vibration x2, one L-R-L-R drag");
        yield return Click(sound);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Click(sound);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Click(vibe);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Click(vibe);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return SweepContinuous(slider, 0.2f, 0f, 1f, 0f, 1f);
        yield return new WaitForSecondsRealtime(0.45f);
        bool continuous = menu.CheatActivated;
        Debug.Log("[TCCheatPlaythrough] continuous drag CheatActivated=" + continuous);

        // Case 2: separate drags — lift between each extreme.
        menu.ClearCheatActivated();
        Debug.Log("[TCCheatPlaythrough] CASE separate: Sound x2, Vibration x2, four separate drags");
        yield return Click(sound);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Click(sound);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Click(vibe);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return Click(vibe);
        yield return new WaitForSecondsRealtime(0.15f);
        yield return DragSlider(slider, 0.2f, 0f, reuseDown: true, release: true);
        yield return DragSlider(slider, 0f, 1f, reuseDown: true, release: true);
        yield return DragSlider(slider, 1f, 0f, reuseDown: true, release: true);
        yield return DragSlider(slider, 0f, 1f, reuseDown: true, release: true);
        yield return new WaitForSecondsRealtime(0.45f);
        bool separate = menu.CheatActivated;
        Debug.Log("[TCCheatPlaythrough] separate drags CheatActivated=" + separate);

        bool ok = continuous && separate;
        Debug.Log("[TCCheatPlaythrough] CheatActivated continuous=" + continuous + " separate=" + separate + " ok=" + ok);
        done?.Invoke(ok);
    }

    static GameObject FindNamed(CanvasGroup settings, string row, string child)
    {
        if (!settings) return null;
        var t = settings.transform.Find("Card/" + row + "/" + child);
        if (t) return t.gameObject;
        foreach (var x in settings.GetComponentsInChildren<Transform>(true))
            if (x.name == row)
            {
                var c = x.Find(child);
                if (c) return c.gameObject;
            }
        return null;
    }

    static IEnumerator Click(GameObject go)
    {
        var ev = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = ScreenPoint(go)
        };
        ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerDownHandler);
        yield return null;
        ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(go, ev, ExecuteEvents.pointerClickHandler);
        yield return null;
    }

    /// Finger stays down the whole time: pointer-down, sweep through every knot, pointer-up.
    static IEnumerator SweepContinuous(Slider slider, params float[] knots)
    {
        if (knots == null || knots.Length < 2) yield break;
        var ev = NewDrag(slider, knots[0]);
        var target = DragTarget(slider);
        ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.beginDragHandler);
        ev.pointerDrag = target;
        // Dwell a frame so an already-at-extreme start is seeded.
        yield return null;
        for (int k = 0; k < knots.Length - 1; k++)
        {
            yield return DragBetween(slider, ev, target, knots[k], knots[k + 1], release: false);
            // Sit on the end-stop a couple of frames like a real thumb.
            ev.position = SliderScreen(slider, knots[k + 1]);
            for (int d = 0; d < 2; d++)
            {
                ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.dragHandler);
                yield return null;
            }
        }
        ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.endDragHandler);
        ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.pointerUpHandler);
        yield return new WaitForSecondsRealtime(0.08f);
    }

    static IEnumerator DragSlider(Slider slider, float fromU, float toU, bool reuseDown = true, bool release = false)
    {
        var ev = NewDrag(slider, fromU);
        var target = DragTarget(slider);
        if (reuseDown)
        {
            ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.beginDragHandler);
            ev.pointerDrag = target;
            yield return null;
        }
        yield return DragBetween(slider, ev, target, fromU, toU, release);
    }

    static IEnumerator DragBetween(Slider slider, PointerEventData ev, GameObject target, float fromU, float toU, bool release)
    {
        // Overshoot slightly past 0/1 so the handle definitely hits the clamp.
        float a = fromU < 0.5f && fromU <= 0f ? -0.06f : fromU;
        float b = toU <= 0f ? -0.06f : (toU >= 1f ? 1.06f : toU);
        if (fromU >= 1f) a = 1.06f;
        const int steps = 16;
        for (int i = 1; i <= steps; i++)
        {
            float u = Mathf.Lerp(a, b, i / (float)steps);
            ev.position = SliderScreen(slider, u);
            ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.dragHandler);
            yield return null;
        }
        if (release)
        {
            ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.endDragHandler);
            ExecuteEvents.ExecuteHierarchy(target, ev, ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(0.08f);
        }
    }

    static PointerEventData NewDrag(Slider slider, float u)
    {
        var ev = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        };
        ev.position = SliderScreen(slider, u);
        ev.pressPosition = ev.position;
        ev.pointerDrag = DragTarget(slider);
        return ev;
    }

    static GameObject DragTarget(Slider slider)
    {
        if (slider && slider.handleRect) return slider.handleRect.gameObject;
        return slider ? slider.gameObject : null;
    }

    static Vector2 ScreenPoint(GameObject go)
    {
        var rt = go.transform as RectTransform;
        var canvas = go.GetComponentInParent<Canvas>();
        var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (rt) return RectTransformUtility.WorldToScreenPoint(cam, rt.position);
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    static Vector2 SliderScreen(Slider slider, float u)
    {
        var rt = slider.GetComponent<RectTransform>();
        var canvas = slider.GetComponentInParent<Canvas>();
        var cam = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // 0 = bottom-left, 3 = bottom-right, 1 = top-left, 2 = top-right
        Vector3 left = (corners[0] + corners[1]) * 0.5f;
        Vector3 right = (corners[3] + corners[2]) * 0.5f;
        return RectTransformUtility.WorldToScreenPoint(cam, Vector3.Lerp(left, right, Mathf.Clamp01(u)));
    }
}
