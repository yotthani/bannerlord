/**
 * Generate LOTR-themed test sprite themes for BannerlordThemeSwitcher.
 *
 * Creates colored placeholder PNGs, ThemeManifest.xml, and SpriteConfig.xml
 * for each LOTR culture mapped from vanilla Bannerlord factions.
 *
 * Usage: node tools/generate_test_themes.mjs
 */

import { mkdirSync, writeFileSync, existsSync } from 'fs';
import { join } from 'path';
import { deflateSync } from 'zlib';

// =========================================================================
// LOTR Culture Definitions
// =========================================================================

const THEMES = [
  {
    id: 'Gondor',
    name: 'Kingdom of Gondor',
    description: 'The White City — Tower of Guard. Silver and blue.',
    baseCulture: 'empire',
    boundKingdom: 'empire',
    colors: {
      primary: [220, 220, 235],     // Silver-white
      secondary: [70, 100, 160],    // Steel blue
      accent: [180, 180, 200],      // Pale silver
      border: [140, 160, 200],      // Blue-steel
      dark: [30, 35, 50],           // Dark navy
      highlight: [230, 240, 255],   // Bright silver
    },
    colorScheme: {
      Primary: '#DCDCEB', Secondary: '#4664A0', Tertiary: '#B4B4C8',
      Text: '#E6F0FFEE', TextMuted: '#A0B0C8', TextHighlight: '#E6F0FF',
      TextTitle: '#DCDCEB', TextOnPrimary: '#1E2332', TextDisabled: '#607080',
      Background: '#1E233299', BackgroundDark: '#141822BB', BackgroundLight: '#2A3040AA',
      BackgroundAccent: '#4664A020', BackgroundHover: '#4664A040', BackgroundSelected: '#4664A060',
      Border: '#8CA0C8AA', BorderMuted: '#8CA0C855', BorderHighlight: '#DCDCEBFF',
      BorderSecondary: '#4664A0AA',
      ButtonBackground: '#4664A025', ButtonHover: '#4664A050',
      ButtonPressed: '#4664A080', ButtonDisabled: '#40506044', ButtonBorder: '#8CA0C8CC',
      Success: '#32CD32', Warning: '#FFA500', Error: '#DC143C', Info: '#4169E1',
      Gold: '#C0C0C0', Experience: '#6A8CCE', Health: '#DC143C', Morale: '#32CD32',
      Shadow: '#14182288', Glow: '#4664A066',
    },
  },
  {
    id: 'Rohan',
    name: 'Kingdom of Rohan',
    description: 'Riders of the Mark — Green and gold, wind and horse.',
    baseCulture: 'vlandia',
    boundKingdom: 'vlandia',
    colors: {
      primary: [60, 120, 50],       // Forest green
      secondary: [200, 170, 50],    // Gold
      accent: [90, 150, 70],        // Lighter green
      border: [200, 170, 50],       // Gold border
      dark: [25, 35, 20],           // Dark green
      highlight: [240, 210, 80],    // Bright gold
    },
    colorScheme: {
      Primary: '#3C7832', Secondary: '#C8AA32', Tertiary: '#5A9646',
      Text: '#F0FFE8EE', TextMuted: '#A0C890', TextHighlight: '#F0DC50',
      TextTitle: '#C8AA32', TextOnPrimary: '#1A2314', TextDisabled: '#607050',
      Background: '#1A231499', BackgroundDark: '#0F160BBB', BackgroundLight: '#2A3820AA',
      BackgroundAccent: '#3C783220', BackgroundHover: '#3C783240', BackgroundSelected: '#3C783260',
      Border: '#C8AA32AA', BorderMuted: '#C8AA3255', BorderHighlight: '#F0DC50FF',
      BorderSecondary: '#3C7832AA',
      ButtonBackground: '#3C783225', ButtonHover: '#3C783250',
      ButtonPressed: '#3C783280', ButtonDisabled: '#40503844', ButtonBorder: '#C8AA32CC',
      Success: '#32CD32', Warning: '#FFA500', Error: '#DC143C', Info: '#4169E1',
      Gold: '#C8AA32', Experience: '#6B8E23', Health: '#DC143C', Morale: '#32CD32',
      Shadow: '#0F160B88', Glow: '#C8AA3266',
    },
  },
  {
    id: 'Erebor',
    name: 'Kingdom Under the Mountain',
    description: 'Dwarven halls of stone and gold. Forged in fire.',
    baseCulture: 'sturgia',
    boundKingdom: 'sturgia',
    colors: {
      primary: [140, 100, 50],      // Bronze/gold
      secondary: [180, 50, 30],     // Forge red
      accent: [100, 90, 80],        // Stone gray
      border: [200, 160, 60],       // Gold
      dark: [35, 25, 20],           // Dark stone
      highlight: [220, 180, 80],    // Bright gold
    },
    colorScheme: {
      Primary: '#8C6432', Secondary: '#B4321E', Tertiary: '#645A50',
      Text: '#FFF0DCEE', TextMuted: '#C8B090', TextHighlight: '#DCB450',
      TextTitle: '#C8A03C', TextOnPrimary: '#231A14', TextDisabled: '#706050',
      Background: '#231A1499', BackgroundDark: '#160F0ABB', BackgroundLight: '#3A2A1EAA',
      BackgroundAccent: '#8C643220', BackgroundHover: '#8C643240', BackgroundSelected: '#8C643260',
      Border: '#C8A03CAA', BorderMuted: '#C8A03C55', BorderHighlight: '#DCBA50FF',
      BorderSecondary: '#B4321EAA',
      ButtonBackground: '#8C643225', ButtonHover: '#8C643250',
      ButtonPressed: '#8C643280', ButtonDisabled: '#50403044', ButtonBorder: '#C8A03CCC',
      Success: '#32CD32', Warning: '#FFA500', Error: '#B4321E', Info: '#4169E1',
      Gold: '#DCBA50', Experience: '#C87832', Health: '#B4321E', Morale: '#32CD32',
      Shadow: '#160F0A88', Glow: '#B4321E66',
    },
  },
  {
    id: 'Lothlorien',
    name: 'Realm of Lothlórien',
    description: 'The Golden Wood — ancient elven grace, starlight and mallorn.',
    baseCulture: 'battania',
    boundKingdom: 'battania',
    colors: {
      primary: [200, 190, 100],     // Golden leaf
      secondary: [80, 140, 80],     // Forest green
      accent: [220, 210, 150],      // Light gold
      border: [180, 170, 80],       // Aged gold
      dark: [20, 25, 15],           // Deep forest
      highlight: [240, 230, 170],   // Starlight gold
    },
    colorScheme: {
      Primary: '#C8BE64', Secondary: '#508C50', Tertiary: '#DCD296',
      Text: '#FFFFF0EE', TextMuted: '#C8C8A0', TextHighlight: '#F0E6AA',
      TextTitle: '#C8BE64', TextOnPrimary: '#14190F', TextDisabled: '#707060',
      Background: '#14190F99', BackgroundDark: '#0A0F08BB', BackgroundLight: '#2A321EAA',
      BackgroundAccent: '#C8BE6420', BackgroundHover: '#C8BE6440', BackgroundSelected: '#C8BE6460',
      Border: '#B4AA50AA', BorderMuted: '#B4AA5055', BorderHighlight: '#F0E6AAFF',
      BorderSecondary: '#508C50AA',
      ButtonBackground: '#508C5025', ButtonHover: '#508C5050',
      ButtonPressed: '#508C5080', ButtonDisabled: '#50504044', ButtonBorder: '#B4AA50CC',
      Success: '#32CD32', Warning: '#FFA500', Error: '#DC143C', Info: '#4169E1',
      Gold: '#F0E6AA', Experience: '#C8BE64', Health: '#DC143C', Morale: '#50C850',
      Shadow: '#0A0F0888', Glow: '#C8BE6466',
    },
  },
  {
    id: 'Harad',
    name: 'Dominion of Harad',
    description: 'South-kingdom of crimson and gold. Desert sun and oliphaunts.',
    baseCulture: 'aserai',
    boundKingdom: 'aserai',
    colors: {
      primary: [180, 40, 30],       // Crimson
      secondary: [200, 160, 50],    // Desert gold
      accent: [220, 80, 50],        // Warm red
      border: [200, 160, 50],       // Gold
      dark: [40, 15, 10],           // Dark crimson
      highlight: [240, 200, 80],    // Bright gold
    },
    colorScheme: {
      Primary: '#B4281E', Secondary: '#C8A032', Tertiary: '#DC5032',
      Text: '#FFF0E0EE', TextMuted: '#C8A080', TextHighlight: '#F0C850',
      TextTitle: '#C8A032', TextOnPrimary: '#280F0A', TextDisabled: '#705040',
      Background: '#280F0A99', BackgroundDark: '#1E0A06BB', BackgroundLight: '#3C1A10AA',
      BackgroundAccent: '#B4281E20', BackgroundHover: '#B4281E40', BackgroundSelected: '#B4281E60',
      Border: '#C8A032AA', BorderMuted: '#C8A03255', BorderHighlight: '#F0C850FF',
      BorderSecondary: '#B4281EAA',
      ButtonBackground: '#B4281E25', ButtonHover: '#B4281E50',
      ButtonPressed: '#B4281E80', ButtonDisabled: '#50302044', ButtonBorder: '#C8A032CC',
      Success: '#32CD32', Warning: '#FFA500', Error: '#B4281E', Info: '#4169E1',
      Gold: '#F0C850', Experience: '#DC5032', Health: '#B4281E', Morale: '#32CD32',
      Shadow: '#1E0A0688', Glow: '#B4281E66',
    },
  },
  {
    id: 'Rhun',
    name: 'Confederacy of Rhûn',
    description: 'Eastern steppe riders — bronze and dark iron, endless plains.',
    baseCulture: 'khuzait',
    boundKingdom: 'khuzait',
    colors: {
      primary: [160, 80, 40],       // Bronze
      secondary: [80, 60, 50],      // Dark iron
      accent: [200, 120, 60],       // Warm bronze
      border: [180, 100, 50],       // Bright bronze
      dark: [30, 20, 15],           // Deep brown
      highlight: [220, 160, 80],    // Gold-bronze
    },
    colorScheme: {
      Primary: '#A05028', Secondary: '#503C32', Tertiary: '#C87832',
      Text: '#F0E0D0EE', TextMuted: '#B49878', TextHighlight: '#DCA050',
      TextTitle: '#C87832', TextOnPrimary: '#1E140F', TextDisabled: '#706050',
      Background: '#1E140F99', BackgroundDark: '#140E0ABB', BackgroundLight: '#322218AA',
      BackgroundAccent: '#A0502820', BackgroundHover: '#A0502840', BackgroundSelected: '#A0502860',
      Border: '#B46432AA', BorderMuted: '#B4643255', BorderHighlight: '#DCA050FF',
      BorderSecondary: '#503C32AA',
      ButtonBackground: '#A0502825', ButtonHover: '#A0502850',
      ButtonPressed: '#A0502880', ButtonDisabled: '#50403044', ButtonBorder: '#B46432CC',
      Success: '#32CD32', Warning: '#FFA500', Error: '#DC143C', Info: '#4169E1',
      Gold: '#DCA050', Experience: '#C87832', Health: '#DC143C', Morale: '#32CD32',
      Shadow: '#140E0A88', Glow: '#A0502866',
    },
  },
  {
    id: 'Umbar',
    name: 'Corsairs of Umbar',
    description: 'Black sails and sea-steel. Raiders of the southern coast.',
    baseCulture: 'khuzait',
    boundKingdom: null, // Naval has no vanilla kingdom binding
    colors: {
      primary: [30, 50, 80],        // Dark navy
      secondary: [160, 40, 40],     // Blood red
      accent: [50, 70, 100],        // Steel blue
      border: [100, 110, 130],      // Tarnished silver
      dark: [10, 15, 25],           // Abyss black
      highlight: [180, 60, 60],     // Crimson
    },
    colorScheme: {
      Primary: '#1E3250', Secondary: '#A02828', Tertiary: '#324664',
      Text: '#D0E0F0EE', TextMuted: '#8090A8', TextHighlight: '#B43C3C',
      TextTitle: '#6E7E96', TextOnPrimary: '#0A0F19', TextDisabled: '#506070',
      Background: '#0A0F1999', BackgroundDark: '#060A12BB', BackgroundLight: '#1E2838AA',
      BackgroundAccent: '#1E325020', BackgroundHover: '#1E325040', BackgroundSelected: '#A0282840',
      Border: '#6E7E82AA', BorderMuted: '#6E7E8255', BorderHighlight: '#B43C3CFF',
      BorderSecondary: '#1E3250AA',
      ButtonBackground: '#1E325025', ButtonHover: '#A0282840',
      ButtonPressed: '#A0282870', ButtonDisabled: '#30405044', ButtonBorder: '#6E7E82CC',
      Success: '#32CD32', Warning: '#FFA500', Error: '#A02828', Info: '#4169E1',
      Gold: '#C0A040', Experience: '#6080B0', Health: '#A02828', Morale: '#32CD32',
      Shadow: '#060A1288', Glow: '#A0282866',
    },
  },
];

