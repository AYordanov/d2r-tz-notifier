# Terror Zone Notifier

A .NET 10 (isolated worker) Azure Function that emails you each morning at **08:00 UK time**
listing when **Mephisto (Durance of Hate)** is terrorized today, using the d2emu single-player
terror-zone calendar as the source and SendGrid to send the mail.

Designed for **D2R single-player, Reign of the Warlock** (30-minute rotation).

## How it works

1. `TerrorZoneNotifier` fires daily at 08:00 (interpreted in `WEBSITE_TIME_ZONE`).
2. It downloads the static feed `https://d2emu.com/data/tz-2023-localized.json`.
3. It converts every UTC slot into your local zone, keeps the ones whose English name matches any
   zone in `zone-targets.json` and start **today**, merges back-to-back 30-min slots of the same
   zone into single windows, and drops any window that's already ended (so the 8am run won't report
   last night's zones — ongoing windows are kept).
4. Outcomes:
   - **A tracked zone today** → email with each boss's window(s), immunities, boss-pack count, super uniques.
   - **No tracked zone today** → silent by default; set `SEND_WHEN_NONE=true` for a "nothing today" note.
   - **Today missing from the feed** (stale file / horizon exhausted) → always emails a gap alert.

There is also a `RunNow` HTTP trigger so you can test on demand without waiting for 08:00.

## Configuration (app settings)

| Setting | Required | Default | Notes |
|---|---|---|---|
| `SENDGRID_API_KEY` | yes | — | SendGrid API key. |
| `SENDGRID_TEMPLATE_ID` | yes | — | Dynamic template id (`d-…`). Create it from [`sendgrid-template.html`](Templates/sendgrid-template.html). |
| `MEPHISTO_FROM_EMAIL` | yes | — | Must be a **verified sender** in SendGrid. |
| `MEPHISTO_TO_EMAIL` | yes | — | Where the mail goes (your inbox). |
| `SEND_WHEN_NONE` | no | `false` | Email on days when no tracked zone is up. |
| `TIME_ZONE_ID` | no | `Europe/London` | IANA id; resolved cross-platform on .NET 6+. |
| `WEBSITE_TIME_ZONE` | no | `GMT Standard Time` | Controls how the 08:00 CRON is interpreted in Azure. See below. |

### Tracked zones (`zone-targets.json`)

Which zones to watch lives in [`zone-targets.json`](Schema/zone-targets.json), bundled with the app (not an
app setting). Each entry has a `keyword` (substring matched against the feed's English zone name) and
a `boss` (label shown in the email):

```json
[
  { "keyword": "Durance", "boss": "Mephisto" },
  { "keyword": "Catacombs", "boss": "Andariel" }
]
```

Add or remove entries to change what's tracked, then redeploy. If the file is missing it falls back to
tracking Durance (Mephisto).

### Email template (SendGrid dynamic template)

The email's look lives in a SendGrid **dynamic template**, not in code — the app only sends data
(`EmailData`). To create it:

1. In SendGrid: **Email API → Dynamic Templates → Create a Dynamic Template → Add Version → Code Editor**.
2. Paste the contents of [`sendgrid-template.html`](Templates/sendgrid-template.html) into the editor, and set the
   version **Subject** to `{{subject}}`.
3. Save, copy the template id (`d-…`), and set it as the `SENDGRID_TEMPLATE_ID` app setting.

To preview the design in SendGrid's editor, paste this into its *Test Data* panel:

```json
{ "subject": "Terror zones today — Mephisto 10:00", "dateLabel": "Sunday 31 May", "hasWindows": true, "isFeedGap": false,
  "groups": [ { "boss": "Mephisto", "zone": "Durance of Hate", "windows": [ { "time": "10:00–11:00 BST" } ], "immunities": "Fire, Lightning", "packs": "5–8", "uniques": "—" } ] }
```

The template renders three states from one payload — windows today, nothing today, and a feed-gap
alert — via Handlebars (`hasWindows` / `isFeedGap`). It's a clean light email (dark ink on white) with a
blood-red header banner, blood-red `Cinzel` headings, and a gold-brown frame. The light palette is
deliberate: Gmail's mobile dark mode recolors near-black backgrounds, so a light design stays legible
everywhere. Custom fonts only render in clients that allow them (e.g. Apple Mail); Gmail falls back to
serif but keeps the colours.

## The 8am / timezone bit (read this)

The CRON is `0 0 8 * * *` — 08:00. Azure evaluates timer CRONs in **UTC** unless you set
`WEBSITE_TIME_ZONE`. To keep it at 8am UK year-round (auto-handling the BST/GMT switch):

- **Windows-hosted Function App:** `WEBSITE_TIME_ZONE = GMT Standard Time` (the Windows id for UK time).
- **Linux-hosted Function App:** `WEBSITE_TIME_ZONE = Europe/London`.

If you skip this, the job runs at 08:00 **UTC**, i.e. 9am during BST and 8am during GMT.
`TIME_ZONE_ID` is separate — it only affects how slot times are computed/displayed, and defaults
to UK time already.

## Run locally

Prereqs: .NET 10 SDK, Azure Functions Core Tools v4, and Azurite (for the timer's storage lock).

```bash
dotnet restore
# start Azurite in another terminal (e.g. `azurite` or the VS Code extension)
func start
```

Then trigger a run immediately (don't wait for 8am):

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
