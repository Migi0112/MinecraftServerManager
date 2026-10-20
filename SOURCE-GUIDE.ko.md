# 소스 코드 읽는 순서

이 프로젝트는 C#/.NET 8 WinForms 프로그램입니다. 화면을 마우스로 배치하는 디자이너 대신 C# 코드로 컨트롤을 생성하고 배치합니다.
각 C# 파일 맨 위의 `[소스 안내]`는 파일의 역할, 주요 함수 앞의 `[동작 설명]`은 입력부터 결과까지의 목적을 설명합니다.

## 처음 열 때

Visual Studio에서는 `NeoForgeServerManager.csproj`를 열고, VS Code에서는 이 폴더를 엽니다.
코드 읽기만 할 때는 빌드할 필요가 없습니다. 실행 파일을 새로 만들려면 .NET 8 SDK와 프로젝트 의존성이 필요합니다.

## 먼저 읽을 파일

1. `Program.cs`: 프로그램이 어디서 시작되는지 확인합니다.
2. `HomeScreen.cs`: 사용자가 처음 보는 서버목록입니다. **HomeForm.cs는 이름과 달리 새 서버 만들기 코드**이므로 혼동하지 마세요.
3. `MainForm.cs`: 관리 화면에서 버튼을 눌렀을 때 어떤 서비스를 호출하는지 봅니다.
4. `ServerService.cs`: 서버 실행·로그·명령 전송이 실제로 어떻게 동작하는지 봅니다.
5. `Profile.cs`: 서버 경로와 설정이 어떤 형태로 저장되는지 봅니다.

## 기능별 실행 흐름

- 서버 만들기: `HomeScreen` → `CreateServerWizard` (`HomeForm.cs`, `ApprovedWizard.cs`) → `InstallerService`의 로더별 어댑터 → 서버 등록 정보 저장.
- 모드팩 서버 만들기: `CreateServerWizard.OpenModpack` → `ModpackPage` 검색·버전 선택 → `ModpackService` SHA512 검증·서버용 파일 설치·공식 로더 설치 → 대표 이미지 저장 → `ServerRegistrations.Register`. 취소/실패한 임시 폴더는 등록하지 않습니다.
- 서버 시작: `MainForm.StartAsync` → `ServerService.Start` → Java/run.bat 실행 → 출력 읽기/해석 → 관리 화면 상태 표시.
- 모드 검색/설치: `ExtensionNavigation` → `ManagementExtensions` → `ModPluginService` 검색 → `ExtensionDependencies.Plan` → 사용자 확인 → `Install`.
- 설치된 모드 의존성: `PlayerManagement` 목록 → `DetailWindows.ShowModDetails` → 파일 해시 조회 또는 `LocalModMetadata` 읽기 → 의존성 계획/선택 설치.
- 업데이트: `AppUpdates.Check` → 서명 확인 → 목록 표시 → 다운로드와 해시 확인 → 설치 프로그램 실행.
- 디자인 수정: `WarmTheme`(색상/공통 컨트롤), `DesignSystem`(창/카드), 해당 화면의 Layout 파일(배치)을 함께 봅니다.

## 자주 나오는 C# 표현

- `partial class MainForm`: 하나의 클래스가 여러 파일로 나뉘어 있다는 뜻입니다. MainForm.cs에 없는 함수가 다른 파일에 있어도 같은 화면의 일부일 수 있습니다.
- `async` / `await`: 다운로드 등 기다림이 필요한 작업을 화면이 멈추지 않게 연결합니다.
- `Click +=`: 버튼을 누를 때 실행할 코드를 연결합니다.
- `Task`: 나중에 완료되는 작업입니다. `Task.Run`은 오래 걸리는 일을 별도 작업으로 실행할 때 사용합니다.
- `using`: 파일·이미지·네트워크 자원을 사용한 뒤 해제합니다.
- `Profile`: 서버 등록 정보입니다. Minecraft 게임 프로필이나 실제 서버 폴더 그 자체가 아닙니다.
- `Plan`과 `Install`: 설치 내용 계산/검증과 실제 파일 변경을 분리한 것입니다.

## 수정 시 알아둘 점

실행/설치/삭제 코드에는 폴더 잠금, 경로 검사, 기존 파일 확인이 있습니다. 이것을 제거하면 실행 중인 서버나 다른 파일에 영향을 줄 수 있습니다.
테스트 코드는 `tests/`에 있습니다. 실제 서버 폴더 대신 별도 시험 폴더로 확인하세요.
프로그램 업데이트의 공개키는 Assets에 있지만 배포 서명 개인키는 소스 배포에 포함하지 않습니다.

## 파일별 기능

