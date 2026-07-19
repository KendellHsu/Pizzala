using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem;
using TMPro;

// Drives the pre-roll Timeline's dialogue: a Signal Emitter calls PauseAndShowDialogue(line)
// to freeze the Timeline and show a line; the player presses the VR trigger to continue to the
// next Signal. Advancing is bound to the right-hand controller trigger, with a keyboard stand-in
// so the flow is still testable on desktop. Matches the input idiom in IntroSequenceController.
public class DialogueTimelineController : MonoBehaviour
{
    [Header("Timeline")]
    public PlayableDirector director;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TMP_Text dialogueText;

    [Header("Input - continue (trigger)")]
    [Tooltip("VR controller binding used to advance to the next dialogue line.")]
    public string triggerVrPath = "<XRController>{RightHand}/trigger";
    [Tooltip("Keyboard stand-in for the trigger (desktop testing).")]
    public string triggerKeyboardPath = "<Keyboard>/space";

    InputAction continueAction;
    bool waitingForInput;

    void Awake()
    {
        continueAction = new InputAction("DialogueContinue", InputActionType.Button, triggerVrPath);
        if (!string.IsNullOrEmpty(triggerKeyboardPath)) continueAction.AddBinding(triggerKeyboardPath);
    }

    void OnEnable() => continueAction?.Enable();
    void OnDisable() => continueAction?.Disable();
    void OnDestroy() => continueAction?.Dispose();

    void Start()
    {
        dialoguePanel.SetActive(false);
    }

    void Update()
    {
        if (waitingForInput && continueAction != null && continueAction.WasPressedThisFrame())
            OnContinue();
    }

    public void PauseAndShowDialogue(string line)
    {
        director.Pause();
        dialogueText.text = line;
        dialoguePanel.SetActive(true);
        waitingForInput = true;
    }

    void OnContinue()
    {
        waitingForInput = false;
        dialoguePanel.SetActive(false);
        director.Resume();
    }
}
