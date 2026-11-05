"""Builds the self-contained HTML pages shipped inside the VSIX (images embedded), from the Markdown documentation.

Usage, from docs/tools: `python build_html.py` (needs the `markdown` package). Output: src/CodeStraight.Vsix/Resources.
"""
import base64
import os
import re

import markdown
from markdown.extensions.toc import slugify as toc_slugify

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'src', 'CodeStraight.Vsix', 'Resources')

STYLE = """
:root { color-scheme: light dark; --bg: #ffffff; --fg: #1f2328; --muted: #59636e; --border: #d1d9e0; --code: #f6f8fa; --accent: #005cb0; }
@media (prefers-color-scheme: dark) { :root { --bg: #1e1e1e; --fg: #e6e6e6; --muted: #a0a0a0; --border: #3c3c3c; --code: #2a2a2a; --accent: #4aa3ff; } }
body { background: var(--bg); color: var(--fg); font: 15px/1.6 "Segoe UI", system-ui, sans-serif; margin: 0; }
main { max-width: 1100px; margin: 0 auto; padding: 24px 16px 64px; }
nav { position: sticky; top: 0; background: var(--bg); border-bottom: 1px solid var(--border); padding: 10px 16px; z-index: 1; }
nav a { margin-right: 18px; color: var(--accent); text-decoration: none; font-weight: 600; }
h1 { border-bottom: 1px solid var(--border); padding-bottom: 6px; margin-top: 48px; }
h2 { margin-top: 36px; } h3 { margin-top: 28px; }
a { color: var(--accent); }
img { max-width: 100%; height: auto; border-radius: 6px; border: 1px solid var(--border); }
code { background: var(--code); padding: 1px 5px; border-radius: 4px; font-family: Consolas, "Cascadia Mono", monospace; font-size: 13px; }
pre { background: var(--code); padding: 12px 14px; border-radius: 6px; overflow-x: auto; }
pre code { padding: 0; background: none; }
table { border-collapse: collapse; margin: 12px 0; display: block; overflow-x: auto; }
th, td { border: 1px solid var(--border); padding: 6px 10px; text-align: left; vertical-align: top; }
th { background: var(--code); }
blockquote { margin: 12px 0; padding: 2px 14px; border-left: 4px solid var(--accent); color: var(--muted); }
hr { border: 0; border-top: 1px solid var(--border); margin: 40px 0; }
"""


def embed_images(html, base):
    def replace(match):
        path = os.path.normpath(os.path.join(base, match.group(1)))
        with open(path, 'rb') as image:
            data = base64.b64encode(image.read()).decode('ascii')
        kind = os.path.splitext(path)[1][1:]
        return f'src="data:image/{kind};base64,{data}"'

    return re.sub(r'src="([^"]+\.(?:png|gif))"', replace, html)


def render(markdown_path, prefix):
    with open(markdown_path, encoding='utf-8') as file:
        text = file.read()
    # Links between documents point to sections of the same page.
    text = text.replace('(docs/MANUAL.md)', '(#manual)').replace('(../README.md)', '(#features)').replace('(CHANGELOG.md)', '(#changes)')
    slug = toc_slugify
    text = re.sub(r'\]\(#([^)]+)\)', lambda m: f'](#{prefix}-{slug(m.group(1), "-")})' if m.group(1) not in ('manual', 'features', 'changes') else m.group(0), text)
    html = markdown.markdown(text, extensions=['tables', 'fenced_code', 'toc'],
                             extension_configs={'toc': {'slugify': lambda value, sep: prefix + "-" + toc_slugify(value, sep)}})
    return embed_images(html, os.path.dirname(markdown_path))


def page(title, sections):
    links = ''.join(f'<a href="#{anchor}">{label}</a>' for anchor, label, _ in sections)
    body = ''.join(f'<section id="{anchor}">{html}</section><hr>' for anchor, _, html in sections)
    return f'<!doctype html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">' \
           f'<title>{title}</title><style>{STYLE}</style></head><body><nav>{links}</nav><main>{body}</main></body></html>'


features = render(os.path.join(ROOT, 'README.md'), 'f')
manual = render(os.path.join(ROOT, 'docs', 'MANUAL.md'), 'm')
changes = render(os.path.join(ROOT, 'CHANGELOG.md'), 'c')

with open(os.path.join(OUT, 'CodeStraight.html'), 'w', encoding='utf-8') as file:
    file.write(page('CodeStraight', [
        ('features', 'Funcionalidades · Features', features),
        ('manual', 'Manual de uso · User manual', manual),
        ('changes', 'Versiones · Release notes', changes),
    ]))

with open(os.path.join(OUT, 'ReleaseNotes.html'), 'w', encoding='utf-8') as file:
    file.write(page('CodeStraight · Release notes', [('changes', 'Versiones · Release notes', changes)]))

print('ok', [(name, os.path.getsize(os.path.join(OUT, name))) for name in ('CodeStraight.html', 'ReleaseNotes.html')])
