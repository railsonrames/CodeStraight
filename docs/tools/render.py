"""Renders the documentation images of CodeStraight from the before/after files produced by the real engine.

Usage (from docs/tools): `dotnet run` (writes out/*.before.cs and *.after.cs), then `python render.py` (needs Pillow).
`python render.py --demo-only` renders only the animation of the README.
"""
import difflib
import os
import re
import sys

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'out')
DST = os.path.join(HERE, '..', 'images')
os.makedirs(DST, exist_ok=True)

FONTS = r'C:\Windows\Fonts'
MONO = ImageFont.truetype(os.path.join(FONTS, 'consola.ttf'), 15)
MONO_BOLD = ImageFont.truetype(os.path.join(FONTS, 'consolab.ttf'), 15)
UI = ImageFont.truetype(os.path.join(FONTS, 'segoeui.ttf'), 15)
UI_BOLD = ImageFont.truetype(os.path.join(FONTS, 'segoeuib.ttf'), 15)
UI_BIG = ImageFont.truetype(os.path.join(FONTS, 'segoeuib.ttf'), 44)
UI_MID = ImageFont.truetype(os.path.join(FONTS, 'segoeui.ttf'), 22)

# Visual Studio dark theme.
BG = (30, 30, 30)
PANEL = (37, 37, 38)
TEXT = (220, 220, 220)
GUTTER = (43, 145, 175)
KEYWORD = (86, 156, 214)
CONTROL = (216, 160, 223)
TYPE = (78, 201, 176)
STRING = (214, 157, 133)
COMMENT = (87, 166, 74)
NUMBER = (181, 206, 168)
ADDED = (35, 62, 42)
REMOVED = (75, 30, 30)
HEADER = (45, 45, 48)
ACCENT = (0, 122, 204)

CHAR_W = MONO.getbbox('M')[2]
LINE_H = 21
PAD = 16
GUTTER_W = 4 * CHAR_W + 14

KEYWORDS = set('''abstract as base bool byte char checked class const decimal default delegate double enum event explicit
extern false fixed float implicit in int interface internal is lock long namespace new null object operator out override
params private protected public readonly ref sbyte sealed short sizeof stackalloc static string struct this true typeof uint
ulong unchecked unsafe ushort using virtual void volatile var async await get set init record'''.split())
CONTROL_WORDS = set('if else for foreach while do switch case break continue return throw try catch finally yield'.split())
TOKEN = re.compile(r'(?P<comment>//.*)|(?P<string>\$?@?"(?:[^"\\]|\\.)*"?)|(?P<number>\b\d+\b)|(?P<word>[A-Za-z_][A-Za-z0-9_]*)|(?P<other>.)')


def tokens(line):
    for match in TOKEN.finditer(line):
        kind = match.lastgroup
        value = match.group(0)
        if kind == 'comment':
            yield value, COMMENT
        elif kind == 'string':
            yield value, STRING
        elif kind == 'number':
            yield value, NUMBER
        elif kind == 'word':
            if value in KEYWORDS:
                yield value, KEYWORD
            elif value in CONTROL_WORDS:
                yield value, CONTROL
            elif value[0].isupper():
                yield value, TYPE
            else:
                yield value, TEXT
        else:
            yield value, TEXT


def read(name):
    with open(os.path.join(SRC, name), encoding='utf-8') as file:
        return file.read().replace('\r\n', '\n').rstrip('\n').expandtabs(4).split('\n')


def marks(before, after):
    """Line numbers changed in each version (removed in before, added in after)."""
    removed, added = set(), set()
    for tag, i1, i2, j1, j2 in difflib.SequenceMatcher(a=before, b=after, autojunk=False).get_opcodes():
        if tag != 'equal':
            removed.update(range(i1, i2))
            added.update(range(j1, j2))
    return removed, added


def code_size(lines, guideline):
    columns = max([len(line) for line in lines] + [guideline or 0, 40])
    return GUTTER_W + columns * CHAR_W + 2 * PAD, len(lines) * LINE_H + 2 * PAD


