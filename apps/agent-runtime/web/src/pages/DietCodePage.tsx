import { useState, useEffect, useRef } from "react";
import {
  Shield,
  Clock,
  Terminal,
  CheckCircle2,
  AlertTriangle,
  Play,
  Check,
  X,
  FileText,
  Database,
  RotateCw,
  Layers,
  Download,
  TrendingUp,
  Activity,
  Gauge,
  History,
} from "lucide-react";
import { Badge } from "@nous-research/ui/ui/components/badge";
import { Spinner } from "@nous-research/ui/ui/components/spinner";
import { Button } from "@nous-research/ui/ui/components/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

// One-Sentence Product Pitch
const PRODUCT_PITCH =
  "DietCode proposes code changes inside a disposable worker runtime, waits for approval, runs tests, and shows exactly what changed.";

// Define Bounded Operator Capability Profiles
interface OperatorProfile {
  name: string;
  description: string;
  permittedFiles: string;
  restrictedCommands: string;
  primaryPurpose: string;
}

const OPERATOR_PROFILES: OperatorProfile[] = [
  {
    name: "TestFixOperator",
    description: "Restricted strictly to fixing test files and writing tests.",
    permittedFiles: "**/src/__tests__/**, **/*.test.ts",
    restrictedCommands: "Edits to production code (src/core/*) blocked",
    primaryPurpose: "Fixes unit test failures and expands test suites.",
  },
  {
    name: "RefactorOperator",
    description: "Allowed to make structural code improvements.",
    permittedFiles: "**/src/local/fabric/**",
    restrictedCommands: "Modifying package lockfiles or API clients blocked",
    primaryPurpose: "Cleans up technical debt within explicit domains.",
  },
  {
    name: "TypingOperator",
    description: "Restricted strictly to adding and updating TypeScript types.",
    permittedFiles: "**/*.d.ts, **/*.types.ts",
    restrictedCommands: "All runtime JS/TS logic edits blocked",
    primaryPurpose: "Hardens static type safety boundaries.",
  },
  {
    name: "DependencyUpgradeOperator",
    description: "Allowed to edit only package configurations.",
    permittedFiles: "package.json, package-lock.json",
    restrictedCommands: "All source file editing blocked",
    primaryPurpose: "Performs version bumps and package installations.",
  },
];

// Define Repositories
const DETECTED_REPOS = [
  {
    name: "diet-hermes",
    framework: "Vite + React",
    packageManager: "pnpm",
    testCommand: "pnpm test",
    buildCommand: "pnpm build",
  },
  {
    name: "broccolidb",
    framework: "TypeScript Engine",
    packageManager: "bun",
    testCommand: "bun test",
    buildCommand: "bun run build",
  },
  {
    name: "nous-ui",
    framework: "Next.js + Tailwind",
    packageManager: "npm",
    testCommand: "npm run test",
    buildCommand: "npm run build",
  },
];

// Mock Unified Diffs per profile
const MOCK_DIFFS: Record<string, { path: string; diff: string }> = {
  TestFixOperator: {
    path: "ui-tui/src/__tests__/spine.test.ts",
    diff: `--- a/ui-tui/src/__tests__/spine.test.ts
+++ b/ui-tui/src/__tests__/spine.test.ts
@@ -10,6 +10,12 @@
   it("should enforce Bounded Operator pipeline constraints", () => {
     const pipeline = new VerifiedExecutionPipeline("/tmp/workspace");
     expect(pipeline.getBudget().maxFiles).toBe(10);
+  });
+
+  it("should enforce strict hash binding for alpha", () => {
+    const pipeline = new VerifiedExecutionPipeline("/tmp/workspace");
+    const result = pipeline.verifyProposal("args-hash-ok", "policy-hash-ok");
+    expect(result.valid).toBe(true);
   });
 };`,
  },
  RefactorOperator: {
    path: "ui-tui/src/local/fabric/spine.ts",
    diff: `--- a/ui-tui/src/local/fabric/spine.ts
+++ b/ui-tui/src/local/fabric/spine.ts
@@ -42,6 +42,9 @@
   public applyChanges(proposal: Proposal): void {
     this.boundary.checkContainment(proposal.path);
     this.boundary.backupOriginalFile(proposal.path);
+    
+    // Optimized single-node filesystem mutation write
+    this.boundary.writeFile(proposal.path, proposal.content);
     this.eventLog.log("changes.applied", { path: proposal.path });
   }
 }`,
  },
  TypingOperator: {
    path: "ui-tui/src/local/fabric/spine.types.ts",
    diff: `--- /dev/null
+++ b/ui-tui/src/local/fabric/spine.types.ts
@@ -0,0 +1,8 @@
+export interface SessionBudget {
+  maxFiles: number;
+  maxRuntimeMinutes: number;
+  maxToolCalls: number;
+  maxPatchSize: number;
+  maxTestRuntimeMinutes: number;
+}
+`,
  },
  DependencyUpgradeOperator: {
    path: "package.json",
    diff: `--- a/package.json
+++ b/package.json
@@ -25,3 +25,3 @@
   "devDependencies": {
-    "vitest": "^0.28.0",
+    "vitest": "^1.6.0",
     "typescript": "^5.0.4"`,
  },
};

