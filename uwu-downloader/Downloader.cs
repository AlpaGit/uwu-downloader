using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Google.FlatBuffers;

namespace uwu_downloader;

/// <summary>
///  Download the game in a semi-optimized way
/// It will download each chunks file by file then try to find where the chunk is located in the every other files and write them
/// For now it doesn't check if the "other" files chunks already exist / are correct or not
/// So it will erase the parts and use a lot of I/O
/// <remarks>IT MAY BE A BIT OVER-ENGINEERED FOR A SIMPLE DOWNLOADER</remarks>
/// </summary>
public class Downloader
{
    // Avoid storing too much unprocessed data in memory
    private const int MaxRamUsage = 1024 * 1024 * 512; // 512 MB

    // Configure which fragments to download, we only need "win32_x64" for execute the game
    private static readonly List<string> FragmentToDownload = ["win32_x64"];
    
    private ManifestT _manifest = new ManifestT();
    private string _path = "unknown";

    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    private readonly ConcurrentDictionary<string, byte[]> _filesToSave = new ConcurrentDictionary<string, byte[]>();

    private readonly HashSet<string> _downloadedChunks = [];

    private readonly Dictionary<string, List<(FileT, ChunkT)>> _chunks = new Dictionary<string, List<(FileT, ChunkT)>>();
    private readonly Dictionary<string, List<FileT>> _files = new Dictionary<string, List<FileT>>();

    private readonly ConcurrentDictionary<string, bool> _downloadedFiles = new ConcurrentDictionary<string, bool>();
    
    private readonly ManualResetEventSlim _waitHandle = new ManualResetEventSlim(true);
    
    private readonly SemaphoreSlim _maxChunksSemaphore = new SemaphoreSlim(20, 20);
    private readonly SemaphoreSlim _maxFilesSemaphore = new SemaphoreSlim(10, 10);

    private long _waitingBytes;
    
    private long _maxSizeToInstall;
    private long _downloadedSize;
    private long _installedSize;
    
    public async Task DownloadGame(string game, string release, string version, Configuration configuration)
    {
        var fragmentsRaw = configuration.Get("FRAGMENTS");
        var onDownloadUrl = configuration.Get("ON_DOWNLOAD_URL");
        
        if (!string.IsNullOrEmpty(fragmentsRaw))
        {
            FragmentToDownload.Clear();
            FragmentToDownload.AddRange(fragmentsRaw.Split(","));
        }
        
        _path = Path.Combine(Directory.GetCurrentDirectory(), game, release);
        Directory.CreateDirectory(_path);

        using var httpClient = new HttpClient();
        var metaFile = await httpClient.GetByteArrayAsync($"https://cytrus.cdn.ankama.com/{game}/releases/{release}/windows/{version}.manifest");

        var manifestRoot = Manifest.GetRootAsManifest(new ByteBuffer(metaFile));

        _manifest = manifestRoot.UnPack();
        PopulateCache();

        var sw = Stopwatch.StartNew();
        await Logger.WebhookInfo($"Downloading {game}...");
        
        // We start a thread only for saving files, u jealous Javascript ?
        var thread = new Thread(SaveFiles)
        {
            Priority = ThreadPriority.Highest
        };
        
        thread.Start();

        await DownloadBundles(game, _manifest.Fragments);

        await _cancellationTokenSource.CancelAsync();
        
        await Logger.WebhookInfo($"{game} downloaded in {sw.Elapsed.TotalSeconds}s");

        if (configuration.Get("POST_DOWNLOAD").Equals("true", StringComparison.CurrentCultureIgnoreCase))
        {
            await PostDownload();

            try
            {
                System.IO.File.Delete(Path.Combine(_path, "BepInEx.zip"));
                Directory.Delete(Path.Combine(_path, "protocol"));
            }
            catch
            {
                // ignored
            }
        }
        
        if(!string.IsNullOrEmpty(onDownloadUrl))
            await httpClient.PostAsJsonAsync(onDownloadUrl, _downloadedFiles.Keys);
        
        Logger.Info($"{game} installed");
    }