def draw_code(draw, x, y, lines, highlight, color, guideline=None, height=None, first_number=1):
    width, natural = code_size(lines, guideline)
    height = height or natural
    draw.rectangle([x, y, x + width, y + height], fill=BG)
    for index, line in enumerate(lines):
        top = y + PAD + index * LINE_H
        if index in highlight:
            draw.rectangle([x + GUTTER_W - 6, top - 1, x + width, top + LINE_H - 2], fill=color)
        draw.text((x + 6, top), f'{index + first_number:>3}', font=MONO, fill=GUTTER)
        cursor = x + GUTTER_W
        for value, fill in tokens(line):
            draw.text((cursor, top), value, font=MONO, fill=fill)
            cursor += len(value) * CHAR_W
    if guideline:
        gx = x + GUTTER_W + guideline * CHAR_W
        draw.line([gx, y + 4, gx, y + height - 4], fill=(220, 80, 80), width=1)
        draw.text((gx + 4, y + 4), str(guideline), font=UI, fill=(220, 80, 80))
    return width, height


def header(draw, x, y, width, title):
    draw.rectangle([x, y, x + width, y + 30], fill=HEADER)
    draw.rectangle([x, y + 28, x + width, y + 30], fill=ACCENT)
    draw.text((x + 12, y + 5), title, font=UI_BOLD, fill=(241, 241, 241))
    return 31


def comparison(name, file_name, titles=('Antes · Before', 'Después · After'), guideline=None, stacked=False, after_name=None):
    before = read(f'{name}.before.cs')
    after = read(f'{after_name or name}.after.cs')
    removed, added = marks(before, after)
    bw, bh = code_size(before, guideline)
    aw, ah = code_size(after, guideline)
    gap = 12
    if stacked:
        width = max(bw, aw)
        image = Image.new('RGB', (width + 2 * gap, bh + ah + 2 * 31 + 3 * gap), PANEL)
        draw = ImageDraw.Draw(image)
        y = gap
        y += header(draw, gap, y, width, titles[0])
        draw_code(draw, gap, y, before, removed, REMOVED, guideline)
        y += bh + gap
        y += header(draw, gap, y, width, titles[1])
        draw_code(draw, gap, y, after, added, ADDED, guideline)
    else:
        height = max(bh, ah)
        image = Image.new('RGB', (bw + aw + 3 * gap, height + 31 + 2 * gap), PANEL)
        draw = ImageDraw.Draw(image)
        header(draw, gap, gap, bw, titles[0])
        header(draw, 2 * gap + bw, gap, aw, titles[1])
        draw_code(draw, gap, gap + 31, before, removed, REMOVED, guideline, height)
        draw_code(draw, 2 * gap + bw, gap + 31, after, added, ADDED, guideline, height)
    image.save(os.path.join(DST, file_name), optimize=True)
    print('ok', file_name, image.size)


def three_way(file_name, guideline):
    """Before, one_per_line and fill, stacked; only the class, with the real line numbers."""
    before = read('wrap-one-per-line.before.cs')
    versions = [
        ('Antes · Before', before, REMOVED),
        ('wrap_list_style = one_per_line', read('wrap-one-per-line.after.cs'), ADDED),
        ('wrap_list_style = fill   ·   wrap_tolerance = 12', read('wrap-fill.after.cs'), ADDED),
    ]
    parts = []
    for title, lines, color in versions:
        highlight = marks(before, lines)[1] if lines is not before else marks(before, versions[1][1])[0]
        first = next(i for i, line in enumerate(lines) if line.startswith('public class'))
        parts.append((title, lines[first:], {i - first for i in highlight if i >= first}, color, first))
    sizes = [code_size(lines, guideline) for _, lines, _, _, _ in parts]
    width = max(w for w, _ in sizes)
    gap = 12
    image = Image.new('RGB', (width + 2 * gap, sum(h for _, h in sizes) + 3 * 31 + 4 * gap), PANEL)
    draw = ImageDraw.Draw(image)
    y = gap
    for (title, lines, highlight, color, first), (_, height) in zip(parts, sizes):
        y += header(draw, gap, y, width, title)
        draw_code(draw, gap, y, lines, highlight, color, guideline, first_number=first + 1)
        y += height + gap
    image.save(os.path.join(DST, file_name), optimize=True)
    print('ok', file_name, image.size)


