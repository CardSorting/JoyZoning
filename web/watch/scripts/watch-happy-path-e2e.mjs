/**
 * Manual happy-path driver for Watch UI against a running control plane + next dev.
 * Usage: node scripts/watch-happy-path-e2e.mjs
 */
import { chromium } from "playwright";

const BASE = process.env.WATCH_URL ?? "http://localhost:3000";
const TASK_ID = process.env.TASK_ID ?? "f6d456dc-707b-427c-b130-6459529db5cc";
const results = [];

function record(step, ok, detail = "") {
  results.push({ step, ok, detail });
  const mark = ok ? "PASS" : "FAIL";
  console.log(`[${mark}] ${step}${detail ? ` — ${detail}` : ""}`);
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();

  try {
    await page.goto(`${BASE}/?taskId=${TASK_ID}`, { waitUntil: "networkidle", timeout: 60_000 });

    // 1. Task appears live
    await page.waitForSelector('[data-testid="operator-console"]', { timeout: 45_000 });
    const hasLive =
      (await page.locator("[data-joyzoning-mode]").count()) > 0 ||
      (await page.getByText(/Ready for review|Paused|Building|Watching/i).count()) > 0;
    record("1. task appears live", hasLive);

    const workerList = await page.getByTestId("worker-list").count();
    const workerCard = await page.locator("text=TinyQuest Campfire").count();
    record("2. worker appears", workerList > 0 && workerCard > 0, `cards=${workerCard}`);

    const recommended = await page.getByTestId("mode-emphasis-bar").textContent();
    record(
      "3. mode recommendation appears",
      /recommended/i.test(recommended ?? ""),
      recommended?.slice(0, 80) ?? "",
    );

    const emphasis = page.getByTestId("mode-emphasis-bar");
    await emphasis.getByRole("tab", { name: /Planning/i }).click();
    await page.waitForTimeout(300);
    const mergeStillVisible = (await page.getByTestId("merge-queue-panel").count()) > 0;
    record("4. emphasis scroll (planning) keeps console visible", mergeStillVisible);

    await emphasis.getByRole("tab", { name: /Planning/i }).click();
    const pinnedMode = await page.evaluate(() =>
      sessionStorage.getItem("jz-watch-mode-emphasis"),
    );
    const pinnedFlag = await page.evaluate(() =>
      sessionStorage.getItem("jz-watch-mode-emphasis-pinned"),
    );
    record(
      "5. emphasis pin holds",
      pinnedMode === "planning" && pinnedFlag === "1",
      `stored=${pinnedMode}`,
    );

    const openWs = page.getByRole("button", { name: /Open workspace/i }).first();
    if ((await openWs.count()) > 0) {
      await openWs.click();
      await page.waitForTimeout(400);
      record("6. open workspace", true, "open-path");
    } else {
      record("6. open workspace", false, "no Open workspace button");
    }

    await page.waitForSelector("text=Ready to merge", { timeout: 15_000 });
    const approveBtn = page.getByRole("button", { name: /^Approve$/i }).first();
    const hasApprove = (await approveBtn.count()) > 0;
    if (hasApprove) {
      await approveBtn.click();
      await page.waitForSelector("#worker-decision-title", { timeout: 5000 });
      const title = await page.locator("#worker-decision-title").textContent();
      record("7. review action works", /approve/i.test(title ?? ""), title ?? "");
      await page.getByLabel("Close").click();
    } else {
      const reviewHtml = await page.locator('[data-joyzoning-mode="review"]').innerText().catch(() => "");
      record("7. review action works", false, `no Approve in queue (${reviewHtml.slice(0, 120)}…)`);
    }

    // 8. Depart clears session/mode URL
    await page.getByRole("button", { name: "Leave" }).click();
    await page.waitForTimeout(400);
    const afterUrl = page.url();
    const modeGone = !afterUrl.includes("mode=");
    const taskGone = !afterUrl.includes("taskId=");
    const storageClear = await page.evaluate(
      () =>
        !sessionStorage.getItem("jz-watch-mode") &&
        !sessionStorage.getItem("jz-watch-mode-user-pinned"),
    );
    const entryBack = (await page.getByText("Operator console").count()) > 0;
    record(
      "8. depart clears session/mode URL",
      modeGone && taskGone && storageClear && entryBack,
      `url=${afterUrl}`,
    );
  } catch (e) {
    record("fatal", false, e instanceof Error ? e.message : String(e));
  } finally {
    await browser.close();
  }

  const failed = results.filter((r) => !r.ok);
  console.log("\n--- summary ---");
  console.log(JSON.stringify(results, null, 2));
  process.exit(failed.length ? 1 : 0);
}

main();
