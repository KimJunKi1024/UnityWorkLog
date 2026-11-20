# 변경 기록

## [1.2.0] - 2026-11-20
- 환경 확인: 에디터를 켤 때 Python 3.8 이상과 markdown 패키지가 있는지 확인하고, 없으면 안내 창을 띄움 ([설치] [나중에] [다시 묻지 않기])
  - Python이 없으면 winget으로 새 창에서 설치 (winget이 없으면 다운로드 페이지)
  - markdown이 없으면 `python -m pip install markdown`을 실행하고 결과를 알림 (실패하면 `--user`로 다시 시도)
- 메뉴 Tools → 작업 기록 → 환경 확인 (언제든 직접 확인)
- Python 자동 탐색이 표준 설치 위치(`%LOCALAPPDATA%\Programs\Python\Python3*` 등)도 봄. 설치 직후 Unity를 다시 켜지 않아도 찾음
- Python 실행 실패 안내에 환경 확인 메뉴를 함께 적음

## [1.1.0] - 2026-11-12
- 새 버전 알림: Git 주소로 설치한 경우, 에디터를 켤 때 하루 한 번 저장소의 `vX.Y.Z` 태그를 확인해 새 버전이 있으면 콘솔에 남기고 알림 창을 띄움 ([업데이트] [이 버전 건너뛰기] [나중에])
- 메뉴 Tools → 작업 기록 → 업데이트 확인 (언제든 직접 확인)

## [1.0.0] - 2026-11-12
- 첫 공개: 기록 창(Tools → 작업 기록 → 기록 창), 설정 화면(Tools → 작업 기록 → 설정), 파이썬 도구 `worklog_add.py`
- 엘라린 프로젝트의 `add_worklog.py`를 일반화
  - MD·HTML 경로, 항목 폴더, 기록 제목을 설정으로 (`ProjectSettings/WorkLogSettings.json`)
  - 작업 기록 폴더는 PC별 설정 (`UserSettings/WorkLogLocal.json`) 또는 드라이브 자동 탐색
  - MD·HTML이 없으면 빈 틀로 새로 만듦, `--next`(다음 번호), `--init`(빈 파일만 만들기), 번호만으로 항목 지정
  - 이미 있는 기록에 추가한 결과는 예전 도구와 같음 (CRLF MD의 목차 줄바꿈만 바르게 고침)