// Vanilla sprites to override with themed versions
const SPRITE_OVERRIDES = [
  { name: 'button_canvas_9', w: 64, h: 64, nine: true, type: 'button' },
  { name: 'button_frame_9', w: 64, h: 64, nine: true, type: 'frame' },
  { name: 'frame_9', w: 128, h: 128, nine: true, type: 'panel' },
  { name: 'rounded_frame_9', w: 96, h: 96, nine: true, type: 'rounded' },
  { name: 'rounded_canvas_9', w: 96, h: 96, nine: true, type: 'canvas' },
  { name: 'BlankWhiteSquare_9', w: 32, h: 32, nine: true, type: 'fill' },
  { name: 'BlankWhiteSquare', w: 16, h: 16, nine: false, type: 'flat' },
  { name: 'dialog_option_canvas_9', w: 64, h: 48, nine: true, type: 'option' },
  { name: 'scroll_button_9', w: 32, h: 32, nine: true, type: 'scroll' },
];

// =========================================================================
// PNG Generator (pure Node.js — no dependencies)
// =========================================================================

function createPNG(width, height, pixelFn) {
  // Build raw RGBA pixel data
  const rawData = Buffer.alloc(height * (1 + width * 4)); // filter byte + RGBA per pixel
  for (let y = 0; y < height; y++) {
    const rowStart = y * (1 + width * 4);
    rawData[rowStart] = 0; // filter: None
    for (let x = 0; x < width; x++) {
      const [r, g, b, a] = pixelFn(x, y, width, height);
      const offset = rowStart + 1 + x * 4;
      rawData[offset] = r;
      rawData[offset + 1] = g;
      rawData[offset + 2] = b;
      rawData[offset + 3] = a;
    }
  }

  const compressed = deflateSync(rawData);

  // PNG signature
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);

  // IHDR chunk
  const ihdr = createChunk('IHDR', (() => {
    const buf = Buffer.alloc(13);
    buf.writeUInt32BE(width, 0);
    buf.writeUInt32BE(height, 4);
    buf[8] = 8;  // bit depth
    buf[9] = 6;  // color type: RGBA
    buf[10] = 0; // compression
    buf[11] = 0; // filter
    buf[12] = 0; // interlace
    return buf;
  })());

  // IDAT chunk
  const idat = createChunk('IDAT', compressed);

  // IEND chunk
  const iend = createChunk('IEND', Buffer.alloc(0));

  return Buffer.concat([signature, ihdr, idat, iend]);
}

function createChunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);

  const typeAndData = Buffer.concat([Buffer.from(type), data]);

  // CRC32
  const crc = crc32(typeAndData);
  const crcBuf = Buffer.alloc(4);
  crcBuf.writeUInt32BE(crc >>> 0, 0);

  return Buffer.concat([length, typeAndData, crcBuf]);
}

// CRC32 lookup table
const crcTable = (() => {
  const table = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) {
      c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1);
    }
    table[n] = c;
  }
  return table;
})();

function crc32(buf) {
  let crc = 0xFFFFFFFF;
  for (let i = 0; i < buf.length; i++) {
    crc = crcTable[(crc ^ buf[i]) & 0xFF] ^ (crc >>> 8);
  }
  return (crc ^ 0xFFFFFFFF) >>> 0;
}

// =========================================================================
// Sprite Pixel Generators
// =========================================================================

function makeButtonSprite(colors) {
  return (x, y, w, h) => {
    const border = 4;
    const isEdge = x < border || x >= w - border || y < border || y >= h - border;
    if (isEdge) {
      return [...colors.border, 220];
    }
    // Gradient fill
    const t = y / h;
    const r = Math.round(colors.primary[0] * (1 - t * 0.3));
    const g = Math.round(colors.primary[1] * (1 - t * 0.3));
    const b = Math.round(colors.primary[2] * (1 - t * 0.3));
    return [r, g, b, 200];
  };
}

