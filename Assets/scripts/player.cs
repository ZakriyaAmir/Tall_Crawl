using UnityEngine;

/// Climbing + input. Drag horizontally to circle the trunk (smoothed, dead-zone, sensitivity from
/// settings), swipe up / tap the jump area to hop bee swarms. Editor: A/D or arrows, Space to jump,
/// mouse drag works too.
public class player : MonoBehaviour
{
    public static player Instance { get; private set; }

    public Transform p1;
    public Animator anim;
    public Camera maincam;
    public bool paused = true, GameOver, boost;
    public float currentScore, calcSpeed;
    public bool jumpAllowed = true;
    public bool steerAllowed = true;
    public bool IsRolling => boost; // Roll animation happens during chilli mode
    public float CurrentYaw => yaw;

    /// Used by the WEB swing so pendulum motion can set both height and yaw.
    public void ApplySwingTransform(Vector3 pos, float yawDeg)
    {
        if (!p1) return;
        var p = p1.position;
        p.y = pos.y;
        p1.position = p;
        yaw = targetYaw = yawDeg;
        var e = p1.localEulerAngles; e.y = yaw; p1.localEulerAngles = e;
        if (C != null) currentScore = (pos.y - startY) * C.metresPerUnit;
    }

    bool IsDeadState => GameOver || (spider != null && spider.IsDead);
    bool IsGamePaused =>
        (gameManager.Instance != null && (gameManager.Instance.IsPaused || gameManager.Instance.runEnded))
        || (TCGameUI.Instance != null && TCGameUI.Instance.IsPauseOpen)
        || (Time.timeScale <= 0f && (TCGameUI.Instance == null || !TCGameUI.Instance.IsTutorialFrozen));

    /// True whenever gameplay input (steer, drag, swipe, tap, keyboard) must be ignored.
    public bool IsInputBlocked => IsDeadState || paused || IsGamePaused;

    public void ResetInputState()
    {
        dragging = false;
        axisLock = GestureAxis.None;
        lastTapTime = -1f;
        targetYaw = yaw;
        if (p1)
        {
            var e = p1.localEulerAngles;
            e.y = yaw;
            p1.localEulerAngles = e;
        }
    }

    public void SetSteerAllowed(bool allowed)
    {
        steerAllowed = allowed;
        if (!allowed)
        {
            targetYaw = yaw;
            dragging = false;
        }
    }

    public void SetJumpAllowed(bool allowed)
    {
        jumpAllowed = allowed;
    }

    public static System.Action OnJumped, OnTurned, OnRolled;
    public static float DebugSteer; // used by the automated screenshot tour

    TCConfig C;
    Animator camAnim;
    bool camBoost; [HideInInspector] public float camHoldUntil;
    float targetYaw, yaw, startY, jumpCooldown;
    bool canDoubleJump;
    Vector2 touchStart, lastPos, lastTapPos, rawTouchDownPos; float touchStartTime, rawTouchDownTime, lastTapTime = -1f; bool dragging;
    enum GestureAxis { None, Horizontal, Vertical }
    GestureAxis axisLock;
    float gestureStartYaw;
    playerInteract spider;

