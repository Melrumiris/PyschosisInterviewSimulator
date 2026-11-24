using UnityEngine;

/// Puts the patient model into its talking animation while his voice AudioSource is playing,
/// and opens the jaw a little with the voice loudness. Put it next to PatientSessionController.
public class PatientTalkAnimator : MonoBehaviour
{
    public AudioSource voice;          // empty = the AudioSource on this GameObject
    public Animator animator;          // empty = the first Animator in the children
    public string talkingParameter = "Talking";
    [Tooltip("Extra jaw opening driven by voice loudness, on top of the canned jaw motion in Sit_Talk.")]
    public bool driveJawFromAudio = true;
    public float maxJawOpenDegrees = 6.5f, gain = 40f;

    Transform jaw; readonly float[] buf = new float[256]; float open;

    void Awake()
    {
        if (!voice) voice = GetComponent<AudioSource>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator && animator.isHuman) jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
    }

    void Update()
    {
        if (animator && voice) animator.SetBool(talkingParameter, voice.isPlaying);
    }

    void LateUpdate()
    {
        if (!driveJawFromAudio || !jaw || !voice) return;
        float target = 0f;
        if (voice.isPlaying)
        {
            voice.GetOutputData(buf, 0);
            float rms = 0f; foreach (var s in buf) rms += s * s;
            target = Mathf.Clamp01(Mathf.Sqrt(rms / buf.Length) * gain);
        }
        open = Mathf.Lerp(open, target, 0.4f);
        jaw.localRotation *= Quaternion.AngleAxis(open * maxJawOpenDegrees, Vector3.right);
    }
}
