# JoyZoning Agent Contract

Before editing:
1. Run `joyzoning agent-context --json`
2. Run `joyzoning status --json`
3. Read only files listed in `importantFiles`

After editing:
1. Run `joyzoning verify`
2. Run `joyzoning snapshot --json`
3. Report changed files, verification result, and remaining risks

Do not scan the whole repo unless `joyzoning doctor --json` says the manifest is stale.
