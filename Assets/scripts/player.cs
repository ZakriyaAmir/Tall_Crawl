using UnityEngine;

/// Climbing + input. Drag horizontally to circle the trunk (smoothed, dead-zone, sensitivity from
/// settings), swipe up / tap the jump area to hop bee swarms. Editor: A/D or arrows, Space to jump,
/// mouse drag works too.
public class player : MonoBehaviour
{
    public Transform p1;
    public Animator anim;
    public Camera maincam;
    public bool paused = true, GameOver, boost;
    public float currentScore, calcSpeed;
    public bool jumpAllowed = true;

    public static System.Action OnJumped, OnTurned;
    public static float DebugSteer; // used by the automated screenshot tour

    TCConfig C;
    Animator camAnim;
    bool camBoost; [HideInInspector] public float camHoldUntil;
    float targetYaw, yaw, startY, jumpCooldown, yawAtTouch;
    bool canDoubleJump;
    Vector2 touchStart, lastPos, lastTapPos; float touchStartTime, lastTapTime = -1f; bool dragging, swipeUsed, passedDeadZone;
    playerInteract spider;

    void Awake()
    {
        C = TCConfig.I;
        if (!p1) p1 = transform;
        if (!maincam) maincam = Camera.main;
        if (maincam) camAnim = maincam.GetComponent<Animator>();
        spider = GetComponentInChildren<playerInteract>();
        yaw = targetYaw = p1.localEulerAngles.y;
        startY = p1.position.y;
    }

    public float Metres => currentScore;

    void Update()
    {
        if (paused || GameOver) return;
        float dt = Time.deltaTime;
        float d = C.Difficulty(currentScore);
        float baseSpeed = Mathf.Lerp(C.startSpeed, C.maxSpeed, d);
        calcSpeed = boost ? baseSpeed * C.boostMultiplier : baseSpeed;
        var pos = p1.position; pos.y += calcSpeed * dt; p1.position = pos;
        currentScore = (pos.y - startY) * C.metresPerUnit;

        bool camB = boost;
        if (camAnim && camBoost != camB) { camBoost = camB; camAnim.SetBool("boost", camB); }

        HandleInput(dt);
        yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-C.rotationSmoothing * dt));
        var e = p1.localEulerAngles; e.y = yaw; p1.localEulerAngles = e;
        if (jumpCooldown > 0) jumpCooldown -= dt;
        if (spider && !spider.IsJumping) canDoubleJump = false;

        // Speed up spider animation speed with climbing speed (keeps run and jump animations snappy as difficulty rises)
        if (anim && !GameOver && !paused)
        {
            float speedScale = Mathf.Clamp(calcSpeed / Mathf.Max(0.1f, C.startSpeed), 1f, 2.2f);
            anim.speed = speedScale;
        }
    }

    void HandleInput(float dt)
    {
        float degPerPixel = C.dragDegreesPerScreenWidth / Mathf.Max(1, Screen.width) * TCSave.Sensitivity;

        // Keyboard (editor / desktop)
        float k = Input.GetAxisRaw("Horizontal") + DebugSteer;
        if (Mathf.Abs(k) > 0.1f) { targetYaw -= k * C.keyboardTurnSpeed * TCSave.Sensitivity * dt; OnTurned?.Invoke(); }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) jump();

        // Touch, falling back to mouse in the editor
        bool down, held, up; Vector2 p;
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0); p = t.position;
            down = t.phase == TouchPhase.Began; up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled; held = !down && !up;
        }
        else
        {
            p = Input.mousePosition; down = Input.GetMouseButtonDown(0); up = Input.GetMouseButtonUp(0); held = Input.GetMouseButton(0) && !down;
        }

        if (down)
        {
            if (UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1)) return;
            dragging = true; swipeUsed = false; passedDeadZone = false; touchStart = lastPos = p; touchStartTime = Time.unscaledTime; yawAtTouch = targetYaw;
        }
        else if (dragging && (held || up))
        {
            Vector2 total = p - touchStart;
            if (!passedDeadZone && total.magnitude > C.deadZonePixels) passedDeadZone = true;
            // Swipe up = jump (fast, mostly vertical)
            if (!swipeUsed && total.y > C.swipeUpThreshold * Screen.height && total.y > Mathf.Abs(total.x) * 1.3f && Time.unscaledTime - touchStartTime < 0.35f)
            { swipeUsed = true; targetYaw = yawAtTouch; jump(); } // undo the sideways drift the swipe caused
            if (passedDeadZone && !swipeUsed)
            {
                float dx = p.x - lastPos.x;
                if (Mathf.Abs(dx) > 0.01f) { targetYaw -= dx * degPerPixel; OnTurned?.Invoke(); }
            }
            lastPos = p;
            if (up)
            {
                // Double tap = jump (two quick taps close together, neither one a drag or swipe)
                bool isTap = !swipeUsed && !passedDeadZone && (Time.unscaledTime - touchStartTime) < C.doubleTapMaxHold;
                if (isTap)
                {
                    bool second = lastTapTime > 0 && (Time.unscaledTime - lastTapTime) < C.doubleTapWindow
                        && Vector2.Distance(p, lastTapPos) < C.doubleTapMaxDistance * Screen.height;
                    if (second) { lastTapTime = -1f; jump(); }
                    else { lastTapTime = Time.unscaledTime; lastTapPos = p; }
                }
                else lastTapTime = -1f;
                dragging = false;
            }
        }
    }

    public void jump()
    {
        if (paused || GameOver) return;

        // Calculate jump durations: nice, sustained hangtime for visibility, with gentle speed scaling at high climb speeds
        float speedRatio = Mathf.Clamp(calcSpeed / Mathf.Max(0.1f, C.startSpeed), 1f, 1.4f);
        float jumpDur = 0.95f / speedRatio;
        float dblJumpDur = 1.35f / speedRatio;

        // In jump mode: a second jump trigger executes a backflip double jump
        if (spider && spider.IsJumping && canDoubleJump)
        {
            canDoubleJump = false;
            jumpCooldown = 0.5f / speedRatio;
            // Trigger double jump without resetting the underlying base jump state
            spider.doubleJump(dblJumpDur);
            OnJumped?.Invoke();
            return;
        }

        if (jumpCooldown > 0) return;
        jumpCooldown = 0.35f / speedRatio; // cooldown window allowing double-jump trigger while in air
        canDoubleJump = true;
        if (anim) anim.SetTrigger("jump");
        if (spider) spider.jump(jumpDur);
        OnJumped?.Invoke();
    }
}