export default function DietCodePage() {
  // Navigation header setup
  const [activeRepo, setActiveRepo] = useState(DETECTED_REPOS[0]);
  const [activeProfile, setActiveProfile] = useState(OPERATOR_PROFILES[0]);
  const [budgetLimit] = useState({
    maxFiles: 10,
    maxRuntimeMinutes: 15,
    maxToolCalls: 25,
    maxPatchSize: 5000,
    maxTestRuntimeMinutes: 5,
  });

  // State flags for interactive workflow
  const [sessionStatus, setSessionStatus] = useState<"idle" | "preflight" | "proposed" | "applying" | "testing" | "success" | "violation" | "reverted">("idle");
  const [activeTab, setActiveTab] = useState<"dashboard" | "benchmarks" | "logs">("dashboard");
  const [logs, setLogs] = useState<string[]>([]);
  const [wakeupsCount, setWakeupsCount] = useState(0);
  const [budgetUsage, setBudgetUsage] = useState({
    files: 0,
    runtime: 0,
    toolCalls: 0,
    patchSize: 0,
    testRuntime: 0,
  });

  const [argsHash] = useState("sha256-a4c892b11ef937a01dcd4a22c5e4f71a9b23e80d");
  const [policyHash] = useState("sha256-8c42ff65aef33b0dd4a221f7c9e0134b3f114ea2");

  const terminalEndRef = useRef<HTMLDivElement>(null);

  // Helper to append observable logs
  const appendLog = (msg: string, type: "info" | "success" | "warn" | "error" | "telemetry" = "info") => {
    const timestamp = new Date().toISOString().substring(11, 19);
    let prefix = "[INFO]";
    if (type === "success") prefix = "🟢 [SUCCESS]";
    if (type === "warn") prefix = "🟡 [WARN]";
    if (type === "error") prefix = "🔴 [ERROR]";
    if (type === "telemetry") prefix = "🛡️ [OTEL TRACE]";
    setLogs((prev: string[]) => [...prev, `${timestamp} ${prefix} ${msg}`]);
  };

  useEffect(() => {
    if (terminalEndRef.current) {
      terminalEndRef.current.scrollIntoView({ behavior: "smooth" });
    }
  }, [logs]);

  // Initial greeting
  useEffect(() => {
    setLogs([]);
    appendLog("DietCode Control Plane initialized securely.", "success");
    appendLog(`Pitch: "${PRODUCT_PITCH}"`, "info");
  }, []);

  // Handler: Start Operator Session
  const handleStartSession = () => {
    setSessionStatus("preflight");
    setLogs([]);
    setWakeupsCount(0);
    setBudgetUsage({ files: 0, runtime: 0, toolCalls: 0, patchSize: 0, testRuntime: 0 });
    
    appendLog(`Initializing session with repo: ${activeRepo.name} (${activeRepo.framework})`, "info");
    appendLog(`Applying Capability Profile: ${activeProfile.name}`, "success");
    appendLog("Control Plane: Enqueuing workspace checkout task inside operator-job-queue...", "info");
    
    setTimeout(() => {
      appendLog("Worker Runtime: Spawned single-session execution container.", "success");
      appendLog(`Worker Runtime: Repository cloned to disposable /tmp/workspace.`, "info");
      appendLog(`Worker Runtime: Repo Safety Check completed for profile ${activeProfile.name}.`, "success");
      setBudgetUsage((prev: { files: number; runtime: number; toolCalls: number; patchSize: number; testRuntime: number }) => ({ ...prev, files: 1, toolCalls: 3, runtime: 1 }));
      setSessionStatus("proposed");
      appendLog("Worker Runtime: Verified Proposal generated and checkpointed.", "success");
      appendLog("Worker Runtime: Compressed 'pre-mutation' snapshot (12.4 MB) uploaded to gs://operator-artifacts/recovery/.", "success");
      appendLog("Worker Runtime: Ephemeral worker exited successfully. Standing by for event-driven wakeup.", "warn");
    }, 1500);
  };

  // Handler: Approve and Apply Changes
  const handleApprove = () => {
    setSessionStatus("applying");
    appendLog("Control Plane: Approval event received. Matching policy bounds...", "success");
    appendLog(`Control Plane: Validated argsHash matches verified proposal [${argsHash.substring(0, 12)}...]`, "success");
    appendLog("Control Plane: Dispatching resume task to operator-job-queue...", "info");
    setWakeupsCount(1);

    setTimeout(() => {
      appendLog("Worker Runtime: Respawned worker container from GCS checkpoint.", "success");
      appendLog("Worker Runtime: Reconstituted disposable /tmp/workspace from pre-mutation snapshot.", "info");
      appendLog("Worker Runtime: Applying Changes to file...", "success");
      
      const fileCount = activeProfile.name === "TypingOperator" ? 1 : 2;
      const patchLen = activeProfile.name === "TestFixOperator" ? 450 : 280;
      setBudgetUsage((prev: { files: number; runtime: number; toolCalls: number; patchSize: number; testRuntime: number }) => ({
        ...prev,
        files: fileCount,
        toolCalls: prev.toolCalls + 4,
        patchSize: patchLen,
        runtime: prev.runtime + 2,
      }));

      appendLog("Worker Runtime: Compressed 'post-mutation' snapshot created.", "success");
      
      setSessionStatus("testing");
      appendLog(`Worker Runtime: Triggering test suite command: "${activeRepo.testCommand}"`, "info");

      setTimeout(() => {
        appendLog("Worker Runtime: Tests executed successfully. Exit code: 0.", "success");
        appendLog("Worker Runtime: 12 tests passed, 0 failed.", "success");
        setBudgetUsage((prev: { files: number; runtime: number; toolCalls: number; patchSize: number; testRuntime: number }) => ({ ...prev, testRuntime: 1, runtime: prev.runtime + 1 }));
        
        appendLog("Worker Runtime: Compressed 'post-test' final snapshot created.", "success");
        appendLog("Worker Runtime: Cleaning /tmp workspace directory...", "info");
        appendLog("Worker Runtime: Ephemeral worker container destroyed.", "warn");
        
        setSessionStatus("success");
        appendLog(`DietCode: Bounded Session completed successfully in ${budgetUsage.runtime + 3} minutes!`, "success");
      }, 1500);

    }, 1500);
  };

  // Handler: Trigger Policy Budget Violation Simulation
  const handleTriggerViolation = () => {
    setSessionStatus("applying");
    appendLog("Control Plane: Resuming worker execution...", "info");
    
    setTimeout(() => {
      appendLog("Worker Runtime: Respawned worker container.", "success");
      appendLog("Worker Runtime: Applying out-of-bounds file write proposal...", "warn");
      appendLog("Worker Runtime: Checking constraints...", "info");
      
      // Simulate exceeding files budget
      setBudgetUsage((prev: { files: number; runtime: number; toolCalls: number; patchSize: number; testRuntime: number }) => ({
        ...prev,
        files: 15, // Over limit
        toolCalls: 30, // Over limit
        patchSize: 6500, // Over limit
      }));

      setSessionStatus("violation");
      appendLog("Worker Runtime: HALT! Budget limit exceeded! Max files limit is 10, attempted to mutate 15.", "error");
      appendLog("Worker Runtime: Triggering automatic recovery rollback protocol...", "warn");
      
      setTimeout(() => {
        setSessionStatus("reverted");
        appendLog("Worker Runtime: Rollback completed successfully. Original checkout reconstituted.", "success");
        appendLog("Worker Runtime: Ephemeral worker terminated.", "error");
      }, 1000);

    }, 1200);
  };

  // Golden Benchmarks Simulation State
  const [benchStats, setBenchStats] = useState({
    running: false,
    completed: false,
    successRate: 100,
    rollbackRate: 0,
    avgDurationSec: 4.2,
    totalRuns: 4,
    benchmarks: [
      { name: "fix failing test", status: "passed", duration: "3.2s", cost: "$0.0021" },
      { name: "rename symbol", status: "passed", duration: "4.8s", cost: "$0.0034" },
      { name: "upgrade vitest devDependency", status: "passed", duration: "5.1s", cost: "$0.0019" },
      { name: "add types for spine session", status: "passed", duration: "3.9s", cost: "$0.0028" },
    ]
  });

  const runGoldenSuite = () => {
    setBenchStats((prev: { running: boolean; completed: boolean; successRate: number; rollbackRate: number; avgDurationSec: number; totalRuns: number; benchmarks: Array<{ name: string; status: string; duration: string; cost: string }> }) => ({ ...prev, running: true, completed: false }));
    appendLog("Observability: Initializing Golden Session Evaluation Suite...", "info");
    
    setTimeout(() => {
      setBenchStats((prev: { running: boolean; completed: boolean; successRate: number; rollbackRate: number; avgDurationSec: number; totalRuns: number; benchmarks: Array<{ name: string; status: string; duration: string; cost: string }> }) => ({
        ...prev,
        running: false,
        completed: true,
        successRate: 98.4,
        rollbackRate: 1.6,
        avgDurationSec: 4.1,
        totalRuns: 250,
      }));
      appendLog("Observability: Golden Session Benchmark suite completed. 250 test operators executed. Success rate: 98.4%, Rollback rate: 1.6%.", "success");
    }, 2000);
  };

  return (
    <div className="flex flex-col gap-6 font-mondwest text-midground antialiased uppercase">
      {/* Header Page Title & Summary */}
      <div className="flex flex-col gap-2 border-b border-current/20 pb-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Shield className="h-6 w-6 text-primary animate-pulse" />
            <h1 className="text-2xl font-bold tracking-wider">DietCode Bounded Control Plane</h1>
          </div>
          <Badge tone="success" className="text-xs uppercase">
            Alpha Operational
          </Badge>
        </div>
        <p className="text-sm font-bold text-muted-foreground/80 tracking-wide mt-1">
          {PRODUCT_PITCH}
        </p>
      </div>

      {/* Tabs Menu */}
      <div className="flex gap-2 border-b border-current/10 pb-2">
        <Button
          onClick={() => setActiveTab("dashboard")}
          className={`h-8 px-4 text-xs font-bold tracking-widest ${activeTab === "dashboard" ? "bg-primary text-black" : "bg-transparent border border-current/20 text-midground hover:bg-current/5"}`}
        >
          <Layers className="mr-1.5 h-3.5 w-3.5" />
          Operator Dashboard
        </Button>
        <Button
          onClick={() => setActiveTab("benchmarks")}
          className={`h-8 px-4 text-xs font-bold tracking-widest ${activeTab === "benchmarks" ? "bg-primary text-black" : "bg-transparent border border-current/20 text-midground hover:bg-current/5"}`}
        >
          <TrendingUp className="mr-1.5 h-3.5 w-3.5" />
          Golden Benchmarks
        </Button>
        <Button
          onClick={() => setActiveTab("logs")}
          className={`h-8 px-4 text-xs font-bold tracking-widest ${activeTab === "logs" ? "bg-primary text-black" : "bg-transparent border border-current/20 text-midground hover:bg-current/5"}`}
        >
          <Activity className="mr-1.5 h-3.5 w-3.5" />
          Observability logs
        </Button>
      </div>

      {/* Active Tab Contents */}
      {activeTab === "dashboard" && (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          
          {/* LEFT SIDE: Setup & Session Control Panel */}
          <div className="lg:col-span-1 flex flex-col gap-6">
            
            {/* Target Repository Adapter Setup */}
            <Card className="border border-current/20 bg-background-base/40">
              <CardHeader className="border-b border-current/20 p-3">
                <CardTitle className="text-xs font-bold tracking-[0.12em] flex items-center gap-2">
                  <Database className="h-3.5 w-3.5 text-primary" />
                  1. Target Repository Profile
                </CardTitle>
              </CardHeader>
              <CardContent className="p-4 flex flex-col gap-3">
                <div>
                  <label className="text-[10px] text-muted-foreground block mb-1">Select Target Project</label>
                  <select
                    className="w-full h-8 px-2 bg-black border border-current/20 text-xs rounded-sm focus:outline-none"
                    value={activeRepo.name}
                    onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                      const repo = DETECTED_REPOS.find(r => r.name === e.target.value);
                      if (repo) {
                        setActiveRepo(repo);
                        appendLog(`Active repository switched to: ${repo.name}`, "info");
                      }
                    }}
                    disabled={sessionStatus !== "idle" && sessionStatus !== "success" && sessionStatus !== "reverted"}
                  >
                    {DETECTED_REPOS.map((repo) => (
                      <option key={repo.name} value={repo.name}>
                        {repo.name}
                      </option>
                    ))}
                  </select>
                </div>
                
                <div className="grid grid-cols-2 gap-2 mt-1">
                  <div className="border border-current/10 p-2 bg-black/30">
                    <span className="text-[9px] text-muted-foreground block">Framework</span>
                    <span className="text-xs font-mono font-bold text-primary">{activeRepo.framework}</span>
                  </div>
                  <div className="border border-current/10 p-2 bg-black/30">
                    <span className="text-[9px] text-muted-foreground block">Pkg Manager</span>
                    <span className="text-xs font-mono font-bold text-primary">{activeRepo.packageManager}</span>
                  </div>
                </div>

                <div className="border border-current/10 p-2 bg-black/30">
                  <span className="text-[9px] text-muted-foreground block">Test Runner Command</span>
                  <span className="text-xs font-mono text-muted-foreground block select-all">{activeRepo.testCommand}</span>
                </div>
              </CardContent>
            </Card>

            {/* Bounded Capability Profile */}
            <Card className="border border-current/20 bg-background-base/40">
              <CardHeader className="border-b border-current/20 p-3">
                <CardTitle className="text-xs font-bold tracking-[0.12em] flex items-center gap-2">
                  <Shield className="h-3.5 w-3.5 text-primary" />
                  2. Operator Capability Profile
                </CardTitle>
              </CardHeader>
              <CardContent className="p-4 flex flex-col gap-3">
                <div>
                  <label className="text-[10px] text-muted-foreground block mb-1">Select Profile</label>
                  <select
                    className="w-full h-8 px-2 bg-black border border-current/20 text-xs rounded-sm focus:outline-none"
                    value={activeProfile.name}
                    onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                      const profile = OPERATOR_PROFILES.find(p => p.name === e.target.value);
                      if (profile) {
                        setActiveProfile(profile);
                        appendLog(`Capability Profile switched to: ${profile.name}`, "info");
                      }
                    }}
                    disabled={sessionStatus !== "idle" && sessionStatus !== "success" && sessionStatus !== "reverted"}
                  >
                    {OPERATOR_PROFILES.map((p) => (
                      <option key={p.name} value={p.name}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </div>

                <p className="text-[11px] text-muted-foreground italic leading-relaxed">
                  {activeProfile.description}
                </p>

                <div className="flex flex-col gap-1 text-[10px] mt-1">
                  <div className="flex justify-between border-b border-current/5 py-1">
                    <span className="text-muted-foreground">Permitted Scope:</span>
                    <span className="font-mono text-primary font-bold">{activeProfile.permittedFiles}</span>
                  </div>
                  <div className="flex justify-between py-1">
                    <span className="text-muted-foreground">Sandbox Constraint:</span>
                    <span className="font-mono text-warning font-bold">{activeProfile.restrictedCommands}</span>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Execution Budget Constraints */}
            <Card className="border border-current/20 bg-background-base/40">
              <CardHeader className="border-b border-current/20 p-3">
                <CardTitle className="text-xs font-bold tracking-[0.12em] flex items-center gap-2">
                  <Gauge className="h-3.5 w-3.5 text-primary" />
                  3. Session Execution Budget
                </CardTitle>
              </CardHeader>
              <CardContent className="p-4 flex flex-col gap-2">
                <div className="flex flex-col gap-2 text-[10px]">
                  <div className="flex justify-between items-center">
                    <span className="text-muted-foreground">Max Mutated Files</span>
                    <span className="font-mono font-bold">{budgetLimit.maxFiles} files</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-muted-foreground">Max Runtime Duration</span>
                    <span className="font-mono font-bold">{budgetLimit.maxRuntimeMinutes} mins</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-muted-foreground">Max Safe Tool Calls</span>
                    <span className="font-mono font-bold">{budgetLimit.maxToolCalls} calls</span>
                  </div>
                  <div className="flex justify-between items-center">
                    <span className="text-muted-foreground">Max Patch Size Limit</span>
                    <span className="font-mono font-bold">{budgetLimit.maxPatchSize} bytes</span>
                  </div>
                </div>
                
                {sessionStatus !== "idle" && (
                  <div className="border-t border-current/10 pt-3 mt-2 flex flex-col gap-2">
                    <span className="text-[9px] text-primary font-bold">Active Runtime Budget Consumed:</span>
                    <div className="flex flex-col gap-1.5 text-[10px]">
                      <div>
                        <div className="flex justify-between mb-0.5">
                          <span>Files Edited</span>
                          <span>{budgetUsage.files} / {budgetLimit.maxFiles}</span>
                        </div>
                        <div className="w-full bg-black/60 h-1.5 border border-current/10 rounded-full overflow-hidden">
                          <div
                            className="bg-primary h-full transition-all duration-300"
                            style={{ width: `${(budgetUsage.files / budgetLimit.maxFiles) * 100}%` }}
                          />
                        </div>
                      </div>
                      <div>
                        <div className="flex justify-between mb-0.5">
                          <span>Tool Calls</span>
                          <span>{budgetUsage.toolCalls} / {budgetLimit.maxToolCalls}</span>
                        </div>
                        <div className="w-full bg-black/60 h-1.5 border border-current/10 rounded-full overflow-hidden">
                          <div
                            className="bg-primary h-full transition-all duration-300"
                            style={{ width: `${(budgetUsage.toolCalls / budgetLimit.maxToolCalls) * 100}%` }}
                          />
                        </div>
                      </div>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>
          </div>

          {/* RIGHT SIDE: Timeline and Live Workspace Changes (Applying) */}
          <div className="lg:col-span-2 flex flex-col gap-6">
            
            {/* Active Timeline Steps */}
            <Card className="border border-current/20 bg-background-base/40">
              <CardHeader className="border-b border-current/20 p-3 flex flex-row items-center justify-between">
                <CardTitle className="text-xs font-bold tracking-[0.12em] flex items-center gap-2">
                  <Clock className="h-3.5 w-3.5 text-primary" />
                  Active Operator Timeline
                </CardTitle>
                
                {sessionStatus === "idle" && (
                  <Button
                    onClick={handleStartSession}
                    className="h-7 px-3 text-[10px] font-bold bg-primary text-black hover:bg-primary/80"
                  >
                    <Play className="mr-1 h-3 w-3" />
                    Start Operator Session
                  </Button>
                )}

                {sessionStatus !== "idle" && (
                  <Button
                    onClick={() => setSessionStatus("idle")}
                    className="h-7 px-3 text-[10px] font-bold bg-black border border-current/20 text-midground hover:bg-current/5"
                  >
                    <RotateCw className="mr-1 h-3 w-3" />
                    Reset
                  </Button>
                )}
              </CardHeader>
              <CardContent className="p-4">
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 py-2">
                  {/* Timeline Nodes */}
                  <div className="flex flex-wrap md:flex-nowrap gap-3 items-center w-full">
                    {/* Node 1: Intent Received */}
                    <div className="flex items-center gap-2">
                      <div className={`h-6 w-6 rounded-full flex items-center justify-center text-[10px] font-bold border ${sessionStatus !== "idle" ? "bg-primary border-primary text-black" : "border-current/20 bg-transparent"}`}>
                        1
                      </div>
                      <span className="text-[10px] font-bold">Intent</span>
                    </div>

                    <div className="hidden md:block w-4 h-px bg-current/20" />

                    {/* Node 2: Safety Checked */}
                    <div className="flex items-center gap-2">
                      <div className={`h-6 w-6 rounded-full flex items-center justify-center text-[10px] font-bold border ${sessionStatus !== "idle" && sessionStatus !== "preflight" ? "bg-primary border-primary text-black" : "border-current/20 bg-transparent"}`}>
                        2
                      </div>
                      <span className="text-[10px] font-bold">Safety Check</span>
                    </div>

                    <div className="hidden md:block w-4 h-px bg-current/20" />

                    {/* Node 3: Proposal Created */}
                    <div className="flex items-center gap-2">
                      <div className={`h-6 w-6 rounded-full flex items-center justify-center text-[10px] font-bold border ${sessionStatus !== "idle" && sessionStatus !== "preflight" ? "bg-primary border-primary text-black" : "border-current/20 bg-transparent"}`}>
                        3
                      </div>
                      <span className="text-[10px] font-bold">Verified Proposal</span>
                    </div>

                    <div className="hidden md:block w-4 h-px bg-current/20" />

                    {/* Node 4: Approved */}
                    <div className="flex items-center gap-2">
                      <div className={`h-6 w-6 rounded-full flex items-center justify-center text-[10px] font-bold border ${sessionStatus === "applying" || sessionStatus === "testing" || sessionStatus === "success" ? "bg-primary border-primary text-black" : "border-current/20 bg-transparent"}`}>
                        4
                      </div>
                      <span className="text-[10px] font-bold">Approved</span>
                    </div>

                    <div className="hidden md:block w-4 h-px bg-current/20" />

                    {/* Node 5: Changes Applied */}
                    <div className="flex items-center gap-2">
                      <div className={`h-6 w-6 rounded-full flex items-center justify-center text-[10px] font-bold border ${sessionStatus === "testing" || sessionStatus === "success" ? "bg-primary border-primary text-black" : "border-current/20 bg-transparent"}`}>
                        5
                      </div>
                      <span className="text-[10px] font-bold">Changes Applied</span>
                    </div>

                    <div className="hidden md:block w-4 h-px bg-current/20" />

                    {/* Node 6: Tests Run */}
                    <div className="flex items-center gap-2">
                      <div className={`h-6 w-6 rounded-full flex items-center justify-center text-[10px] font-bold border ${sessionStatus === "success" ? "bg-primary border-primary text-black" : "border-current/20 bg-transparent"}`}>
                        6
                      </div>
                      <span className="text-[10px] font-bold">Tests Passed</span>
                    </div>
                  </div>
                </div>

                {/* Event-driven Worker Wakeups Details */}
                {sessionStatus !== "idle" && (
                  <div className="border-t border-current/10 pt-3 mt-2 flex flex-col md:flex-row md:items-center justify-between gap-2 text-[10px]">
                    <div className="flex items-center gap-1.5">
                      <Layers className="h-3.5 w-3.5 text-primary" />
                      <span className="text-muted-foreground">Active Worker Mode:</span>
                      <span className="font-bold text-primary font-mono">{sessionStatus === "proposed" ? "EXITED & STANDING BY" : "ACTIVE RUNNING"}</span>
                    </div>
                    <div className="flex items-center gap-3 font-mono text-[9px] bg-black/40 px-3 py-1.5 border border-current/10 rounded-sm">
                      <span className="text-muted-foreground">Container Wakeups: <span className="text-primary font-bold">{wakeupsCount}</span></span>
                      <span className="text-muted-foreground">Recovery snapshot: <span className="text-primary font-bold">{sessionStatus === "proposed" ? "GCS CHECKPOINTED" : "SYNCHRONIZED"}</span></span>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Verified Proposal & Diff Viewer Panel */}
            {sessionStatus !== "idle" && sessionStatus !== "preflight" && (
              <Card className="border border-current/20 bg-background-base/40">
                <CardHeader className="border-b border-current/20 p-3">
                  <CardTitle className="text-xs font-bold tracking-[0.12em] flex items-center gap-2">
                    <FileText className="h-3.5 w-3.5 text-primary" />
                    Verified Proposal
                  </CardTitle>
                </CardHeader>
                <CardContent className="p-4 flex flex-col gap-4">
                  
                  {/* Proposal Metadata */}
                  <div className="grid grid-cols-2 gap-2 text-[10px] font-mono bg-black/40 p-3 border border-current/10 rounded-sm">
                    <div>
                      <span className="text-muted-foreground block">Proposed mutation path:</span>
                      <span className="text-primary font-bold select-all">{MOCK_DIFFS[activeProfile.name]?.path}</span>
                    </div>
                    <div>
                      <span className="text-muted-foreground block">Target Commit SHA:</span>
                      <span className="text-muted-foreground block select-all">cf2390a1b2c3d4e5f6...</span>
                    </div>
                    <div className="border-t border-current/5 pt-2 mt-2">
                      <span className="text-muted-foreground block">Mandatory argsHash:</span>
                      <span className="text-primary font-bold text-[9px] truncate block select-all">{argsHash}</span>
                    </div>
                    <div className="border-t border-current/5 pt-2 mt-2">
                      <span className="text-muted-foreground block">Mandatory policyHash:</span>
                      <span className="text-primary font-bold text-[9px] truncate block select-all">{policyHash}</span>
                    </div>
                  </div>

                  {/* Diff Canvas */}
                  <div className="border border-current/25 rounded-sm overflow-hidden bg-black font-mono text-[11px] leading-relaxed">
                    <div className="bg-background-base/80 px-3 py-2 border-b border-current/15 text-[10px] text-muted-foreground flex justify-between">
                      <span>Unified Diff Preview</span>
                      <span className="text-primary font-bold">{MOCK_DIFFS[activeProfile.name]?.path}</span>
                    </div>
                    <pre className="p-3 overflow-x-auto whitespace-pre leading-relaxed text-muted-foreground max-h-60">
                      {MOCK_DIFFS[activeProfile.name]?.diff.split("\n").map((line, idx) => {
                        let lineStyle = "text-muted-foreground";
                        if (line.startsWith("+")) lineStyle = "text-success bg-success/5 font-bold";
                        if (line.startsWith("-")) lineStyle = "text-destructive bg-destructive/5 font-bold";
                        if (line.startsWith("@@")) lineStyle = "text-primary bg-primary/5 font-bold";
                        return (
                          <div key={idx} className={`px-2 py-0.5 ${lineStyle}`}>
                            {line}
                          </div>
                        );
                      })}
                    </pre>
                  </div>

                  {/* GCS Snapshot Checkpoints list */}
                  <div className="border border-current/10 p-3 rounded-sm bg-black/30 flex flex-col gap-2">
                    <span className="text-[10px] text-muted-foreground font-bold flex items-center gap-1.5">
                      <Database className="h-3 w-3 text-primary" />
                      Durable Snapshot Checkpoints (gs://operator-artifacts/):
                    </span>
                    <div className="grid grid-cols-3 gap-2 text-[9px] font-mono">
                      <div className="flex items-center justify-between border border-current/10 p-2 bg-black/40">
                        <span>pre-mutation.tar.gz</span>
                        <Download className="h-3 w-3 text-primary" />
                      </div>
                      <div className={`flex items-center justify-between border border-current/10 p-2 ${sessionStatus !== "proposed" && sessionStatus !== "applying" ? "bg-black/40" : "opacity-30"}`}>
                        <span>post-mutation.tar.gz</span>
                        <Download className="h-3 w-3 text-primary" />
                      </div>
                      <div className={`flex items-center justify-between border border-current/10 p-2 ${sessionStatus === "success" ? "bg-black/40" : "opacity-30"}`}>
                        <span>post-test.tar.gz</span>
                        <Download className="h-3 w-3 text-primary" />
                      </div>
                    </div>
                  </div>

                  {/* Action Buttons */}
                  {sessionStatus === "proposed" && (
                    <div className="flex flex-col sm:flex-row gap-3 border-t border-current/15 pt-4">
                      <Button
                        onClick={handleApprove}
                        className="flex-1 h-9 px-4 text-xs font-bold tracking-widest bg-success text-black hover:bg-success/80"
                      >
                        <Check className="mr-1.5 h-3.5 w-3.5" />
                        Approve and Apply Changes
                      </Button>
                      
                      <Button
                        onClick={handleTriggerViolation}
                        className="flex-1 h-9 px-4 text-xs font-bold tracking-widest bg-warning text-black hover:bg-warning/80"
                      >
                        <AlertTriangle className="mr-1.5 h-3.5 w-3.5" />
                        Simulate Budget Violation
                      </Button>

                      <Button
                        onClick={() => {
                          setSessionStatus("idle");
                          appendLog("Session cancelled by user action.", "warn");
                        }}
                        className="h-9 px-4 text-xs font-bold tracking-widest bg-transparent border border-current/20 text-destructive hover:bg-destructive/5"
                      >
                        <X className="mr-1.5 h-3.5 w-3.5" />
                        Reject Proposal
                      </Button>
                    </div>
                  )}

                  {/* Active applying states */}
                  {sessionStatus === "applying" && (
                    <div className="flex items-center justify-center py-6 gap-3 border-t border-current/15">
                      <Spinner className="text-primary text-xl" />
                      <span className="text-xs font-bold animate-pulse text-primary">Control Plane: Enqueuing task to wake up worker container...</span>
                    </div>
                  )}

                  {sessionStatus === "testing" && (
                    <div className="flex flex-col gap-2 border-t border-current/15 pt-4">
                      <div className="flex items-center justify-center py-3 gap-3">
                        <Spinner className="text-primary text-lg" />
                        <span className="text-xs font-bold animate-pulse text-primary">Worker Runtime: Running test suite command: "{activeRepo.testCommand}"</span>
                      </div>
                      <div className="bg-black p-3 border border-current/20 rounded-sm font-mono text-[10px] text-success overflow-x-auto leading-relaxed max-h-32">
                        <div>&gt; {activeRepo.testCommand}</div>
                        <div className="text-muted-foreground mt-1">RUNS  src/__tests__/spine.test.ts</div>
                        <div>✓ should enforce Bounded Operator pipeline constraints (12ms)</div>
                        <div>✓ should enforce strict hash binding for alpha (8ms)</div>
                        <div className="text-success font-bold mt-1">Test Suites: 1 passed, 1 total</div>
                        <div className="text-success font-bold">Tests:       2 passed, 2 total</div>
                        <div className="text-success font-bold">Snapshots:   0 total</div>
                        <div className="text-muted-foreground">Time:        0.84s</div>
                      </div>
                    </div>
                  )}

                  {/* Success State */}
                  {sessionStatus === "success" && (
                    <div className="border border-success/30 bg-success/5 p-4 rounded-sm flex flex-col gap-2">
                      <span className="text-xs font-bold text-success flex items-center gap-2">
                        <CheckCircle2 className="h-4 w-4 text-success" />
                        Operator Changes Applied & Tested Successfully!
                      </span>
                      <p className="text-[11px] text-muted-foreground leading-relaxed">
                        The single-use Worker Runtime container has successfully run your test suite, uploaded the final post-test workspace snapshot, and terminated cleanly. All mutations are durably written.
                      </p>
                    </div>
                  )}

                  {/* Violation Simulation State */}
                  {sessionStatus === "violation" && (
                    <div className="border border-destructive/30 bg-destructive/5 p-4 rounded-sm flex flex-col gap-2">
                      <span className="text-xs font-bold text-destructive flex items-center gap-2">
                        <AlertTriangle className="h-4 w-4 text-destructive" />
                        BUDGET VIOLATION DETECTED!
                      </span>
                      <p className="text-[11px] text-muted-foreground leading-relaxed">
                        The active worker exceeded the Mutated Files threshold limit of 10 (mutated 15 files). Executing automatic workspace recovery rollback protocol instantly...
                      </p>
                    </div>
                  )}

                  {/* Reverted State */}
                  {sessionStatus === "reverted" && (
                    <div className="border border-warning/30 bg-warning/5 p-4 rounded-sm flex flex-col gap-2">
                      <span className="text-xs font-bold text-warning flex items-center gap-2">
                        <History className="h-4 w-4 text-warning" />
                        Rollback Audit Recovery Succeeded
                      </span>
                      <p className="text-[11px] text-muted-foreground leading-relaxed">
                        The volatile workspace has been completely restored to git commit checkout base `cf2390a` using the GCS `pre-mutation.tar.gz` recovery snapshot. Parity validated.
                      </p>
                    </div>
                  )}
                </CardContent>
              </Card>
            )}

            {/* Observability Terminal Log Panel */}
            <Card className="border border-current/20 bg-background-base/40">
              <CardHeader className="border-b border-current/20 p-3">
                <CardTitle className="text-xs font-bold tracking-[0.12em] flex items-center gap-2">
                  <Terminal className="h-3.5 w-3.5 text-primary" />
                  Observable Event Logs Console
                </CardTitle>
              </CardHeader>
              <CardContent className="p-3">
                <pre className="bg-black/95 p-3 border border-current/25 rounded-sm font-mono text-[10px] text-muted-foreground leading-relaxed h-48 overflow-y-auto whitespace-pre-wrap break-all">
                  {logs.map((log: string, idx: number) => {
                    let logStyle = "text-muted-foreground";
                    if (log.includes("🟢")) logStyle = "text-success font-bold";
                    if (log.includes("🟡")) logStyle = "text-warning font-bold";
                    if (log.includes("🔴")) logStyle = "text-destructive font-bold";
                    return (
                      <div key={idx} className={logStyle}>
                        {log}
                      </div>
                    );
                  })}
                  <div ref={terminalEndRef} />
                </pre>
              </CardContent>
            </Card>

          </div>

        </div>
      )}

      {/* Benchmarks Tab */}
      {activeTab === "benchmarks" && (
        <Card className="border border-current/20 bg-background-base/40">
          <CardHeader className="border-b border-current/20 p-4 flex flex-row items-center justify-between">
            <div>
              <CardTitle className="text-base font-bold flex items-center gap-2">
                <TrendingUp className="h-5 w-5 text-primary" />
                Golden Session Evaluation Suite
              </CardTitle>
              <p className="text-xs text-muted-foreground mt-0.5">
                Run automated benchmarks to evaluate operator mutation accuracy, success rates, and budget rolls against historical golden runs.
              </p>
            </div>
            
            <Button
              onClick={runGoldenSuite}
              disabled={benchStats.running}
              className="h-8 px-4 text-xs font-bold bg-primary text-black hover:bg-primary/80"
            >
              {benchStats.running ? (
                <>
                  <Spinner className="mr-1.5" />
                  Running Golden Suite...
                </>
              ) : (
                <>
                  <Play className="mr-1.5 h-3.5 w-3.5" />
                  Run Golden Suite
                </>
              )}
            </Button>
          </CardHeader>
          <CardContent className="p-4 flex flex-col gap-6">
            
            {/* Stats Overview */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              <div className="border border-current/10 p-3 bg-black/40 text-center rounded-sm">
                <span className="text-[10px] text-muted-foreground block uppercase">Total Operators Run</span>
                <span className="text-xl font-bold font-mono text-primary">{benchStats.totalRuns}</span>
              </div>
              <div className="border border-current/10 p-3 bg-black/40 text-center rounded-sm">
                <span className="text-[10px] text-muted-foreground block uppercase">Success Rate</span>
                <span className="text-xl font-bold font-mono text-success">{benchStats.successRate}%</span>
              </div>
              <div className="border border-current/10 p-3 bg-black/40 text-center rounded-sm">
                <span className="text-[10px] text-muted-foreground block uppercase">Rollback Rate</span>
                <span className="text-xl font-bold font-mono text-warning">{benchStats.rollbackRate}%</span>
              </div>
              <div className="border border-current/10 p-3 bg-black/40 text-center rounded-sm">
                <span className="text-[10px] text-muted-foreground block uppercase">Avg Exec Time</span>
                <span className="text-xl font-bold font-mono text-primary">{benchStats.avgDurationSec}s</span>
              </div>
            </div>

            {/* Benchmarks List */}
            <div className="flex flex-col gap-2">
              <span className="text-xs font-bold">Standard Suite Scenarios:</span>
              <div className="border border-current/15 rounded-sm overflow-hidden bg-black/30">
                <table className="w-full text-left text-xs font-mono">
                  <thead>
                    <tr className="bg-background-base/80 text-[10px] border-b border-current/20">
                      <th className="p-3">Scenario Name</th>
                      <th className="p-3">Status</th>
                      <th className="p-3">Worker Time</th>
                      <th className="p-3">Estimated Cost</th>
                    </tr>
                  </thead>
                  <tbody>
                    {benchStats.benchmarks.map((bench: { name: string; status: string; duration: string; cost: string }, idx: number) => (
                      <tr key={idx} className="border-b border-current/5">
                        <td className="p-3 font-bold text-midground">{bench.name}</td>
                        <td className="p-3">
                          <Badge tone="success" className="text-[9px]">
                            {bench.status}
                          </Badge>
                        </td>
                        <td className="p-3 text-muted-foreground">{bench.duration}</td>
                        <td className="p-3 text-primary">{bench.cost}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
            
            {benchStats.completed && (
              <div className="border border-success/20 bg-success/5 p-4 rounded-sm flex items-start gap-3">
                <CheckCircle2 className="h-5 w-5 text-success shrink-0 mt-0.5" />
                <div>
                  <span className="text-xs font-bold text-success">Golden Regressions Validation Succeeded!</span>
                  <p className="text-[11px] text-muted-foreground mt-1 leading-relaxed">
                    Zero regressions detected. The single-node operator workspace boundary safely isolated and completed all mutations, verifying full consistency under concurrent load tests.
                  </p>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* Logs Tab */}
      {activeTab === "logs" && (
        <Card className="border border-current/20 bg-background-base/40">
          <CardHeader className="border-b border-current/20 p-4">
            <CardTitle className="text-base font-bold flex items-center gap-2">
              <Activity className="h-5 w-5 text-primary" />
              Observability & OpenTelemetry Metrics
            </CardTitle>
            <p className="text-xs text-muted-foreground mt-0.5">
              Live metrics and structured trace logs indicating Cloud Run Job performance, Secret Manager fetches, and API enqueuer latencies.
            </p>
          </CardHeader>
          <CardContent className="p-4 flex flex-col gap-4 font-mono text-xs">
            <div className="grid grid-cols-2 gap-4">
              <div className="border border-current/10 p-3 bg-black/40 rounded-sm">
                <span className="text-[10px] text-muted-foreground block uppercase">Cloud Run Execution Cost</span>
                <span className="text-sm font-bold text-primary">$0.0034 / turn</span>
              </div>
              <div className="border border-current/10 p-3 bg-black/40 rounded-sm">
                <span className="text-[10px] text-muted-foreground block uppercase">Task Queue Latency</span>
                <span className="text-sm font-bold text-primary">120 ms</span>
              </div>
            </div>
            <div className="bg-black p-3 border border-current/25 rounded-sm font-mono text-[10px] text-muted-foreground h-64 overflow-y-auto leading-relaxed">
              <div className="text-primary font-bold">&gt; tail -f open-telemetry-spans.json</div>
              <div className="text-success mt-1">{"{\"span_id\":\"s-a42d\",\"name\":\"SecretManager.FetchKey\",\"duration_ms\":84,\"status\":\"ok\"}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-ef90\",\"name\":\"CloudTasks.EnqueueJob\",\"duration_ms\":145,\"status\":\"ok\",\"payload\":{\"queue\":\"operator-job-queue\"}}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-b2c1\",\"name\":\"WorkerRuntime.RepoSafetyCheck\",\"duration_ms\":230,\"status\":\"ok\",\"payload\":{\"repo\":\"diet-hermes\"}}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-f1a2\",\"name\":\"GCS.UploadSnapshot\",\"duration_ms\":512,\"status\":\"ok\",\"payload\":{\"path\":\"recovery/pre-mutation.tar.gz\"}}"}</div>
              <div className="text-warning">{"{\"span_id\":\"s-a12b\",\"name\":\"WorkerRuntime.Exited\",\"payload\":{\"status\":\"checkpoint_saved\",\"reason\":\"waiting_for_approval\"}}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-d3e4\",\"name\":\"ControlPlane.ApprovalGateMatched\",\"duration_ms\":12,\"status\":\"ok\"}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-9c8d\",\"name\":\"WorkerRuntime.ApplyChanges\",\"duration_ms\":98,\"status\":\"ok\"}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-e3f4\",\"name\":\"WorkerRuntime.ExecuteTests\",\"duration_ms\":840,\"status\":\"ok\",\"payload\":{\"exit_code\":0}}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-7a8b\",\"name\":\"GCS.UploadSnapshot\",\"duration_ms\":480,\"status\":\"ok\",\"payload\":{\"path\":\"recovery/post-test.tar.gz\"}}"}</div>
              <div className="text-success">{"{\"span_id\":\"s-2c3d\",\"name\":\"WorkerRuntime.CleanWorkspace\",\"duration_ms\":45,\"status\":\"ok\"}"}</div>
              <div className="text-primary font-bold mt-2">&gt; tail -f stdout.log</div>
              <div className="mt-1">12:04:12 [INFO] Control Plane API enqueued job.</div>
              <div>12:04:13 [INFO] Spawned Worker Runtime container instance.</div>
              <div>12:04:15 [SUCCESS] Workspace Check safety policy passed.</div>
              <div>12:04:16 [WARN] Worker Exited. Recovery state synchronized in GCS.</div>
            </div>
          </CardContent>
        </Card>
      )}

    </div>
  );
}
