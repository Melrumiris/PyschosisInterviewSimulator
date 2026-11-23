using System;

// Oglindeste exact JSON-ul intors de nodul "Code in JavaScript" din workflow-ul pacientului.
[Serializable]
public class NonverbalBehavior
{
    public float eye_contact_ratio;
    public string facial_affect;
    public string body_behavior;
    public int response_latency_ms;
}

[Serializable]
public class ClinicalState
{
    public string current_trust_level;
    public int applied_disorganization_level;
    public string[] disclosed_topics;
}

[Serializable]
public class PatientReply
{
    public string spoken_response;
    public NonverbalBehavior nonverbal_behavior;
    public ClinicalState clinical_state;
    public string audio_base64;
    public string audio_mime_type;
    public string transcript;   // optional: apare doar daca adaugi campul in nodul Code (vezi pasul 6)
}