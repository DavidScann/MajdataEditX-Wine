# Scrolling Lag Fix for Wine/Linux

## Issue
After the initial Wine/Linux performance fix that reduced the visual effect refresh timer from 1ms to 16ms, users reported that scrolling the waveform still becomes progressively laggier the further into the song they scroll, to the point of being unusable.

## Root Cause Analysis

### The Problem
The `DrawWave()` method had an algorithmic inefficiency in the beat calculation loop (lines 742-765):

```csharp
// BEFORE - Always starts from beginning of song
double time = SimaiProcess.first;
for (var i = 1; i < bpmChangeTimes.Count; i++)
{
    while (time - bpmChangeTimes[i] < -0.05)
    {
        // Calculates ALL beats from song start to current position
        if (currentBeat == 1)
            strongBeat.Add(time);
        else
            weakBeat.Add(time);
        currentBeat++;
        time += timePerBeat;
    }
    time = bpmChangeTimes[i];
    currentBeat = 1;
}

foreach (var btime in strongBeat)
{
    if (btime - currentTime > deltatime) continue; // Skips most beats
    var x = ((float)(btime / step) - startindex) * linewidth;
    graphics.DrawLine(pen, x, 0, x, 75);
}
```

**The Issue:**
1. The loop calculates ALL beats from the start of the song (`SimaiProcess.first`) to the end
2. As you scroll further into the song, more beats accumulate in the `strongBeat` and `weakBeat` lists
3. The draw loops then iterate through ALL accumulated beats, skipping most with `continue`
4. At 3 minutes into a song at 150 BPM, this means processing ~450 beats per frame, with only ~20-30 visible

This creates **O(n)** performance degradation where n = current time position in the song.

## Solution

### Optimized Algorithm
The fix implements a **windowed calculation** that only processes beats within the visible range:

```csharp
// AFTER - Only calculates beats in visible window
var visibleStart = currentTime - deltatime;
var visibleEnd = currentTime + deltatime;

// Find the BPM section containing the visible range
var startBpmIndex = 1;
for (var i = 1; i < bpmChangeTimes.Count; i++)
{
    if (bpmChangeTimes[i] > visibleStart)
    {
        startBpmIndex = i;
        break;
    }
}

// Calculate where we should start based on the BPM at visibleStart
var bpmAtStart = bpmChangeValues[startBpmIndex - 1];
timePerBeat = 1d / (bpmAtStart / 60d);

// Find the beat position closest to (but before) visibleStart
var referenceTime = bpmChangeTimes[startBpmIndex - 1];
if (visibleStart > referenceTime)
{
    var timeSinceLastBpmChange = visibleStart - referenceTime;
    var beatsSinceChange = (int)(timeSinceLastBpmChange / timePerBeat);
    time = referenceTime + beatsSinceChange * timePerBeat;
    currentBeat = (beatsSinceChange % signature) + 1;
}
else
{
    // Visible range starts before or at first BPM change
    time = SimaiProcess.first;
}

// Only calculate beats within visible window
for (var i = startBpmIndex; i < bpmChangeTimes.Count; i++)
{
    while (time - bpmChangeTimes[i] < -0.05)
    {
        if (time >= visibleStart && time <= visibleEnd)
        {
            if (currentBeat == 1)
                strongBeat.Add(time);
            else
                weakBeat.Add(time);
        }
        else if (time > visibleEnd)
        {
            break; // Stop when past visible range
        }
        currentBeat++;
        time += timePerBeat;
    }
    
    if (time > visibleEnd)
        break;
        
    time = bpmChangeTimes[i];
    currentBeat = 1;
}

// No need to skip beats - all are visible
foreach (var btime in strongBeat)
{
    var x = ((float)(btime / step) - startindex) * linewidth;
    graphics.DrawLine(pen, x, 0, x, 75);
}
```

### Key Improvements

1. **Windowed Calculation**: Only processes beats within `currentTime ± deltatime`
2. **Smart Starting Point**: Calculates the correct beat position at `visibleStart` instead of always starting from song beginning
3. **Early Exit**: Stops calculating when beats exceed the visible range
4. **Constant Complexity**: Performance is now **O(1)** - always processes ~20-30 beats regardless of song position

## Performance Comparison

| Position in Song | Beats Calculated (Before) | Beats Calculated (After) | Improvement |
|------------------|---------------------------|--------------------------|-------------|
| 0:30 @ 150 BPM   | ~75 beats                | ~30 beats                | 2.5x        |
| 1:00 @ 150 BPM   | ~150 beats               | ~30 beats                | 5x          |
| 3:00 @ 150 BPM   | ~450 beats               | ~30 beats                | 15x         |
| 5:00 @ 150 BPM   | ~750 beats               | ~30 beats                | 25x         |

*Assumes deltatime = 4 seconds (visible range = 8 seconds total)*

## Expected Results

- **Constant Performance**: Scrolling responsiveness remains the same throughout the entire song
- **Wine/Linux**: Eliminates progressive lag when scrolling through long songs
- **No Visual Changes**: Beat lines display identically to before
- **Backward Compatible**: No changes to data structures or external APIs

## Files Modified

1. **MainWindowCore.cs**
   - Optimized beat calculation loop in `DrawWave()` method
   - Added windowed calculation with smart starting point
   - Added early exit conditions for performance

## Testing Recommendations

1. Load a long song (5+ minutes) at various BPMs (120-180)
2. Scroll to different positions (beginning, middle, end)
3. Verify beat lines display correctly at all positions
4. Confirm scrolling performance is consistent throughout the song
5. Test with BPM changes to ensure transitions are handled correctly
6. Verify on both Windows and Wine/Linux environments
