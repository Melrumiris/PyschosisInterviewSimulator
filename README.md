# Psychosis Interview Simulator - AI Workflows

Plan Doc: https://docs.google.com/document/d/1h3ZP0ndCo3sc2a0P2ViaHwm60MgdmyQ0OMTr2c3FL_s/
Main Modules: https://docs.google.com/document/d/1oJIGm0fvaIhgMCvIDY6yqbyMYGkH-lVT1jzME1WEQ-E/

This branch (`AI_Workflows`) contains the **n8n workflows** that power the simulated
patient interviews in the VR training simulator. The Unity client records the
therapist's voice, sends it to n8n, and receives the patient's spoken answer plus
non-verbal/clinical state.

## Architecture at a glance

```text
Unity VR client
   |  POST /webhook (patient audio + player_id + patient_id)
   v
n8n patient workflow
   |-- ElevenLabs  -> speech-to-text
   |-- OpenAI      -> patient agent (gpt-4o-mini)
   |     |-- Postgres chat memory (short-term, per player + patient)
   |     |-- Supabase vector store (long-term clinical profile / RAG)
   |     `-- Postgres query (previous session memories)
   |-- ElevenLabs  -> text-to-speech
   `-- JSON response (audio + behaviour + trust/clinical state)

Supporting workflows:
   Initialize patients  -> embeds the 5 patient profiles into Supabase
   Session to long-term -> summarises a finished session into episodic memory
   End of game          -> deletes a finished game's chat + vector data
```

## Workflow catalog

| File | Trigger | Role |
| --- | --- | --- |
| `N8N Workflows/Initialize patients.json` | Manual | One-time/seed workflow. Clears `patients_knowledge`, then embeds the knowledge chunks of all five patients into the Supabase vector store. |
| `N8N Workflows/Patient_01 - Matthew Ross.json` | `POST` webhook | Simulated patient session: transcribes therapist audio, runs the Matthew Ross agent with memory + RAG, returns TTS audio and clinical/non-verbal state. |
| `N8N Workflows/Patient_02 - Emily Carter.json` | `POST` webhook | Same graph as above, persona **Emily Carter**. |
| `N8N Workflows/Patient_03 - Jake Thompsom.json` | `POST` webhook | Same graph as above, persona **Jake Thompson**. |
| `N8N Workflows/Patient_04 - Theodore Miller.json` | `POST` webhook | Same graph as above, persona **Theodore "Ted" Miller**. |
| `N8N Workflows/Patient_05 - Rachel Morgan.json` | `POST` webhook | Same graph as above, persona **Rachel Morgan**. |
| `N8N Workflows/Session to long-term.json` | `DELETE` webhook | At session end: reads the chat history, lets the "Cleaner" agent write an episodic memory, stores it as `past_interview` in Supabase, then clears the short-term chat memory. |
| `N8N Workflows/End of game.json` | `DELETE` webhook | At game end: deletes the player's chat history and all Supabase knowledge rows for that player + patient combination. |

## Workflow details

### Initialize patients

Run this **once** (or whenever the patient knowledge base changes) before the
first interview.

1. `Execute a SQL query` runs `DELETE FROM patients_knowledge;` to reset the store.
2. Five `Code in JavaScript` nodes contain the raw knowledge chunks for each
   patient (`matthew_ross`, `emily_carter`, `jake_thompson`, `theodore_miller`,
   `rachel_morgan`). Each chunk carries `patient_id`, `chunk_id`, `category` and
   `min_trust`.
3. Each chunk is embedded with the OpenAI embeddings node and inserted into the
   Supabase vector-store table `patients_knowledge` via its own vector-store node.

> The chunks are intentionally written in first person, with trust/disclosure
> conditions, so the agent knows what the patient is willing to reveal at each
> trust level.

### Patient_01 ... Patient_05 (the interview loop)

All five files share the same graph; only the persona, system prompt, voice and
webhook path differ.

1. **Webhook** receives a `multipart/form-data` request from Unity with the
   therapist's audio and the session identifiers.
