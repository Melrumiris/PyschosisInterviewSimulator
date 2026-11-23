using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// Orchestreaza: microfon -> n8n -> audio. Are UI de debug prin OnGUI (nu necesita Canvas).
/// Pune-l pe un GameObject "Patient" impreuna cu N8nClient, MicRecorder si un AudioSource.
[RequireComponent(typeof(N8nClient), typeof(MicRecorder), typeof(AudioSource))]
public class PatientSessionController : MonoBehaviour
{
    [Range(1, 5)] public int disorganizationLevel = 1;
    [Tooltip("Aplica latenta simulata (response_latency_ms) minus timpul real deja scurs. Debifat = raspunde cat de repede poate.")]
    public bool applySimulatedLatency = true;
    [Tooltip("Multiplicator pentru latenta simulata (1 = valoarea din n8n).")]
    public float simulatedLatencyScale = 1f;
    public bool logNonverbalForLater = true;   // doar loghează, nu animă (etapa următoare)

    N8nClient api; MicRecorder mic; AudioSource speaker;
    enum State { Idle, Recording, Waiting, Speaking, Ending }
    State state = State.Idle;
    readonly StringBuilder log = new StringBuilder();
    Vector2 scroll;
    PatientReply last;
    double tRelease, tReply;   // pentru latenta perceputa

    void Awake()
    {
        api = GetComponent<N8nClient>();
        mic = GetComponent<MicRecorder>();
        speaker = GetComponent<AudioSource>();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#endif
        Log($"patient_id={api.patientId}  player_id={api.PlayerId}");
    }

    void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space)) BeginTalk();
        if (Input.GetKeyUp(KeyCode.Space))   EndTalk();
