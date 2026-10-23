# 사용자 동작 기반 진단

앱 상단 **문의 및 버그제보**에서 이메일·랜메신저 문의처와 현재 로그 경로를 확인합니다.
문제가 발생한 시각, 직전에 누른 버튼, 기대한 동작, 실제 결과와 함께 로그 폴더의 파일을 보내 주세요.
앱이 실행 중이어도 파일을 복사할 수 있습니다. 자동 전송은 하지 않습니다.

## 위치와 보관

- `%APPDATA%\TeamWeeklyMeeting\logs\meeting.log`
- 약 5 MiB가 되면 `meeting.log.1`~`.4`로 순환합니다. 최신 파일 포함 5개를 보관합니다.
- UTF-8 JSON Lines 형식입니다. 한 줄이 한 사건이며, 실행 식별자 `run`으로 앱을 다시 실행한 기록을 구분합니다.
- `at`은 한국 시간(+09:00), 시작 기록에는 앱 버전·프로세스 번호·OS·.NET 버전이 있습니다.

## 분석 순서

1. 사용자가 알려 준 시각의 `run`을 찾고 `action.begin`에서 실행한 버튼과 당시 상태를 확인합니다.
2. `team.select`, `attendee.*`, `presenter.select`, `view.zoom`으로 선택·참석자·발표자·배율 변화를 확인합니다.
3. `browser.navigation-start/end`의 페이지 경로·탐색 번호·성공 여부·WebErrorStatus와 `report.opened/failed`를 대조합니다.
4. `presentation.start/key/end`, `browser.input`으로 보고 모드 전환, 키 이동, 드래그·휠 이후 스크롤 위치를 재구성합니다.
   `presentation.annotation`은 이동·펜·형광펜·지우개 모드와 획 수, 되돌리기·전체 지우기 동작을 기록합니다. 실제 그림 좌표나 그림 내용은 기록하지 않습니다.
5. `action.failed`, `browser.process-failed`, `app.*-failed`, `app.unhandled`, `app.fatal`의 예외 종류·메시지·스택을 확인합니다.
6. 조직도 문제는 `directory.page`의 웹페이지 추출 건수와 `directory.loaded`의 조직 부모·순서·구성원 소속을 확인합니다. `directory.page-failed`는 로그인 폼·주간 메뉴 누락·빈 메뉴를 구분합니다. DB에는 접속하지 않습니다.

`action.end`에는 소요 시간과 최종 상태가 있습니다. 설정 읽기·저장 오류, 로그인 취소, 문의창 열기·복사·로그 폴더 열기도 기록합니다.

로그인 지연은 `startup.environment-ready`, `startup.browser-ready`, `auth.session-cleared`의 `elapsedMs`로 앱 내부 준비 시간을 나눕니다. `auth.window-shown` 뒤 `auth.page-ready`까지는 로그인창의 준비 구간이며 후자에 소요 시간을 기록합니다. 사이트 탐색 시간은 같은 `NavigationId`의 시작·완료 시간차입니다. 계정 입력과 경고창 확인 시간은 사이트 지연에 합산하지 마세요. `auth.auto-failed`의 `not-submitted`는 사이트 함수가 로그인 요청을 제출하지 않아 즉시 수동 전환한 경우입니다.

자동로그인은 `auth.auto-start/success/failed/skipped`, 저장·해제는 `auth.remember-saved/disabled`, 파일 오류는 `auth.storage-failed`, 로그아웃은 `auth.logout-start/complete`로 확인합니다. `browser.navigation-end`의 HTTP 상태와 WebErrorStatus로 인증 거부와 연결 오류를 구분합니다. 상세 원인 코드는 [Wiki](../docs/wiki/Troubleshooting.md)에 있습니다. 인증 파일 `autologin.dat`는 문의 자료에 포함하지 마세요.
휠 입력은 묶어서 기록하여 과도한 로그를 줄입니다. 로그 경로가 기록된 폴더에서 전체 파일을 받으면 회전 직전의 동작도 분석할 수 있습니다.

## 기록 범위

계정 ID, 사용자·조직명, 팀 및 참석 선택, 보고 기간·글 번호, 배율, 탐색 경로, 스크롤 위치와 오류를 기록합니다.
비밀번호·쿠키·세션 토큰·보고서 본문은 기록하지 않습니다. 페이지 URL의 쿼리 문자열도 남기지 않습니다.
사내 사용자·조직 정보와 접속 정보가 있으므로 로그는 사내에서만 공유합니다.
