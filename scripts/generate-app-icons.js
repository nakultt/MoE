// Regenerates the MSIX visual assets, AppIcon.ico, and model brand tiles from branding/.
// Usage (from the repository root, with Node.js and Playwright available):
//   node scripts/generate-app-icons.js
// Requires Python 3 with Pillow for the .ico container.
'use strict';

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const branding = path.join(root, 'branding');
const assets = path.join(root, 'src', 'MoE.ModelManager', 'Assets');
const brandsOut = path.join(assets, 'Brands');

const logo = fs.readFileSync(path.join(branding, 'logo.svg'), 'utf8');
const logoSmall = fs.readFileSync(path.join(branding, 'logo-small.svg'), 'utf8');
const dataUri = (svg) => `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
const fileUri = (file) => {
  const ext = path.extname(file).slice(1);
  const type = ext === 'svg' ? 'image/svg+xml' : `image/${ext}`;
  return `data:${type};base64,${fs.readFileSync(file).toString('base64')}`;
};

// Sizes at or below this threshold use the simplified mark so it stays legible.
const smallThreshold = 32;
const markFor = (size) => dataUri(size <= smallThreshold ? logoSmall : logo);

// [file name, canvas width, canvas height, logo size]
const tiles = [
  ['Square44x44Logo.scale-100.png', 44, 44, 44],
  ['Square44x44Logo.scale-150.png', 66, 66, 66],
  ['Square44x44Logo.scale-200.png', 88, 88, 88],
  ['Square44x44Logo.scale-400.png', 176, 176, 176],
  ['Square150x150Logo.scale-100.png', 150, 150, 96],
  ['Square150x150Logo.scale-200.png', 300, 300, 192],
  ['Wide310x150Logo.scale-100.png', 310, 150, 96],
  ['Wide310x150Logo.scale-200.png', 620, 300, 192],
  ['SplashScreen.scale-100.png', 620, 300, 160],
  ['SplashScreen.scale-200.png', 1240, 600, 320],
  ['LockScreenLogo.scale-200.png', 48, 48, 48],
  ['StoreLogo.png', 50, 50, 50],
];
for (const size of [16, 20, 24, 32, 48, 256]) {
  tiles.push([`Square44x44Logo.targetsize-${size}.png`, size, size, size]);
  tiles.push([`Square44x44Logo.targetsize-${size}_altform-unplated.png`, size, size, size]);
}
tiles.push(['Square44x44Logo.targetsize-48_altform-lightunplated.png', 48, 48, 48]);

const icoSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

// [output, source file, tile background, mark scale, full bleed]
const brands = [
  ['qwen.png', 'qwen.svg', '#FFFFFF', 0.64, false],
  ['gemma.png', 'gemma.svg', '#FFFFFF', 0.84, false],
  ['nvidia.png', 'nvidia.svg', '#FFFFFF', 0.72, false],
  ['flux.png', 'flux.svg', '#111111', 0.62, false],
  ['wan.png', 'wan.png', '#FFFFFF', 0.82, false],
  ['kokoro.png', 'kokoro.png', '#000000', 1, true],
];

async function render(page, html, width, height, out) {
  await page.setViewportSize({ width, height });
  await page.setContent(`<!doctype html><html><body style="margin:0;background:transparent">${html}</body></html>`);
  await page.waitForTimeout(50);
  await page.screenshot({ path: out, omitBackground: true, clip: { x: 0, y: 0, width, height } });
}

(async () => {
  fs.mkdirSync(brandsOut, { recursive: true });
  const icoDir = fs.mkdtempSync(path.join(require('os').tmpdir(), 'moe-ico-'));
  const browser = await chromium.launch();
  const page = await browser.newPage({ deviceScaleFactor: 1 });

  for (const [name, w, h, s] of tiles) {
    const html = `<div style="width:${w}px;height:${h}px;display:flex;align-items:center;justify-content:center">
      <img src="${markFor(s)}" width="${s}" height="${s}"></div>`;
    await render(page, html, w, h, path.join(assets, name));
  }

  const icoFiles = [];
  for (const size of icoSizes) {
    const out = path.join(icoDir, `${size}.png`);
    await render(page, `<img src="${markFor(size)}" width="${size}" height="${size}" style="display:block">`, size, size, out);
    icoFiles.push(out);
  }

  for (const [out, source, background, scale, fullBleed] of brands) {
    const size = 96;
    const mark = Math.round(size * scale);
    const border = background === '#FFFFFF' ? 'box-shadow:inset 0 0 0 2px rgba(0,0,0,.08);' : '';
    const img = fullBleed
      ? `<img src="${fileUri(path.join(branding, 'brands', source))}" style="width:100%;height:100%;object-fit:cover">`
      : `<img src="${fileUri(path.join(branding, 'brands', source))}" style="width:${mark}px;height:${mark}px;${source === 'flux.svg' ? 'filter:invert(1);' : ''}">`;
    const html = `<div style="width:${size}px;height:${size}px;border-radius:22px;overflow:hidden;background:${background};${border}display:flex;align-items:center;justify-content:center">${img}</div>`;
    await render(page, html, size, size, path.join(brandsOut, out));
  }

  await browser.close();

  execFileSync('python3', ['-I', '-c', `
import sys
from PIL import Image
files = sys.argv[2:]
images = [Image.open(f).convert('RGBA') for f in files]
images[-1].save(sys.argv[1], format='ICO', sizes=[i.size for i in images], append_images=images[:-1])
`, path.join(assets, 'AppIcon.ico'), ...icoFiles]);
  fs.rmSync(icoDir, { recursive: true, force: true });
  console.log('Generated app icons and brand tiles.');
})();
