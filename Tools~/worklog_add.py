# -*- coding: utf-8 -*-
"""
작업 기록 추가 도구 (MD + HTML 동시 갱신) — Unity 패키지 com.teamelarin.worklog

사용법:
    python worklog_add.py <항목.md 또는 번호> [...] [--project <Unity 프로젝트>] [--root "<작업 기록 폴더>"] [--dry-run]
    python worklog_add.py --next [--json]     # 작업 기록 위치와 다음 번호
    python worklog_add.py --init              # MD·HTML이 없으면 빈 파일을 만듦

설정:
    ProjectSettings/WorkLogSettings.json (프로젝트 공용, Git에 올림)
        entriesDir  항목(entryNNN.md) 폴더, 프로젝트 기준        (기본 WorkLog/entries)
        mdPath      작업 기록 폴더 기준 MD 경로                  (기본 MD/<프로젝트 폴더 이름>_작업기록.md)
        htmlPath    작업 기록 폴더 기준 HTML 경로                (기본 HTML/<프로젝트 폴더 이름>_작업기록.html)
        title       기록 제목 (새 MD·HTML 머리글)                (기본 프로젝트 폴더 이름)
    UserSettings/WorkLogLocal.json (이 PC만, Git에 올리지 않음)
        root        작업 기록 폴더. 비우면 자동 탐색

작업 기록 폴더 찾는 순서: --root → 환경 변수 WORKLOG_ROOT → UserSettings/WorkLogLocal.json → 드라이브 자동 탐색
(D:~Z: 의 "내 드라이브\\작업 기록" 또는 "My Drive\\작업 기록")

항목 파일 형식 (한 파일에 항목 하나):
    ### 158. 제목 (2026-10-27)
    - **요청:** ...
    - **변경:** ...
      - 하위 항목은 공백 2칸 + "- "

규칙:
    - 제목 줄 괄호 안에는 날짜만 (예: 2026-10-27 또는 2026-10-24~25).
    - 글머리는 반드시 "- **라벨:** 내용" (콜론은 굵게 안쪽).
    - 번호는 MD의 마지막 번호 + 1 이어야 함 (아니면 중단).
    - 해당 날짜 탭이 HTML에 없으면 새 탭을 맨 앞에 만듦.
    - MD·HTML이 없으면 빈 틀로 새로 만듦.
    - 바꾸기 전 MD·HTML을 <root>/_backup/ 에 시간 이름으로 복사.

필요: Python 3.8+, `pip install markdown`
"""
import argparse, datetime, json, os, re, shutil, sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

try:
    import markdown
except ImportError:
    sys.exit("markdown 패키지가 필요합니다: pip install markdown")

HERE = os.path.dirname(os.path.abspath(__file__))
TEMPLATE_HTML = os.path.join(HERE, "worklog_template.html")
SETTINGS_REL = os.path.join("ProjectSettings", "WorkLogSettings.json")
LOCAL_REL = os.path.join("UserSettings", "WorkLogLocal.json")
HEAD_RE = re.compile(r"### (\d+)\. (.+) \((\d{4}-\d{2}-[0-9~]+)\)\n")
DRIVE_FOLDERS = ("내 드라이브", "My Drive")


# ───────────── 설정 ─────────────

def find_project(cli_project):
    if cli_project:
        return os.path.abspath(cli_project)
    d = os.getcwd()
    while True:
        if os.path.isdir(os.path.join(d, "ProjectSettings")) and os.path.isdir(os.path.join(d, "Assets")):
            return d
        parent = os.path.dirname(d)
        if parent == d:
            return os.getcwd()
        d = parent


def read_json(path):
    if not os.path.isfile(path):
        return {}
    try:
        with open(path, encoding="utf-8-sig") as f:
            data = json.load(f)
        return data if isinstance(data, dict) else {}
    except (OSError, ValueError) as e:
        sys.exit(f"설정 파일을 읽지 못했습니다({path}): {e}")


def clean(v):
    return str(v or "").strip().strip('"').strip()


def load_settings(project):
    name = os.path.basename(os.path.normpath(project))
    s = read_json(os.path.join(project, SETTINGS_REL))
    local = read_json(os.path.join(project, LOCAL_REL))
    return dict(
        entriesDir=clean(s.get("entriesDir")) or "WorkLog/entries",
        mdPath=clean(s.get("mdPath")) or f"MD/{name}_작업기록.md",
        htmlPath=clean(s.get("htmlPath")) or f"HTML/{name}_작업기록.html",
        title=clean(s.get("title")) or name,
        root=clean(local.get("root")),
    )


