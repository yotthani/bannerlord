const fs = require('fs');
let c = fs.readFileSync('Patches/BrushModifier.cs', 'utf8');

// 1. Replace ShouldSkipBrush to exclude battle/combat UI
const oldSkip = `return name.Contains("debug") || name.Contains("empty") || name.Contains("invisible") ||
                   name.Contains("transparent") || name.Contains("clear") ||
                   (name.Contains("icon") && !name.Contains("background") && !name.Contains("frame")) ||
                   (name.Contains("sprite") && !name.Contains("background")) ||
                   name.Contains("crest") || name.Contains("sigil") || name.Contains("emblem");`;

const newSkip = `// Skip debug/invisible brushes
            if (name.Contains("debug") || name.Contains("empty") || name.Contains("invisible") ||
                name.Contains("transparent") || name.Contains("clear"))
                return true;

            // Skip icon/sprite brushes (except backgrounds/frames)
            if ((name.Contains("icon") && !name.Contains("background") && !name.Contains("frame")) ||
                (name.Contains("sprite") && !name.Contains("background")) ||
                name.Contains("crest") || name.Contains("sigil") || name.Contains("emblem"))
                return true;

            // Skip battle/combat UI — health bars, troop cards, order panels etc.
            if (name.Contains("health") || name.Contains("hitpoint") || name.Contains("hp") ||
                name.Contains("shield") || name.Contains("armor") ||
                name.Contains("troop") || name.Contains("soldier") || name.Contains("agent") ||
                name.Contains("combat") || name.Contains("battle") || name.Contains("mission") ||
                name.Contains("order") || name.Contains("formation") ||
                name.Contains("morale") || name.Contains("stamina") || name.Contains("courage"))
                return true;

            return false;`;

if (!c.includes('name.Contains("crest") || name.Contains("sigil") || name.Contains("emblem");')) {
    console.log('ERROR: ShouldSkipBrush block not found');
    process.exit(1);
}
c = c.replace(oldSkip, newSkip);
console.log('1. ShouldSkipBrush expanded with battle/combat exclusions');

// 2. Remove health/morale/experience from ApplyComponentPattern progress section
const oldProgress = `if (name.Contains("health") || name.Contains("hp"))
                    data.LayerColor = c.Health;
                else if (name.Contains("experience") || name.Contains("xp"))
                    data.LayerColor = c.Experience;
                else if (name.Contains("morale"))
                    data.LayerColor = c.Morale;
                else if (name.Contains("fill"))`;

const newProgress = `if (name.Contains("fill"))`;

if (!c.includes(oldProgress)) {
    console.log('WARNING: Progress block not found (may already be fixed)');
} else {
    c = c.replace(oldProgress, newProgress);
    console.log('2. Removed health/morale/experience from progress bar pattern');
}

fs.writeFileSync('Patches/BrushModifier.cs', c, 'utf8');
console.log('Done!');