def output_pane(file_name):
    lines = [
        '[10:42:13] Servicio.cs: trailing_comma (invalid commas): no changes',
        '[10:42:13] Servicio.cs: rename_symbols: changed',
        '[10:42:13] Servicio.cs: namespace_declarations: changed',
        '[10:42:13] Servicio.cs: remove_unused_usings: changed',
        '[10:42:13] Servicio.cs: sort_usings: changed',
        '[10:42:13] Servicio.cs: accessibility_modifiers: no changes',
        '[10:42:13] Servicio.cs: format_document: changed',
        '[10:42:13] Servicio.cs: wrap_long_lines: changed',
        '[10:42:13] Servicio.cs: trailing_comma: changed',
        '[10:42:13] Servicio.cs: normalize_line_endings: no changes',
        '[10:42:13] Applied to Servicio.cs in 212 ms.',
        '[10:42:13] Servicio.cs: normalize_charset: on disk before saving: not valid UTF-8 (legacy code page, e.g. Windows-1252); saving as Utf8',
        '[10:42:13] Servicio.cs: normalize_charset: saved as UTF-8 without BOM',
    ]
    width = max(len(line) for line in lines) * CHAR_W + 2 * PAD
    height = 34 + 34 + len(lines) * LINE_H + 2 * PAD
    image = Image.new('RGB', (width, height), BG)
    draw = ImageDraw.Draw(image)
    draw.rectangle([0, 0, width, 33], fill=HEADER)
    draw.text((12, 6), 'Salida · Output', font=UI_BOLD, fill=(241, 241, 241))
    draw.rectangle([0, 34, width, 67], fill=(51, 51, 55))
    draw.text((12, 41), 'Mostrar salida de · Show output from:', font=UI, fill=(200, 200, 200))
    draw.rectangle([290, 39, 470, 62], fill=(63, 63, 70), outline=ACCENT)
    draw.text((298, 41), 'CodeStraight', font=UI, fill=(241, 241, 241))
    for index, line in enumerate(lines):
        draw.text((PAD, 68 + PAD + index * LINE_H), line, font=MONO, fill=TEXT if 'changed' in line or 'saved' in line or 'Applied' in line else (150, 150, 150))
    image.save(os.path.join(DST, file_name), optimize=True)
    print('ok', file_name, image.size)


def guideline_editor(file_name):
    lines = read('long-strings.after.cs')[5:13]
    guideline = 120
    width, height = code_size(lines, guideline)
    image = Image.new('RGB', (width, height), BG)
    draw = ImageDraw.Draw(image)
    draw_code(draw, 0, 0, lines, set(), ADDED)
    gx = GUTTER_W + guideline * CHAR_W
    draw.line([gx, 0, gx, height], fill=(128, 128, 128), width=1)
    image.save(os.path.join(DST, file_name), optimize=True)
    print('ok', file_name, image.size)


def charset(file_name):
    rows = [
        ('Windows-1252', ['M', 'é', 't', 'o', 'd', 'o'], ['4D', 'E9', '74', '6F', '64', '6F'], REMOVED),
        ('UTF-8 (sin BOM · no BOM)', ['M', 'é', 't', 'o', 'd', 'o'], ['4D', 'C3 A9', '74', '6F', '64', '6F'], ADDED),
    ]
    cell = 74
    width = 260 + 6 * cell + 40
    image = Image.new('RGB', (width, 230), PANEL)
    draw = ImageDraw.Draw(image)
    draw.text((20, 14), 'normalize_charset: "Método"', font=UI_BOLD, fill=(241, 241, 241))
    for row, (label, chars, codes, color) in enumerate(rows):
        top = 60 + row * 82
        draw.text((20, top + 18), label, font=UI_BOLD, fill=(220, 220, 220))
        for index, (char, code) in enumerate(zip(chars, codes)):
            left = 260 + index * cell
            fill = color if char == 'é' else BG
            draw.rectangle([left, top, left + cell - 6, top + 66], fill=fill, outline=(70, 70, 74))
            draw.text((left + 26, top + 6), char, font=MONO_BOLD, fill=TEXT)
            draw.text((left + 10, top + 36), code, font=MONO, fill=NUMBER)
    image.save(os.path.join(DST, file_name), optimize=True)
    print('ok', file_name, image.size)


