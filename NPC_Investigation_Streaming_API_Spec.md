# NPC Investigation Streaming API Spec

## Goal
- Extend the existing `POST /investigation/npc` API so the Unity client can receive NPC replies as a stream.
- The Unity client is already prepared to handle both normal JSON responses and NDJSON streaming responses.
- Recommended response format is `application/x-ndjson` with one JSON object per line.

## Endpoint
- Method: `POST`
- Path: `/investigation/npc`
- Request Content-Type: `application/json`
- Response Content-Type: `application/x-ndjson`

## Request Body
```json
{
  "sceneId": "bar_murder_case_01",
  "phase": "investigation",
  "playerId": "player",
  "npcId": "rebecca",
  "personaKey": "rebeca",
  "interaction": {
    "actionType": "Talk",
    "playerIntentText": "그날 밤 바 안 분위기가 어땠는지 묻는다.",
    "topicId": null,
    "evidenceId": null,
    "objectId": null,
    "statementRefIds": []
  },
  "sceneState": {
    "discoveredEvidenceIds": [],
    "unlockedTopicIds": [],
    "activeHypothesisIds": [],
    "interactableObjectIds": []
  },
  "npcLocalState": {
    "hasIntroduced": true,
    "conversationCount": 2,
    "knownTopicsUnlocked": [],
    "lastKnownSignal": {
      "stress": 0.12,
      "distortion": 0.05,
      "focus": 0.81
    },
    "lastInteractionTime": null,
    "cachedRecentStatements": []
  },
  "conversationContext": {
    "recentExchanges": [
      {
        "speaker": "npc",
        "text": "어서 와. 오늘은 평소보다 질문이 많겠네."
      },
      {
        "speaker": "player",
        "text": "그날 밤 바 안 분위기가 어땠는지 묻는다."
      }
    ]
  }
}
```

## Streaming Response Format
- Response should be newline-delimited JSON.
- Each line must be one complete JSON object.
- Recommended order:
  1. `start`
  2. zero or more `delta`
  3. optional `signal`
  4. `complete`
- On failure, return `error` instead of `complete`.

## Stream Chunk Types

### 1. start
Sent once when the NPC reply stream begins.

```json
{
  "type": "start",
  "messageId": "msg_001",
  "npcDisplayName": "리베카"
}
```

### 2. delta
Sent multiple times as the LLM generates text.

```json
{
  "type": "delta",
  "messageId": "msg_001",
  "text": "그날은 "
}
```

### 3. signal
Optional mid-stream signal update.

```json
{
  "type": "signal",
  "messageId": "msg_001",
  "signal": {
    "stress": 0.34,
    "distortion": 0.18,
    "focus": 0.66
  }
}
```

### 4. complete
Final streamed result. This must contain the full final response object.

```json
{
  "type": "complete",
  "messageId": "msg_001",
  "response": {
    "ok": true,
    "replyText": "그날은 평소보다 조용한 척하는 사람들이 많았어.",
    "signal": {
      "stress": 0.34,
      "distortion": 0.18,
      "focus": 0.66
    },
    "stateDelta": {
      "unlockTopicIds": [
        "bar_mood"
      ],
      "markStatements": [
        {
          "statementId": "rebeca_stmt_002",
          "text": "그날은 평소보다 조용한 척하는 사람들이 많았어."
        }
      ]
    },
    "presentationHints": {
      "animation": "guarded_idle",
      "voiceTone": "calm",
      "uiNoiseLevel": 0.18
    },
    "error": ""
  }
}
```

### 5. error
Streamed error response.

```json
{
  "type": "error",
  "error": "persona not found: rebeca"
}
```

## Final Response Object Shape
The `complete.response` payload must match this structure.

```json
{
  "ok": true,
  "replyText": "NPC final line",
  "signal": {
    "stress": 0.0,
    "distortion": 0.0,
    "focus": 0.0
  },
  "stateDelta": {
    "unlockTopicIds": [],
    "markStatements": []
  },
  "presentationHints": {
    "animation": "guarded_idle",
    "voiceTone": "calm",
    "uiNoiseLevel": 0.0
  },
  "error": ""
}
```

## Client Expectations
The Unity client currently supports:
- normal JSON success response
- normal JSON failure response
- NDJSON streaming response using these chunk types:
  - `start`
  - `delta`
  - `signal`
  - `complete`
  - `error`

Behavior expected by the client:
- `start` opens a temporary streaming message row
- each `delta` appends to the visible NPC reply
- `signal` updates the bio-signal panel immediately
- `complete` commits the final NPC message and applies `stateDelta`
- `error` cancels the temporary streaming message and shows an error notice

## Recommended Server Behavior
- Keep existing validation logic for:
  - missing `personaKey`
  - missing persona file
  - invalid `interaction.playerIntentText`
- After validation, start the stream with `start`
- Forward generated text incrementally as `delta`
- Send at least one final `complete`
- Use UTF-8 encoding
- Flush after each line so Unity receives chunks promptly

## Error Handling
Recommended HTTP status behavior:
- `200` for successful streaming session
- `400` for invalid request payload
- `404` for missing persona
- `500` for internal server error

Recommended non-stream error body:
```json
{
  "ok": false,
  "replyText": "",
  "signal": {
    "stress": 0.0,
    "distortion": 0.0,
    "focus": 0.0
  },
  "stateDelta": {
    "unlockTopicIds": [],
    "markStatements": []
  },
  "presentationHints": {},
  "error": "persona not found: rebeca"
}
```

## Implementation Notes
- NDJSON is preferred over SSE because the current Unity client is prepared to parse newline-delimited JSON chunks.
- If the model provider returns token chunks, map each chunk directly into `delta` lines.
- If only sentence-level fragments are available, those can still be sent as `delta`.
- The final `complete` chunk must still be sent even if the full reply was already visible through `delta` chunks.

## Minimal Success Example
```text
{"type":"start","messageId":"msg_001","npcDisplayName":"리베카"}
{"type":"delta","messageId":"msg_001","text":"그날은 "}
{"type":"delta","messageId":"msg_001","text":"좀 이상하게 조용했어."}
{"type":"signal","messageId":"msg_001","signal":{"stress":0.31,"distortion":0.14,"focus":0.69}}
{"type":"complete","messageId":"msg_001","response":{"ok":true,"replyText":"그날은 좀 이상하게 조용했어.","signal":{"stress":0.31,"distortion":0.14,"focus":0.69},"stateDelta":{"unlockTopicIds":["bar_mood"],"markStatements":[{"statementId":"rebeca_stmt_002","text":"그날은 좀 이상하게 조용했어."}]},"presentationHints":{"animation":"guarded_idle","voiceTone":"calm","uiNoiseLevel":0.14},"error":""}}
```