2. **Transcribe audio or video** (ElevenLabs `speechToText`) turns the audio into
   text.
3. **Execute a SQL query** loads up to three `past_interview` memories for the
   same `patient_id` + `player_id` from `patients_knowledge`.
4. **Agent** (LangChain agent + OpenAI `gpt-4o-mini`) receives the transcript,
   previous memories and the active disorganization level. It:
   - retrieves relevant profile chunks with the **Supabase Vector Store**
     (`retrieve-as-tool`, filtered by `patient_id`);
   - keeps short-term context in **Postgres Chat Memory** with session key
     `<patient_id>_<player_id>`;
   - returns a strict JSON schema: `spoken_response`, `nonverbal_behavior` and
     `clinical_state`.
5. **Code in JavaScript** validates/sanitises the agent output, attaches the TTS
   audio as base64, and normalises the allowed non-verbal/trust values.
6. **Convert text to speech** (ElevenLabs) renders `spoken_response` with the
   persona's voice.
7. **Respond to Webhook** returns the JSON payload to Unity.

Personas:

| Workflow | Persona | Age | Webhook path |
| --- | --- | --- | --- |
| `Patient_01 - Matthew Ross.json` | Matthew "Matt" Ross | 26 | `ade7a97f-feb3-4b6c-976b-b7bb1af5ad53` |
| `Patient_02 - Emily Carter.json` | Emily "Em" Carter | 29 | `52409f8b-1c27-4e86-89bf-6e0bbfeab143` |
| `Patient_03 - Jake Thompsom.json` | Jake Thompson | 22 | `970a0e02-8f26-4192-a818-344fddcf2f4c` |
| `Patient_04 - Theodore Miller.json` | Theodore "Ted" Miller | 41 | `06a35bc1-ec64-484d-8b1c-1ec888322e4d` |
| `Patient_05 - Rachel Morgan.json` | Rachel Morgan | 33 | `30282cd9-396f-4982-a01d-8930afefb520` |

### Session to long-term

Triggered when a single interview session ends.

1. `Select Session convo` reads `n8n_chat_histories` for
   `session_id = <patient_id>_<player_id>` and builds a transcript, including the
   ending trust level and disclosed topics.
2. `Cleaner` (OpenAI `gpt-4o-mini`) writes a first-person, <150-word episodic
   memory of the session.
3. The memory is embedded and inserted into `patients_knowledge` with
   `type = past_interview`, `patient_id`, `player_id`, `session_id`,
   `ending_trust_level` and `topics_disclosed`.
4. `Sterge din chat memo` deletes the short-term rows from `n8n_chat_histories`.

### End of game

Triggered when the whole game ends (not just one session).

1. `Sterge din chat memo1` deletes the player's rows from `n8n_chat_histories`.
2. `Delete a row` deletes Supabase `patients_knowledge` rows matching
   `metadata->>session_id = <patient_id>_<player_id>`.
3. `Respond to Webhook` acknowledges the deletion.

## Webhook reference

| Method | Path | Workflow | Expected body |
| --- | --- | --- | --- |
| `POST` | `ade7a97f-feb3-4b6c-976b-b7bb1af5ad53` | Patient 01 - Matthew Ross | `multipart/form-data`: `player_id`, `patient_id`, `disorganization_level`, audio file in the `data` field |
| `POST` | `52409f8b-1c27-4e86-89bf-6e0bbfeab143` | Patient 02 - Emily Carter | same as above |
| `POST` | `970a0e02-8f26-4192-a818-344fddcf2f4c` | Patient 03 - Jake Thompson | same as above |
| `POST` | `06a35bc1-ec64-484d-8b1c-1ec888322e4d` | Patient 04 - Theodore Miller | same as above |
| `POST` | `30282cd9-396f-4982-a01d-8930afefb520` | Patient 05 - Rachel Morgan | same as above |
| `DELETE` | `f21c5e15-3118-4f19-a58c-bdff9802e419` | Session to long-term | JSON: `player_id`, `patient_id` |
| `DELETE` | `a18e833e-bb2a-4d9f-bfc9-5dbd9be633c0` | End of game | JSON: `player_id`, `patient_id` |