#endif
    }

    // ---- API public (le poti lega mai tarziu la butoane VR / OVRInput) ----
    public void BeginTalk()
    {
        if (state != State.Idle) return;
        mic.StartRecording();
        state = State.Recording;
    }

    public void EndTalk()
    {
        if (state != State.Recording) return;
        byte[] wav = mic.StopRecording();
        if (wav == null) { state = State.Idle; return; }
        tRelease = Time.realtimeSinceStartupAsDouble;
        state = State.Waiting;
        StartCoroutine(Turn(wav));
    }

    public void SaveSessionToMemory() { if (state == State.Idle) StartCoroutine(EndFlow(false)); }
    public void ResetEverything() { if (state == State.Idle) StartCoroutine(EndFlow(true)); }

    // ---- fluxuri ----
    IEnumerator Turn(byte[] wav)
    {
        PatientReply reply = null; double rtt = 0; string err = null;
        yield return api.SendTurn(wav, disorganizationLevel, (r, t) => { reply = r; rtt = t; }, e => err = e);

        if (err != null) { Log("EROARE: " + err); state = State.Idle; yield break; }

        last = reply;
        tReply = Time.realtimeSinceStartupAsDouble;
        Log($"Pacient: {reply.spoken_response}");
        Log($"  RTT {rtt:F1}s | trust={reply.clinical_state?.current_trust_level} " +
            $"| disorg={reply.clinical_state?.applied_disorganization_level} " +
            $"| topics=[{string.Join(",", reply.clinical_state?.disclosed_topics ?? new string[0])}]");
        if (logNonverbalForLater && reply.nonverbal_behavior != null)
            Log($"  (nonverbal, neaplicat inca) {JsonUtility.ToJson(reply.nonverbal_behavior)}");

        state = State.Speaking;
        yield return PlayAudio(reply.audio_base64, reply.audio_mime_type);
        state = State.Idle;
    }

    IEnumerator PlayAudio(string b64, string mime)
    {
        if (string.IsNullOrEmpty(b64)) { Log("(fara audio in raspuns)"); yield break; }

        string m = (mime ?? "").ToLowerInvariant();
        AudioType type = m.Contains("wav") ? AudioType.WAV : m.Contains("ogg") ? AudioType.OGGVORBIS : AudioType.MPEG;
        string ext = type == AudioType.WAV ? "wav" : type == AudioType.OGGVORBIS ? "ogg" : "mp3";

        string head = b64.Substring(0, Math.Min(30, b64.Length));
        Log($"[audio] base64 length={b64.Length} start=\"{head}\" mime={mime}");
        if (b64.StartsWith("filesystem"))
        {
            Log("[audio] EROARE: n8n a trimis o referinta 'filesystem', nu audio. Foloseste getBinaryDataBuffer in nodul Code.");
            yield break;
        }

        byte[] bytes = null; string path = null; string decodeErr = null;
        try
        {
            bytes = Convert.FromBase64String(b64);
            path = Path.Combine(Application.persistentDataPath, "patient_reply." + ext);
            File.WriteAllBytes(path, bytes);
        }
        catch (Exception e) { decodeErr = e.Message; }
        if (decodeErr != null) { Log("[audio] EROARE decodare/scriere: " + decodeErr); yield break; }
        Log($"[audio] bytes={bytes.Length} fisier={path}");

        using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { Log("Audio: " + req.error); yield break; }

            AudioClip clip = null; string clipErr = null;
            try { clip = DownloadHandlerAudioClip.GetContent(req); } catch (Exception e) { clipErr = e.Message; }
            if (clip == null) { Log("[audio] EROARE clip: " + clipErr); yield break; }
            speaker.clip = clip;

            // Latenta simulata (simptom) minus timpul real deja scurs de la eliberarea butonului.
            double realElapsed = Time.realtimeSinceStartupAsDouble - tRelease;
            double target = (applySimulatedLatency && last?.nonverbal_behavior != null)
                ? last.nonverbal_behavior.response_latency_ms / 1000.0 * simulatedLatencyScale : 0;
            double wait = Math.Max(0, target - realElapsed);
            if (wait > 0) yield return new WaitForSecondsRealtime((float)wait);

            speaker.Play();
            double tPlay = Time.realtimeSinceStartupAsDouble;
            Log($"[latenta] tinta simulata={target:F1}s | real inainte de asteptare={realElapsed:F1}s | " +
                $"asteptare adaugata={wait:F1}s | total perceput={tPlay - tRelease:F1}s | " +
                $"n8n+retea={tReply - tRelease:F1}s");

            var listener = FindFirstObjectByType<AudioListener>();
            Log($"[audio] clip={clip.length:F1}s ch={clip.channels} | volume={speaker.volume} mute={speaker.mute} " +
                $"spatialBlend={speaker.spatialBlend} | AudioListener={(listener ? listener.name : "LIPSA")} " +
                $"| AudioListener.volume={AudioListener.volume} pause={AudioListener.pause}");

            float timeout = Time.realtimeSinceStartup + clip.length + 2f;
            while (speaker.isPlaying && Time.realtimeSinceStartup < timeout) yield return null;
        }
    }

    IEnumerator EndFlow(bool fullReset)
    {
        state = State.Ending;
        string res = null, err = null;
        Log(fullReset ? "End of game..." : "Salvez sesiunea in memorie (poate dura 10-20 s)...");
        yield return fullReset ? api.EndGame(r => res = r, e => err = e)
                               : api.EndSession(r => res = r, e => err = e);
        Log(err != null ? "EROARE: " + err : "OK: " + res);
        state = State.Idle;
    }

    // ---- UI debug ----
    void Log(string s) { log.AppendLine(s); Debug.Log(s); scroll.y = float.MaxValue; }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 560, Screen.height - 20), GUI.skin.box);
        GUILayout.Label($"Stare: {state}   (tine apasat SPACE sau butonul pentru a vorbi)");

        GUI.enabled = state == State.Idle || state == State.Recording;
        string label = state == State.Recording ? "■ Eliberează ca să trimiți" : "● Ține apăsat: vorbește";
        var r = GUILayoutUtility.GetRect(540, 60);
        if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) BeginTalk();
        if (Event.current.type == EventType.MouseUp && state == State.Recording) EndTalk();
        GUI.Box(r, label);

        GUI.enabled = state == State.Idle;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Dezorganizare: {disorganizationLevel}", GUILayout.Width(130));
        disorganizationLevel = Mathf.RoundToInt(GUILayout.HorizontalSlider(disorganizationLevel, 1, 5));
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Încheie sesiunea (salvează în memorie)", GUILayout.Height(35))) SaveSessionToMemory();
        if (GUILayout.Button("End of game (șterge tot)", GUILayout.Height(30))) ResetEverything();
        GUI.enabled = true;

        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(log.ToString());
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}