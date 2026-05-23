# GitOps & CI/CD Integration Guide 🔄

This page provides the reference architecture and configuration files to integrate the DietCode Control Plane directly into your team's pull request and software development lifecycle (SDLC) workflows.

```text
  [ Pull Request Opened ]
             │
             ▼ (Webhook)
  [ GitHub Actions Pipeline ] ──(Calls API)──> [ Control Plane (FastAPI) ]
                                                       │
                                                       ▼ (Worker runs)
  [ PR Comment Posted ] <──(Returns Diff)──────────────┘
```

---

## 1. The GitOps Pull Request Workflow

By hooking the Control Plane into your version control system (VCS), you can achieve automated, sandboxed policy checking for every proposed modification:

1. **Developer opens a Pull Request:** A GitHub Actions workflow is triggered.
2. **Preflight enqueued:** The pipeline calls `POST /sessions` on the Control Plane, providing the repository reference, commit SHA, and the target instruction.
3. **Automated verification:** The sandboxed Worker container checks permissions, verifies capability profiles, and calculates policy hashes.
4. **Interactive review:** The Worker returns the safe `proposal.diff` patch, which the CI pipeline automatically posts as a structured markdown comment on the Pull Request, waiting for a team maintainer to grant approval.

---

## 2. GitHub Actions Workflow Template

Create the following file in your repository at `.github/workflows/dietcode-preflight.yml`:

```yaml
name: DietCode Bounded Preflight

on:
  pull_request:
    types: [opened, synchronize, reopened]

permissions:
  pull-requests: write
  contents: read

jobs:
  preflight-audit:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout Code
        uses: actions/checkout@v4

      - name: Trigger DietCode Preflight Session
        id: preflight
        run: |
          RESPONSE=$(curl -s -X POST "https://dreambees-alchemist.web.app/api/sessions" \
            -H "Content-Type: application/json" \
            -d '{
              "repoName": "${{ github.repository }}",
              "framework": "React/Vite",
              "profileName": "frontend-polisher",
              "instruction": "Audit PR modifications for style guides and security policy bounds."
            }')
          
          SESSION_ID=$(echo $RESPONSE | jq -r '.sessionId')
          echo "session_id=$SESSION_ID" >> $GITHUB_OUTPUT
          echo "Session enqueued with ID: $SESSION_ID"

      - name: Poll for Diff Proposal
        id: poll_proposal
        run: |
          for i in {1..30}; do
            STATUS_RESP=$(curl -s "https://dreambees-alchemist.web.app/api/sessions/${{ steps.preflight.outputs.session_id }}")
            STATUS=$(echo $STATUS_RESP | jq -r '.status')
            
            if [ "$STATUS" = "proposed" ]; then
              echo "Proposal ready!"
              # Download generated proposal diff from the public API endpoint
              DIFF_CONTENT=$(curl -s "https://dreambees-alchemist.web.app/api/sessions/${{ steps.preflight.outputs.session_id }}/diff")
              echo "diff<<EOF" >> $GITHUB_OUTPUT
              echo "$DIFF_CONTENT" >> $GITHUB_OUTPUT
              echo "EOF" >> $GITHUB_OUTPUT
              exit 0
            elif [ "$STATUS" = "violation" ] || [ "$STATUS" = "reverted" ]; then
              echo "Audit flagged a policy violation."
              exit 1
            fi
            
            echo "Waiting for preflight container... (Attempt $i)"
            sleep 10
          done
          echo "Timeout waiting for preflight."
          exit 1

      - name: Post Proposal Review Comment
        uses: actions/github-script@v7
        with:
          script: |
            const diff = `${{ steps.poll_proposal.outputs.diff }}`;
            const sessionId = "${{ steps.preflight.outputs.session_id }}";
            const commentBody = `### 🪐 DietCode Bounded Preflight Audit Complete

            An isolated sandboxed worker has verified the safety and constraints of the proposed mutations.

            #### 🔍 Proposed Patch Diff:
            \`\`\`diff
            ${diff}
            \`\`\`

            #### 🚀 Next Steps:
            * To apply and merge these changes, click [Approve & Apply](https://dreambees-alchemist.web.app/sessions/${sessionId}) inside the DietCode Control Console.
            * To discard and revert the session, click [Reject Proposal](https://dreambees-alchemist.web.app/sessions/${sessionId}).`;

            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body: commentBody
            });
```