The patient webhooks respond with JSON shaped like:

```json
{
  "spoken_response": "I don't know... maybe someone is watching.",
  "nonverbal_behavior": {
    "eye_contact_ratio": 0.1,
    "facial_affect": "guarded",
    "body_behavior": "arms_crossed",
    "response_latency_ms": 4000
  },
  "clinical_state": {
    "current_trust_level": "very_low",
    "applied_disorganization_level": 2,
    "disclosed_topics": []
  },
  "audio_base64": "<base64 mp3>",
  "audio_mime_type": "audio/mpeg"
}
```

## External services and data stores

| Service | Used by | Purpose |
| --- | --- | --- |
| **OpenAI** | All AI workflows | `gpt-4o-mini` for patient/cleaner agents, `text-embedding-3-small` (1536 dims) for embeddings |
| **ElevenLabs** | Patient workflows | Speech-to-text and persona text-to-speech |
| **Supabase (Postgres + pgvector)** | Initialize, Patients, Session, End of game | `patients_knowledge` vector table (clinical profile + episodic memories) |
| **Postgres** | Patients, Session, End of game | `n8n_chat_histories` short-term chat memory (created automatically by the Postgres Chat Memory node) |

Credentials are **not** stored in the exported JSON. After importing, create and
re-assign the credential names used by the workflows:

| Credential type | Name in the workflows |
| --- | --- |
| OpenAI API | `Payed` |
| ElevenLabs API | `IRM credidentials` |
| Postgres | `IRM Project` |
| Supabase API | `Supabase account` |

## Run n8n locally with Docker + ngrok

This is the quick setup for developing/testing the workflows before publishing
them. n8n runs in Docker on your machine and ngrok gives it a public HTTPS URL so
Unity (or a headset) can reach the webhooks.

### 1. Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) with Docker
  Compose.
