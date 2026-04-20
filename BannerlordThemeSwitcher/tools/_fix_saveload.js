const fs = require('fs');
const f = 'D:/Work/Sources/github/bannerlord/BannerlordThemeSwitcher/Behaviors/ThemeKingdomBehavior.cs';
let c = fs.readFileSync(f, 'utf8');

// 1. Add OnGameLoadedEvent registration
const regOld = [
    '            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);',
    '            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);',
    '            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);'
].join('\n');

const regNew = [
    '            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);',
    '            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);',
    '            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);',
    '            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);'
].join('\n');

if (c.includes(regOld)) {
    c = c.replace(regOld, regNew);
    console.log('Added OnGameLoadedEvent registration');
} else {
    console.error('Could not find RegisterEvents block');
    process.exit(1);
}

// 2. Add OnGameLoaded handler after OnNewGameCreated
const newGameOld = [
    '        private void OnNewGameCreated(CampaignGameStarter starter)',
    '        {',
    '            Debug.Print("[ThemeSwitcher] New game created");',
    '            ApplyThemeForKingdom(null);',
    '        }'
].join('\n');

const newGameNew = [
    '        private void OnNewGameCreated(CampaignGameStarter starter)',
    '        {',
    '            Debug.Print("[ThemeSwitcher] New game created");',
    '            ApplyThemeForKingdom(null);',
    '        }',
    '',
    '        private void OnGameLoaded(CampaignGameStarter starter)',
    '        {',
    '            Debug.Print("[ThemeSwitcher] Save game loaded - checking kingdom/culture");',
    '            CheckCurrentKingdom();',
    '        }'
].join('\n');

if (c.includes(newGameOld)) {
    c = c.replace(newGameOld, newGameNew);
    console.log('Added OnGameLoaded handler');
} else {
    console.error('Could not find OnNewGameCreated method');
    process.exit(1);
}

// 3. Modify CheckCurrentKingdom to fall back to culture
const checkOld = [
    '                var kingdom = Clan.PlayerClan.Kingdom;',
    '                var kingdomId = kingdom?.StringId;',
    '                ',
    '                Debug.Print($"[ThemeSwitcher] Current kingdom: {kingdomId ?? \\"none\\"}");',
    '                ApplyThemeForKingdom(kingdomId);'
].join('\n');

const checkNew = [
    '                var kingdom = Clan.PlayerClan.Kingdom;',
    '                var kingdomId = kingdom?.StringId;',
    '                ',
    '                // Fallback: if player has no kingdom, use their culture ID',
    '                // (culture IDs match kingdom IDs: "sturgia", "empire", etc.)',
    '                if (string.IsNullOrEmpty(kingdomId))',
    '                {',
    '                    var cultureId = Clan.PlayerClan.Culture?.StringId;',
    '                    Debug.Print($"[ThemeSwitcher] No kingdom, using culture: {cultureId ?? \\"none\\"}");',
    '                    kingdomId = cultureId;',
    '                }',
    '                ',
    '                Debug.Print($"[ThemeSwitcher] Current kingdom: {kingdomId ?? \\"none\\"}");',
    '                ApplyThemeForKingdom(kingdomId);'
].join('\n');

if (c.includes(checkOld)) {
    c = c.replace(checkOld, checkNew);
    console.log('Added culture fallback to CheckCurrentKingdom');
} else {
    console.error('Could not find CheckCurrentKingdom body');
    process.exit(1);
}

fs.writeFileSync(f, c, 'utf8');
console.log('All edits applied successfully');