function makeFrameSprite(colors) {
  return (x, y, w, h) => {
    const outer = 3;
    const inner = 6;
    const isOuter = x < outer || x >= w - outer || y < outer || y >= h - outer;
    const isInner = !isOuter && (x < inner || x >= w - inner || y < inner || y >= h - inner);
    if (isOuter) return [...colors.border, 240];
    if (isInner) return [...colors.highlight, 180];
    return [...colors.dark, 30]; // Nearly transparent center
  };
}

function makePanelSprite(colors) {
  return (x, y, w, h) => {
    const border = 5;
    const isEdge = x < border || x >= w - border || y < border || y >= h - border;
    if (isEdge) return [...colors.border, 200];
    // Semi-transparent dark fill
    return [...colors.dark, 160];
  };
}

function makeRoundedSprite(colors) {
  return (x, y, w, h) => {
    const cx = w / 2, cy = h / 2;
    const rx = w / 2 - 2, ry = h / 2 - 2;
    const dist = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2;
    if (dist > 1.0) return [0, 0, 0, 0]; // Outside = transparent
    if (dist > 0.85) return [...colors.border, 220]; // Border ring
    // Inner fill with subtle gradient
    const t = y / h;
    const r = Math.round(colors.primary[0] * 0.8 + colors.dark[0] * 0.2 * t);
    const g = Math.round(colors.primary[1] * 0.8 + colors.dark[1] * 0.2 * t);
    const b = Math.round(colors.primary[2] * 0.8 + colors.dark[2] * 0.2 * t);
    return [r, g, b, 180];
  };
}

