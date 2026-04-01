namespace NoMercyBot.Globals.Information;

public static class WidgetFiles
{
    public static string WidgetsPath => Path.Combine(AppFiles.AppPath, "widgets");

    public static string GetWidgetPath(Ulid widgetId)
    {
        return Path.Combine(WidgetsPath, widgetId.ToString());
    }

    public static string GetWidgetSourcePath(Ulid widgetId)
    {
        return Path.Combine(GetWidgetPath(widgetId), "source");
    }

    public static string GetWidgetDistPath(Ulid widgetId)
    {
        return Path.Combine(GetWidgetPath(widgetId), "dist");
    }

    public static string GetWidgetConfigFile(Ulid widgetId)
    {
        return Path.Combine(GetWidgetPath(widgetId), "widget.json");
    }

    public static string GetWidgetIndexFile(Ulid widgetId)
    {
        return Path.Combine(GetWidgetDistPath(widgetId), "index.html");
    }

    public static void EnsureWidgetDirectoryExists(Ulid widgetId)
    {
        string widgetPath = GetWidgetPath(widgetId);
        string sourcePath = GetWidgetSourcePath(widgetId);
        string distPath = GetWidgetDistPath(widgetId);

        Directory.CreateDirectory(widgetPath);
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(distPath);
    }

    public static void CreateBasicTestWidget(Ulid widgetId, string widgetName)
    {
        EnsureWidgetDirectoryExists(widgetId);

        string indexPath = GetWidgetIndexFile(widgetId);
        string basicHtml =
            $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{widgetName}</title>
    <style>
        body {{ 
            margin: 0; 
            padding: 20px; 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; 
            background: rgba(0,0,0,0.8); 
            color: white; 
            border-radius: 8px;
        }}
        .widget-container {{ 
            text-align: center; 
            max-width: 400px;
            margin: 0 auto;
        }}
        .status {{
            padding: 8px 16px;
            border-radius: 4px;
            margin: 10px 0;
            font-weight: bold;
        }}
        .status.connecting {{ background-color: #ffa500; }}
        .status.connected {{ background-color: #4caf50; }}
        .status.error {{ background-color: #f44336; }}
        .event-log {{
            margin-top: 20px;
            padding: 10px;
            background: rgba(255,255,255,0.1);
            border-radius: 4px;
            max-height: 200px;
            overflow-y: auto;
            text-align: left;
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class=""widget-container"">
        <h2>{widgetName}</h2>
        <p>Widget ID: <code>{widgetId}</code></p>
        <div id=""status"" class=""status connecting"">Connecting...</div>
        <div class=""event-log"" id=""eventLog"">
            <div><strong>Event Log:</strong></div>
        </div>
    </div>
    
    <script src=""https://unpkg.com/@microsoft/signalr@latest/dist/browser/signalr.min.js""></script>
    <script>
        const widgetId = '{widgetId}';
        const statusEl = document.getElementById('status');
        const eventLogEl = document.getElementById('eventLog');
        
        // Initialize SignalR connection
        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/widgets')
            .withAutomaticReconnect()
            .build();
            
        function updateStatus(message, className) {{
            statusEl.textContent = message;
            statusEl.className = 'status ' + className;
        }}
        
        function logEvent(message) {{
            const timestamp = new Date().toLocaleTimeString();
            const logEntry = document.createElement('div');
            logEntry.innerHTML = `<span style=""opacity: 0.7"">[` + timestamp + `]</span> ` + message;
            eventLogEl.appendChild(logEntry);
            eventLogEl.scrollTop = eventLogEl.scrollHeight;
        }}
        
        // Connection event handlers
        connection.onclose(() => {{
            updateStatus('Disconnected', 'error');
            logEvent('❌ Connection lost');
        }});
        
        connection.onreconnecting(() => {{
            updateStatus('Reconnecting...', 'connecting');
            logEvent('🔄 Reconnecting...');
        }});
        
        connection.onreconnected(() => {{
            updateStatus('Connected', 'connected');
            logEvent('✅ Reconnected successfully');
            joinWidgetGroup();
        }});
        
        // Widget event handlers
        connection.on('WidgetEvent', (event) => {{
            logEvent(`📩 Event: ${{event.EventType}} - ${{JSON.stringify(event.Data)}}`);
        }});
        
        connection.on('WidgetReload', () => {{
            logEvent('🔄 Reload requested - refreshing widget...');
            setTimeout(() => location.reload(), 1000);
        }});
        
        function joinWidgetGroup() {{
            connection.invoke('JoinWidgetGroup', widgetId)
                .then(() => logEvent('🏠 Joined widget group'))
                .catch(err => logEvent('❌ Failed to join group: ' + err));
        }}
        
        // Start connection
        connection.start()
            .then(() => {{
                updateStatus('Connected', 'connected');
                logEvent('✅ Connected to SignalR hub');
                joinWidgetGroup();
            }})
            .catch(err => {{
                updateStatus('Connection Failed', 'error');
                logEvent('❌ Connection failed: ' + err);
            }});
        
        logEvent('🚀 Widget {widgetName} initializing...');
    </script>
</body>
</html>";

        File.WriteAllText(indexPath, basicHtml);
    }

    public static void DeleteWidgetDirectory(Ulid widgetId)
    {
        string widgetPath = GetWidgetPath(widgetId);
        if (Directory.Exists(widgetPath))
            Directory.Delete(widgetPath, true);
    }

    public static IEnumerable<string> GetAllWidgetPaths()
    {
        return [WidgetsPath];
    }
}
