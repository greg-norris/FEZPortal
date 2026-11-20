"""Regenerates the font resources for the Portal projects (one .tcfnt per UiFont value).

Usage: python tools/make_ui_fonts.py [ProjectFolder ...]   (no arguments = every project below)
Change a typeface or size here, rerun, then rebuild. Resource names stay the same, so no code changes.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from make_tcfnt import build  # noqa: E402

FONTS = 'C:/Windows/Fonts/'
# Tahoma Bold stays legible at small pixel sizes; Arial Bold looks better large.
UI_FONTS = [
    ('FontTiny', 'tahomabd.ttf', 11),
    ('FontSmall', 'tahomabd.ttf', 12),
    ('FontMedium', 'tahomabd.ttf', 14),
    ('FontLarge', 'arialbd.ttf', 16),
    ('FontXLarge', 'arialbd.ttf', 20),
    ('FontHuge', 'arialbd.ttf', 32),
]

# Project folder -> its fonts, in UiFont enum order.
PROJECTS = {
    'FEZPortalHelloWorld': UI_FONTS,
    'PortalDesktop': UI_FONTS + [('FontClock', 'arialbd.ttf', 60), ('FontPill', 'tahomabd.ttf', 10)],
}


def resource_id(name):
    """The 16-bit id TinyCLR's resx code generator assigns to a resource name (FontResources enum values)."""
    def i32(v):
        v &= 0xFFFFFFFF
        return v - (1 << 32) if v & 0x80000000 else v

    def i16(v):
        v &= 0xFFFF
        return v - 0x10000 if v & 0x8000 else v

    h1 = h2 = i32((5381 << 16) + 5381)
    for i, ch in enumerate(name):
        if i % 2 == 0:
            h1 = i32(((h1 << 5) + h1) ^ ord(ch))
        else:
            h2 = i32(((h2 << 5) + h2) ^ ord(ch))
    h = i32(h1 + i32(h2 * 1566083941))
    return i16(i16(h >> 16) ^ i16(h))


def make(project):
    out_dir = os.path.join(os.path.dirname(__file__), '..', project, 'Resources')
    os.makedirs(out_dir, exist_ok=True)
    print(f'== {project}')
    for name, ttf, size in PROJECTS[project]:
        data, height = build(FONTS + ttf, size)
        with open(os.path.join(out_dir, name + '.tcfnt'), 'wb') as f:
            f.write(data)
        print(f'{name}: {ttf} {size}px, line height {height}px, {len(data)} bytes')
    print('\nFontResources enum entries for Resources.Designer.cs:')
    for name, _, _ in PROJECTS[project]:
        print(f'            {name} = {resource_id(name)},')
    print()


if __name__ == '__main__':
    for project in sys.argv[1:] or PROJECTS:
        make(project)