| 파일 | 역할 |
|---|---|
| `Program.cs` | 프로그램 시작점. WinForms 초기화와 글꼴 로딩을 마친 뒤 서버목록(HomeForm)을 엽니다. |
| `HomeScreen.cs` | 서버목록 화면(HomeForm). 서버 카드/자세히 보기, 검색, 새로고침 등록, 화면 이동과 실행 중인 관리 창을 연결합니다. |
| `HomeForm.cs` | 파일 이름과 달리 새 서버 만들기(CreateServerWizard)의 기본 동작입니다. 단계 이동, 입력 검증, 서버 설치와 등록을 담당합니다. |
| `ApprovedWizard.cs` | 새 서버 만들기의 단계별 화면 배치입니다. 서버 종류·버전·메모리·설정·이미지·최종 확인을 표시하고 공식 버전을 조회합니다. |
| `MainForm.cs` | 서버 한 대를 관리하는 화면의 중심입니다. 실행/종료, 명령 전송, 설정 저장, 백업과 상태 갱신을 각 서비스에 연결합니다. |
| `ServerService.cs` | 화면과 분리된 서버 실행 서비스입니다. Java 프로세스, 콘솔 입출력, 플레이어/TPS 상태와 정상·강제 종료를 관리합니다. |
| `ProcessTree.cs` | 실행한 프로세스의 자식 프로세스를 찾는 Windows 보조 코드입니다. 배치 파일 아래의 Java를 추적하는 데 사용합니다. |
| `Profile.cs` | 서버 한 대의 경로·버전·메모리 등 등록 정보와 JSON 저장 기능입니다. 실제 월드/JAR 파일과 등록 정보는 별도로 보관합니다. |
| `SupportServices.cs` | 프로필 저장소, 프로세스 자원 측정, Java 관련 조회 등 여러 화면이 공유하는 보조 서비스입니다. |
| `InstallerService.cs` | 공식 배포처 다운로드와 로더별 설치 어댑터입니다. Paper·Purpur·Fabric·Forge·NeoForge마다 버전 조회와 설치 절차를 구분합니다. |
| `ServerImportService.cs` | 기존 서버를 찾아 실행 방식과 버전을 분석하고 등록하는 기능입니다. 새 서버 설치 화면과는 별도 경로입니다. |
| `ServerDiscovery.cs` | 서버 저장 폴더를 검색하여 등록 가능한 서버를 찾습니다. 폴더의 실제 서버 파일을 확인하며 경로를 정규화합니다. |
| `ServerRegistrations.cs` | 한 폴더가 서버목록에 중복 표시되는 것을 막습니다. 표시할 대표 등록 정보 선택과 저장 시 중복 검사를 담당합니다. |
| `ServerFolders.cs` | 서버 폴더 이름과 경로를 다루는 공통 규칙입니다. 화면 이름을 실제 저장 폴더 경로에 연결할 때 사용합니다. |
| `HomePreferences.cs` | 메인 화면의 카드/자세히 보기 선택과 서버 표시 순서를 저장합니다. |
| `HomeDragReorder.cs` | 서버 카드/행의 핸들을 잡아 순서를 바꾸는 기능입니다. 드래그 중 위치 표시, 이동과 순서 저장을 처리합니다. |
| `Dashboard.cs` | 서버 관리 화면의 상태 표시와 탐색 구성을 담당합니다. 서버 실행 상태를 화면 표시로 연결합니다. |
| `ExecutionLayout.cs` | 실행 옵션 화면의 배치입니다. Java 경로, 메모리, 재시작 관련 입력 컨트롤을 정렬합니다. |
| `ServerInfoLayout.cs` | 서버 정보 화면을 구성하고 관련 정보와 작업 버튼을 배치합니다. |
| `Settings.cs` | 프로그램 설정 데이터와 설정 화면, 네트워크/방화벽 관련 작업을 담습니다. 서버 저장 폴더와 백업 폴더도 여기서 관리합니다. |
| `SettingsLayout.cs` | 설정 화면의 시각적 배치를 구성합니다. 실제 설정 값과 저장 동작은 Settings.cs 등의 코드와 연결됩니다. |
| `PropertiesFile.cs` | server.properties 파일을 읽고 수정하는 기본 파일 처리입니다. 서버 옵션의 키와 값을 다룹니다. |
| `PropertiesService.cs` | 서버 설정 항목의 정의와 편집기입니다. 항목별 입력 방식, 표시와 설정 값 수집을 담당합니다. |
| `PropertyHelpPopup.cs` | 서버 설정 옆 도움말을 보여주는 팝업입니다. 설정의 의미를 입력 화면 가까이에서 확인하게 합니다. |
| `PlayerManagement.cs` | 플레이어·권한 목록과 설치된 모드/플러그인 목록을 관리 화면에 연결합니다. 목록 갱신과 모드 대표 이미지 로딩도 포함합니다. |
| `PlayerHistoryData.cs` | 플레이어 기록과 저장된 플레이어 데이터를 읽는 계층입니다. 기록 화면에서 사용하는 정보로 변환합니다. |
| `PlayerHistoryPage.cs` | 접속자/기록 화면의 표시와 사용자 동작입니다. 저장된 플레이어 정보와 상세 보기 기능을 연결합니다. |
| `OnlinePlayerPicker.cs` | 명령어 대상 선택 드롭다운입니다. 접속 중인 이름을 검색·선택하고 퇴장한 대상은 선택에서 해제합니다. |
| `PlayerSkinView.cs` | 플레이어 스킨을 표시하는 화면 컨트롤입니다. 플레이어 상세 화면에서 외형을 확인할 때 사용합니다. |
| `InventoryView.cs` | 플레이어의 저장된 인벤토리와 아이템을 보여주는 화면입니다. 실시간 게임 인벤토리를 직접 수정하는 기능은 아닙니다. |
| `StoredItem.cs` | 저장된 아이템의 ID·개수·슬롯·NBT 정보 모델과 컨테이너 읽기 계약입니다. |
| `ContainerReaders.cs` | 셜커 상자/꾸러미와 Sophisticated Backpacks처럼 저장 구조가 다른 컨테이너의 내부 아이템을 읽습니다. |
| `ItemEnchantments.cs` | 아이템에 저장된 인챈트 정보를 읽고 표시용 이름/레벨로 다루는 코드입니다. |
| `ItemArtwork.cs` | 아이템 이미지를 불러오고 표시용으로 관리합니다. 목록에서 아이템을 그림으로 구분할 때 사용합니다. |
| `GiveItemCatalog.cs` | 아이템 지급용 검색 목록입니다. 게임 버전에 맞는 아이템과 한국어/영어 이름·이미지 정보를 불러옵니다. |
| `CommandPalette.cs` | 명령어 팔레트입니다. 명령 검색, 즐겨찾기/최근 기록, 아이템·플레이어 선택과 실행 전 명령문 생성을 담당합니다. |
| `RuntimeExtensions.cs` | 콘솔 표시와 실행 중 화면 동작을 보완하는 MainForm의 일부입니다. 로그 필터·원문 보존과 컨트롤 배치를 처리합니다. |
| `ConsoleLogFilter.cs` | 반복되는 차원/TPS 안내를 분류합니다. 경고·오류·채팅까지 숨기지 않도록 구분하는 필터입니다. |
| `Catalog.cs` | 모드/플러그인 및 플레이어 목록에 쓰는 카드·표 보기입니다. 선택 상태, 이미지, 행 동작과 공통 이미지 선택기도 포함합니다. |
| `ModPluginService.cs` | Modrinth 검색 결과와 공식 배포 파일을 다룹니다. 로더·게임 버전별 검색, 인기순 100개 조회와 설치 보조 기능입니다. |
| `ManagementExtensions.cs` | 온라인 모드/플러그인 검색 화면입니다. 인기 목록, 검색·페이지 이동, 설치 확인과 현재 스크롤 위치 보존을 연결합니다. |
| `ExtensionNavigation.cs` | 모드 관리 안에서 온라인 검색 화면을 여는 연결 코드입니다. 검색 종료 후 설치된 목록을 다시 표시합니다. |
| `ExtensionResultsList.cs` | 온라인 검색 목록의 그리기와 이미지 갱신을 조절합니다. 보이는 항목 중심으로 갱신하여 깜빡임과 불필요한 다시 그리기를 줄입니다. |
| `ExtensionEnvironmentSummary.cs` | 온라인 검색 상단에 배포처·로더·Minecraft 버전 조건을 표시하는 컨트롤입니다. |
| `ExtensionSearchNotice.cs` | 온라인 검색 실패 시 사용자에게 오류와 재시도 선택을 보여주는 안내 창입니다. |
| `InstalledExtensions.cs` | 설치된 JAR과 배포처 프로젝트를 연결합니다. 파일 해시 조회, 메타데이터 캐시, 대표 이미지와 설치 여부 확인을 담당합니다. |
| `LocalModMetadata.cs` | JAR 내부의 모드 ID·버전·필수 의존성을 읽습니다. Kotlin처럼 내부 JAR에 정보가 있는 경우와 버전 범위 검사도 처리합니다. |
| `JarBranding.cs` | JAR 안의 로고와 이름 등 표시 정보를 읽습니다. 외부 이미지 조회와 별개로 파일에 포함된 대표 이미지를 찾습니다. |
| `ExtensionDependencies.cs` | 필수 의존성의 설치 계획과 실행, 확인 목록 UI입니다. 재귀 의존성, 충돌, 기존 파일 변경, 해시·버전 검사 후 설치합니다. |
| `DetailWindows.cs` | 설치된 모드의 의존성 확인/선택 설치 창과 서버가 꺼져 있을 때의 운영자 기록 변경 기능입니다. |
| `ModArtwork.cs` | Modrinth 대표 이미지 다운로드·캐시입니다. 이미지 크기와 출처를 제한하고 표시용 크기로 변환합니다. |
| `ModEnvironment.cs` | 서버/클라이언트 설치 위치 배지와 의존성 선택 카드 디자인입니다. 코랄 체크박스·선택 배경과 미확인 정보 표시를 담당합니다. |
| `ComingSoonButton.cs` | 자동 모드 설치의 준비 중 버튼입니다. 클릭 실행을 막고 아직 제공되지 않는 기능임을 안내합니다. |
| `BackupService.cs` | 백업 ZIP 생성·복원과 보관 개수 관리입니다. 대상 경로를 검사하고 작업 중 파일을 보호하는 핵심 처리입니다. |
| `BackupArchiveList.cs` | 백업 파일 목록을 표시하는 전용 컨트롤입니다. |
| `ArchiveViews.cs` | 백업/로그 등 보관된 파일을 관리 화면에서 조회하고 관련 작업으로 연결하는 표시 코드입니다. |
| `RestoreDialog.cs` | 백업 복원 전에 대상과 내용을 확인하는 창입니다. |
| `WorldImportService.cs` | 기존 월드 폴더나 압축 파일을 서버에 적용하는 서비스입니다. 경로 검증과 작업 상태를 관리합니다. |
| `WorldManagementWindow.cs` | 월드 선택·가져오기·적용 관련 화면입니다. 실제 파일 처리는 WorldImportService에 연결합니다. |
| `ServerDeletion.cs` | 서버 등록 정보·실제 폴더·연결된 백업의 삭제 계획과 실행입니다. 삭제 대상과 경로를 확인하는 처리입니다. |
| `ServerDeleteWindow.cs` | 서버 삭제 확인 화면입니다. 서버 폴더와 백업을 삭제할지 각각 선택하게 합니다. |
| `AppUpdates.cs` | 프로그램 업데이트 목록 조회·서명 검증·다운로드·설치 시작입니다. 배포 파일의 크기와 해시도 검증합니다. |
| `ReleaseHistoryCard.cs` | 업데이트 한 버전의 제목·설명·설치 버튼을 배치하는 카드입니다. 설명과 버튼이 겹치지 않게 영역을 나눕니다. |
| `UpdateConfirmationWindow.cs` | 업데이트/이전 버전 설치 전에 버전과 변경 내용을 확인하는 창입니다. |
| `UpdateProgressWindow.cs` | 업데이트 연결·다운로드·검증 진행률과 취소 동작을 표시하는 창입니다. |
| `WarmTheme.cs` | 크림·코랄 색상과 공통 입력/버튼 디자인입니다. 여러 화면이 같은 스타일을 사용하도록 합니다. |
| `DesignSystem.cs` | 공통 창·둥근 카드·확인/입력 대화상자 등 화면 구성 요소입니다. |
| `UiKit.cs` | 레이블·버튼·입력기 등 화면을 만드는 공통 도구와 컨트롤입니다. |
| `ReferenceLayout.cs` | 기준 크기에 맞춰 컨트롤을 배치하는 레이아웃과 아이콘 표시 도구입니다. |
| `Scenery.cs` | 풍경 배경의 로딩·캐시와 그리기입니다. 페이지와 카드 배경을 일관되게 표시합니다. |
| `BufferedPanels.cs` | 이중 버퍼링을 사용하는 패널입니다. 화면을 그리는 중간 상태가 보여 깜빡이는 현상을 줄입니다. |
| `FontAssets.cs` | 프로그램에 포함된 글꼴을 불러와 화면 컨트롤에서 사용할 수 있게 합니다. |
| `SoftwareArtwork.cs` | Paper·Fabric 등 서버 소프트웨어의 대표 이미지를 제공하는 코드입니다. |
| `WindowResizeInput.cs` | 창 가장자리와 모서리의 크기 조절 입력 판정입니다. 가로·세로·대각선 크기 조절을 지원합니다. |

| `ModMaintenance.cs` | 정확한 파일 해시로 호환 업데이트를 조회하고, 의존성·잠금·해시 검증 뒤 교체합니다. 클라이언트 전용 모드 정리와 실패 시 원복도 처리합니다. |
| `ModMaintenanceWindow.cs` | 업데이트 확인·서버용 모드 정리의 선택 카드, 진행 상태, 확인 화면입니다. 타이머로 목록을 재생성하지 않습니다. |
