# MicCast

Keep the app a small, draggable, borderless microphone widget. One click starts or stops; the context menu exits. Do not add a main window, web UI, or bundled drivers.

Use the ordinary stereo VB-CABLE render endpoint. Exclude 16 Ch and capture endpoints. Always save the original three default-render roles before mutation and retain the recovery journal until restoration verifies successfully.

Build: `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`.
Logic tests: `powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1`.
Hardware tests: `powershell -NoProfile -ExecutionPolicy Bypass -File .\test.ps1 -Hardware`.

Hardware tests temporarily activate the microphone and change default playback. The user deferred further live testing until the next classroom session; wait for that instruction before running them again.

Public repository: SUGE2016/MicCast. License: MIT. Public author: 新BUG主理人. The owner authorized GitHub publication with downloadable EXE releases. Preserve this metadata. Version tags publish through CI; ordinary main pushes only build and test. Do not bundle third-party drivers. Keep local device IDs and generated logs out of version control.