    void Awake()
    {
        Instance = this;
        jumpAllowed = true;
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
        if (IsDeadState || IsGamePaused)
        {
            ResetInputState();
            return;
        }
        if (paused)
        {
            if (TCGameUI.Instance && !TCGameUI.Instance.Started)
            {
                bool down = (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) || Input.GetMouseButtonDown(0);
                if (down)
                {
                    TCGameUI.Instance.StartRun();
                }
            }
            if (paused)
            {
                ResetInputState();
                return;
            }
        }
        float dt = Time.deltaTime;
        float baseSpeed = C.ClimbSpeedAtDistance(currentScore);
        float speedMult = boost ? C.boostMultiplier : 1f;
        calcSpeed = baseSpeed * speedMult;
        if (TCWebBonus.Instance && TCWebBonus.Instance.IsSwinging)
        {
            calcSpeed = TCWebBonus.Instance.ClimbSpeed;
            ResetInputState();
            if (TCWebBonus.Instance.OwnsMotion)
            {
                // Vertical motion is applied by the swing (pendulum + climbs). Don't double-add.
                currentScore = (p1.position.y - startY) * C.metresPerUnit;
                goto AfterClimb;
            }
        }
        var pos = p1.position; pos.y += calcSpeed * dt; p1.position = pos;
        currentScore = (pos.y - startY) * C.metresPerUnit;
        AfterClimb: ;

        bool camB = boost;
        if (camAnim && camBoost != camB) { camBoost = camB; camAnim.SetBool("boost", camB); }

        float effectiveDt = (Time.timeScale < 0.2f) ? Time.unscaledDeltaTime : dt;
        if (TCWebBonus.Instance && TCWebBonus.Instance.IsSwinging)
        {
            // no input mid-swing
        }
        else HandleInput(effectiveDt);
        if (!(TCWebBonus.Instance && TCWebBonus.Instance.OwnsMotion))
        {
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-C.rotationSmoothing * effectiveDt));
            var e = p1.localEulerAngles; e.y = yaw; p1.localEulerAngles = e;
        }
        if (jumpCooldown > 0) jumpCooldown -= effectiveDt;
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
        if (IsInputBlocked)
        {
            ResetInputState();
            return;
        }
        float degPerPixel = C.dragDegreesPerScreenWidth / Mathf.Max(1, Screen.width) * TCSave.Sensitivity;

        // Keyboard (editor / desktop)
        float k = Input.GetAxisRaw("Horizontal") + DebugSteer;
        if (steerAllowed && Mathf.Abs(k) > 0.05f) { targetYaw -= k * C.keyboardTurnSpeed * TCSave.Sensitivity * dt; OnTurned?.Invoke(); }
        if (jumpAllowed && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))) jump();

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
            // Only ignore touch if directly tapping an interactive UI button (e.g. Pause)
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                int pointerId = Input.touchCount > 0 ? Input.GetTouch(0).fingerId : -1;
                if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(pointerId))
                {
                    var ped = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = p };
                    var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(ped, results);
                    bool onBtn = false;
                    foreach (var r in results)
                    {
                        var btn = r.gameObject.GetComponentInParent<UnityEngine.UI.Button>();
                        if (btn != null && btn.enabled && btn.gameObject.name != "Start") { onBtn = true; break; }
                    }
                    if (onBtn) return;
                }
            }
            dragging = true;
            axisLock = GestureAxis.None;
            gestureStartYaw = targetYaw;
            rawTouchDownPos = touchStart = lastPos = p;
            rawTouchDownTime = touchStartTime = Time.unscaledTime;
        }
        else if (dragging && (held || up))
        {
            float dx = p.x - lastPos.x;
            float upwardDist = p.y - rawTouchDownPos.y;
            float sideDist = Mathf.Abs(p.x - rawTouchDownPos.x);
            float lockDist = Screen.height * 0.025f;

            // Dominant-axis lock: once the gesture is clearly a jump swipe, never steer,
            // and once it's clearly a turn, never jump.
            if (axisLock == GestureAxis.None)
            {
                if (upwardDist > lockDist && upwardDist > sideDist * 1.15f)
                    axisLock = GestureAxis.Vertical;
                else if (sideDist > lockDist && sideDist > Mathf.Abs(upwardDist) * 0.9f)
                    axisLock = GestureAxis.Horizontal;
            }

            // Only apply left/right steering on a horizontal (or still-undecided and not vertical) drag
            if (steerAllowed && axisLock != GestureAxis.Vertical && Mathf.Abs(dx) > 0.001f)
            {
                targetYaw -= dx * degPerPixel;
                OnTurned?.Invoke();
            }

            float minSwipeDist = Screen.height * 0.055f;
            bool isGenuineSwipeUp = jumpAllowed
                && axisLock != GestureAxis.Horizontal
                && upwardDist > minSwipeDist
                && upwardDist > sideDist * 1.15f
                && (Time.unscaledTime - rawTouchDownTime) < 0.5f;

            if (isGenuineSwipeUp)
            {
                // Discard any accidental yaw applied before the swipe was classified as vertical
                targetYaw = gestureStartYaw;
                axisLock = GestureAxis.Vertical;
                touchStart = p;
                touchStartTime = Time.unscaledTime;
                lastTapTime = -1f;
                jump();
            }

            lastPos = p;

            if (up)
            {
                // Double tap = jump (two quick taps close together; single tap does NOT jump)
                float totalDist = Vector2.Distance(p, rawTouchDownPos);
                float touchDur = Time.unscaledTime - rawTouchDownTime;

                float maxTapHold = C != null ? C.doubleTapMaxHold : 0.25f;
                float doubleTapWindow = C != null ? Mathf.Max(0.35f, C.doubleTapWindow) : 0.35f;
                float maxTapDist = (C != null ? C.doubleTapMaxDistance : 0.12f) * Screen.height;

                bool isTap = totalDist < Screen.height * 0.04f && touchDur < maxTapHold;
                if (isTap)
                {
                    bool isDoubleTap = lastTapTime > 0 && (Time.unscaledTime - lastTapTime) < doubleTapWindow
                        && Vector2.Distance(p, lastTapPos) < maxTapDist;

                    if (isDoubleTap)
                    {
                        lastTapTime = -1f;
                        if (jumpAllowed) jump();
                    }
                    else
                    {
                        lastTapTime = Time.unscaledTime;
                        lastTapPos = p;
                    }
                }
                else
                {
                    lastTapTime = -1f;
                }

                dragging = false;
            }
        }
    }

    public void jump()
    {
        if (IsInputBlocked) return;

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

    public void roll(float duration = 1.2f)
    {
        // Rolling is now automatic during chilli pepper boost mode.
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }
}
