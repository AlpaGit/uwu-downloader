using uwu_downloader;

var config = new Configuration();
config.Load();

var entry = new EntryPoint(config);
await entry.Run();