function makeCanvasSprite(colors) {
  return (x, y, w, h) => {
    const border = 3;
    const isEdge = x < border || x >= w - border || y < border || y >= h - border;
    if (isEdge) return [...colors.accent, 150];
    return [...colors.primary, 120];
  };
}

function makeFillSprite(colors) {
  return (x, y, w, h) => {
    return [...colors.primary, 140];
  };
}

function makeFlatSprite(colors) {
  return (x, y, w, h) => {
    return [...colors.primary, 255];
  };
}

function makeOptionSprite(colors) {
  return (x, y, w, h) => {
    const border = 2;
    const isEdge = x < border || x >= w - border || y < border || y >= h - border;
    if (isEdge) return [...colors.secondary, 180];
    const t = x / w;
    const r = Math.round(colors.dark[0] + (colors.primary[0] - colors.dark[0]) * t * 0.3);
    const g = Math.round(colors.dark[1] + (colors.primary[1] - colors.dark[1]) * t * 0.3);
    const b = Math.round(colors.dark[2] + (colors.primary[2] - colors.dark[2]) * t * 0.3);
    return [r, g, b, 150];
  };
}

function makeScrollSprite(colors) {
  return (x, y, w, h) => {
    const border = 2;
    const isEdge = x < border || x >= w - border || y < border || y >= h - border;
    if (isEdge) return [...colors.border, 200];
    return [...colors.accent, 160];
  };
}

