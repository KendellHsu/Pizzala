using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.InputSystem;   // 換成這個
using TMPro;

public class DialogueTimelineController : MonoBehaviour
{
    [Header("Timeline")]
    public PlayableDirector director;

    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TMP_Text dialogueText;

    bool waitingForInput;

    void Start()
    {
        dialoguePanel.SetActive(false);
    }

    void Update()
    {
        if (waitingForInput 
            && Keyboard.current != null 
            && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            OnContinue();
        }
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