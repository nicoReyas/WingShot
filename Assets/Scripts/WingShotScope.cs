using UnityEngine;

// Focus and optical zoom leave the round clock independent of aiming and weapon bonuses.
[DefaultExecutionOrder(25)]
public class WingShotScope : MonoBehaviour
{
    [Range(.3f, .8f)] public float zoomScale = .5f;
    public const float FocusZoomScale = .9f, FocusBirdSpeed = .97f, QuickscopeWindow = .5f;
    [SerializeField] float aimAge, nextQuickscopeAt;
    [SerializeField] bool quickscopeShotUsed, sniperAtAimStart;
    [SerializeField] GameManager game;
    [SerializeField] Camera view;
    [SerializeField] Vector3 homePosition, aimPosition;
    [SerializeField] float homeSize;
    [field: SerializeField] public bool IsAiming { get; private set; }
    public bool IsSniperAiming => IsAiming && game != null && game.Powerups.SniperRemaining > 0f;
    public float AimBirdSpeedMultiplier => IsAiming && !IsSniperAiming ? FocusBirdSpeed : 1f;
    float CurrentZoomScale => game != null && game.Powerups.SniperRemaining > 0f ? zoomScale : FocusZoomScale;
    public float LensAmount => IsSniperAiming ? ZoomAmount : 0f;
    public float ZoomAmount => view == null || homeSize <= 0f ? 0f :
        Mathf.Clamp01((homeSize - view.orthographicSize) / (homeSize * (1f - zoomScale)));
    public Vector2 AimScreenPosition => view == null ? new Vector2(Screen.width, Screen.height) * .5f : view.pixelRect.center;
    public float NormalSize => homeSize;
    public Vector3 HomePosition => homePosition;
    public Vector3 WorldFromViewport(Vector2 point)
    {
        return new Vector3(homePosition.x + (point.x - .5f) * homeSize * 2f * view.aspect,
            homePosition.y + (point.y - .5f) * homeSize * 2f, 0f);
    }

    void Awake()
    {
        game = GetComponent<GameManager>();
        view = Camera.main;
        if (view == null) return;
        homeSize = view.orthographicSize;
        homePosition = aimPosition = view.transform.position;
    }

    void Update()
    {
        if (game == null || view == null) return;
        if (!game.IsPlaying || !Application.isFocused)
        {
            ResetView();
            return;
        }
        bool entering = WingShotInput.ScopeHeld && !IsAiming;
        SetAiming(WingShotInput.ScopeHeld);
        if (IsAiming && !IsSniperAiming)
        {
            Vector2 pointer = WingShotInput.PointerPosition;
            aimPosition = ClampToMeadow(WorldFromViewport(new Vector2(pointer.x / Mathf.Max(1f, view.pixelWidth),
                pointer.y / Mathf.Max(1f, view.pixelHeight))), homeSize * FocusZoomScale);
        }
        AdvanceView(Time.unscaledDeltaTime, entering ? Vector2.zero : WingShotInput.PointerDelta);
    }

    // Also usable by other input adapters and the editor gameplay checks.
    public bool SetAiming(bool aiming, Vector2? screenPoint = null)
    {
        if (view == null || game == null) return false;
        if (aiming && !game.IsPlaying) return false;
        if (aiming && !IsAiming)
        {
            Vector2 pointer = screenPoint ?? WingShotInput.PointerPosition;
            var point = view.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, Mathf.Abs(view.transform.position.z)));
            aimPosition = ClampToMeadow(new Vector3(point.x, point.y, homePosition.z), homeSize * CurrentZoomScale);
            aimAge = 0f;
            quickscopeShotUsed = false;
            sniperAtAimStart = game.Powerups.SniperRemaining > 0f && Time.time >= nextQuickscopeAt;
        }
        IsAiming = aiming;
        Cursor.lockState = IsSniperAiming ? CursorLockMode.Locked : CursorLockMode.None;
        if (IsSniperAiming) Cursor.visible = false;
        return true;
    }

    public void AdvanceView(float deltaTime, Vector2 pointerDelta)
    {
        if (view == null || deltaTime <= 0f) return;
        if (IsAiming) aimAge += deltaTime;
        if (IsSniperAiming)
        {
            float unitsPerPixel = view.orthographicSize * 2f / Mathf.Max(1f, view.pixelHeight);
            aimPosition += new Vector3(pointerDelta.x, pointerDelta.y, 0f) * unitsPerPixel * .75f;
            aimPosition = ClampToMeadow(aimPosition, homeSize * CurrentZoomScale);
        }
        float blend = 1f - Mathf.Exp(-deltaTime * 16f);
        float desiredSize = IsAiming ? homeSize * CurrentZoomScale : homeSize;
        view.orthographicSize = Mathf.Lerp(view.orthographicSize, desiredSize, blend);
        if (Mathf.Abs(view.orthographicSize - desiredSize) < .001f) view.orthographicSize = desiredSize;
        Vector3 desiredPosition = IsAiming ? ClampToMeadow(aimPosition, view.orthographicSize) : homePosition;
        view.transform.position = Vector3.Lerp(view.transform.position, desiredPosition, blend);
        if ((view.transform.position - desiredPosition).sqrMagnitude < .000001f) view.transform.position = desiredPosition;
    }

    Vector3 ClampToMeadow(Vector3 position, float size)
    {
        float vertical = Mathf.Max(0f, homeSize - size);
        float horizontal = vertical * view.aspect;
        position.x = Mathf.Clamp(position.x, homePosition.x - horizontal, homePosition.x + horizontal);
        position.y = Mathf.Clamp(position.y, homePosition.y - vertical, homePosition.y + vertical);
        position.z = homePosition.z;
        return position;
    }

    public void ResetView()
    {
        IsAiming = false;
        if (view != null && homeSize > 0f)
        {
            view.orthographicSize = homeSize;
            view.transform.position = homePosition;
        }
        Cursor.lockState = CursorLockMode.None;
    }

    // Exactly one accepted shot can claim the short window; misses and crate hits use it too.
    public bool ConsumeQuickscopeShot()
    {
        bool eligible = IsSniperAiming && sniperAtAimStart && !quickscopeShotUsed && aimAge <= QuickscopeWindow;
        if (IsAiming && !quickscopeShotUsed)
        {
            quickscopeShotUsed = true;
            if (eligible) nextQuickscopeAt = Time.time + .75f;
        }
        return eligible;
    }

    public void ResetRound() { ResetView(); nextQuickscopeAt = 0f; }

    void OnDisable() { ResetView(); }
    void OnDestroy() { ResetView(); }
}