def auto_root():
    for d in "GHIJKLMNOPQRSTUVWXYZDEF":
        for folder in DRIVE_FOLDERS:
            p = f"{d}:\\{folder}\\작업 기록"
            if os.path.isdir(p):
                return p
    return ""


def find_root(cli_root, settings):
    for v in (cli_root, os.environ.get("WORKLOG_ROOT"), settings["root"], auto_root()):
        v = clean(v)
        if v:
            if not os.path.isdir(v):
                sys.exit(f"작업 기록 폴더가 없습니다: {v}")
            return v
    sys.exit("작업 기록 폴더를 찾지 못했습니다. --root, 환경 변수 WORKLOG_ROOT, 또는 설정(Tools → 작업 기록 → 설정)의 작업 기록 폴더를 지정하세요.")


# ───────────── 빈 파일 틀 ─────────────

def new_md(title, nl):
    today = datetime.date.today().isoformat()
    lines = [
        f"# {title} 작업 기록",
        "",
        "> 이 파일 하나에 프로젝트 작업 기록을 모두 정리하고, 새 작업은 **작업 기록** 끝에 이어서 추가합니다.",
        f"> 최종 갱신: {today}",
        "",
        "## 목차",
        "",
        "---",
        "",
        "## 작업 기록",
        "",
        "---",
        "",
        "## 부록",
        "",
    ]
    return nl.join(lines)


def new_html(title, md_rel):
    t = open(TEMPLATE_HTML, encoding="utf-8", newline="").read()
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    source = " › ".join(["작업 기록"] + [p for p in re.split(r"[\\/]", md_rel) if p])
    return t.replace("__TITLE__", title).replace("__NOW__", now).replace("__SOURCE__", source)


# ───────────── 항목 → MD·HTML ─────────────

def inline(s):
    h = markdown.markdown(s.strip())
    return re.sub(r"^<p>(.*)</p>$", r"\1", h, flags=re.S)


