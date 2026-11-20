"""Generate TinyCLR .tcfnt font resources from a TrueType font.

Usage:
    python make_tcfnt.py <font.ttf> <pixel-size> <out.tcfnt>
    python make_tcfnt.py C:/Windows/Fonts/arialbd.ttf 12 ArialBold12.tcfnt

Output layout (little-endian), matching the .tcfnt files GHI's font converter produces:
    resource file header   magic, version 2, header size 20, resource header size 8, count 1
    resource header        id 0, kind 2 (font), flags 0, data size
    font description       metrics (8 x int16), range count, char count, flags, pad
    bitmap description     width, height, flags, bits per pixel (1), type
    ranges[n + 1]          first char index, first char, last char, x offset into the strip
    characters[m + 1]      x offset within the range, left margin, right margin
    glyph strip            1 bpp, LSB first, rows padded to 32 bits

Glyphs are rendered with FreeType's monochrome hinting (freetype-py), so small sizes stay
crisp and every glyph sits exactly on the baseline the font's hinting gives it. Pillow's text
drawing rounds some glyphs a pixel off the baseline (e.g. 0 and 9 in Tahoma Bold 12px, 6 and 8
at 14px), so it is used only for the line metrics and the horizontal spacing, which stay as
they were.
Includes printable ASCII (32-126), the degree sign, middle dot, multiplication and division signs,
em dash, minus sign and U+FFFD (drawn as '?').

Needs: pip install pillow freetype-py
"""
import struct
import sys

import freetype
from PIL import Image, ImageDraw, ImageFont

RANGES = [(32, 126), (0xB0, 0xB0), (0xB7, 0xB7), (0xD7, 0xD7), (0xF7, 0xF7), (0x2014, 0x2014), (0x2212, 0x2212), (0xFFFD, 0xFFFD)]
MAGIC = 0xF995B0A8
FONT_FLAGS = 0x10  # value used by GHI-generated fonts


clipped = []   # characters with pixels outside the line height (reported by build)


def pillow_spacing(font, text, height):
    """(left margin, advance) as Pillow places the glyph: the horizontal spacing the fonts always had."""
    advance = int(round(font.getlength(text, mode='1')))
    pad = height
    img = Image.new('1', (advance + pad * 2, height), 0)
    draw = ImageDraw.Draw(img)
    draw.fontmode = '1'
    draw.text((pad, font.getmetrics()[0]), text, font=font, fill=1, anchor='ls')
    px = img.load()
    ink = [x for x in range(img.width) if any(px[x, y] for y in range(height))]
    return (ink[0] - pad if ink else 0), advance


def render_glyph(face, font, ch, ascent, height):
    """Returns (left margin, ink width, right margin, columns) for one character.

    Columns are lists of 0/1 for rows 0..height-1, with the baseline at row `ascent`.
    Pixels and vertical position come from FreeType; the margins from Pillow (see above).
    """
    text = '?' if ch == '\uFFFD' else ch
    face.load_char(text, freetype.FT_LOAD_RENDER | freetype.FT_LOAD_TARGET_MONO)
    glyph = face.glyph
    left, advance = pillow_spacing(font, text, height)
    bitmap = glyph.bitmap
    rows, width, pitch, buf = bitmap.rows, bitmap.width, bitmap.pitch, bitmap.buffer

    def on(x, y):
        return (buf[y * pitch + x // 8] >> (7 - x % 8)) & 1

    ink = [x for x in range(width) if any(on(x, y) for y in range(rows))]
    if not ink:
        return 0, 0, advance, []
    x0, x1 = ink[0], ink[-1] + 1
    top = ascent - glyph.bitmap_top        # row of the bitmap's first line
    cols = []
    for x in range(x0, x1):
        col = [0] * height
        for y in range(rows):
            row = top + y
            if on(x, y):
                if 0 <= row < height:
                    col[row] = 1
                elif ch not in clipped:
                    clipped.append(ch)   # outside the line (e.g. '@' at 60px): trimmed, as before
        cols.append(col)
    width = x1 - x0
    return left, width, advance - left - width, cols


def build(ttf, size):
    # Line metrics as before (so layouts don't move); glyphs straight from FreeType.
    ascent, descent = ImageFont.truetype(ttf, size).getmetrics()
    height = ascent + descent
    font = ImageFont.truetype(ttf, size)
    face = freetype.Face(ttf)
    face.set_pixel_sizes(0, size)

    range_entries, char_entries, strip = [], [], []
    advances = []
    for lo, hi in RANGES:
        range_entries.append([len(char_entries), lo, hi, len(strip)])
        range_start = len(strip)
        for code in range(lo, hi + 1):
            left, width, right, cols = render_glyph(face, font, chr(code), ascent, height)
            if not (-128 <= left <= 127 and -128 <= right <= 127):
                raise ValueError(f'margin out of range for {chr(code)!r}')
            char_entries.append((len(strip) - range_start, left, right))
            strip.extend(cols)
            advances.append(left + width + right)
    if clipped:
        print(f'  note: {ttf} {size}px: {"".join(clipped)} trimmed to the line height')
        clipped.clear()
    range_entries.append([0, 0, 0, len(strip)])
    char_entries.append((0, 0, 0))

    bmp_w = len(strip)
    stride = (bmp_w + 31) // 32 * 4
    bits = bytearray(stride * height)
    for x, col in enumerate(strip):
        for y, on in enumerate(col):
            if on:
                bits[y * stride + x // 8] |= 1 << (x % 8)

    avg_w = int(round(font.getlength('x', mode='1')))
    max_w = max(advances)
    out = bytearray()
    out += struct.pack('<Hhhhhhhh', height, 0, ascent, descent, 0, 0, avg_w, max_w)
    out += struct.pack('<HHHH', len(RANGES), len(char_entries) - 1, FONT_FLAGS, 0)
    out += struct.pack('<IIHBB', bmp_w, height, 0, 1, 0)
    for first_index, lo, hi, offset in range_entries:
        out += struct.pack('<IHHI', first_index, lo, hi, offset)
    for offset, left, right in char_entries:
        out += struct.pack('<Hbb', offset, left, right)
    out += bits
    while len(out) % 4:
        out += b'\0'

    header = struct.pack('<IIIII', MAGIC, 2, 20, 8, 1) + struct.pack('<hBBI', 0, 2, 0, len(out))
    return header + bytes(out), height


if __name__ == '__main__':
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    data, h = build(sys.argv[1], int(sys.argv[2]))
    open(sys.argv[3], 'wb').write(data)
    print(f'{sys.argv[3]}: {len(data)} bytes, line height {h}px')
