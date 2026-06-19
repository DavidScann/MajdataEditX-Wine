# ARCHIVED PROJECT
This project is now archived because MajdataX recently made the move to Avalonia, on top of a new platform called MajdataNeo. This is a cross-platform UI library, allowing easier porting.

Although upstream is currently only built for Windows, I'll try to maintain a separate fork designed to recompile the project for Linux with additional changes (Opus audio, etc.)

---

# MajdataEdit fork for Wine
* now rebased for MajdataEditX by re-poem

I've made a couple of changes that allow MajdataEditX to run better on Wine, because the original one simply just froze when running it normally.

To use, first download [MajdataViewX](https://github.com/re-poem/MajdataViewX) from the Releases tab.
After that, go into the Actions tab, select the latest build, then download the artifact.
From there, unzip whatever's in the zip file and dump it into the MajdataView folder. Overwrite everything.

I recommend using umu-run to run this project, but any Wine-based runner should work just fine.

Still in testing. Please submit bug reports.
