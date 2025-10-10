# Running MajdataEdit on Wine/Linux

This guide helps you run MajdataEdit on Linux using Wine with optimal performance.

## Performance Optimization

MajdataEdit uses GDI+ rendering which can be slow on Wine. The following settings have been optimized for Wine/Linux:

### Default Changes (v2.0)
- Visual effect refresh rate reduced from 1000 fps to 60 fps (16ms interval)
- Added frame skipping to prevent UI lag
- Improved error handling for drawing operations

### Configuration Options

You can further tune performance by editing `EditorSetting.json`:

#### VisualEffectRefreshRate
Controls the refresh rate for waveform and FFT visualization (in milliseconds).

- **Default**: `16` (~60 fps) - Good balance for most systems
- **For slower systems**: `33` (~30 fps) - Better performance, slightly less smooth
- **For faster systems**: `8` (~120 fps) - Smoother visualization if your system can handle it

Example `EditorSetting.json`:
```json
{
  "VisualEffectRefreshRate": 16,
  "RenderMode": 0,
  ...
}
```

#### RenderMode
Controls how WPF renders the application.

- **0**: Hardware rendering (default) - Usually faster but may have compatibility issues with Wine
- **1**: Software rendering - More compatible with Wine but slower

You can also force software rendering via command line:
```bash
wine MajdataEdit.exe --ForceSoftwareRender
```

### Recommended Settings for Wine

1. **Basic setup** (most users):
   ```json
   {
     "VisualEffectRefreshRate": 16,
     "RenderMode": 0
   }
   ```

2. **Performance issues**:
   ```json
   {
     "VisualEffectRefreshRate": 33,
     "RenderMode": 1
   }
   ```

3. **Powerful system**:
   ```json
   {
     "VisualEffectRefreshRate": 8,
     "RenderMode": 0
   }
   ```

### Other Performance Tips

1. **Close unnecessary applications** - Wine GDI+ can be CPU intensive
2. **Use hardware acceleration** - Ensure your Wine is configured with proper graphics drivers
3. **Update Wine** - Newer versions of Wine have better GDI+ performance
4. **Consider using wine-staging** - May have better graphics performance

## Troubleshooting

### UI is still laggy
- Increase `VisualEffectRefreshRate` to 33 or higher
- Try software rendering mode (`RenderMode: 1`)
- Check Wine console for error messages

### Graphics corruption
- Try software rendering mode (`--ForceSoftwareRender`)
- Update your graphics drivers
- Check Wine configuration for proper Direct3D settings

### Application crashes on startup
- Ensure .NET 6.0 Desktop Runtime is installed in Wine
- Check bass.dll and bass_fx.dll are in the same directory
- Run with `WINEDEBUG=+all` to see detailed error messages

## Getting Help

If you continue to experience issues, please report them with:
- Your Wine version (`wine --version`)
- Your Linux distribution
- Graphics card and driver version
- Console output when running the application
- Your `EditorSetting.json` configuration
