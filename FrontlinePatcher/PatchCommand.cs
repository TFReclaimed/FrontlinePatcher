using System.ComponentModel;
using FrontlinePatcher.Files;
using FrontlinePatcher.Patch;
using FrontlinePatcher.Patch.Patches;
using FrontlinePatcher.Tools;
using Spectre.Console;
using Spectre.Console.Cli;

namespace FrontlinePatcher;

public class PatchCommand : AsyncCommand<PatchCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Input APK file to patch.")]
        [CommandArgument(0, "<APK>")]
        public required string InputApk { get; init; }
        
        [Description("Output APK file after patching.")]
        [CommandArgument(1, "<Output APK>")]
        public required string OutputApk { get; init; }
        
        [Description("Path to the keystore for signing the APK.")]
        [CommandOption("--keystore")]
        public string? KeystorePath { get; init; }
        
        [Description("Password for the keystore.")]
        [CommandOption("--keystore-password")]
        public string? KeystorePassword { get; init; }
        
        [Description("Frontline server URL.")]
        [CommandOption("--frontline-url")]
        public string? FrontlineUrl { get; init; }
        
        [Description("OpenTOY server URL.")]
        [CommandOption("--opentoy-url")]
        [DefaultValue("opentoy.tfflinternal.com")]
        public required string OpenToyUrl { get; init; }
        
        [Description("Pause before building the APK allowing for manual modifications.")]
        [CommandOption("--pause-before-build")]
        public bool? PauseBeforeBuild { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine("TITANFALL: FRONTLINE PATCHER");
        AnsiConsole.WriteLine();

        AnsiConsole.WriteLine("--- Find Tools ---");

        var toolFinder = new ToolFinder();
        toolFinder.FindTools();

        var apkToolPath = toolFinder.GetToolPath("apktool");
        var apkSignerPath = toolFinder.GetToolPath("apksigner");
        var keytoolPath = toolFinder.GetToolPath("keytool");

        if (string.IsNullOrEmpty(apkToolPath) || string.IsNullOrEmpty(apkSignerPath) || string.IsNullOrEmpty(keytoolPath))
        {
            AnsiConsole.MarkupLine("[red]Required tool(s) not found![/]");
            return 1;
        }

        const string tempDir = "temp";
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }

        Directory.CreateDirectory(tempDir);

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("--- Decompile APK ---");

        var decompiledApkDir = Path.Combine(tempDir, "decompiled_apk");
        var decompileSuccess = await AnsiConsole.Status().StartAsync("Decompiling APK...", async _ =>
            await ApkTool.DecompileAsync(apkToolPath, settings.InputApk, decompiledApkDir));
        if (!decompileSuccess)
        {
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("--- Patch Assemblies ---");

        var managedPath = Path.Combine(decompiledApkDir, "assets", "bin", "Data", "Managed");
        var assemblyPath = Path.Combine(managedPath, "Assembly-CSharp.dll");
        var patchedAssemblyPath = Path.Combine(tempDir, "Assembly-CSharp.dll");
        if (!File.Exists(assemblyPath))
        {
            AnsiConsole.MarkupLine($"[red]Assembly not found at \"{assemblyPath}\"[/]");
            return 1;
        }

        var patcher = new AssemblyPatcher(assemblyPath);
        patcher.AddPatch(new GameDebugLogPatch());
        patcher.AddPatch(new StorePurchasePatch());
        patcher.AddPatch(new EmailLoginTimeoutPatch());

        var patchResult = AnsiConsole.Status().Start("Loading assembly...", ctx =>
        {
            patcher.LoadAssembly();
            ctx.Status("Applying patches...");
            
            if (patcher.ApplyPatches())
            {
                AnsiConsole.MarkupLine("[green]All patches applied successfully![/]");
                patcher.SaveAssembly(patchedAssemblyPath);
                return 0;
            }

            AnsiConsole.MarkupLine("[red]Failed to apply patches![/]");
            return 1;
        });

        if (patchResult != 0)
        {
            return patchResult;
        }

        File.Delete(assemblyPath);
        File.Move(patchedAssemblyPath, assemblyPath);
        AnsiConsole.WriteLine("Replaced original assembly with patched assembly.");
        
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("--- Patch Files ---");

        var modificationRules = new List<FileModificationRule>
        {
            // Unofficial official server only supports HTTPS
            new("assets/serverAddresses.json", @"http:\/\/", "https://"),
            
            // Replace S3 with our own server
            new("assets/serverAddresses.json", "pc-tffl-leaderboard.s3-website-us-east-1.amazonaws.com", "prod-us-east-1-gameserver-lb.tfflinternal.com"),
            new("assets/serverAddresses.json", "pc-tffl-html.s3-website-us-east-1.amazonaws.com", "news.tfflinternal.com"),
            
            // Replace Nexon TOY with OpenTOY
            new("smali/kr/co/nexon/toy/api/request/NXToyRequestType.smali", "m-api.nexon.com", settings.OpenToyUrl),

            // Remove scary permissions
            new("AndroidManifest.xml", """^[ \t]*<uses-permission android:name="(?:\.|android\.permission\.)?(GET_ACCOUNTS|READ_CONTACTS|READ_PHONE_STATE|GET_TASKS)"\s*\/>.*$""", string.Empty)
        };
        
        if (settings.FrontlineUrl is not null)
        {
            modificationRules.Add(new FileModificationRule("assets/serverAddresses.json", @"(?<=\/\/|"")([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}", settings.FrontlineUrl));
        }

        if (!FileModifier.ApplyModifications(decompiledApkDir, modificationRules))
        {
            AnsiConsole.MarkupLine("[red]Failed to apply file modifications![/]");
            return 1;
        }
        
        if (settings.PauseBeforeBuild == true)
        {
            AnsiConsole.MarkupLine("[yellow]Press enter to continue...[/]");
            Console.ReadLine();
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("--- Recompile APK ---");

        var recompileSuccess = await AnsiConsole.Status().StartAsync("Recompiling APK...", async _ =>
            await ApkTool.BuildApkAsync(apkToolPath, decompiledApkDir, settings.OutputApk));
        if (!recompileSuccess)
        {
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("--- Sign APK ---");

        const string fallbackKeystorePath = "frontline.keystore";
        const string fallbackKeystorePassword = "frontline";
        
        var keystorePath = settings.KeystorePath ?? fallbackKeystorePath;
        var keystorePassword = settings.KeystorePassword ?? fallbackKeystorePassword;

        if (!File.Exists(keystorePath))
        {
            if (settings.KeystorePath is not null && settings.KeystorePath != fallbackKeystorePath)
            {
                AnsiConsole.MarkupLine($"[red]Keystore file not found at \"{keystorePath}\"![/]");
                return 1;
            }
            
            AnsiConsole.MarkupLine("[red]Keystore path was not provided. Generating a new keystore for you...[/]");
            
            var keystoreGenerationSuccess = await AnsiConsole.Status().StartAsync("Generating keystore...", async _ =>
                await ApkSigner.GenerateKeystore(keytoolPath, keystorePath, keystorePassword));
            if (!keystoreGenerationSuccess)
            {
                return 1;
            }
        }

        var signingSuccess = await AnsiConsole.Status().StartAsync("Signing APK...", async _ =>
            await ApkSigner.SignApkAsync(apkSignerPath, settings.OutputApk, keystorePath, keystorePassword, settings.OutputApk));
        if (!signingSuccess)
        {
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("--- Clean Up ---");

        Directory.Delete(tempDir, true);
        AnsiConsole.WriteLine("Temporary files deleted.");

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Patched APK saved to \"{settings.OutputApk}\"![/]");
        
        return 0;
    }
}