def block(md):
    md = re.sub(r"^((?:  )+)(?=[-\d])", lambda m: "    " * (len(m.group(1)) // 2), md, flags=re.M)
    return markdown.markdown(md, extensions=["tables", "fenced_code"])


def slug(num, title, date):
    a = re.sub(r"[^a-z0-9]+", "-", re.sub(r"`", "", title).lower()).strip("-")
    a = re.sub(r"-+", "-", a)
    d = date.replace("-", "").replace("~", "")
    return f"d{d}-{num}-{a}-{date.replace('~', '-')}".replace("--", "-")


def resolve_entry(arg, project, settings):
    if re.fullmatch(r"\d+", arg):
        return os.path.normpath(os.path.join(project, settings["entriesDir"], f"entry{arg}.md"))
    if os.path.isfile(arg):
        return arg
    p = os.path.join(project, arg)
    return p if os.path.isfile(p) else arg


def parse_entry(path):
    if not os.path.isfile(path):
        sys.exit(f"항목 파일이 없습니다: {path}")
    text = open(path, encoding="utf-8-sig").read().replace("\r\n", "\n").strip()
    m = HEAD_RE.match(text + "\n")
    if not m:
        sys.exit(f"{path}: 첫 줄은 '### 번호. 제목 (날짜)' 형식이어야 합니다.")
    return dict(num=int(m.group(1)), title=m.group(2), date=m.group(3),
                body=(text + "\n")[m.end():].rstrip("\n"), md=text)


def entry_html(e):
    items, cur = [], None
    for ln in e["body"].split("\n"):
        mm = re.match(r"- \*\*(.+?):\*\*\s?(.*)", ln)
        if mm:
            cur = [mm.group(1), mm.group(2), []]
            items.append(cur)
        elif ln.startswith("  ") and cur is not None:
            cur[2].append(ln[2:])
        elif ln.strip():
            sys.exit(f"{e['num']}번: 형식이 맞지 않는 줄 → {ln!r} ('- **라벨:** 내용' 또는 들여쓴 하위 항목만 허용)")
    out = ['<ul class="has-kv">']
    for lab, rest, sub in items:
        body = (" " + inline(rest)) if rest.strip() else ""
        if sub:
            body += block("\n".join(sub))
        out.append(f'<li class="kv"><span class="kv-label">{inline(lab)}</span><div class="kv-body">{body}</div></li>')
    out.append("</ul>")
    e["id"] = slug(e["num"], e["title"], e["date"])
    return (f'<section class="card" id="{e["id"]}"><h2><span class="num">{e["num"]}</span>'
            f'<span class="ttl">{inline(e["title"])}</span><span class="date">{e["date"]}</span></h2>\n'
            + "\n".join(out) + "\n</section>")


def tab_key(date):
    return "log-" + date.split("~")[0]


def md_last(md):
    nums = [int(x) for x in re.findall(r"^### (\d+)\.", md, flags=re.M)]
    return max(nums) if nums else 0


def add_to_md(md, e, nl):
    nums = [int(x) for x in re.findall(r"^### (\d+)\.", md, flags=re.M)]
    last = max(nums) if nums else 0
    if e["num"] in nums:
        sys.exit(f"{e['num']}번은 이미 MD에 있습니다.")
    if e["num"] != last + 1:
        sys.exit(f"번호가 이어지지 않습니다: 마지막 {last}번, 추가하려는 {e['num']}번")
    if last:
        toc = re.search(rf"^- {last}\. [^\r\n]*", md, flags=re.M)
        if not toc:
            sys.exit(f"MD 목차에서 {last}번 줄을 찾지 못했습니다.")
    else:
        toc = re.search(r"^## 목차[ \t]*(?=\r?$)", md, flags=re.M)
        if not toc:
            sys.exit("MD에서 '## 목차' 줄을 찾지 못했습니다.")
    head = e["md"].split("\n", 1)[0][4:]
    md = md[:toc.end()] + nl + "- " + head + md[toc.end():]
    anchor = nl + "---" + nl + nl + "## 부록" + nl
    ai = md.find(anchor)
    if ai < 0:
        sys.exit("MD에서 '## 부록' 앞 구분선을 찾지 못했습니다.")
    body = e["md"].replace("\n", nl)
    md = md[:ai] + nl + "---" + nl + nl + body + nl + md[ai:]
    return md


def add_to_html(h, e):
    sec = entry_html(e)
    if f'id="{e["id"]}"' in h:
        sys.exit(f"{e['num']}번은 이미 HTML에 있습니다.")
    key = tab_key(e["date"])
    toc_li = (f'<li><a href="#{e["id"]}"><span class="tnum">{e["num"]}</span>'
              f'<span class="ttxt">{inline(e["title"])}</span></a></li>')
    pid = f'id="panel-{key}"'
    if pid not in h:
        # 새 날짜 탭 + 패널을 맨 앞에 만듦
        tabs_tag = '<div class="tabs" role="tablist">'
        if tabs_tag not in h:
            sys.exit("HTML에서 탭 목록(<div class=\"tabs\" role=\"tablist\">)을 찾지 못했습니다.")
        btn = (f'<button class="tab" role="tab" type="button" data-tab="{key}" aria-controls="panel-{key}">'
               f'<b>{e["date"]} 작업</b><small>{e["num"]}~{e["num"]}번 · 0개 항목</small></button>')
        t = h.index(tabs_tag) + len(tabs_tag)
        h = h[:t] + btn + h[t:]
        panel = (f'<div class="panel-wrap" data-panel="{key}" id="panel-{key}" role="tabpanel">'
                 f'<nav class="toc" aria-label="목차"><div class="label">목차</div><ol class="toc-list"></ol></nav>'
                 f'<main></main></div>\n  ')
        if '<div class="panel-wrap"' in h:
            t = h.index('<div class="panel-wrap"')
        else:
            # 빈 기록: 탭 목록 닫는 태그 뒤
            t = h.index("</div>", h.index(tabs_tag)) + len("</div>")
            panel = "\n  " + panel.rstrip()
        h = h[:t] + panel + h[t:]
    p = h.index(pid)
    t = h.index("</ol></nav>", p)
    h = h[:t] + toc_li + h[t:]
    m = h.index("</main></div>", p)
    h = h[:m] + sec + h[m:]
    # 탭 요약(번호 범위 · 항목 수) 갱신
    btn_re = re.compile(rf'(aria-controls="panel-{re.escape(key)}"><b>[^<]*</b><small>)(\d+)~(\d+)번 · (\d+)개 항목(</small>)')
    bm = btn_re.search(h)
    if bm:
        first = min(int(bm.group(2)), e["num"])
        h = h[:bm.start()] + f"{bm.group(1)}{first}~{e['num']}번 · {int(bm.group(4)) + 1}개 항목{bm.group(5)}" + h[bm.end():]
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    h = re.sub(r"마지막 갱신 \d{4}-\d{2}-\d{2} \d\d:\d\d", f"마지막 갱신 {now}", h, count=1)
    return h


# ───────────── 실행 ─────────────

def read_or_new(md_p, h_p, title, md_rel):
    md = open(md_p, encoding="utf-8", newline="").read() if os.path.isfile(md_p) else None
    h = open(h_p, encoding="utf-8", newline="").read() if os.path.isfile(h_p) else None
    nl = "\r\n" if md is not None and "\r\n" in md else "\n"
    if md is None:
        md = new_md(title, nl)
    if h is None:
        h = new_html(title, md_rel)
    return md, h, nl


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def main():
    ap = argparse.ArgumentParser(description="작업 기록 MD·HTML에 항목을 추가합니다.")
    ap.add_argument("entries", nargs="*", help="항목 파일 경로 또는 번호(항목 폴더의 entryNNN.md)")
    ap.add_argument("--project", help="Unity 프로젝트 폴더 (기본: 현재 폴더에서 위로 찾음)")
    ap.add_argument("--root", help="작업 기록 폴더 (MD·HTML 상위)")
    ap.add_argument("--dry-run", action="store_true", help="형식·번호 검사만")
    ap.add_argument("--next", action="store_true", help="작업 기록 위치와 다음 번호 출력")
    ap.add_argument("--init", action="store_true", help="MD·HTML이 없으면 빈 파일을 만듦")
    ap.add_argument("--json", action="store_true", help="--next 결과를 JSON으로")
    a = ap.parse_args()

    project = find_project(a.project)
    st = load_settings(project)
    root = find_root(a.root, st)
    md_p, h_p = os.path.normpath(os.path.join(root, st["mdPath"])), os.path.normpath(os.path.join(root, st["htmlPath"]))
    md_exists, h_exists = os.path.isfile(md_p), os.path.isfile(h_p)
    md, h, nl = read_or_new(md_p, h_p, st["title"], st["mdPath"])

    if a.next:
        last = md_last(md)
        info = dict(project=project, root=root, md=md_p, html=h_p, mdExists=md_exists, htmlExists=h_exists,
                    entriesDir=os.path.normpath(os.path.join(project, st["entriesDir"])), title=st["title"], last=last, next=last + 1)
        if a.json:
            print(json.dumps(info, ensure_ascii=False))
        else:
            print(f"작업 기록 폴더: {root}\nMD: {md_p}{'' if md_exists else ' (없음, 처음 추가할 때 만듦)'}\n"
                  f"HTML: {h_p}{'' if h_exists else ' (없음, 처음 추가할 때 만듦)'}\n마지막 번호: {last}, 다음 번호: {last + 1}")
        return

    if a.init:
        made = []
        if not md_exists:
            write(md_p, md); made.append(md_p)
        if not h_exists:
            write(h_p, h); made.append(h_p)
        print("새로 만듦: " + ", ".join(made) if made else "MD·HTML이 이미 있습니다.")
        if not a.entries:
            return

    if not a.entries:
        ap.error("추가할 항목(파일 경로 또는 번호)을 지정하세요.")

    entries = sorted((parse_entry(resolve_entry(p, project, st)) for p in a.entries), key=lambda x: x["num"])
    for e in entries:
        md = add_to_md(md, e, nl)
        h = add_to_html(h, e)
    md = re.sub(r"^> 최종 갱신: \d{4}-\d{2}-\d{2}", f"> 최종 갱신: {entries[-1]['date'].split('~')[0]}", md, count=1, flags=re.M)
    if a.dry_run:
        extra = "" if md_exists and h_exists else " (MD·HTML이 없어 실제 추가 때 새로 만듦)"
        print("dry-run: 문제 없음 →", ", ".join(str(e["num"]) for e in entries) + extra)
        return
    bk = os.path.join(root, "_backup")
    os.makedirs(bk, exist_ok=True)
    stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    if md_exists:
        shutil.copy2(md_p, os.path.join(bk, f"{os.path.splitext(os.path.basename(md_p))[0]}_{stamp}.md"))
    if h_exists:
        shutil.copy2(h_p, os.path.join(bk, f"{os.path.splitext(os.path.basename(h_p))[0]}_{stamp}.html"))
    write(md_p, md)
    write(h_p, h)
    md2 = open(md_p, encoding="utf-8").read()
    h2 = open(h_p, encoding="utf-8").read()
    for e in entries:
        c1 = len(re.findall(rf"^### {e['num']}\.", md2, flags=re.M))
        c2 = h2.count(f'<span class="num">{e["num"]}</span>')
        print(f"{e['num']}번: MD {c1}개, HTML {c2}개" + ("" if c1 == c2 == 1 else "  ← 확인 필요"))


if __name__ == "__main__":
    main()