- [ngrok](https://ngrok.com/download) account + authtoken.
- API keys for OpenAI, ElevenLabs, Supabase, and a Postgres database.

### 2. Create a `.env` file

Create `.env` in the project root (it is ignored by git):

```dotenv
# Public URL printed by ngrok (use your static domain if you have one)
NGROK_URL=https://your-subdomain.ngrok-free.app

# Long random string. Keep it stable or saved credentials stop decrypting.
N8N_ENCRYPTION_KEY=replace-with-a-long-random-string
```

### 3. Create `docker-compose.yml`

```yaml
services:
  n8n:
    image: docker.n8n.io/n8nio/n8n:latest
    container_name: n8n-irm
    restart: unless-stopped
    ports:
      - "5678:5678"
    environment:
      - N8N_HOST=localhost
      - N8N_PORT=5678
      - N8N_PROTOCOL=https
      - N8N_EDITOR_BASE_URL=${NGROK_URL}
      - WEBHOOK_URL=${NGROK_URL}
      - N8N_ENCRYPTION_KEY=${N8N_ENCRYPTION_KEY}
      - N8N_SECURE_COOKIE=false
      - N8N_COMMUNITY_PACKAGES_ENABLED=true
      - GENERIC_TIMEZONE=Europe/Bucharest
    volumes:
      - n8n_data:/home/node/.n8n
    extra_hosts:
      - "host.docker.internal:host-gateway"

volumes:
  n8n_data:
```

> `N8N_SECURE_COOKIE=false` keeps local `http://localhost:5678` login working.
> `N8N_PROTOCOL=https` and the URL variables make n8n generate ngrok HTTPS
> webhook URLs instead of localhost URLs.

### 4. Start n8n

```bash
docker compose up -d
docker compose logs -f n8n
```

Open the editor at <http://localhost:5678> (or through the ngrok URL).

### 5. Start ngrok

In a second terminal:

```bash
ngrok config add-authtoken <YOUR_NGROK_TOKEN>
ngrok http 5678
```

Use the `https://...` URL ngrok prints as `NGROK_URL`, then restart n8n so the
new URL is picked up:

```bash
docker compose up -d --force-recreate
```

If you have a reserved/static ngrok domain, use it so the URL does not change:

```bash
ngrok http --url=your-subdomain.ngrok-free.app 5678
```

### 6. Import the workflows

In the n8n editor:

1. **Workflows -> Add workflow -> Import from File...**
2. Import all files from `N8N Workflows/`.
3. Open each workflow and **Save** it.
4. Activate every workflow that has a webhook (`Patient_*`,
   `Session to long-term`, `End of game`). Inactive workflows do not serve
   production webhook URLs.

> Use the **Production URL** shown in the webhook node, not the "Test URL".
> Test URLs only work while you click "Listen for test event".

### 7. Install the ElevenLabs community node

The patient workflows use the community node
`@elevenlabs/n8n-nodes-elevenlabs`. Install it once:

- **Settings -> Community Nodes -> Install** and enter
  `@elevenlabs/n8n-nodes-elevenlabs`, or
- restart n8n with `N8N_COMMUNITY_PACKAGES_ENABLED=true` and install from the UI.

The remaining nodes (Postgres, Supabase, LangChain/OpenAI) are built into n8n.

### 8. Prepare the Supabase vector table

Run this once in the Supabase SQL editor, then run the **Initialize patients**
workflow manually:

```sql
create extension if not exists vector;

create table if not exists patients_knowledge (
  id bigserial primary key,
  content text,
  metadata jsonb,
  embedding vector(1536)
);

create or replace function match_documents (
  query_embedding vector(1536),
  match_count int default null,
  filter jsonb default '{}'
) returns table (
  id bigint,
  content text,
  metadata jsonb,
  similarity float
)
language plpgsql
as $$
#variable_conflict use_column
begin
  return query
  select
    id,
    content,
    metadata,
    1 - (patients_knowledge.embedding <=> query_embedding) as similarity
  from patients_knowledge
  where metadata @> filter
  order by patients_knowledge.embedding <=> query_embedding
  limit match_count;
end;
$$;
```

The `n8n_chat_histories` table is created automatically by the Postgres Chat
Memory node the first time a patient workflow runs.

### 9. Smoke test

```bash
# Replace with your ngrok URL and one of the patient webhook paths
curl -X POST "https://your-subdomain.ngrok-free.app/webhook/ade7a97f-feb3-4b6c-976b-b7bb1af5ad53" \
  -F "player_id=player42" \
  -F "patient_id=matthew_ross" \
  -F "disorganization_level=1" \
  -F "data=@sample-therapist-question.mp3"
```

If the workflow is wired correctly you get the JSON response shown above with
`audio_base64` populated.

## Security notes

- Never commit `.env`, n8n data volumes, API keys, or exported credentials.
- The exported workflow JSON references credentials by name only; keys are not
  included. Keep it that way.
- On ngrok's free plan the public URL can change on restart. Recreate the n8n
  container after the URL changes, otherwise webhook URLs point to the old host.
- Treat the ngrok URL as public while it is running. Stop ngrok (and/or the
  container) when you are done.

## Troubleshooting

| Symptom | Likely fix |
| --- | --- |
| Webhook returns 404 | Workflow is not active, or you are using the Test URL instead of the Production URL. |
| `WEBHOOK_URL` still shows localhost | Recreate the container after updating `.env`; check `docker compose config`. |
| ElevenLabs node missing | Install `@elevenlabs/n8n-nodes-elevenlabs` and restart n8n. |
| Supabase vector store errors | Re-run the table/function SQL above; confirm the Supabase credential and `patients_knowledge` table name. |
| Agent remembers nothing | Run `Initialize patients`, then `Session to long-term` after a finished session; confirm `patient_id`/`player_id` match Unity. |
| Credentials became invalid after restart | `N8N_ENCRYPTION_KEY` changed; restore the previous key. |
