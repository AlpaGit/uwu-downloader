## A simple optimized Cytrus v6 downloader

This is not a CLI, it's meant to keep running on a Docker / Server or Windows Server if starting the game is needed

- Auto detect new version
- Download in a multi threaded way with a maximum RAM usage of 512MB
- Run the game and wait for it to exit (you must do something to exit it when your plugins / tasks are over)


TODO: 
- Check every chunks of a file and only download the modified one, not the whole file