def icon(file_name, size):
    """A set square over a ruler: code kept straight."""
    scale = 4
    big = size * scale
    image = Image.new('RGBA', (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle([0, 0, big - 1, big - 1], radius=big // 5, fill=(0, 92, 175, 255))
    margin = big // 5
    draw.polygon([(margin, big - margin), (margin, margin), (big - margin, big - margin)], fill=(255, 255, 255, 255))
    inner = big // 3
    draw.polygon([(margin + big // 10, big - margin - big // 10), (margin + big // 10, inner + big // 16),
                  (big - inner - big // 16, big - margin - big // 10)], fill=(0, 92, 175, 255))
    for index in range(1, 6):
        x = margin + index * (big - 2 * margin) // 6
        draw.line([x, big - margin, x, big - margin - big // (16 if index % 2 else 10)], fill=(0, 92, 175, 255), width=scale * 2)
    image = image.resize((size, size), Image.LANCZOS)
    image.save(file_name)
    print('ok', file_name, image.size)


def banner(file_name):
    width, height = 1280, 360
    image = Image.new('RGB', (width, height), (24, 24, 27))
    draw = ImageDraw.Draw(image)
    logo = Image.open(os.path.join(DST, 'icon-200.png'))
    image.paste(logo, (70, 80), logo)
    draw.text((310, 92), 'CodeStraight', font=UI_BIG, fill=(255, 255, 255))
    draw.text((312, 160), 'El código recto en cada guardado, con las reglas de tu .editorconfig.', font=UI_MID, fill=(210, 210, 210))
    draw.text((312, 196), 'Straight code on every save, driven by your .editorconfig.', font=UI_MID, fill=(150, 150, 150))
    draw.text((312, 262), 'Formateador + guía de columna + limpieza de código  →  una sola extensión', font=UI, fill=(78, 201, 176))
    draw.text((312, 286), 'Formatter + column guide + code cleanup  →  one extension', font=UI, fill=(78, 201, 176))
    image.save(os.path.join(DST, file_name), optimize=True)
    print('ok', file_name, image.size)


def save_demo(file_name):
    """The animation of the README: Ctrl+S, each action of the engine in slow motion, and a second save that changes nothing."""
    steps = [
        ('trailing_comma', 'Coma que no compila · Comma that does not compile'),
        ('rename_symbols', 'Nombres y todas sus referencias · Names and every reference'),
        ('namespace_declarations', 'Namespace de archivo · File-scoped namespace'),
        ('remove_unused_usings, sort_usings', 'Usings limpios y ordenados · Clean, sorted usings'),
        ('accessibility_modifiers', 'Modificadores explícitos · Explicit modifiers'),
        ('remove_extra_blank_lines', 'Sin líneas de más · No surplus blank lines'),
        ('format_document', 'Indentación · Indentation'),
        ('wrap_long_lines', 'Dentro del límite de línea · Within the line limit'),
    ]
    guideline = 120
    line_h = 18
    log_lines = 7
    width = 1120
    before = read('save-demo-0.before.cs')
    longest = max(len(before), *(len(read(f'save-demo-{index}.after.cs')) for index in range(len(steps))))
    code_top = 31 + 34
    log_top = code_top + longest * line_h + 2 * PAD
    height = log_top + 30 + log_lines * line_h + PAD

    def frame(lines, caption, title=None, highlight=(), log=(), unsaved=False, slow_motion=False, key=False):
        image = Image.new('RGB', (width, height), BG)
        draw = ImageDraw.Draw(image)
        draw.rectangle([0, 0, width, 30], fill=HEADER)
        draw.rectangle([0, 28, 170, 30], fill=ACCENT)
        draw.text((12, 5), 'Servicio.cs' + (' *' if unsaved else ''), font=UI_BOLD, fill=(241, 241, 241))
        draw.rectangle([0, 31, width, code_top - 1], fill=PANEL)
        cursor = 12
        if title:
            draw.text((cursor, 39), title, font=MONO_BOLD, fill=TYPE)
            cursor += len(title) * CHAR_W + 16
        draw.text((cursor, 36), caption, font=UI, fill=(220, 220, 220))
        if slow_motion:
            note = 'cámara lenta · slow motion'
            draw.text((width - 12 - UI.getlength(note), 36), note, font=UI, fill=(150, 150, 150))
        for index, line in enumerate(lines):
            top = code_top + PAD + index * line_h
            if index in highlight:
                draw.rectangle([GUTTER_W - 6, top - 1, width, top + line_h - 2], fill=ADDED)
            draw.text((6, top), f'{index + 1:>3}', font=MONO, fill=GUTTER)
            column = GUTTER_W
            for value, fill in tokens(line):
                draw.text((column, top), value, font=MONO, fill=fill)
                column += len(value) * CHAR_W
        gx = GUTTER_W + guideline * CHAR_W
        draw.line([gx, code_top, gx, log_top - 1], fill=(128, 128, 128), width=1)
        draw.rectangle([0, log_top, width, log_top + 29], fill=HEADER)
        draw.text((12, log_top + 5), 'Salida · Output: CodeStraight', font=UI_BOLD, fill=(241, 241, 241))
        for index, line in enumerate(list(log)[-log_lines:]):
            color = (150, 150, 150) if line.endswith('no changes') else TEXT
            draw.text((PAD, log_top + 30 + 8 + index * line_h), f'Servicio.cs: {line}', font=MONO, fill=color)
        if key:
            box = [width // 2 - 150, height // 2 - 60, width // 2 + 150, height // 2 + 60]
            draw.rounded_rectangle(box, radius=16, fill=HEADER, outline=ACCENT, width=3)
            draw.text((width // 2, height // 2), 'Ctrl + S', font=UI_BIG, fill=(255, 255, 255), anchor='mm')
        return image

    frames = [
        (frame(before, 'Archivo sin guardar · Unsaved file', unsaved=True), 2800),
        (frame(before, 'Guardando · Saving', unsaved=True, key=True), 1000),
    ]
    previous = before
    total = len(steps)
    for index, (name, caption) in enumerate(steps):
        lines = read(f'save-demo-{index}.after.cs')
        added = marks(previous, lines)[1]
        log = read(f'save-demo-{index}.log')
        frames.append((frame(lines, caption, f'{index + 1}/{total} {name}', added, log, slow_motion=True), 1500))
        previous = lines
    final_log = log
    frames.append((frame(previous, 'Guardado · Saved', log=final_log), 4000))
    frames.append((frame(previous, 'Guardar otra vez · Save again', log=final_log, key=True), 900))
    again = read('save-demo-again.after.cs')
    if again != previous:
        raise ValueError('The second save changed the file.')
    frames.append((frame(again, 'Idempotente: nada cambia · Idempotent: nothing changes', log=read('save-demo-again.log')), 3500))

    images = [image for image, _ in frames]
    sheet = Image.new('RGB', (width, height * len(images)))
    for index, image in enumerate(images):
        sheet.paste(image, (0, index * height))
    palette = sheet.quantize(colors=256, method=Image.Quantize.MEDIANCUT)
    indexed = [image.quantize(palette=palette, dither=Image.Dither.NONE) for image in images]
    indexed[0].save(os.path.join(DST, file_name), save_all=True, append_images=indexed[1:],
                    duration=[duration for _, duration in frames], loop=0, optimize=False, disposal=1)
    print('ok', file_name, (width, height), f'{len(frames)} frames', os.path.getsize(os.path.join(DST, file_name)), 'bytes')


save_demo('save-demo.gif')
if '--demo-only' in sys.argv:
    sys.exit()

comparison('rename', 'rename.png')
comparison('namespace-usings', 'namespace-usings.png')
three_way('wrap.png', 120)
comparison('long-strings', 'long-strings.png', guideline=120, stacked=True)
comparison('operators', 'operators.png', stacked=True)
comparison('trailing-comma', 'trailing-comma.png')
comparison('continuation', 'continuation.png')
comparison('explicit-types', 'explicit-types.png')
output_pane('output.png')
guideline_editor('guideline.png')
charset('charset.png')
icon(os.path.join(DST, 'icon-200.png'), 200)
icon(os.path.join(HERE, '..', '..', 'src', 'CodeStraight.Vsix', 'Resources', 'icon.png'), 128)
banner('banner.png')