    private async Task PostDownload()
    {
        const string bepinExUrl = "https://builds.bepinex.dev/projects/bepinex_be/725/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.725%2Be1974e2.zip";
        
        using var httpClient = new HttpClient();
        
        var bepinex = httpClient.GetByteArrayAsync(bepinExUrl).Result;
        var bepinexPath = Path.Combine(_path, "BepInEx.zip");
        
        await System.IO.File.WriteAllBytesAsync(bepinexPath, bepinex);
        await Logger.WebhookInfo($"BepInEx downloaded");
        
        // extract inside the _game directory
        ZipFile.ExtractToDirectory(bepinexPath, _path, true);

        if (Directory.Exists("plugins"))
        {
            Directory.CreateDirectory(Path.Combine(_path, "BepInEx", "plugins"));

            // we copy every plugins in the plugins folder
            foreach (var file in Directory.GetFiles("plugins"))
            {
                var dest = Path.Combine(_path, "BepInEx", "plugins", Path.GetFileName(file));
                System.IO.File.Copy(file, dest, true);
            }
        }

        await Logger.WebhookInfo($"BepInEx installed");
        
        var startProcess = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(_path, "Dofus.exe"),
            WorkingDirectory = _path
        });
        
        await Logger.WebhookInfo($"Game started");

        if(startProcess != null)
            await startProcess.WaitForExitAsync();
        
        await Logger.WebhookInfo($"Game closed and protocols probably commited");

        // Just to be sure every handle are closed
        await Task.Delay(1000);
        
        try
        {
            // We just try to remove any junk after the start
            Directory.Delete(Path.Combine(_path, "export"), true);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
    
    private void LogCurrentProgress()
    {
        Console.Clear();
        
        Logger.Info($"Installed {_installedSize}/{_maxSizeToInstall} bytes ({_installedSize * 100 / _maxSizeToInstall}%)");
    }
    
    private void SaveFiles()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested || !_filesToSave.IsEmpty)
        {
            var processed = new HashSet<string>();

            foreach (var dataK in _filesToSave)
            {
                foreach (var (file, chunk) in GetHashesUsedByFile(dataK.Key))
                {
                    try
                    {
                        SaveFile(file, chunk, dataK.Value);
                    }
                    catch (Exception e)
                    {
                        Logger.Info($"Failed to save file {file.Name}: {e}");
                    }
                }

                processed.Add(dataK.Key);
                _downloadedChunks.Add(dataK.Key);

                Interlocked.Add(ref _waitingBytes, -dataK.Value.Length);
                //Logger.Info($"Written {dataK.Value.Length} bytes, {_waitingBytes} bytes remaining");

                if (_waitingBytes < MaxRamUsage)
                {
                    _waitHandle.Set();
                }
                
                LogCurrentProgress();
            }

            foreach (var key in processed)
            {
                _filesToSave.TryRemove(key, out _);
            }
        }
    }

    private string GetHashFromHash(List<sbyte> hash)
    {
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }

    private async Task DownloadBundles(string game, List<FragmentT> fragments)
    {
        _maxSizeToInstall = 0;
        
        var fragmentsToDownload = fragments.Where(x => FragmentToDownload.Contains(x.Name)).ToList();
        
        foreach (var file in fragmentsToDownload.SelectMany(fragment => fragment.Files))
        {
            _maxSizeToInstall += file.Size;
        }

        foreach (var fragment in fragmentsToDownload)
        {
            await DownloadFragment(game, fragment);
        }
    }

    private async Task DownloadFragment(string game, FragmentT fragment)
    {
        var tasks = new List<Task>();

        foreach (var file in fragment.Files)
        {
            tasks.Add(DownloadFile(game, fragment.Bundles, file));
        }

        await Task.WhenAll(tasks);
    }

    // get in which bundle the file is located
    private (BundleT?, ChunkT?) GetBundle(List<BundleT> bundles, ChunkT chunk)
    {
        foreach (var bundle in bundles)
        {
            foreach (var chunkC in bundle.Chunks)
            {
                if (Downloader.AreSByteEqual(chunk.Hash, chunkC.Hash))
                {
                    return (bundle, chunkC);
                }
            }
        }

        return (null, null);
    }

    private (BundleT?, ChunkT?) GetBundle(List<BundleT> bundles, List<sbyte> fileHash)
    {
        foreach (var bundle in bundles)
        {
            foreach (var chunkC in bundle.Chunks)
            {
                if (Downloader.AreSByteEqual(fileHash, chunkC.Hash))
                {
                    return (bundle, chunkC);
                }
            }
        }

        return (null, null);
    }

    private void PopulateCache()
    {
        foreach (var fragment in _manifest.Fragments)
        {
            foreach (var file in fragment.Files)
            {
                var hash = GetHashFromHash(file.Hash);

                if (!_files.TryGetValue(hash, out var value))
                {
                    value = new List<FileT>();
                    _files[hash] = value;
                }

                value.Add(file);

                foreach (var chunk in file.Chunks)
                {
                    var chunkHash = GetHashFromHash(chunk.Hash);

                    if (!_chunks.TryGetValue(chunkHash, out var value2))
                    {
                        value2 = new List<(FileT, ChunkT)>();
                        _chunks[chunkHash] = value2;
                    }

                    value2.Add((file, chunk));
                }
            }
        }
    }

    private IEnumerable<(FileT, ChunkT)> GetHashesUsedByFile(string hash)
    {
        if (_files.TryGetValue(hash, out var file))
        {
            foreach (var fileT in file)
            {
                yield return (fileT, new ChunkT
                {
                    Hash = fileT.Hash,
                    Size = fileT.Size,
                    Offset = 0
                });
            }
        }

        if (!_chunks.TryGetValue(hash, out var chunk))
            yield break;

        foreach (var chunkT in chunk)
        {
            yield return chunkT;
        }
    }

    private async Task DownloadFile(string game, List<BundleT> bundles, FileT file)
    {
        await _maxFilesSemaphore.WaitAsync();
        try
        {
            _waitHandle.Wait(_cancellationTokenSource.Token);

            var toDownloadChunks = file.Chunks.ToList();
            
            // create an empty size of the file
            var path = Path.Combine(_path, file.Name);

            if (System.IO.File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length == file.Size)
                {
                    // sha1
                    var content = await System.IO.File.ReadAllBytesAsync(path);
                    var hash = SHA1.HashData(content);

                    // we need to convert the byte to sbyte
                    if (ByteArrayToHexString(hash) == Downloader.ByteArrayToHexString(file.Hash))
                    {
                        Interlocked.Add(ref _installedSize, content.Length);
                        Interlocked.Add(ref _downloadedSize, content.Length);
                        Logger.Info($"File {file.Name} is already downloaded and is correct");
                        return;
                    }
                }

                Logger.Info($"File {file.Name} is already downloaded but is corrupted, we download it");
                
                // We check only the modified chunks
                toDownloadChunks = [];
                
                foreach (var chunk in file.Chunks)
                {
                    var chunkHash = GetHashFromHash(chunk.Hash);
                    
                    // read the file at the offset and size of the chunk
                    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    
                    if(fs.Length < chunk.Offset + chunk.Size)
                    {
                        toDownloadChunks.AddRange(file.Chunks.Where(x => x.Offset >= chunk.Offset));
                        break;
                    }
                    
                    var buffer = new byte[chunk.Size];
                    fs.Seek(chunk.Offset, SeekOrigin.Begin);
                    _ = fs.Read(buffer, 0, buffer.Length);

                    var hash = SHA1.HashData(buffer);
                    var hashAsString = ByteArrayToHexString(hash);

                    if (hashAsString == chunkHash)
                        continue;
                    
                    if (fileInfo.Length == file.Size)
                    {
                        toDownloadChunks.Add(chunk);
                    }
                    else
                    {
                        // it means what is after is modified or atleast the offset will be different so we can just download everything
                        toDownloadChunks.AddRange(file.Chunks.Where(x => x.Offset >= chunk.Offset));
                        break;
                    }
                }
            }

            if (file.Chunks.Count == 0)
            {
                var (bundle, chunkInBundle) = GetBundle(bundles, file.Hash);

                if (bundle == null || chunkInBundle == null)
                {
                    Logger.Info($"File {file.Name} not found in any bundle");
                    return;
                }
                
                await DownloadChunk(game, bundle, [chunkInBundle]);
                return;
            }

            var chunks = new Dictionary<BundleT, List<(ChunkT, ChunkT)>>();

            foreach (var chunk in toDownloadChunks)
            {
                var (bundle, chunkInBundle) = GetBundle(bundles, chunk);

                if (bundle == null || chunkInBundle == null)
                {
                    Logger.Info($"File {file.Name} not found in any bundle");
                    return;
                }

                if (!chunks.TryGetValue(bundle, out var value))
                {
                    value = new List<(ChunkT, ChunkT)>();
                    chunks[bundle] = value;
                }

                value.Add((chunkInBundle, chunk));
            }

            foreach (var (bundle, chunk) in chunks)
            {
                // I don't want to send a header too big
                const int chunkSize = 100;
                var chunkList = chunk.ToList();

                for (var i = 0; i < chunkList.Count; i += chunkSize)
                {
                    var c = chunkList.Skip(i).Take(chunkSize);
                    await DownloadChunk(game, bundle, c.Select(x => x.Item1));
                }
            }
        }
        finally
        {
            _maxFilesSemaphore.Release();
        }
    }

    private async Task DownloadChunk(string game, BundleT bundle, IEnumerable<ChunkT> chunks)
    {
        var hash = Downloader.ByteArrayToHexString(bundle.Hash);

        if (_filesToSave.ContainsKey(hash) || _downloadedChunks.Contains(hash))
        {
            Logger.Info($"Bundle {Downloader.ByteArrayToHexString(bundle.Hash)} is already downloaded");
            return;
        }

        await _maxChunksSemaphore.WaitAsync();
        try
        {
            using var httpClient = new HttpClient();

            var bundleHash = Downloader.ByteArrayToHexString(bundle.Hash);
            var bundleDir = bundleHash[..2];
            var url = $"https://cytrus.cdn.ankama.com/{game}/bundles/{bundleDir}/{bundleHash}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // THE WEBSERVER HAVE THIS THING WHERE IF YOU DON'T PUT THINGS IN ORDER AND REMOVE DUPLICATES IT WILL JUST SEND THE WHOLE FILE
            chunks = chunks.OrderBy(x => x.Offset);
            chunks = chunks.DistinctBy(x => x.Offset);

            var range = chunks
                .Aggregate("bytes=", (current, chunkInBundle) => current + $"{chunkInBundle.Offset}-{chunkInBundle.Offset + chunkInBundle.Size - 1},");

            request.Headers.Add("Range", range[..^1]);

            var response = await httpClient.SendAsync(request);

            var contentType = response.Content.Headers.TryGetValues("Content-Type", out var values) ? values.FirstOrDefault() : null;

            if (contentType == null)
            {
                throw new InvalidOperationException("Response has no content type.");
            }

            // multipart/byteranges; boundary=CloudFront:F5748448D2038E4D18771C6BCAE3D4F6
            if (!contentType.StartsWith("multipart/byteranges"))
            {
                var remainingBytes = await response.Content.ReadAsByteArrayAsync();
                OnChunkDownloaded(remainingBytes);
                return;
            }

            var boundary = Encoding.UTF8.GetBytes("--" + contentType.Split("boundary=").Last());
            var rawContent = await response.Content.ReadAsByteArrayAsync();

            var parts = SplitByBoundary(rawContent, boundary);

            foreach (var part in parts)
            {
                if (part is [13, 10] or [45, 45, 13, 10])
                    continue;

                if (part.Length == 0) continue;

                using var stream = new MemoryStream(part);
                var headers = new Dictionary<string, string>();

                if (stream.ReadByte() == '\r' && stream.ReadByte() == '\n')
                {
                    // We skipped the initial \r\n
                }
                else
                {
                    // Reset if no initial \r\n
                    stream.Position = 0;
                }

                // Read headers manually
                var buffer = new List<byte>();
                byte previous = 0;
                while (stream.Position < stream.Length)
                {
                    var current = (byte)stream.ReadByte();
                    if (previous == '\r' && current == '\n')
                    {
                        var line = Encoding.UTF8.GetString(buffer.ToArray()).Trim();
                        buffer.Clear();

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            // End of headers
                            break;
                        }

                        var headerParts = line.Split(":", 2);
                        if (headerParts.Length == 2)
                        {
                            headers[headerParts[0].Trim()] = headerParts[1].Trim();
                        }
                    }
                    else if (current != '\r')
                    {
                        buffer.Add(current);
                    }

                    previous = current;
                }

                // Remaining bytes are the actual data
                var remainingBytes = new byte[stream.Length - stream.Position - 2];
                _ = stream.Read(remainingBytes, 0, remainingBytes.Length);

                _ = Encoding.UTF8.GetString(remainingBytes);
                
                // IT WILL NEVER HAPPEN BUT I WANNNA KEEP IT
                if (headers.TryGetValue("Content-Transfer-Encoding", out var encoding) && encoding == "base64")
                {
                    remainingBytes = Convert.FromBase64String(Encoding.UTF8.GetString(remainingBytes));
                }

                OnChunkDownloaded(remainingBytes);
            }
        }
        catch (Exception e)
        {
            Logger.Info($"Failed to download bundle {hash}: {e}");
        }
        finally
        {
            _maxChunksSemaphore.Release();
        }
    }

    // Utility to split byte array by boundary
    static List<byte[]> SplitByBoundary(byte[] content, byte[] boundary)
    {
        var parts = new List<byte[]>();
        var boundaryPosition = 0;
        var start = 0;

        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == boundary[boundaryPosition])
            {
                boundaryPosition++;

                if (boundaryPosition != boundary.Length)
                    continue;

                // Found a full boundary match
                parts.Add(content[start..(i - boundary.Length + 1)]);
                start = i + 1;
                boundaryPosition = 0;
            }
            else
            {
                boundaryPosition = 0;
            }
        }

        if (start < content.Length)
        {
            parts.Add(content[start..]);
        }

        return parts;
    }


    private void OnChunkDownloaded(byte[] data)
    {
        Interlocked.Add(ref _downloadedSize, data.Length);
        Interlocked.Add(ref _waitingBytes, data.Length);
        //Logger.Info($"Downloaded {data.Length} bytes, {_waitingBytes} bytes remaining");

        _waitHandle.Wait(_cancellationTokenSource.Token);

        if (_waitingBytes > MaxRamUsage)
        {
            _waitHandle.Reset();
        }

        var hash = SHA1.HashData(data);
        var hashAsString = ByteArrayToHexString(hash);

        _filesToSave.TryAdd(hashAsString, data);

    }

    private void SaveFile(FileT file, ChunkT chunk, byte[] content)
    {
        // save content in the file
        var path = Path.Combine(_path, file.Name);
        
        // create the directory if it doesn't exist
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        fs.Seek(chunk.Offset, SeekOrigin.Begin);
        fs.Write(content);

        //Logger.Info($"Saved {content.Length} bytes to {path}");
        Interlocked.Add(ref _installedSize, content.Length);
        
        _downloadedFiles.TryAdd(file.Name, true);
    }

    private static string ByteArrayToHexString(List<sbyte> byteArray)
    {
        return string.Concat(byteArray.Select(b => b.ToString("x2")));
    }

    private static bool AreSByteEqual(List<sbyte> a, List<sbyte> b)
    {
        if (a.Count != b.Count)
            return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                return false;
        }

        return true;
    }

    string ByteArrayToHexString(byte[] byteArray)
    {
        // convert to sbyte first
        var byteArraySbyte = byteArray.Select(b => (sbyte)b).ToList();

        return Downloader.ByteArrayToHexString(byteArraySbyte);
    }
}