const SPRITE_GENERATORS = {
  button: makeButtonSprite,
  frame: makeFrameSprite,
  panel: makePanelSprite,
  rounded: makeRoundedSprite,
  canvas: makeCanvasSprite,
  fill: makeFillSprite,
  flat: makeFlatSprite,
  option: makeOptionSprite,
  scroll: makeScrollSprite,
};

// =========================================================================
// Theme Manifest Generator
// =========================================================================

function generateManifest(theme) {
  const cs = theme.colorScheme;
  const kingdomSection = theme.boundKingdom
    ? `  <BoundKingdoms>\n    <Kingdom>${theme.boundKingdom}</Kingdom>\n  </BoundKingdoms>`
    : '  <BoundKingdoms />';

  const colorEntries = Object.entries(cs)
    .map(([k, v]) => `    <${k}>${v}</${k}>`)
    .join('\n');

  return `<?xml version="1.0" encoding="utf-8"?>
<Theme>
  <Name>${theme.name}</Name>
  <Description>${theme.description}</Description>
  <Author>BannerlordThemeSwitcher (LOTR Test)</Author>
  <Version>1.0.0</Version>
  <BaseCulture>${theme.baseCulture}</BaseCulture>
  <AutoTheme>false</AutoTheme>
${kingdomSection}
  <Components Brushes="true" Sprites="true" />
  <ColorScheme>
${colorEntries}
  </ColorScheme>
</Theme>
`;
}

// =========================================================================
// SpriteConfig.xml Generator (nine-slice border overrides)
// =========================================================================

function generateSpriteConfig(theme) {
  const entries = SPRITE_OVERRIDES
    .filter(s => s.nine)
    .map(s => {
      // Different border sizes based on sprite type
      let border = 12;
      if (s.type === 'panel') border = 16;
      if (s.type === 'scroll') border = 6;
      if (s.type === 'fill') border = 4;
      if (s.type === 'option') border = 8;
      return `  <NineRegion sprite="${s.name}" left="${border}" right="${border}" top="${border}" bottom="${border}" />`;
    })
    .join('\n');

  return `<?xml version="1.0" encoding="utf-8"?>
<SpriteConfig>
  <!-- Nine-slice border definitions for ${theme.name} -->
${entries}
</SpriteConfig>
`;
}

// =========================================================================
// Edge Case Test Theme
// =========================================================================

