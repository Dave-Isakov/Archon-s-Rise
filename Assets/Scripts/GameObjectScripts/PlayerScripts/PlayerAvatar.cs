using System.Collections;
using UnityEngine;

// The player's animated sprite (spec 2026-07-23, Part D). Lives on an `Avatar`
// CHILD of the PlayerPosition root — the root owns PlayerPosition.cs, the Main
// Camera, and the move arrows, so nothing here can drag the camera.
//
// Presentation only: this component never gates gameplay. Explore spend, fog
// reveal, and phase transitions have all already resolved by the time it runs.
public class PlayerAvatar : MonoBehaviour
{
    public static PlayerAvatar Instance { get; private set; }

    [SerializeField] Animator animator;
    [SerializeField] float moveDuration = 0.25f;
    // How long a Fight/Hurt clip owns the avatar before it resumes. Kept as a
    // scalar rather than read from clip length so an override controller with
    // differently-timed art cannot strand the state machine.
    [SerializeField] float oneShotDuration = 0.4f;

    static readonly int IsWalking    = Animator.StringToHash("isWalking");
    static readonly int FightTrigger = Animator.StringToHash("fight");
    static readonly int HurtTrigger  = Animator.StringToHash("hurt");

    AvatarState current = AvatarState.Idle;
    bool isMoving;
    Coroutine walkRoutine;
    Coroutine oneShotRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // Per-character clips arrive as an AnimatorOverrideController on the
        // CharacterSO. A null one leaves the base controller in place, so a
        // half-authored character renders instead of crashing.
        var character = DataManager.Instance != null ? DataManager.Instance.ActiveCharacter : null;
        if (character != null && character.AnimatorController != null)
            animator.runtimeAnimatorController = character.AnimatorController;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // A one-shot request (Fight/Hurt). Dropped rather than queued when it loses
    // the priority contest — a stale swing firing seconds late reads worse than
    // no swing at all.
    public void Play(AvatarState state)
    {
        if (!AvatarStateRules.ShouldPlay(current, state)) return;
        current = state;

        if (state == AvatarState.Fight)     animator.SetTrigger(FightTrigger);
        else if (state == AvatarState.Hurt) animator.SetTrigger(HurtTrigger);

        if (!AvatarStateRules.IsOneShot(state)) return;
        if (oneShotRoutine != null) StopCoroutine(oneShotRoutine);
        oneShotRoutine = StartCoroutine(ResumeAfterOneShot());
    }

    IEnumerator ResumeAfterOneShot()
    {
        yield return new WaitForSeconds(oneShotDuration);
        current = AvatarStateRules.ResumeAfter(isMoving);
        oneShotRoutine = null;
    }

    // Called after the root has ALREADY snapped to the destination. `offset` is
    // (from - to): the avatar starts displaced backward and eases home, so the
    // character appears to walk into the hex it is logically already on.
    public void PlayWalk(Vector3 offset)
    {
        if (walkRoutine != null) StopCoroutine(walkRoutine);
        walkRoutine = StartCoroutine(WalkRoutine(offset));
    }

    IEnumerator WalkRoutine(Vector3 offset)
    {
        isMoving = true;
        // The slide always runs (the avatar must end up at its parent's origin),
        // but the walk CLIP only takes over if it wins against what is playing.
        bool animate = AvatarStateRules.ShouldPlay(current, AvatarState.Walk);
        if (animate)
        {
            current = AvatarState.Walk;
            animator.SetBool(IsWalking, true);
        }

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(offset, Vector3.zero, t / moveDuration);
            yield return null;
        }
        transform.localPosition = Vector3.zero;

        isMoving = false;
        if (animate)
        {
            animator.SetBool(IsWalking, false);
            if (current == AvatarState.Walk) current = AvatarState.Idle;
        }
        walkRoutine = null;
    }
}
