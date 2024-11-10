# A simple optimized Cytrus v6 downloader

This is not a CLI, it's meant to keep running on a Docker / Server or Windows Server if starting the game is needed

- Auto detect new version
- Download in a multi threaded way with a maximum RAM usage of 512MB
- Run the game and wait for it to exit (you must do something to exit it when your plugins / tasks are over)

## Configuration
This tool use a single .env file (or Environment variables) 
Used keys:
     WEBHOOK_URL (Discord webhook url)
     POST_DOWNLOAD (true/false, true if you the tool to start the game after download)
     FRAGMENTS (csv, a list of fragments to be downloaded (List of Unity: "animations, audio, bones, configuration, data, i18n, maps, picto, skins, win32_x64")
"win32_x64" is required if you want to start the game

## TODO: 
- Check every chunks of a file and only download the modified one, not the whole file

