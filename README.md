# Minecraft Server Manager

A Windows desktop application for creating and managing Minecraft Java Edition servers. Built with C# / .NET 8 WinForms, with a Korean interface.

## Current features

- Create and manage Paper, Purpur, Fabric, Forge and NeoForge servers.
- Start, stop and restart servers; view console logs and player information.
- Configure Java, memory, server properties, backups and player permissions.
- Search and install mods/plugins from Modrinth; inspect required dependencies.
- Import supported Modrinth modpacks as new server entries with pack artwork.
- Check compatible mod updates and apply selected updates with original-file retention.
- Review server/client environment metadata and selectively enable, disable or archive mods.

## CurseForge API application

CurseForge integration is **planned, not currently implemented or approved**. The intended integration is mod/modpack search, project and file metadata, compatible update checks, and downloads only where the official API and authors permit them. Restricted files would remain manual downloads through the official project page. No unofficial API key or access bypass is used.

This repository is for project review and development. Uploading a repository does not itself grant API access or guarantee application approval.

## Build and run

Requirements: Windows x64 and the .NET 8 SDK. Running Minecraft servers also requires a suitable Java installation.

```powershell
dotnet restore NeoForgeServerManager.csproj
dotnet build NeoForgeServerManager.csproj -c Release
dotnet run --project NeoForgeServerManager.csproj
```

Self-contained application:

```powershell
dotnet publish NeoForgeServerManager.csproj -c Release -r win-x64 --self-contained true -o publish
```

`SOURCE-GUIDE.ko.md` explains the code in Korean. UI is implemented in code without WinForms Designer files. The project includes required images and font files. `Assets/Fonts/OFL.txt` contains the font license.

## Validation

The v12.29 release passed 46 isolated maintenance/UI checks and 11 public update-distribution checks. These results do not establish compatibility with every real modpack or replace actual server/gameplay testing.

To run the isolated maintenance/UI checks on a Windows desktop:

```powershell
dotnet build tests/Verification.csproj
./tests/bin/Debug/net8.0-windows/Verification.exe ./test-output ./tests/FakeServer.java --mod-maintenance
```

Use a new empty test-output directory. Do not point fixture tests at a real server or world.

## Data and credentials

This source export excludes user servers, worlds, backups, profiles, logs, private signing keys, API keys, credentials and conversation records. The bundled update **public** key verifies release signatures; it cannot sign updates. Official signed application updates are distributed separately from source builds.

## Licensing and third-party projects

No project-wide open-source license has been selected. Public visibility, if enabled by the owner, is not a blanket redistribution license. Third-party fonts, libraries, software names and artwork retain their respective rights and licenses. This application is not an official Minecraft, Mojang, Microsoft, Modrinth or CurseForge product.

## 한국어 안내

마인크래프트 Java 서버를 만들고 실행·관리하는 Windows 프로그램입니다. 현재 배포 버전은 v12.29입니다. CurseForge 연동은 API 승인 이후 별도 구현할 예정이며, 지금은 Modrinth를 사용합니다.
