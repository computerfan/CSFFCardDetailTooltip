using System.Collections;
using System.IO.IsolatedStorage;
using System.Reflection;
using System.Runtime.Loader;

// Bridge to the pinned DepotDownloader's own serializer and isolated store.
// No Steam password is exported; the resulting refresh-token bundle is still a credential.
internal static class Program
{
    private const string Config = "account.config";
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2 || args[0] is not ("login" or "download" or "self-test"))
        {
            Console.Error.WriteLine("Usage: SteamSession <login|download|self-test> <DepotDownloader.dll> [login-output-file]");
            return 1;
        }
        try
        {
            var dll = Path.GetFullPath(args[1]);
            var resolver = new AssemblyDependencyResolver(dll);
            AssemblyLoadContext.Default.Resolving += (context, name) =>
                resolver.ResolveAssemblyToPath(name) is { } path ? context.LoadFromAssemblyPath(path) : null;
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
            var type = assembly.GetType("DepotDownloader.AccountSettingsStore", throwOnError: true)!;
            var store = (IsolatedStorageFile)type.GetField("IsolatedStorage", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            byte[]? previous = store.FileExists(Config) ? Read(store) : null;
            try
            {
                if (store.FileExists(Config)) store.DeleteFile(Config);
                if (args[0] == "self-test")
                {
                    // Exercise the released binary's serializer, not a substitute format.
                    type.GetMethod("LoadFromFile")!.Invoke(null, [Config]);
                    var instance = type.GetField("Instance")!.GetValue(null)!;
                    ((IDictionary)type.GetProperty("LoginTokens")!.GetValue(instance)!).Add("fixture", "not-a-real-token");
                    type.GetMethod("Save")!.Invoke(null, null);
                    byte[] bytes = Read(store);
                    Write(store, Convert.FromBase64String(Convert.ToBase64String(bytes)));
                    Validate(type, "fixture", false);
                    bool rejected = false;
                    try { Validate(type, "wrong-account", false); }
                    catch (InvalidDataException) { rejected = true; }
                    if (!rejected) throw new InvalidDataException();
                    var outputTest = new DownloadOutput(TextWriter.Null);
                    outputTest.WriteLine("Warning: Depot {0} does not have branch named \"{1}\". Trying {2} branch.", 2868861, "openbetabranch", "public");
                    if (!outputTest.BranchFallback) throw new InvalidDataException();
                    object? versionResult = assembly.EntryPoint!.Invoke(null, [new[] { "--version" }]);
                    int versionCode = versionResult is Task<int> versionTask ? await versionTask : (int)versionResult!;
                    if (versionCode != 0) throw new InvalidDataException();
                    Console.WriteLine("PASS: Session serialization, account mismatch, branch fallback detection and upstream entry point.");
                    return 0;
                }

                string username = Environment.GetEnvironmentVariable("STEAM_USERNAME") ?? "";
                if (string.IsNullOrWhiteSpace(username)) throw new InvalidDataException();
                bool login = args[0] == "login";
                if (login && (args.Length != 3 || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true"))
                    throw new InvalidDataException();
                if (!login)
                {
                    string bundle = Environment.GetEnvironmentVariable("STEAM_SESSION_B64") ?? "";
                    if (bundle.Length is 0 or > 48000) throw new InvalidDataException();
                    Write(store, Convert.FromBase64String(bundle));
                    Validate(type, username, Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true");
                    // Stop rather than hanging if Steam unexpectedly requests interactive authentication.
                    Console.SetIn(new NoInteractiveInput());
                }
                string directory = Environment.GetEnvironmentVariable("STEAM_DOWNLOAD_DIR") ?? throw new InvalidDataException();
                string branch = Environment.GetEnvironmentVariable("STEAM_BRANCH") ?? "openbetabranch";
                if (branch is not ("openbetabranch" or "public")) throw new InvalidDataException();
                var downloadArgs = new List<string> { "-app", "2868860", "-depot", "2868861",
                    "-branch", branch, "-os", "windows", "-osarch", "64", "-username", username,
                    "-remember-password", "-loginid", "2868860", "-dir", directory };
                if (login) downloadArgs.Add("-manifest-only");
                else
                {
                    downloadArgs.AddRange(["-filelist", Environment.GetEnvironmentVariable("STEAM_FILE_LIST") ?? throw new InvalidDataException(), "-validate"]);
                    string manifest = Environment.GetEnvironmentVariable("STEAM_MANIFEST") ?? "";
                    if (manifest.Length > 0)
                    {
                        if (!ulong.TryParse(manifest, out ulong id) || id == 0) throw new InvalidDataException();
                        downloadArgs.AddRange(["-manifest", manifest]);
                    }
                }
                // EntryPoint is the synchronous compiler wrapper for upstream's async Main.
                var originalOutput = Console.Out;
                var downloadOutput = new DownloadOutput(originalOutput);
                int code;
                try
                {
                    Console.SetOut(downloadOutput);
                    object? result = assembly.EntryPoint!.Invoke(null, [downloadArgs.ToArray()]);
                    code = result is Task<int> task ? await task : (int)result!;
                }
                finally { Console.SetOut(originalOutput); }
                if (downloadOutput.BranchFallback)
                {
                    Console.Error.WriteLine("Steam branch was unavailable; refusing DepotDownloader's public-branch fallback.");
                    return 1;
                }
                if (code != 0) return code;
                if (login)
                {
                    Validate(type, username, false);
                    string output = Path.GetFullPath(args[2]);
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    await File.WriteAllTextAsync(output, Convert.ToBase64String(Read(store)));
                    Console.WriteLine("Session saved to the requested file. Keep it private; do not paste it into chat or logs.");
                }
                return 0;
            }
            finally
            {
                if (previous != null) Write(store, previous);
                else if (store.FileExists(Config)) store.DeleteFile(Config);
                if (previous == null ? store.FileExists(Config) : !previous.SequenceEqual(Read(store)))
                    throw new IOException("Session store restoration failed.");
            }
        }
        catch
        {
            Console.Error.WriteLine("Steam session operation failed. Check the account/session setup; renew an expired session with Initialize-SteamSession.ps1.");
            return 1;
        }
    }

    private static void Validate(Type type, string username, bool mask)
    {
        type.GetField("Instance")!.SetValue(null, null);
        try
        {
            type.GetMethod("LoadFromFile")!.Invoke(null, [Config]);
            var instance = type.GetField("Instance")!.GetValue(null)!;
            var tokens = (IDictionary)type.GetProperty("LoginTokens")!.GetValue(instance)!;
            if (!tokens.Contains(username) || string.IsNullOrWhiteSpace(tokens[username] as string)) throw new InvalidDataException();
            if (mask)
                foreach (string property in new[] { "LoginTokens", "GuardData" })
                    foreach (DictionaryEntry item in (IDictionary)type.GetProperty(property)!.GetValue(instance)!)
                        if (item.Value is string value && value.Length > 0) Console.WriteLine("::add-mask::" + value);
        }
        finally { type.GetField("Instance")!.SetValue(null, null); }
    }

    private static byte[] Read(IsolatedStorageFile store)
    {
        using var stream = store.OpenFile(Config, FileMode.Open, FileAccess.Read);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static void Write(IsolatedStorageFile store, byte[] bytes)
    {
        using var stream = store.OpenFile(Config, FileMode.Create, FileAccess.Write);
        stream.Write(bytes);
    }

    private sealed class NoInteractiveInput : TextReader
    {
        public override string? ReadLine() => throw new InvalidOperationException("CI authentication cannot prompt.");
    }

    // The pinned upstream release otherwise silently succeeds after falling back to public.
    private sealed class DownloadOutput(TextWriter destination) : TextWriter
    {
        public bool BranchFallback { get; private set; }
        public override System.Text.Encoding Encoding => destination.Encoding;
        public override void Write(char value) => destination.Write(value);
        public override void Write(string? value) => destination.Write(value);
        public override void WriteLine(string? value)
        {
            if (value?.Contains("does not have branch named", StringComparison.Ordinal) == true)
                BranchFallback = true;
            destination.WriteLine(value);
        }
        public override void Flush() => destination.Flush();
    }
}
