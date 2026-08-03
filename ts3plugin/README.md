# Task Force Radio Plugin

Place the `.ts3_plugin` file here, named `task_force_radio.ts3_plugin`, and commit it
to this repo so the launcher can download it from:

```
https://raw.githubusercontent.com/<owner>/<repo>/main/ts3plugin/task_force_radio.ts3_plugin
```

That URL goes into `Ts3PluginUrl` (Settings in the launcher, or
`src/StrikeLauncher/config/default-config.json` before building).

If you rename the file, update `Ts3PluginDllHint` in the settings to a substring of
the plugin's installed `.dll` name (used to detect whether it's already installed) -
default is `task_force_radio`.
