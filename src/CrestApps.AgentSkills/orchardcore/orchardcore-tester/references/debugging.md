# Debugging Reference

## Log Files

Sites created with `dotnet new occms` (and the Orchard Core repository's `OrchardCore.Cms.Web`) log with NLog to the web project's App_Data folder. With Serilog or another provider, check its configured sinks instead. `$webProject` is the web project folder, as in SKILL.md.

### Log Location

```
$webProject/App_Data/logs/orchard-log-YYYY-MM-DD.log
```

Example: `App_Data/logs/orchard-log-2026-01-28.log`

### Viewing Recent Logs

Since logs are append-only, use `Get-Content -Tail` to view the most recent entries:

```powershell
# View last 50 lines
Get-Content "$webProject/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50

# Follow log in real-time (like tail -f)
Get-Content "$webProject/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 20 -Wait

# Search for errors in today's log
Select-String -Path "$webProject/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Pattern "ERROR|Exception" -Context 2,5
```

### Log Entry Format

Each log entry contains:
- Timestamp
- Log level (DEBUG, INFO, WARN, ERROR, FATAL)
- Tenant name
- Trace ID
- Logger name
- Message
- Exception details (if applicable)

### Log Levels

| Level | Description |
|-------|-------------|
| DEBUG | Detailed debugging information |
| INFO | General information |
| WARN | Warning messages |
| ERROR | Error conditions |
| FATAL | Critical failures |

Default configuration logs `Warning` and above.

### Internal NLog Logs

If NLog itself has issues, check:
```
$webProject/App_Data/logs/internal-nlog.txt
```

## Console Output

When running in background, console output is not visible. Use log files instead.

To capture startup errors, you can redirect output when starting:

```powershell
Start-Process dotnet -ArgumentList "run --urls http://localhost:$port" `
    -WorkingDirectory $webProject `
    -RedirectStandardOutput "App_Data/logs/console-out.log" `
    -RedirectStandardError "App_Data/logs/console-err.log" `
    -NoNewWindow
```

## Common Issues

### Application Won't Start

1. Check if port is already in use:
   ```powershell
   Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
   ```

2. Check console error log:
   ```powershell
   Get-Content "$webProject/App_Data/logs/console-err.log" -Tail 20
   ```

3. Check application log for startup errors:
   ```powershell
   Select-String -Path "$webProject/App_Data/logs/orchard-log-*.log" -Pattern "ERROR|FATAL" | Select-Object -Last 10
   ```

### Database Errors

Look for YesSql-related errors:
```powershell
Select-String -Path "$webProject/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Pattern "YesSql|Database|SQL"
```

### Feature Enable/Disable Errors

```powershell
Select-String -Path "$webProject/App_Data/logs/orchard-log-$(Get-Date -Format 'yyyy-MM-dd').log" -Pattern "Feature|Enable|Disable" -Context 1,3
```

## Clearing Logs

To start fresh:
```powershell
Remove-Item "$webProject/App_Data/logs/*.log" -Force
```
