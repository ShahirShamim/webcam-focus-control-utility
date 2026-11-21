# Webcam Focus Control Utility

A tiny Windows app for manually controlling the focus of a USB webcam. It needs no installs or extra libraries, and works while Teams, Zoom or OBS is using the camera.

Built for and tested with an **A4tech FHD 1080P PC Camera**. It should work with any UVC webcam whose driver exposes a focus control.

## Download

**[Download FocusControl.exe](https://github.com/ShahirShamim/webcam-focus-control-utility/releases/latest/download/FocusControl.exe)** (latest release, about 17 KB). It doesn't need installing; just run it.

The exe isn't code-signed, so Windows SmartScreen may warn you the first time you run it. Click **More info → Run anyway**, or build it yourself (see [Build](#build)). For checksums and older versions, see [all releases](https://github.com/ShahirShamim/webcam-focus-control-utility/releases).

## Features

- Pick a camera from the list (cameras without a focus control are labelled).
- Turn autofocus on or off.
- While autofocus is on, the window shows the camera's current focus level live.
- Untick **Autofocus** to lock focus at that level, then fine-tune it with the slider.
- Arrow keys nudge the focus (hold Shift for ×10), and **−** / **+** / **Default** buttons do the same.
- **Open Camera app** opens the Windows Camera app so you can see the picture while you adjust.
- **Keep on top** (on by default) keeps the window above other windows.

## Build

Windows has the C# compiler built into .NET Framework 4, so no SDK is needed:

```
build.cmd
```

This produces `FocusControl.exe`.

## Command line

```
FocusControl.exe --list                  List cameras with focus range and current value
FocusControl.exe --set <index> <value>   Set manual focus, e.g. --set 0 260
FocusControl.exe --set <index> auto      Turn autofocus back on
FocusControl.exe --probe                 Show every camera control (pan, tilt, zoom, exposure, iris, focus)
```

You can use `--set` from a shortcut or a startup task to restore your preferred focus after the camera is reconnected.

## How it works

It uses DirectShow's `IAMCameraControl` (`CameraControl_Focus`) on the camera's capture filter. That is the same control as the focus slider on the driver's own settings page. The camera doesn't need to be streaming to this app, so you can adjust focus while another app shows the video.

Some cameras go back to autofocus when they are unplugged or the PC restarts.
