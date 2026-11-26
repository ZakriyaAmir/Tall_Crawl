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
    public bool slowmoAllowed = true;
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
        currentTurnAngle = 0f;
        if (p1)
        {
            var e = p1.localEulerAngles;
            e.y = yaw;
            p1.localEulerAngles = e;
        }
        if (spider && hasSpiderBaseRot)
        {
            spider.transform.localRotation = spiderBaseRot;
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

    public void SetSlowmoAllowed(bool allowed)
    {
        slowmoAllowed = allowed;
    }

    public static System.Action OnJumped, OnTurned, OnRolled;
    public static float DebugSteer; // used by the automated screenshot tour

    TCConfig C;
    Animator camAnim;
    bool camBoost; [HideInInspector] public float camHoldUntil;
    float targetYaw, yaw, startY, jumpCooldown;
    bool canDoubleJump;
    Vector2 touchStart, lastPos, lastTapPos, rawTouchDownPos; float touchStartTime, rawTouchDownTime, lastTapTime = -1f; bool dragging, swipeUpConsumed, swipeDownConsumed;
    enum GestureAxis { None, Horizontal, Vertical }
    GestureAxis axisLock;
    float gestureStartYaw;
    playerInteract spider;
    Quaternion spiderBaseRot = Quaternion.identity;
    bool hasSpiderBaseRot;
    float currentTurnAngle;

    void Awake()
    {
        Instance = this;
        jumpAllowed = true;
        C = TCConfig.I;
        if (!p1) p1 = transform;
        if (!maincam) maincam = Camera.main;
        if (maincam) camAnim = maincam.GetComponent<Animator>();
        spider = GetComponentInChildren<playerInteract>();
        if (spider)
        {
            spiderBaseRot = spider.transform.localRotation;
            hasSpiderBaseRot = true;
        }
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
        float prevYaw = yaw;
        if (!(TCWebBonus.Instance && TCWebBonus.Instance.OwnsMotion))
        {
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-C.rotationSmoothing * effectiveDt));
            var e = p1.localEulerAngles; e.y = yaw; p1.localEulerAngles = e;
        }

        // Swiftly and smoothly rotate spider a little towards the direction the player moves
        if (!spider) spider = GetComponentInChildren<playerInteract>();
        if (spider && !hasSpiderBaseRot)
        {
            spiderBaseRot = spider.transform.localRotation;
            hasSpiderBaseRot = true;
        }

        float targetTurnAngle = 0f;
        if (!paused && !GameOver && !(TCWebBonus.Instance && TCWebBonus.Instance.IsSwinging))
        {
            float yawDelta = Mathf.DeltaAngle(prevYaw, yaw);
            float steerDelta = Mathf.DeltaAngle(yaw, targetYaw);
            float turnVelocity = yawDelta / Mathf.Max(1e-4f, effectiveDt);

            // Responsive angle lean: inverted to tilt directly towards movement direction
            float steerPull = Mathf.Clamp(steerDelta * 1.5f, -16f, 16f);
            float velPull = Mathf.Clamp(turnVelocity * 0.14f, -16f, 16f);
            targetTurnAngle = -Mathf.Clamp(steerPull * 0.45f + velPull * 0.55f, -14f, 14f);
        }
        currentTurnAngle = Mathf.Lerp(currentTurnAngle, targetTurnAngle, 1f - Mathf.Exp(-15f * effectiveDt));

        if (spider && hasSpiderBaseRot && !(TCWebBonus.Instance && TCWebBonus.Instance.IsSwinging))
        {
            spider.transform.localRotation = spiderBaseRot * Quaternion.Euler(0f, currentTurnAngle, 0f);
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
        if ((Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            && slowmoAllowed
            && playerInteract.CooldownInventory > 0
            && (TCTimeDilation.Instance == null || !TCTimeDilation.Instance.IsActive))
        {
            TCTimeDilation.TriggerCooldown();
        }

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
            swipeUpConsumed = false;
            swipeDownConsumed = false;
            axisLock = GestureAxis.None;
            gestureStartYaw = targetYaw;
            rawTouchDownPos = touchStart = lastPos = p;
            rawTouchDownTime = touchStartTime = Time.unscaledTime;
        }
        else if (dragging && (held || up))
        {
            bool isMouse = Input.touchCount == 0;
            float dx = p.x - lastPos.x;
            float upwardDist = p.y - rawTouchDownPos.y;
            float sideDist = Mathf.Abs(p.x - rawTouchDownPos.x);
            float lockDist = Mathf.Max(28f, Screen.height * 0.03f);
            float touchAge = Time.unscaledTime - rawTouchDownTime;

            // Once dragging duration passes 0.5s, permanently lock to horizontal steering and forbid jump for this gesture.
            if (touchAge >= 0.5f)
            {
                swipeUpConsumed = true;
                lastTapTime = -1f;
                if (axisLock != GestureAxis.Horizontal)
                    axisLock = GestureAxis.Horizontal;
            }

            // Dominant-axis lock: only lock to vertical if vertical movement STRONGLY dominates horizontal movement.
            // If the user drags left/right with even a slight upward slant, immediately favor horizontal steering
            // so accidental upward thumb/mouse curvature never halts player movement or triggers a jump.
            if (axisLock == GestureAxis.None)
            {
                float horizThreshold = isMouse ? 4f : lockDist * 0.6f;
                if (sideDist > horizThreshold && sideDist >= Mathf.Abs(upwardDist) * (isMouse ? 0.35f : 0.6f))
                {
                    axisLock = GestureAxis.Horizontal;
                    if (isMouse) swipeUpConsumed = true;
                }
                else if (touchAge < 0.5f && Mathf.Abs(upwardDist) > lockDist && Mathf.Abs(upwardDist) > sideDist * (isMouse ? 2.5f : 1.8f))
                {
                    axisLock = GestureAxis.Vertical;
                }
            }

            // Apply left/right steering on horizontal drag (or while undecided, so movement never feels laggy or halted)
            if (steerAllowed && axisLock != GestureAxis.Vertical && Mathf.Abs(dx) > 0.001f)
            {
                targetYaw -= dx * degPerPixel;
                OnTurned?.Invoke();
            }

            float minSwipeDist = Mathf.Max(75f, Screen.height * (isMouse ? 0.11f : 0.065f));
            bool isGenuineSwipeUp = !swipeUpConsumed
                && jumpAllowed
                && axisLock != GestureAxis.Horizontal
                && upwardDist > minSwipeDist
                && upwardDist > sideDist * (isMouse ? 2.5f : 1.5f)
                && touchAge < 0.5f;

            if (isGenuineSwipeUp)
            {
                // Discard any accidental yaw applied before the swipe was classified as vertical
                swipeUpConsumed = true;
                targetYaw = gestureStartYaw;
                axisLock = GestureAxis.Vertical;
                touchStart = p;
                touchStartTime = Time.unscaledTime;
                lastTapTime = -1f;
                TriggerJump(true);
            }

            // Swipe down to trigger slow motion if available (one use per swipe gesture, cannot trigger if already slow)
            bool isGenuineSwipeDown = !swipeDownConsumed
                && slowmoAllowed
                && axisLock != GestureAxis.Horizontal
                && (-upwardDist) > minSwipeDist
                && (-upwardDist) > sideDist * (isMouse ? 2.2f : 1.5f)
                && touchAge < 0.45f
                && playerInteract.CooldownInventory > 0
                && (TCTimeDilation.Instance == null || !TCTimeDilation.Instance.IsActive);

            if (isGenuineSwipeDown)
            {
                swipeDownConsumed = true;
                targetYaw = gestureStartYaw;
                axisLock = GestureAxis.Vertical;
                touchStart = p;
                touchStartTime = Time.unscaledTime;
                lastTapTime = -1f;
                TCTimeDilation.TriggerCooldown();
            }

            lastPos = p;

            if (up)
            {
                // Double tap = jump (two quick taps close together; single tap or short drag does NOT jump)
                float totalDist = Vector2.Distance(p, rawTouchDownPos);
                float touchDur = Time.unscaledTime - rawTouchDownTime;

                float maxTapHold = C != null ? C.doubleTapMaxHold : 0.25f;
                float doubleTapWindow = C != null ? Mathf.Max(0.35f, C.doubleTapWindow) : 0.35f;
                float tapMoveLimit = isMouse ? 6f : Screen.height * 0.03f;
                float maxTapDist = isMouse ? 16f : (C != null ? C.doubleTapMaxDistance : 0.12f) * Screen.height;

                bool isTap = !swipeUpConsumed && axisLock == GestureAxis.None && totalDist < tapMoveLimit && touchDur < Mathf.Min(maxTapHold, 0.5f);
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

    public void jump() => TriggerJump(false);

    void TriggerJump(bool somersault)
    {
        if (IsInputBlocked) return;

        // Calculate jump durations: nice, sustained hangtime for visibility, with gentle speed scaling at high climb speeds
        float speedRatio = Mathf.Clamp(calcSpeed / Mathf.Max(0.1f, C.startSpeed), 1f, 1.4f);
        float jumpDur = 0.95f / speedRatio;
        float dblJumpDur = 1.35f / speedRatio;

        // Execute somersault (backflip + higher arc) on swipe-up or when double-jumping in air
        if (somersault || (spider && spider.IsJumping && canDoubleJump))
        {
            if (somersault && jumpCooldown > 0) return;
            canDoubleJump = false;
            jumpCooldown = 0.5f / speedRatio;
            if (anim) anim.SetTrigger("jump");
            if (spider) spider.doubleJump(dblJumpDur);
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
