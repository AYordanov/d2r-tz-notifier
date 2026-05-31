# Terror Zone Notifier

A .NET 10 (isolated worker) Azure Function that emails you each morning at **09:00 UK time**
listing when **Mephisto (Durance of Hate)** is terrorized today, using the d2emu single-player
terror-zone calendar as the source and SendGrid to send the mail.

Designed for **D2R single-player, Reign of the Warlock** (30-minute rotation).

## How it works

1. `TerrorZoneNotifier` fires daily at 09:00 (interpreted in `WEBSITE_TIME_ZONE`).
2. It downloads the static feed `https://d2emu.com/data/tz-2023-localized.json`.
3. It converts every UTC slot into your local zone, keeps the ones whose English name matches any
   configured `ZONE_TARGETS` keyword and start **today**, and merges back-to-back 30-min slots of
   the same zone into single windows.
4. Outcomes:
   - **A tracked zone today** → email with each boss's window(s), immunities, boss-pack count, super uniques.
   - **No tracked zone today** → silent by default; set `SEND_WHEN_NONE=true` for a "nothing today" note.
   - **Today missing from the feed** (stale file / horizon exhausted) → always emails a gap alert.

There is also a `RunNow` HTTP trigger so you can test on demand without waiting for 09:00.

## Configuration (app settings)

| Setting | Required | Default | Notes |
|---|---|---|---|
| `SENDGRID_API_KEY` | yes | — | SendGrid API key. |
| `MEPHISTO_FROM_EMAIL` | yes | — | Must be a **verified sender** in SendGrid. |
| `MEPHISTO_TO_EMAIL` | yes | — | Where the mail goes (your inbox). |
| `SEND_WHEN_NONE` | no | `false` | Email on days when no tracked zone is up. |
| `ZONE_TARGETS` | no | `[{"keyword":"Durance","boss":"Mephisto"}]` | JSON array of zones to track. Each item has a `keyword` (substring matched against the English zone name) and a `boss` (label shown in the email). Add entries to track more, e.g. `[{"keyword":"Durance","boss":"Mephisto"},{"keyword":"Catacombs","boss":"Andariel"}]`. |
| `TIME_ZONE_ID` | no | `Europe/London` | IANA id; resolved cross-platform on .NET 6+. |
| `WEBSITE_TIME_ZONE` | no | `GMT Standard Time` | Controls how the 09:00 CRON is interpreted in Azure. See below. |

## The 9am / timezone bit (read this)

The CRON is `0 0 9 * * *` — 09:00. Azure evaluates timer CRONs in **UTC** unless you set
`WEBSITE_TIME_ZONE`. To keep it at 9am UK year-round (auto-handling the BST/GMT switch):

- **Windows-hosted Function App:** `WEBSITE_TIME_ZONE = GMT Standard Time` (the Windows id for UK time).
- **Linux-hosted Function App:** `WEBSITE_TIME_ZONE = Europe/London`.

If you skip this, the job runs at 09:00 **UTC**, i.e. 10am during BST and 9am during GMT.
`TIME_ZONE_ID` is separate — it only affects how slot times are computed/displayed, and defaults
to UK time already.

## Run locally

Prereqs: .NET 10 SDK, Azure Functions Core Tools v4, and Azurite (for the timer's storage lock).

```bash
dotnet restore
# start Azurite in another terminal (e.g. `azurite` or the VS Code extension)
func start
```

Then trigger a run immediately (don't wait for 9am):

```bash
curl http://localhost:7071/api/RunNow
```

Fill in `local.settings.json` first (it's gitignored). To exercise the email path locally,
set real SendGrid values; otherwise the send will throw and you'll just see it in the logs.

## Deploy to Azure

1. Create a **Function App**, runtime stack **.NET 10 Isolated**, plan **Consumption** is fine.
2. Set the app settings from the table above (Portal → Configuration, or `az functionapp config appsettings set`),
   including `WEBSITE_TIME_ZONE` and a real `AzureWebJobsStorage` connection string.
3. Publish:

```bash
func azure functionapp publish <your-function-app-name>
```

4. Test in the cloud by calling the `RunNow` URL (Portal → Functions → RunNow → Get Function Url,
   which includes the function key).

## Notes / caveats

- **Feed dependency.** This consumes d2emu's precomputed SP calendar; it does not reimplement the
  rotation algorithm. The gap-alert path tells you if the feed stops covering today.
- **Personal use.** You're emailing yourself, not redistributing the data.
- **Package versions** in the `.csproj` are current as of writing — `dotnet restore` and bump if
  the Worker/SDK templates have moved on.
- **Slot length** is hard-coded to 30 minutes in `TerrorZoneScheduleService` (Reign of the Warlock).
  Change `SlotLength` to 60 min if you ever run vanilla.
