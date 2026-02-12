# Bannerlord Modding Toolkit (BMT)

## Renamed from "Combat Analyzer" - now covers:
- Combat data capture
- Exception/error capture  
- Performance monitoring
- Balance analysis
- UI design tools

---

## Data Sharing - Zero Backend Solutions

### Option 1: GitHub Gist (Recommended)
**Cost:** Free  
**Setup:** Users need GitHub account + personal access token

```
User's Game → Capture Mod → JSON file
                              ↓
                         Auto-upload to Gist (anonymous or user's account)
                              ↓
                         Returns Gist URL
                              ↓
Admin/Analyst ← Paste URL → Analysis Tool fetches JSON
```

**Pros:**
- Free, unlimited
- Version history built-in
- Can be public or secret
- API is simple

**Implementation:**
```csharp
public static async Task<string> UploadToGist(string json, string filename)
{
    var gist = new {
        description = "BMT Capture Data",
        public = false,
        files = new Dictionary<string, object> {
            { filename, new { content = json } }
        }
    };
    
    // Anonymous gist (no auth needed, but can't edit later)
    var response = await http.PostAsync(
        "https://api.github.com/gists", 
        new StringContent(JsonSerializer.Serialize(gist)));
    
    var result = await response.Content.ReadFromJsonAsync<GistResponse>();
    return result.html_url; // Share this URL
}
```

---

### Option 2: Discord Webhook
**Cost:** Free  
**Setup:** Create webhook in your Discord server

```
User's Game → Capture Mod → JSON file
                              ↓
                         POST to Discord webhook
                              ↓
                         Appears as message with attachment
                              ↓
Admin monitors Discord channel
```

**Pros:**
- Zero user setup (webhook URL in mod settings)
- Community already on Discord
- Instant notifications

**Implementation:**
```csharp
public static async Task SendToDiscord(string json, string webhookUrl)
{
    using var content = new MultipartFormDataContent();
    content.Add(new StringContent(json), "file", "capture_data.json");
    content.Add(new StringContent($"New capture: {DateTime.UtcNow}"), "content");
    
    await http.PostAsync(webhookUrl, content);
}
```

---

### Option 3: Pastebin-style
**Cost:** Free  
**Options:** Pastebin, Hastebin, dpaste, ix.io

```csharp
// ix.io - no API key needed
public static async Task<string> UploadToPaste(string json)
{
    var content = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("f:1", json)
    });
    
    var response = await http.PostAsync("http://ix.io", content);
    return await response.Content.ReadAsStringAsync(); // Returns URL
}
```

---

### Option 4: Google Drive (User's own)
**Cost:** Free  
**Setup:** User authorizes once

- Data stays in user's own Drive
- They share folder with admin
- Or export link manually

---

### Option 5: Peer-to-Peer File Sharing
**Cost:** Free  
**Method:** Generate magnet link / IPFS hash

For large datasets, could use:
- IPFS (decentralized)
- WebTorrent (browser-compatible torrents)

---

## Recommended Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     User's Game (Mod)                           │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ CaptureManager                                             │  │
│  │  - Combat events                                           │  │
│  │  - Exceptions/errors                                       │  │
│  │  - Performance metrics                                     │  │
│  │                                                            │  │
│  │ Export Options (MCM configurable):                         │  │
│  │  □ Save to local folder (always)                          │  │
│  │  □ Upload to GitHub Gist                                   │  │
│  │  □ Send to Discord webhook                                 │  │
│  │  □ Copy share link to clipboard                            │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
   GitHub Gist          Discord Channel      Local Folder
   (secret URL)         (webhook posts)      (manual share)
         │                    │                    │
         └────────────────────┼────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Admin's Analysis Tool                         │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ Data Import:                                               │  │
│  │  - Paste Gist/paste URL → auto-fetch                      │  │
│  │  - Drag-drop JSON files                                    │  │
│  │  - Watch Discord channel (bot optional)                    │  │
│  │  - Bulk import folder                                      │  │
│  │                                                            │  │
│  │ Analysis:                                                  │  │
│  │  - Aggregate across all submissions                        │  │
│  │  - Pattern detection                                       │  │
│  │  - Claude AI insights                                      │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Privacy & Consent

```csharp
// First-time prompt
public static void ShowDataSharingConsent()
{
    InformationManager.ShowInquiry(new InquiryData(
        "Data Sharing",
        "BMT can share anonymous gameplay data to help improve mod balance.\n\n" +
        "Data includes: combat stats, errors, performance\n" +
        "Does NOT include: player name, save data, personal info\n\n" +
        "You can change this anytime in MCM settings.",
        true, true, "Allow", "Decline",
        () => Settings.Instance.AllowDataSharing = true,
        () => Settings.Instance.AllowDataSharing = false
    ));
}
```

---

## Data Minimization

What we capture (anonymous):
- Combat: damage, weapon, troop type, armor values
- Errors: exception type, stack trace, game state
- Performance: operation name, duration

What we DON'T capture:
- Player name / Steam ID
- Save file data
- System specs (unless opt-in)
- Chat / text input
- File paths (sanitized)

---

## MCM Settings for Sharing

```csharp
[SettingPropertyGroup("Data Sharing")]
[SettingPropertyBool("Enable Anonymous Sharing", HintText = "Share data to help mod development")]
public bool AllowDataSharing { get; set; } = false;

[SettingPropertyGroup("Data Sharing")]
[SettingPropertyDropdown("Share Method", HintText = "How to share captured data")]
public Dropdown<string> ShareMethod { get; set; } = new(new[] {
    "Manual (local files only)",
    "GitHub Gist (secret link)",
    "Discord Webhook",
    "Clipboard Link"
}, 0);

[SettingPropertyGroup("Data Sharing")]
[SettingPropertyText("Discord Webhook URL", HintText = "Paste webhook URL from your Discord server")]
public string DiscordWebhook { get; set; } = "";

[SettingPropertyGroup("Data Sharing")]
[SettingPropertyBool("Auto-share on Session End", HintText = "Automatically upload when battle ends")]
public bool AutoShare { get; set; } = false;
```

---

## Suggested New Name

**"Bannerlord Modding Toolkit" (BMT)** or sub-options:
- **BMT Capture** - the in-game mod
- **BMT Analyzer** - the desktop analysis tool
- **BMT Designer** - the UI design tool

Or simpler:
- **ModForge** - toolkit for mod development
- **BL DevTools** - Bannerlord Developer Tools
- **Calradia Toolkit** - thematic name
