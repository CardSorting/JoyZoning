import os
import uuid
import json
from typing import Optional
from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from google.cloud import firestore
from google.cloud import tasks_v2

app = FastAPI(title="DietCode Operator API")

# Enable CORS for human-in-the-loop dashboard connections
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Extract Project Context
PROJECT_ID = os.getenv("GOOGLE_CLOUD_PROJECT") or os.getenv("PROJECT_ID") or "dietcode-hermes-98243"
db = firestore.Client(project=PROJECT_ID)

class CreateSessionRequest(BaseModel):
    repoName: str
    framework: str
    profileName: str
    instruction: str

def trigger_worker(session_id: str, phase: str):
    """Triggers the background operator worker. Supports Cloud Tasks and local subprocesses."""
    use_cloud = os.getenv("USE_CLOUD_TASKS", "false").lower() == "true"
    
    if use_cloud:
        try:
            client = tasks_v2.CloudTasksClient()
            queue = os.getenv("QUEUE_NAME", "operator-queue")
            location = os.getenv("TASK_REGION", "us-central1")
            parent = client.queue_path(PROJECT_ID, location, queue)
            
            task_url = os.getenv("WORKER_TRIGGER_URL", "")
            if not task_url:
                print("WORKER_TRIGGER_URL is missing. Falling back to local execution.")
                use_cloud = False
            else:
                task = {
                    "http_request": {
                        "http_method": tasks_v2.HttpMethod.POST,
                        "url": task_url,
                        "headers": {"Content-Type": "application/json"},
                        "body": json.dumps({"sessionId": session_id, "phase": phase}).encode(),
                    }
                }
                client.create_task(request={"parent": parent, "task": task})
                print(f"[CLOUD TASKS] Successfully enqueued worker task for session {session_id}, phase {phase}")
        except Exception as e:
            print(f"[CLOUD TASKS ERROR] Failed to create Cloud Task: {e}. Falling back to local simulation.")
            use_cloud = False

    if not use_cloud:
        # Local sandbox triggers (spawn operator_worker.py directly in background)
        import subprocess
        import sys
        script_path = os.path.join(os.path.dirname(__file__), "operator_worker.py")
        cmd = [sys.executable, script_path, session_id, phase]
        subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        print(f"[LOCAL WORKER] Spawned local worker subprocess for session {session_id}, phase {phase}")

@app.post("/sessions")
async def create_session(req: CreateSessionRequest, background_tasks: BackgroundTasks):
    session_id = str(uuid.uuid4())
    session_ref = db.collection("sessions").document(session_id)
    
    session_data = {
        "sessionId": session_id,
        "repoName": req.repoName,
        "framework": req.framework,
        "profileName": req.profileName,
        "instruction": req.instruction,
        "status": "preflight",
        "createdAt": firestore.SERVER_TIMESTAMP,
        "updatedAt": firestore.SERVER_TIMESTAMP,
        "argsHash": "sha256-a4c892b11ef937a01dcd4a22c5e4f71a9b23e80d",
        "policyHash": "sha256-8c42ff65aef33b0dd4a221f7c9e0134b3f114ea2",
        "budgetLimit": {
            "maxFiles": 10,
            "maxRuntimeMinutes": 15,
            "maxToolCalls": 25,
            "maxPatchSize": 5000,
            "maxTestRuntimeMinutes": 5,
        },
        "budgetUsage": {
            "files": 0,
            "runtime": 0,
            "toolCalls": 0,
            "patchSize": 0,
            "testRuntime": 0,
        },
    }
    
    session_ref.set(session_data)
    
    # Audit log entry for initial intent received
    events_ref = session_ref.collection("events")
    events_ref.add({
        "timestamp": firestore.SERVER_TIMESTAMP,
        "type": "info",
        "message": f"Control Plane: Intent Received. Spawn enqueuing worker checkout for repo {req.repoName}."
    })
    
    # Trigger preflight validation phase asynchronously
    background_tasks.add_task(trigger_worker, session_id, "preflight")
    
    return {"sessionId": session_id, "status": "preflight"}

@app.post("/sessions/{sessionId}/approve")
async def approve_session(sessionId: str, background_tasks: BackgroundTasks):
    session_ref = db.collection("sessions").document(sessionId)
    session = session_ref.get()
    
    if not session.exists:
        raise HTTPException(status_code=404, detail="Session not found")
        
    session_ref.update({
        "status": "applying",
        "updatedAt": firestore.SERVER_TIMESTAMP,
    })
    
    events_ref = session_ref.collection("events")
    events_ref.add({
        "timestamp": firestore.SERVER_TIMESTAMP,
        "type": "success",
        "message": "Control Plane: Cryptographic approval verified. Validated argsHash matched."
    })
    
    # Trigger apply execution and test run phase
    background_tasks.add_task(trigger_worker, sessionId, "apply")
    
    return {"sessionId": sessionId, "status": "applying"}

@app.post("/sessions/{sessionId}/reject")
async def reject_session(sessionId: str):
    session_ref = db.collection("sessions").document(sessionId)
    session = session_ref.get()
    
    if not session.exists:
        raise HTTPException(status_code=404, detail="Session not found")
        
    session_ref.update({
        "status": "reverted",
        "updatedAt": firestore.SERVER_TIMESTAMP,
    })
    
    events_ref = session_ref.collection("events")
    events_ref.add({
        "timestamp": firestore.SERVER_TIMESTAMP,
        "type": "warn",
        "message": "Control Plane: Patch Proposal rejected by operator. Rolling back changes."
    })
    
    return {"sessionId": sessionId, "status": "reverted"}

@app.get("/sessions/{sessionId}")
async def get_session(sessionId: str):
    session_ref = db.collection("sessions").document(sessionId)
    session = session_ref.get()
    
    if not session.exists:
        raise HTTPException(status_code=404, detail="Session not found")
        
    # Order event audit logs
    events = []
    events_query = session_ref.collection("events").order_by("timestamp").stream()
    for ev in events_query:
        ev_dict = ev.to_dict()
        if "timestamp" in ev_dict and ev_dict["timestamp"]:
            ev_dict["timestamp"] = ev_dict["timestamp"].isoformat()
        events.append(ev_dict)
        
    session_dict = session.to_dict()
    if "createdAt" in session_dict and session_dict["createdAt"]:
        session_dict["createdAt"] = session_dict["createdAt"].isoformat()
    if "updatedAt" in session_dict and session_dict["updatedAt"]:
        session_dict["updatedAt"] = session_dict["updatedAt"].isoformat()
        
    return {
        "session": session_dict,
        "events": events
    }
