using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// Chunky jelly press feedback for every hypercasual button (polish2): squash on press, springy
/// stretch + overshoot on release, small tilt wobble. Works on top of other scale animations
/// (menu breathe / pulse coroutines) by treating any scale written by someone else as the new base.
public class TCPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
{
    [Tooltip("Squash amount while held (x grows, y shrinks).")] public float squash = 0.1f;
    [Tooltip("Spring stiffness / damping for the release wobble.")] public float stiffness = 420f, damping = 15f;
    public float wobbleDegrees = 3f;

    Vector3 baseScale = Vector3.one, lastWritten; Quaternion baseRot = Quaternion.identity; bool hasBase, held, inside;
    Vector2 val, vel; float rot, rotVel; Selectable sel;

    void Awake() { sel = GetComponent<Selectable>(); Capture(); }
    void OnEnable() { Capture(); val = vel = Vector2.zero; rot = rotVel = 0; held = false; }
    void OnDisable() { if (hasBase) transform.localScale = baseScale; transform.localRotation = baseRot; held = false; }
    void Capture() { baseScale = transform.localScale; if (baseScale.sqrMagnitude < 1e-6f) baseScale = Vector3.one; lastWritten = baseScale; baseRot = transform.localRotation; hasBase = true; }

    bool Interactable => !sel || sel.IsInteractable();
    public void OnPointerDown(PointerEventData e) { if (!Interactable) return; held = inside = true; }
    public void OnPointerUp(PointerEventData e)
    {
        if (!held) return; held = false;
        // release kick: stretch tall then settle with overshoot
        vel += new Vector2(-squash * 26f, squash * 30f);
        rotVel += (Random.value < 0.5f ? -1f : 1f) * wobbleDegrees * 40f;
    }
    public void OnPointerExit(PointerEventData e) { inside = false; }
    public void OnPointerEnter(PointerEventData e) { inside = true; }

    void LateUpdate()
    {
        var cur = transform.localScale;
        if ((cur - lastWritten).sqrMagnitude > 1e-8f) baseScale = cur; // someone else animated the scale: follow it

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        Vector2 target = held && inside ? new Vector2(squash, -squash * 1.2f) : Vector2.zero;
        var acc = (target - val) * stiffness - vel * damping;
        vel += acc * dt; val += vel * dt;
        float racc = -rot * stiffness - rotVel * damping; rotVel += racc * dt; rot += rotVel * dt;

        if (val.sqrMagnitude < 1e-7f && vel.sqrMagnitude < 1e-6f && Mathf.Abs(rot) < 0.01f && Mathf.Abs(rotVel) < 0.1f && !held)
        {
            val = vel = Vector2.zero; rot = rotVel = 0;
            if (transform.localScale != baseScale) transform.localScale = baseScale;
            if (transform.localRotation != baseRot) transform.localRotation = baseRot;
            lastWritten = transform.localScale; return;
        }
        var s = new Vector3(baseScale.x * (1 + val.x), baseScale.y * (1 + val.y), baseScale.z);
        transform.localScale = s; lastWritten = s;
        transform.localRotation = baseRot * Quaternion.Euler(0, 0, rot);
    }
}
