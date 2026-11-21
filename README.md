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

Most USB webcams follow the **UVC** (USB Video Class) standard. UVC defines camera controls such as focus, exposure and zoom that the camera itself carries out. Windows makes these controls available through **DirectShow**. The app is one C# file (`FocusControl.cs`) that talks to DirectShow directly through COM, so it needs no extra libraries.

1. **Finding cameras.** It uses DirectShow's system device enumerator to list every video input device. For each one, it reads the friendly name (such as "A4tech FHD 1080P PC Camera") and opens the device's capture filter.
2. **Getting the focus control.** It asks each filter for the `IAMCameraControl` interface and calls `GetRange(CameraControl_Focus)`. This returns the camera's minimum, maximum, step size and default focus values, and whether the camera supports auto and manual modes. Cameras that return no range are fixed-focus, and the list labels them "(no focus control)".
3. **Setting focus.** Moving the slider calls `Set(CameraControl_Focus, value, Manual)`. Ticking Autofocus calls `Set(..., Auto)`. These are the same calls the focus slider on the driver's own settings page makes.
4. **Live autofocus readout.** While autofocus is on, a timer calls `Get(CameraControl_Focus)` four times a second. The camera reports the focus position its autofocus has chosen, and the app shows that number and moves the slider to match.
5. **Locking focus.** When you untick Autofocus, the app reads the current position first and switches to manual at exactly that value, so the picture doesn't jump.

The app never streams video. It only sends control commands, so the camera can keep running in Teams, Zoom, OBS or the Camera app while you adjust focus there.

**Limitations**

- Focus values are camera-specific numbers (1–1023 on the A4tech), not distances. A higher number doesn't mean the same thing on every camera.
- Some cameras go back to autofocus when they are unplugged or the PC restarts. You can run `FocusControl.exe --set 0 <value>` from a startup shortcut to restore your setting.
- Built-in laptop cameras are often fixed-focus, so they show no focus control.
