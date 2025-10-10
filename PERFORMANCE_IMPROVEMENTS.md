# Performance Improvements for Wine/Linux - Technical Summary

## Issue
MajdataEdit was experiencing severe UI lag when running under Wine on Linux, making it practically unusable.

## Root Cause Analysis

### Primary Issue: Excessive Refresh Rate
The `visualEffectRefreshTimer` was set to **1 millisecond** interval, attempting to redraw at **1000 frames per second**.

```csharp
// BEFORE
private readonly Timer visualEffectRefreshTimer = new(1);
```

Each timer tick executed:
- `DrawWave()` - Complex waveform rendering with GDI+ operations
- `DrawFFT()` - FFT spectrum analysis visualization

### Secondary Issues

1. **No Frame Skipping on FFT**: The `DrawFFT()` method lacked frame skipping, allowing multiple drawing operations to queue up
2. **Unsafe Flag Management**: The `isDrawing` flag could get stuck if an exception occurred
3. **Wine GDI+ Performance**: System.Drawing (GDI+) operations are significantly slower under Wine than native Windows
4. **No Configurability**: Users couldn't adjust the refresh rate for their system capabilities

## Solutions Implemented

### 1. Reduced Timer Frequency (60x improvement)
```csharp
// AFTER
private readonly Timer visualEffectRefreshTimer = new(16); // ~60 fps
```

**Impact**: Reduced attempted frame rate from 1000 fps to 60 fps

### 2. Added Frame Skipping to DrawFFT()
```csharp
// BEFORE
private void DrawFFT()
{
    Dispatcher.InvokeAsync(() =>
    {
        // drawing code
    });
}

// AFTER
private bool isDrawingFFT;

private void DrawFFT()
{
    if (isDrawingFFT) return; // Skip if already drawing
    
    Dispatcher.InvokeAsync(() =>
    {
        isDrawingFFT = true;
        try
        {
            // drawing code
        }
        finally
        {
            isDrawingFFT = false;
        }
    });
}
```

**Impact**: Prevents multiple concurrent FFT drawing operations

### 3. Protected Drawing Flags with Try-Finally
```csharp
// BEFORE
private void DrawWave()
{
    if (isDrawing) return;
    Dispatcher.Invoke(() =>
    {
        isDrawing = true;
        // drawing code
        isDrawing = false;
    });
}

// AFTER
private void DrawWave()
{
    if (isDrawing) return;
    Dispatcher.Invoke(() =>
    {
        isDrawing = true;
        try
        {
            // drawing code
        }
        finally
        {
            isDrawing = false; // Always reset, even on error
        }
    });
}
```

**Impact**: Prevents flag from getting stuck on exceptions

### 4. Added Configurable Refresh Rate
```csharp
// In Majson.cs - EditorSetting class
public int VisualEffectRefreshRate = 16; // ms, ~60fps

// In MainWindowCore.cs - ReadEditorSetting()
visualEffectRefreshTimer.Interval = editorSetting.VisualEffectRefreshRate;
```

**Impact**: Users can adjust refresh rate in `EditorSetting.json`:
- `16` ms = 60 fps (default, balanced)
- `33` ms = 30 fps (better performance)
- `8` ms = 120 fps (smoother, if system can handle it)

## Performance Comparison

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Timer Interval | 1 ms | 16 ms (configurable) | 16x slower (better) |
| Attempted FPS | 1000 | 60 | 94% reduction |
| Frame Skipping | Partial (Wave only) | Full (Wave + FFT) | Complete |
| Error Safety | Vulnerable | Protected | Robust |
| User Control | None | Full | Configurable |

## Files Modified

1. **MainWindowCore.cs**
   - Changed timer interval from 1ms to 16ms
   - Added `isDrawingFFT` flag
   - Added frame skipping to `DrawFFT()`
   - Protected both drawing methods with try-finally
   - Applied `VisualEffectRefreshRate` setting on startup

2. **Majson.cs**
   - Added `VisualEffectRefreshRate` property to `EditorSetting` class

3. **WINE_SETUP.md** (new)
   - Comprehensive Wine/Linux setup guide
   - Performance tuning instructions
   - Troubleshooting tips

## Expected Results

- **Wine/Linux**: Smooth, responsive UI at 60 fps (configurable)
- **Windows**: No change in behavior, maintains compatibility
- **Low-end systems**: Can reduce to 30 fps via configuration
- **High-end systems**: Can increase to 120 fps via configuration

## Backward Compatibility

- New `VisualEffectRefreshRate` setting has sensible default (16ms)
- Existing `EditorSetting.json` files will use default if property missing
- No breaking changes to existing functionality
- Auto-saves updated settings to include new property

## Testing Recommendations

1. **Basic functionality**: Verify waveform and FFT display correctly
2. **Performance**: Monitor CPU usage during playback
3. **Configuration**: Test different refresh rates (8, 16, 33, 50 ms)
4. **Error handling**: Test with malformed chart data to ensure flags reset
5. **Wine compatibility**: Test on multiple Wine versions (6.x, 7.x, 8.x)

## Additional Notes

- The existing `chartChangeTimer` (1000ms debounce) for text changes is unchanged and appropriate
- The `waveStopMonitorTimer` (33ms) is unchanged and appropriate  
- The `currentTimeRefreshTimer` (100ms) is unchanged and appropriate
- No forced garbage collection calls exist in the codebase (good practice)
