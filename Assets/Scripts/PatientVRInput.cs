using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// Push-to-talk din controller + panou de stare in lume. Pune-l pe acelasi GameObject cu PatientSessionController.
/// Foloseste UnityEngine.XR.InputDevices, deci merge indiferent de setarea Input System.
public class PatientVRInput : MonoBehaviour
{
    public enum Btn { PrimaryAorX, SecondaryBorY, Trigger, Grip, ThumbstickClick }

    [Header("Vorbit (tine apasat)")]
    public XRNode talkHand = XRNode.RightHand;
    public Btn talkButton = Btn.PrimaryAorX;      // A pe mana dreapta

    [Header("Salveaza sesiunea (tine apasat)")]
    public XRNode saveHand = XRNode.LeftHand;
    public Btn saveButton = Btn.PrimaryAorX;      // X pe mana stanga
    public float saveHoldSeconds = 2f;

    [Header("End of game + inchide aplicatia (tine apasat)")]
    public XRNode endHand = XRNode.RightHand;
    public Btn endButton = Btn.SecondaryBorY;     // B pe mana dreapta
    public float endHoldSeconds = 3f;

    [Header("Conflicte cu template-ul")]
    [Tooltip("Actiunile Input System al caror nume contine unul din aceste cuvinte sunt dezactivate la pornire (ex: Jump).")]
    public string[] disableActionsNamed = { "Jump" };
    [Tooltip("Logheaza actiunile legate de butoanele A/B/X/Y, ca sa afli cum se numeste cea care face sarituri.")]
    public bool logActionsOnFaceButtons = true;

    [Header("Panou stare (optional)")]
    [Tooltip("Un TextMeshPro 3D (nu UI) pus in scena, la nivelul ochilor.")]
    public TMP_Text statusText;
    public int logLines = 6;

    PatientSessionController session;
    bool talkPrev;
    float saveHeld;
    bool saveFired;
    float endHeld;
    bool endFired;

    void Awake() => session = GetComponent<PatientSessionController>();

    IEnumerator Start()
    {
        // asteptam ca template-ul sa-si activeze actiunile
        yield return new WaitForSeconds(1.5f);
#if ENABLE_INPUT_SYSTEM
        foreach (var a in InputSystem.ListEnabledActions())
        {
            bool onFaceButton = false;
            foreach (var b in a.bindings)
            {
                string path = b.effectivePath ?? "";
                if (path.Contains("primaryButton") || path.Contains("secondaryButton")) { onFaceButton = true; break; }
            }
            if (logActionsOnFaceButtons && onFaceButton)
                Debug.Log($"[VRInput] actiune pe buton A/B/X/Y: {a.actionMap?.name}/{a.name}");

            foreach (var name in disableActionsNamed)
            {
                if (!string.IsNullOrEmpty(name) && a.name.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    a.Disable();
                    Debug.Log($"[VRInput] dezactivat: {a.actionMap?.name}/{a.name}");
                }
            }
        }
#endif
    }

    static bool Pressed(XRNode node, Btn b)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        if (!dev.isValid) return false;
        bool v = false;
        switch (b)
        {
            case Btn.PrimaryAorX: dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out v); break;
            case Btn.SecondaryBorY: dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out v); break;
            case Btn.Trigger: dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out v); break;
            case Btn.Grip: dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out v); break;
            case Btn.ThumbstickClick: dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out v); break;
        }
        return v;
    }

    void Update()
    {
        // push-to-talk
        bool talk = Pressed(talkHand, talkButton);
        if (talk && !talkPrev) session.BeginTalk();
        if (!talk && talkPrev) session.EndTalk();
        talkPrev = talk;

        // salvare sesiune: tinut apasat saveHoldSeconds
        if (Pressed(saveHand, saveButton))
        {
            saveHeld += Time.unscaledDeltaTime;
            if (saveHeld >= saveHoldSeconds && !saveFired && session.IsIdle)
            {
                saveFired = true;
                session.SaveSessionToMemory();
            }
        }
        else { saveHeld = 0; saveFired = false; }

        // end of game: tinut apasat endHoldSeconds
        if (Pressed(endHand, endButton))
        {
            endHeld += Time.unscaledDeltaTime;
            if (endHeld >= endHoldSeconds && !endFired && session.IsIdle)
            {
                endFired = true;
                session.EndGameAndQuit();
            }
        }
        else { endHeld = 0; endFired = false; }

        if (statusText != null)
        {
            string hint = session.StateName == "Idle" ? "Tine apasat butonul de vorbit" : session.StateName;
            if (endHeld > 0.2f && !endFired)
                hint = $"END OF GAME in {Mathf.Max(0f, endHoldSeconds - endHeld):F1}s (elibereaza ca sa anulezi)";
            statusText.text = $"[{hint}]\n{session.RecentLog(logLines)}";
        }
    }
}