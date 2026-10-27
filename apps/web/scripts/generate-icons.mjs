// P0-067.2 — generates every app icon from one piece of geometry.
//
//   node apps/web/scripts/generate-icons.mjs        (from the repo root)
//
// Placeholder mark until the club's real logo lands (the masthead's logo
// slot): a jersey-block "EE" monogram, slanted forward, over the
// masthead's green/red accent strike. The letters are plain rectangles, so
// no font is needed and the mark stays crisp down to 16px.
//
// Colors are the dark-theme tokens from app/globals.css, converted from
// OKLCH here so the two can't drift apart silently. Renders with sharp,
// which Next.js already installs for image optimisation; no new dependency.

import { mkdir, writeFile } from "node:fs/promises"
import { dirname, join } from "node:path"
import { fileURLToPath } from "node:url"
import sharp from "sharp"

const webRoot = join(dirname(fileURLToPath(import.meta.url)), "..")

// --- colors: OKLCH (globals.css) -> sRGB hex -------------------------------

function oklchToHex(l, c, hDeg) {
  const h = (hDeg * Math.PI) / 180
  const a = c * Math.cos(h)
  const b = c * Math.sin(h)
  const l_ = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3
  const m_ = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3
  const s_ = (l - 0.0894841775 * a - 1.291485548 * b) ** 3
  const linear = [
    4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_,
    -1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_,
    -0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_,
  ]
  return (
    "#" +
    linear
      .map((x) => {
        const v = x <= 0.0031308 ? 12.92 * x : 1.055 * x ** (1 / 2.4) - 0.055
        return Math.round(Math.min(1, Math.max(0, v)) * 255)
          .toString(16)
          .padStart(2, "0")
      })
      .join("")
  )
}

export const colors = {
  background: oklchToHex(0.155, 0.012, 240), // --background
  foreground: oklchToHex(0.97, 0.003, 240), // --foreground
  primary: oklchToHex(0.6, 0.135, 168), // --primary (Bangladesh green)
  live: oklchToHex(0.66, 0.225, 25), // --live (Bangladesh red)
}

// --- geometry (512 x 512 design grid) ---------------------------------------

const LETTER_W = 120
const LETTER_H = 220
const STEM = 44 // vertical spine width and arm thickness
const MID_ARM = 96 // middle arm is shorter, like a block numeral
const GAP = 28
const STRIKE_GAP = 30
const STRIKE_H = 22
const SLANT = -8 // degrees; forward lean

const markW = LETTER_W * 2 + GAP
const markH = LETTER_H + STRIKE_GAP + STRIKE_H

function letterE(x, y) {
  const midY = y + (LETTER_H - STEM) / 2
  return [
    `<rect x="${x}" y="${y}" width="${STEM}" height="${LETTER_H}"/>`,
    `<rect x="${x}" y="${y}" width="${LETTER_W}" height="${STEM}"/>`,
    `<rect x="${x}" y="${midY}" width="${MID_ARM}" height="${STEM}"/>`,
    `<rect x="${x}" y="${y + LETTER_H - STEM}" width="${LETTER_W}" height="${STEM}"/>`,
  ].join("")
}

// The mark centred on (256, 256) at the given scale.
function mark(scale) {
  const x0 = 256 - markW / 2
  const y0 = 256 - markH / 2
  const strikeY = y0 + LETTER_H + STRIKE_GAP
  // Masthead strike proportions: green 2 : red 1, with a small gap.
  const greenW = Math.round((markW - 8) * (2 / 3))
  return `
  <g transform="translate(256 256) scale(${scale}) skewX(${SLANT}) translate(-256 -256)">
    <g fill="${colors.foreground}">${letterE(x0, y0)}${letterE(x0 + LETTER_W + GAP, y0)}</g>
    <rect x="${x0}" y="${strikeY}" width="${greenW}" height="${STRIKE_H}" fill="${colors.primary}"/>
    <rect x="${x0 + greenW + 8}" y="${strikeY}" width="${markW - greenW - 8}" height="${STRIKE_H}" fill="${colors.live}"/>
  </g>`
}

// rounded: transparent corners (browser tab, desktop install).
// full-bleed: the platform applies its own mask (Android maskable, iOS).
// Maskable content must sit inside the central 80% circle, hence the smaller scale.
function svg({ rounded, scale }) {
  const bg = rounded
    ? `<rect width="512" height="512" rx="112" fill="${colors.background}"/>`
    : `<rect width="512" height="512" fill="${colors.background}"/>`
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">${bg}${mark(scale)}</svg>`
}

const anySvg = svg({ rounded: true, scale: 1 })
const maskableSvg = svg({ rounded: false, scale: 0.8 })
const appleSvg = svg({ rounded: false, scale: 0.86 })

async function png(source, size) {
  return sharp(Buffer.from(source), { density: 72 * (size / 512) * 4 })
    .resize(size, size)
    .png({ compressionLevel: 9 })
    .toBuffer()
}

// ICO holding PNG images (supported by every browser since Vista-era IE).
function ico(images) {
  const header = Buffer.alloc(6 + 16 * images.length)
  header.writeUInt16LE(0, 0)
  header.writeUInt16LE(1, 2) // type: icon
  header.writeUInt16LE(images.length, 4)
  let offset = header.length
  images.forEach(({ size, data }, i) => {
    const entry = 6 + 16 * i
    header.writeUInt8(size >= 256 ? 0 : size, entry)
    header.writeUInt8(size >= 256 ? 0 : size, entry + 1)
    header.writeUInt16LE(1, entry + 4) // colour planes
    header.writeUInt16LE(32, entry + 6) // bits per pixel
    header.writeUInt32LE(data.length, entry + 8)
    header.writeUInt32LE(offset, entry + 12)
    offset += data.length
  })
  return Buffer.concat([header, ...images.map((i) => i.data)])
}

async function write(relative, data) {
  const path = join(webRoot, relative)
  await mkdir(dirname(path), { recursive: true })
  await writeFile(path, data)
  console.log(`wrote ${relative} (${data.length} bytes)`)
}

await write("app/icon.svg", anySvg)
await write("app/apple-icon.png", await png(appleSvg, 180))
await write(
  "app/favicon.ico",
  ico([
    { size: 16, data: await png(anySvg, 16) },
    { size: 32, data: await png(anySvg, 32) },
    { size: 48, data: await png(anySvg, 48) },
  ]),
)
await write("public/icons/icon-192.png", await png(anySvg, 192))
await write("public/icons/icon-512.png", await png(anySvg, 512))
await write("public/icons/icon-maskable-512.png", await png(maskableSvg, 512))
console.log(colors)
