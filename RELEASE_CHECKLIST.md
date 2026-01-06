# 🚀 Release Checklist

Follow this process to deploy a new version of Auto Typer.

## 1. Pre-Release
- [ ] **Bump Version**: Update `<Version>` in `AutoTyper.csproj`.
- [ ] **Update JSON**: Update `update.json` with the new version number and changelog.
- [ ] **Clean Build**: Run `dotnet clean`.

## 2. Build Binaries
- [ ] **Publish EXE**: 
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
  ```
- [ ] **Build Installer**:
  - Open `setup.iss` with Inno Setup (or run `ISCC setup.iss`).
  - Verify `AutoTyper-byGo-Setup.exe` is created.

## 3. Deployment
- [ ] **GitHub Release**:
  - Go to GitHub > Releases > Draft a new release.
  - Tag: `v1.X.X`
  - Title: `v1.X.X`
  - Drag & Drop:
    - `AutoTyper-byGo-Setup.exe` (Installer)
    - `AutoTyper.exe` (Portable)
  - Publish.

## 4. Post-Release
- [ ] **Update Website**: Ensure `docs/download.html` links are valid.
- [ ] **Verify Update**: Run the app and click "Check for Updates" to test the `update.json` path.
