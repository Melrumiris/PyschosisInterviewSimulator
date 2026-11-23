using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// Toate apelurile HTTP catre n8n. Nu stie nimic de microfon sau audio playback.
public class N8nClient : MonoBehaviour
{
    [Header("n8n")]
    [Tooltip("Ex: http://localhost:5678  sau  https://xxxx.ngrok-free.app  (fara slash la final)")]
    public string baseUrl = "https://wildly-refocus-boots.ngrok-free.dev";
    [Tooltip("true = /webhook/ (workflow ACTIV). false = /webhook-test/ (doar dupa 'Listen for test event')")]
    public bool useProductionUrls = true;

    [Header("Cai webhook (din noduri)")]
    public string chatPath = "ade7a97f-feb3-4b6c-976b-b7bb1af5ad53"; // Patient 01 - POST
    public string sessionEndPath = "f21c5e15-3118-4f19-a58c-bdff9802e419"; // Session to long-term - DELETE
    public string endGamePath = "a18e833e-bb2a-4d9f-bfc9-5dbd9be633c0"; // End of game - DELETE

    [Header("Identitati")]
    public string patientId = "matthew_ross"; // trebuie sa fie identic cu P din Initialize_patients
    public string playerIdOverride = "";      // gol = se genereaza unul stabil
    public int timeoutSeconds = 120;

    public string PlayerId
    {
        get
        {
            if (!string.IsNullOrEmpty(playerIdOverride)) return playerIdOverride;
            var id = PlayerPrefs.GetString("player_id", "");
            if (string.IsNullOrEmpty(id))
            {
                id = "player" + Guid.NewGuid().ToString("N").Substring(0, 8);
                PlayerPrefs.SetString("player_id", id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    // Evita pagina de avertizare ngrok (free) care ar intoarce HTML in loc de JSON.
    static void Common(UnityWebRequest req) => req.SetRequestHeader("ngrok-skip-browser-warning", "true");

    string Url(string path) =>
        $"{baseUrl.TrimEnd('/')}/{(useProductionUrls ? "webhook" : "webhook-test")}/{path}";

    /// O tura de conversatie: trimite WAV-ul terapeutului, primeste raspunsul pacientului.
    /// Multipart construit manual: campurile text NU au Content-Type, ca n8n sa le puna in body.*
    /// (WWWForm adauga Content-Type pe fiecare camp si n8n le trateaza ca fisiere binare.)
    public IEnumerator SendTurn(byte[] wav, int disorganizationLevel,
                                Action<PatientReply, double> onOk, Action<string> onError)
    {
        string boundary = "----PatientVR" + Guid.NewGuid().ToString("N");
        byte[] body;
        using (var ms = new MemoryStream())
        {
            void Write(string t) { var b = Encoding.UTF8.GetBytes(t); ms.Write(b, 0, b.Length); }
            void Field(string name, string value) =>
                Write($"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n");

            Field("patient_id", patientId);
            Field("player_id", PlayerId);
            Field("disorganization_level", Mathf.Clamp(disorganizationLevel, 1, 5).ToString());

            // "data" = numele implicit al proprietatii binare citite de nodul ElevenLabs STT
            Write($"--{boundary}\r\nContent-Disposition: form-data; name=\"data\"; filename=\"audio.wav\"\r\nContent-Type: audio/wav\r\n\r\n");
            ms.Write(wav, 0, wav.Length);
            Write($"\r\n--{boundary}--\r\n");
            body = ms.ToArray();
        }

        double t0 = Time.realtimeSinceStartupAsDouble;
        using (var req = new UnityWebRequest(Url(chatPath), "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(body);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + boundary);
            req.timeout = timeoutSeconds;
            Common(req);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"Chat {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
                yield break;
            }
            double rtt = Time.realtimeSinceStartupAsDouble - t0;
            try
            {
                var reply = JsonUtility.FromJson<PatientReply>(req.downloadHandler.text);
                onOk?.Invoke(reply, rtt);
            }
            catch (Exception e) { onError?.Invoke("JSON invalid: " + e.Message + "\n" + req.downloadHandler.text); }
        }
    }

    /// Salveaza sesiunea in memoria pe termen lung (Session to long-term) si sterge chat-ul.
    public IEnumerator EndSession(Action<string> onDone, Action<string> onError) =>
        DeleteWithBody(sessionEndPath, onDone, onError);

    /// Reset total pentru acest player + pacient (End of game): sterge chat + memoria.
    public IEnumerator EndGame(Action<string> onDone, Action<string> onError) =>
        DeleteWithBody(endGamePath, onDone, onError);

    IEnumerator DeleteWithBody(string path, Action<string> onDone, Action<string> onError)
    {
        string json = JsonUtility.ToJson(new Ids { player_id = PlayerId, patient_id = patientId });
        using (var req = new UnityWebRequest(Url(path), "DELETE"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            Common(req);
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success) onDone?.Invoke(req.downloadHandler.text);
            else onError?.Invoke($"{path} {req.responseCode}: {req.error}\n{req.downloadHandler.text}");
        }
    }

    [Serializable] class Ids { public string player_id; public string patient_id; }
}