function generateEdgeCaseTheme(themesDir) {
  const dir = join(themesDir, 'SpriteTest_EdgeCases');
  const spritesDir = join(dir, 'Sprites');
  const iconsDir = join(spritesDir, 'icons');
  const emptyDir = join(spritesDir, 'empty_folder');

  mkdirSync(iconsDir, { recursive: true });
  mkdirSync(emptyDir, { recursive: true });

  // 1. Tiny 1x1 pixel (edge case)
  const tiny = createPNG(1, 1, () => [255, 0, 255, 255]);
  writeFileSync(join(spritesDir, 'tiny.png'), tiny);

  // 2. Large 512x512 (tests atlas packing with big sprites)
  const large = createPNG(512, 512, (x, y, w, h) => {
    const cx = w / 2, cy = h / 2;
    const dist = Math.sqrt((x - cx) ** 2 + (y - cy) ** 2) / (w / 2);
    if (dist > 1) return [0, 0, 0, 0];
    const r = Math.round(100 + 100 * Math.sin(x / 20));
    const g = Math.round(100 + 100 * Math.cos(y / 20));
    return [r, g, 180, 200];
  });
  writeFileSync(join(spritesDir, 'large_panel.png'), large);

  // 3. Custom nine-slice border (tested via SpriteConfig.xml)
  const borderSprite = createPNG(48, 48, (x, y, w, h) => {
    const b = 8;
    const isEdge = x < b || x >= w - b || y < b || y >= h - b;
    return isEdge ? [255, 200, 0, 255] : [40, 30, 60, 120];
  });
  writeFileSync(join(spritesDir, 'custom_border_9.png'), borderSprite);

  // 4. Sprites in subfolders (tests path normalization)
  const star = createPNG(24, 24, (x, y, w, h) => {
    const cx = 12, cy = 12;
    const angle = Math.atan2(y - cy, x - cx);
    const dist = Math.sqrt((x - cx) ** 2 + (y - cy) ** 2);
    const starR = 10 * (0.5 + 0.5 * Math.cos(angle * 5));
    return dist < starR ? [255, 220, 50, 255] : [0, 0, 0, 0];
  });
  writeFileSync(join(iconsDir, 'test_star.png'), star);

  const shield = createPNG(24, 24, (x, y, w, h) => {
    const cx = 12;
    const inShield = Math.abs(x - cx) < (12 - y * 0.5) && y < 20 && y > 2;
    return inShield ? [100, 150, 220, 255] : [0, 0, 0, 0];
  });
  writeFileSync(join(iconsDir, 'test_shield.png'), shield);

  // 5. A sprite to be aliased via SpriteConfig
  const aliasTarget = createPNG(64, 64, (x, y, w, h) => {
    const border = 4;
    const isEdge = x < border || x >= w - border || y < border || y >= h - border;
    return isEdge ? [0, 255, 128, 255] : [20, 80, 40, 180];
  });
  writeFileSync(join(spritesDir, 'my_custom_button.png'), aliasTarget);

  // ThemeManifest.xml
  writeFileSync(join(dir, 'ThemeManifest.xml'), `<?xml version="1.0" encoding="utf-8"?>
<Theme>
  <Name>Sprite Edge Case Tests</Name>
  <Description>Tests edge cases: tiny sprites, large sprites, subfolders, aliases, empty folders</Description>
  <Author>BannerlordThemeSwitcher (Test)</Author>
  <Version>1.0.0</Version>
  <AutoTheme>false</AutoTheme>
  <BoundKingdoms />
  <Components Brushes="false" Sprites="true" />
  <ColorScheme>
    <Primary>#808080</Primary>
    <Secondary>#606060</Secondary>
  </ColorScheme>
</Theme>
`);

  // SpriteConfig.xml with custom borders + alias
  writeFileSync(join(dir, 'SpriteConfig.xml'), `<?xml version="1.0" encoding="utf-8"?>
<SpriteConfig>
  <!-- Custom nine-slice borders (different from default 16px) -->
  <NineRegion sprite="custom_border_9" left="8" right="8" top="8" bottom="8" />

  <!-- Alias: use my_custom_button.png as replacement for vanilla button_canvas_9 -->
  <Alias file="my_custom_button.png" replaces="button_canvas_9" />
</SpriteConfig>
`);

  console.log(`  ✓ SpriteTest_EdgeCases (6 sprites + empty folder + SpriteConfig.xml)`);
}

// =========================================================================
// Main
// =========================================================================

const themesDir = join(process.cwd(), 'Themes');

console.log('🎭 Generating LOTR Test Themes for BannerlordThemeSwitcher\n');
console.log(`Output: ${themesDir}\n`);

// Generate LOTR culture themes
for (const theme of THEMES) {
  const dir = join(themesDir, theme.id);
  const spritesDir = join(dir, 'Sprites');
  mkdirSync(spritesDir, { recursive: true });

  // Generate sprite PNGs
  for (const sprite of SPRITE_OVERRIDES) {
    const gen = SPRITE_GENERATORS[sprite.type];
    const png = createPNG(sprite.w, sprite.h, gen(theme.colors));
    writeFileSync(join(spritesDir, `${sprite.name}.png`), png);
  }

  // Generate ThemeManifest.xml
  writeFileSync(join(dir, 'ThemeManifest.xml'), generateManifest(theme));

  // Generate SpriteConfig.xml
  writeFileSync(join(dir, 'SpriteConfig.xml'), generateSpriteConfig(theme));

  console.log(`  ✓ ${theme.id} (${theme.name}) — ${SPRITE_OVERRIDES.length} sprite overrides`);
}

// Generate edge case test theme
console.log('');
generateEdgeCaseTheme(themesDir);

console.log(`\n✅ Done! Generated ${THEMES.length + 1} themes with sprite assets.`);
console.log('\nTheme → LOTR mapping:');
for (const theme of THEMES) {
  console.log(`  ${theme.baseCulture.padEnd(10)} → ${theme.id.padEnd(12)} (${theme.name})`);
}
