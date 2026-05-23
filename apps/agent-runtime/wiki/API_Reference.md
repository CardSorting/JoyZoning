# REST API Reference Guide 📑

This document describes the REST API endpoints exposed by the DietCode Control Plane (`operator_api.py`).

All request and response bodies utilize standard JSON format.

---

## 1. Create Session
Initialize a brand new bounded mutation session.

* **Endpoint:** `POST /sessions`
* **Content-Type:** `application/json`

### Request Body Schema:
```json
{
  "repoName": "String (Required. Repository locator, e.g. NousResearch/hermes-agent)",
  "framework": "String (Required. App framework, e.g. React/Vite)",
  "profileName": "String (Required. Capability profile, e.g. frontend-polisher)",
  "instruction": "String (Required. Goal description for the worker)",
  "budgetLimit": {
    "maxFiles": "Integer (Optional. Max file mutations. Default: 10)",
    "maxRuntimeMinutes": "Integer (Optional. Container timeout. Default: 15)",
    "maxToolCalls": "Integer (Optional. Max iterations. Default: 25)"
  }
}
```

### Response (200 OK):
```json
{
  "success": true,
  "sessionId": "b479df20-80d4-4ca8-bd4f-a79df89b4f0b",
  "status": "preflight",
  "message": "Session b479df20-80d4-4ca8-bd4f-a79df89b4f0b initialized. Preflight enqueued."
}
```

---

## 2. Approve Proposal
Authorize the Control Plane to apply the generated diff proposal and launch the unit testing suite.

* **Endpoint:** `POST /sessions/{sessionId}/approve`

### Request Body:
*None required (empty payload)*

### Response (200 OK):
```json
{
  "success": true,
  "sessionId": "b479df20-80d4-4ca8-bd4f-a79df89b4f0b",
  "status": "applying",
  "message": "Session b479df20-80d4-4ca8-bd4f-a79df89b4f0b approved. Applying mutations..."
}
```

### Error Responses:
* **400 Bad Request:** If session is not in `proposed` state.
* **404 Not Found:** If `sessionId` does not exist in Firestore.

---

## 3. Reject Proposal
Discard the generated diff proposal and reset/revert the session.

* **Endpoint:** `POST /sessions/{sessionId}/reject`

### Request Body:
*None required (empty payload)*

### Response (200 OK):
```json
{
  "success": true,
  "sessionId": "b479df20-80d4-4ca8-bd4f-a79df89b4f0b",
  "status": "reverted",
  "message": "Session b479df20-80d4-4ca8-bd4f-a79df89b4f0b rejected. Workspace reverted."
}
```

---

## 4. Get Session Status & Events
Retrieve the session metadata and stream the chronological event timeline.

* **Endpoint:** `GET /sessions/{sessionId}`

### Response (200 OK):
```json
{
  "sessionId": "b479df20-80d4-4ca8-bd4f-a79df89b4f0b",
  "repoName": "NousResearch/hermes-agent",
  "framework": "React/Vite",
  "profileName": "frontend-polisher",
  "instruction": "Fix font tracking class warnings",
  "status": "success",
  "createdAt": "2026-05-18T03:40:00Z",
  "updatedAt": "2026-05-18T03:42:15Z",
  "budgetLimit": {
    "maxFiles": 10,
    "maxRuntimeMinutes": 15,
    "maxToolCalls": 25
  },
  "budgetUsage": {
    "files": 2,
    "runtime": 3,
    "toolCalls": 5
  },
  "events": [
    {
      "timestamp": "2026-05-18T03:40:01Z",
      "type": "info",
      "message": "Session b479df20-80d4-4ca8-bd4f-a79df89b4f0b initialized. Preflight enqueued."
    },
    {
      "timestamp": "2026-05-18T03:40:12Z",
      "type": "info",
      "message": "Worker Runtime: Code proposal diff successfully generated and uploaded."
    },
    {
      "timestamp": "2026-05-18T03:41:40Z",
      "type": "info",
      "message": "Session approved. Applying mutations..."
    },
    {
      "timestamp": "2026-05-18T03:42:10Z",
      "type": "success",
      "message": "Unit tests completed successfully with 100% coverage."
    }
  ]
}
```
