#!/usr/bin/env python3
"""
Generates src/api/generated.ts (TypeScript types of the API) from the C# DTOs and enums of the backend.

    python scripts/generate-api-types.py            (run from the frontend folder)

Reads:  ../src/ShippingERP.Application/DTOs/**/*.cs  and  ../src/ShippingERP.Domain/Enums/*.cs
Rules (they follow the JSON the API sends: camelCase names, enums as strings):
  - class / record  -> export interface (": Base" -> "extends Base" when Base is generated too)
  - positional record -> export interface with its parameters
  - enum            -> export type X = 'A' | 'B' plus export const XValues = ['A', 'B'] as const
  - *Request / *Query / *Command classes: every property optional (the API has defaults)
  - responses: nullable C# types become "T | null"
  - long, int, decimal... -> number; DateTime, Guid, TimeSpan -> string; List / IReadOnlyList / [] -> T[]
Re-run it after changing a DTO; never edit generated.ts by hand.
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
DTO_DIR = os.path.join(ROOT, 'src', 'ShippingERP.Application', 'DTOs')
ENUM_DIR = os.path.join(ROOT, 'src', 'ShippingERP.Domain', 'Enums')
OUT = os.path.join(HERE, '..', 'src', 'api', 'generated.ts')
PERMISSIONS_CS = os.path.join(ROOT, 'src', 'ShippingERP.Domain', 'Authorization', 'Permissions.cs')
PERMISSIONS_OUT = os.path.join(HERE, '..', 'src', 'auth', 'permissions.generated.ts')

PRIMITIVES = {
    'string': 'string', 'char': 'string', 'bool': 'boolean',
    'byte': 'number', 'sbyte': 'number', 'short': 'number', 'ushort': 'number', 'int': 'number', 'uint': 'number',
    'long': 'number', 'ulong': 'number', 'decimal': 'number', 'double': 'number', 'float': 'number',
    'DateTime': 'string', 'DateTimeOffset': 'string', 'DateOnly': 'string', 'TimeOnly': 'string', 'TimeSpan': 'string',
    'Guid': 'string', 'object': 'unknown', 'dynamic': 'unknown', 'JsonElement': 'unknown',
}
LIST_TYPES = {'List', 'IList', 'IReadOnlyList', 'IEnumerable', 'ICollection', 'IReadOnlyCollection', 'HashSet', 'ISet'}
DICT_TYPES = {'Dictionary', 'IDictionary', 'IReadOnlyDictionary'}
HANDWRITTEN = {'PagedResult': 'PagedResult'}


def strip_comments(text):
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
    return re.sub(r'//[^\n]*', '', text)


def camel(name):
    """Same algorithm as System.Text.Json JsonNamingPolicy.CamelCase ("CODAmount" -> "codAmount", "IsCOD" -> "isCOD")."""
    if not name or not name[0].isupper():
        return name
    chars = list(name)
    for i in range(len(chars)):
        if i == 1 and not chars[i].isupper():
            break
        has_next = i + 1 < len(chars)
        if i > 0 and has_next and not chars[i + 1].isupper():
            if chars[i + 1] == ' ':
                chars[i] = chars[i].lower()
            break
        chars[i] = chars[i].lower()
    return ''.join(chars)


def split_generic_args(text):
    depth, current, parts = 0, '', []
    for ch in text:
        if ch == '<':
            depth += 1
        elif ch == '>':
            depth -= 1
        if ch == ',' and depth == 0:
            parts.append(current.strip())
            current = ''
        else:
            current += ch
    if current.strip():
        parts.append(current.strip())
    return parts


def ts_type(cs, known, generic_params=()):
    cs = cs.strip()
    nullable = cs.endswith('?')
    if nullable:
        cs = cs[:-1].strip()
    if cs.endswith('[]'):
        inner, _ = ts_type(cs[:-2], known, generic_params)
        result = f'{wrap(inner)}[]'
    else:
        m = re.match(r'^([\w.]+)\s*<(.*)>$', cs)
        if m:
            name = m.group(1).split('.')[-1]
            args = split_generic_args(m.group(2))
            if name in LIST_TYPES:
                result = f'{wrap(ts_type(args[0], known, generic_params)[0])}[]'
            elif name in DICT_TYPES:
                result = f'Record<string, {ts_type(args[1], known, generic_params)[0]}>'
            elif name == 'Nullable':
                return ts_type(args[0], known, generic_params)[0], True
            else:
                mapped = HANDWRITTEN.get(name, name if name in known else 'unknown')
                if mapped == 'unknown':
                    result = 'unknown'
                else:
                    result = mapped + '<' + ', '.join(ts_type(a, known, generic_params)[0] for a in args) + '>'
        else:
            name = cs.split('.')[-1]
            if name in PRIMITIVES:
                result = PRIMITIVES[name]
            elif name in generic_params:
                result = name
            elif name in known or name in HANDWRITTEN:
                result = HANDWRITTEN.get(name, name)
            else:
                result = 'unknown'
    return result, nullable


def wrap(t):
    return f'({t})' if '|' in t else t


def is_request(name):
    return re.search(r'(Request|Query|Command|Filter)$', name) is not None


def parse_files():
    enums, classes = {}, {}
    files = []
    for folder in (ENUM_DIR, DTO_DIR):
        for dirpath, _, names in os.walk(folder):
            for n in sorted(names):
                if n.endswith('.cs'):
                    files.append(os.path.join(dirpath, n))
    for path in sorted(files):
        text = strip_comments(open(path, encoding='utf-8-sig').read())
        for m in re.finditer(r'public\s+enum\s+(\w+)\s*(?::\s*\w+)?\s*\{(.*?)\}', text, re.S):
            members = [re.split(r'\s*=', x.strip())[0].strip() for x in m.group(2).split(',')]
            members = [x for x in members if x and re.match(r'^\w+$', x)]
            enums[m.group(1)] = members
        # positional records
        for m in re.finditer(r'public\s+(?:sealed\s+)?record\s+(\w+)\s*\(([^)]*)\)\s*(?::[^;{]*)?\s*[;{]', text, re.S):
            params = []
            for p in split_generic_args(m.group(2)):
                p = re.sub(r'\[[^\]]*\]\s*', '', p).strip()
                p = p.split('=')[0].strip()
                pm = re.match(r'^(.+?)\s+(\w+)$', p)
                if pm:
                    params.append((pm.group(1), pm.group(2), False))
            classes.setdefault(m.group(1), {'base': None, 'props': params, 'generic': (), 'file': path})
        # classes / records with bodies
        for m in re.finditer(r'public\s+((?:sealed|abstract|static|partial)\s+)*(class|record)\s+(\w+)(\s*<[^>]+>)?\s*(?::\s*([^{]+?))?\s*\{', text):
            modifiers = m.group(1) or ''
            name = m.group(3)
            if 'static' in modifiers:
                continue
            generic = tuple(x.strip() for x in m.group(4).strip()[1:-1].split(',')) if m.group(4) else ()
            base = None
            if m.group(5):
                first = m.group(5).split(',')[0].strip()
                first = re.sub(r'<.*>', '', first)
                if not first.startswith('I') or not first[1:2].isupper():
                    base = first
            body, depth, i = '', 0, m.end() - 1
            while i < len(text):
                ch = text[i]
                if ch == '{':
                    depth += 1
                elif ch == '}':
                    depth -= 1
                    if depth == 0:
                        break
                body += ch
                i += 1
            body = body[1:]
            props = []
            # only top-level members of this body
            level, top = 0, ''
            for ch in body:
                if ch == '{':
                    level += 1
                    top += ch if level == 1 else ''
                    continue
                if ch == '}':
                    level -= 1
                    top += ch if level == 0 else ''
                    continue
                if level == 0:
                    top += ch
            for pm in re.finditer(r'public\s+(?!static|const|override\s+string)(?:required\s+|virtual\s+|new\s+)*([\w.<>,\[\]?\s]+?)\s+(\w+)\s*(\{\}|=>)', top):
                ptype, pname, kind = pm.group(1).strip(), pm.group(2), pm.group(3)
                if ptype in ('class', 'record', 'enum', 'void') or '(' in ptype:
                    continue
                props.append((ptype, pname, kind == '=>'))
            if name in classes and classes[name]['props'] and not props:
                continue
            classes[name] = {'base': base, 'props': props, 'generic': generic, 'file': path}
    return enums, classes


def main():
    enums, classes = parse_files()
    known = set(enums) | set(classes)
    lines = [
        '/* eslint-disable */',
        '// AUTO-GENERATED by scripts/generate-api-types.py from the C# DTOs and enums. Do not edit by hand.',
        "import type { PagedResult } from './types';",
        '',
    ]
    for name in sorted(enums):
        members = enums[name]
        if not members:
            continue
        lines.append(f'export type {name} = ' + ' | '.join(f"'{x}'" for x in members) + ';')
        lines.append(f'export const {name}Values = [' + ', '.join(f"'{x}'" for x in members) + '] as const;')
        lines.append('')
    for name in sorted(classes):
        if name in HANDWRITTEN:
            continue
        info = classes[name]
        generic = info['generic']
        header = f'export interface {name}' + (f"<{', '.join(generic)}>" if generic else '')
        base = info['base']
        if base and base in classes and base not in HANDWRITTEN:
            header += f' extends {base}'
        optional_all = is_request(name)
        lines.append(header + ' {')
        seen = set()
        for ptype, pname, computed in info['props']:
            key = camel(pname)
            if key in seen:
                continue
            seen.add(key)
            t, nullable = ts_type(ptype, known, generic)
            if optional_all:
                lines.append(f'  {key}?: {t}' + (' | null' if nullable else '') + ';')
            elif computed:
                lines.append(f'  readonly {key}: {t}' + (' | null' if nullable else '') + ';')
            else:
                lines.append(f'  {key}: {t}' + (' | null' if nullable else '') + ';')
        lines.append('}')
        lines.append('')
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines).rstrip() + '\n')
    print(f'{len(enums)} enums, {len(classes)} types -> {os.path.relpath(OUT, os.getcwd())}')
    write_permissions()


def write_permissions():
    """Permissions.cs (nested static classes of const strings) -> a typed object of permission codes."""
    text = strip_comments(open(PERMISSIONS_CS, encoding='utf-8-sig').read())
    groups = []
    for m in re.finditer(r'public\s+static\s+class\s+(\w+)\s*\{([^{}]*)\}', text):
        consts = re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"', m.group(2))
        if consts:
            groups.append((m.group(1), consts))
    lines = [
        '// AUTO-GENERATED by scripts/generate-api-types.py from ShippingERP.Domain/Authorization/Permissions.cs.',
        'export const Permissions = {'
    ]
    for name, consts in groups:
        lines.append(f'  {name}: {{')
        for const, code in consts:
            lines.append(f"    {const}: '{code}',")
        lines.append('  },')
    lines.append('} as const;')
    lines.append('')
    lines.append('type Values<T> = T[keyof T];')
    lines.append('export type PermissionCode = Values<{ [K in keyof typeof Permissions]: Values<(typeof Permissions)[K]> }>;')
    with open(PERMISSIONS_OUT, 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print(f'{sum(len(c) for _, c in groups)} permissions -> {os.path.relpath(PERMISSIONS_OUT, os.getcwd())}')


if __name__ == '__main__':
    sys.exit(main())
