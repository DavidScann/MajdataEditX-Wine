# Complete Wine/Linux Performance Fix Summary

## Overview
MajdataEdit experienced severe UI lag when running under Wine on Linux. This document summarizes the complete solution involving two separate performance fixes.

## Fix #1: Visual Effect Refresh Rate Optimization (Previous)
**File Modified:** `MainWindowCore.cs`, `Majson.cs`

### Problem
- Timer refresh rate set to 1ms (attempting 1000 FPS)
- GDI+ operations are slower under Wine than native Windows
- No frame skipping on FFT drawing
- Not configurable

### Solution
- Reduced timer interval from 1ms to 16ms (~60 FPS)
- Added frame skipping to DrawFFT() method
- Made refresh rate configurable via EditorSetting.json
- Protected drawing flags with try-finally

### Result
- 94% reduction in redraw attempts
- Configurable performance (30-120 FPS)
- Stable UI at program load

**Documentation:** `PERFORMANCE_IMPROVEMENTS.md`

---

## Fix #2: Beat Calculation Optimization (This Fix)
**File Modified:** `MainWindowCore.cs`

### Problem
Even after Fix #1, scrolling the waveform became progressively laggier the further into the song you scrolled. This was due to an algorithmic inefficiency in the `DrawWave()` method.

The beat calculation loop processed **all beats from the start of the song** to the current position, accumulating beats over time. At 3 minutes into a 150 BPM song, this meant processing ~450 beats per frame, with only ~20-30 being visible.

### Root Cause
```csharp
// OLD CODE - O(n) complexity where n = song position
double time = SimaiProcess.first;  // Always starts from beginning
for (var i = 1; i < bpmChangeTimes.Count; i++)
{
    while (time - bpmChangeTimes[i] < -0.05)
    {
        // Adds ALL beats from start to current time
        if (currentBeat == 1)
            strongBeat.Add(time);
        else
            weakBeat.Add(time);
        currentBeat++;
        time += timePerBeat;
    }
}

// Then skips most of them
foreach (var btime in strongBeat)
{
    if (btime - currentTime > deltatime) continue;  // Skips 90%+ of beats
    graphics.DrawLine(...);
}
```

### Solution: Windowed Calculation
Implemented a **windowed beat calculation** that only processes beats within the visible range (`currentTime ± deltatime`):

```csharp
// NEW CODE - O(1) complexity (constant ~20-30 beats)
var visibleStart = currentTime - deltatime;
var visibleEnd = currentTime + deltatime;

// Find BPM section for visible range
var startBpmIndex = 1;
for (var i = 1; i < bpmChangeTimes.Count; i++)
{
    if (bpmChangeTimes[i] > visibleStart)
    {
        startBpmIndex = i;
        break;
    }
}

// Calculate smart starting point
var bpmAtStart = bpmChangeValues[startBpmIndex - 1];
timePerBeat = 1d / (bpmAtStart / 60d);
var referenceTime = bpmChangeTimes[startBpmIndex - 1];

if (visibleStart > referenceTime)
{
    var beatsSinceChange = (int)((visibleStart - referenceTime) / timePerBeat);
    time = referenceTime + beatsSinceChange * timePerBeat;
    currentBeat = (beatsSinceChange % signature) + 1;
}
else
{
    time = SimaiProcess.first;
}

// Only calculate beats in visible window
for (var i = startBpmIndex; i < bpmChangeTimes.Count; i++)
{
    while (time - bpmChangeTimes[i] < -0.05)
    {
        if (time >= visibleStart && time <= visibleEnd)
        {
            // Only add visible beats
            if (currentBeat == 1)
                strongBeat.Add(time);
            else
                weakBeat.Add(time);
        }
        else if (time > visibleEnd)
        {
            break;  // Early exit
        }
        currentBeat++;
        time += timePerBeat;
    }
    
    if (time > visibleEnd)
        break;  // Early exit
}

// No skipping needed - all beats are visible
foreach (var btime in strongBeat)
{
    graphics.DrawLine(...);
}
```

### Key Improvements
1. **Smart Starting Point:** Calculates beat position at `visibleStart` using BPM math
2. **Windowed Processing:** Only adds beats within `[visibleStart, visibleEnd]` range  
3. **Early Exit:** Stops calculating when beats exceed visible range
4. **Constant Complexity:** Always processes ~20-30 beats regardless of song position

### Performance Impact

| Position in Song | Beats Before | Beats After | Speedup |
|------------------|--------------|-------------|---------|
| 0:30 @ 150 BPM   | ~75          | ~30         | 2.5x    |
| 1:00 @ 150 BPM   | ~150         | ~30         | 5x      |
| 3:00 @ 150 BPM   | ~450         | ~30         | 15x     |
| 5:00 @ 150 BPM   | ~750         | ~30         | 25x     |

*Assumes deltatime = 4 seconds (8 second visible range)*

### Result
- Constant scrolling performance throughout entire song
- Eliminates progressive lag when scrolling far into songs
- No visual changes to beat line display
- Fully backward compatible

**Documentation:** `SCROLLING_LAG_FIX.md`

---

## Combined Effect
The two fixes work together to provide smooth Wine/Linux performance:

1. **Fix #1** reduces the refresh rate from 1000 FPS to 60 FPS
2. **Fix #2** reduces beat calculations from O(n) to O(1)

### Overall Performance
- **At song start:** ~94% reduction in processing (Fix #1)
- **3 minutes in:** ~98% reduction in processing (Fix #1 + Fix #2)
- **5 minutes in:** ~99% reduction in processing (Fix #1 + Fix #2)

### Testing Checklist
- [x] Verify waveform displays correctly at song start
- [x] Test scrolling performance at beginning, middle, and end of long songs
- [x] Verify beat lines appear correctly at all positions
- [x] Test with various BPM values (60-200)
- [x] Test with songs that have BPM changes
- [x] Verify on Wine/Linux (primary target)
- [x] Verify on Windows (ensure no regression)
- [x] Test different deltatime values (zoom levels)

## Files Modified Summary

### MainWindowCore.cs
- Initial timer optimization (16ms refresh)
- Frame skipping for FFT
- Windowed beat calculation
- Early exit conditions

### Majson.cs
- Added VisualEffectRefreshRate setting

### Documentation
- PERFORMANCE_IMPROVEMENTS.md (Fix #1)
- SCROLLING_LAG_FIX.md (Fix #2)
- WINE_PERFORMANCE_FIXES.md (This summary)
- WINE_SETUP.md (Setup guide)
- CHANGES_SUMMARY.txt (Original summary)

## User Configuration

Users can tune performance in `EditorSetting.json`:

```json
{
  "VisualEffectRefreshRate": 16  // 8=120fps, 16=60fps, 33=30fps
}
```

## Conclusion

These optimizations make MajdataEdit fully usable under Wine on Linux:
- **Fix #1** addressed the excessive refresh rate issue
- **Fix #2** addressed the algorithmic inefficiency in beat calculation
- Together they provide **smooth, consistent performance** throughout the application
