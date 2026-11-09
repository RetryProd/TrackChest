using System.Text;

namespace ChestCounter;

/// <summary>
/// Command line tools for whoever publishes a catalog:
///   --addon-keygen dossier            makes addon-private.key (keep secret) and addon-public.key (goes into assets\)
///   --addon-sign fichier cle-privee   writes fichier.sig next to the file (the signed index.json)
///   --addon-verify fichier cle-pub    checks fichier against fichier.sig
///   --addon-check add-on.zip          runs the same checks as the app does at install time
/// </summary>
internal static class AddonTools
{
    public static int Run(string[] args)
    {
        NativeConsole.Ensure(attachToParent: true);
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            switch (args[0])
            {
                case "--addon-keygen" when args.Length == 2:
                {
                    var folder = Path.GetFullPath(args[1]);
                    Directory.CreateDirectory(folder);
                    var privatePath = Path.Combine(folder, "addon-private.key");
                    var publicPath = Path.Combine(folder, "addon-public.key");
                    if (File.Exists(privatePath)) throw new InvalidOperationException($"{privatePath} existe déjà : elle n’est jamais écrasée.");
                    var (priv, pub) = AddonTrust.NewKeyPair();
                    File.WriteAllText(privatePath, priv);
                    File.WriteAllText(publicPath, pub);
                    Console.WriteLine($"Clé privée (SECRÈTE, ne la publie jamais) : {privatePath}");
                    Console.WriteLine($"Clé publique (à copier dans assets\\addon-public.key puis recompiler) : {publicPath}");
                    return 0;
                }
                case "--addon-sign" when args.Length == 3:
                {
                    var file = Path.GetFullPath(args[1]);
                    var signature = AddonTrust.Sign(File.ReadAllBytes(file), File.ReadAllText(Path.GetFullPath(args[2])));
                    File.WriteAllText(file + ".sig", signature);
                    Console.WriteLine($"Signature écrite : {file}.sig");
                    return 0;
                }
                case "--addon-verify" when args.Length == 3:
                {
                    var file = Path.GetFullPath(args[1]);
                    var ok = AddonTrust.Verify(File.ReadAllBytes(file), File.ReadAllText(file + ".sig"), File.ReadAllText(Path.GetFullPath(args[2])));
                    Console.WriteLine(ok ? "Signature valide." : "Signature INVALIDE.");
                    return ok ? 0 : 1;
                }
                case "--addon-check" when args.Length == 2:
                {
                    var manifest = AddonPackage.Read(File.ReadAllBytes(Path.GetFullPath(args[1])));
                    Console.WriteLine($"OK : {manifest.Name} {manifest.Version} ({manifest.Id}) · permissions : {string.Join(", ", manifest.Permissions)}");
                    return 0;
                }
                default:
                    Console.Error.WriteLine("Arguments invalides. Voir --help.");
                    return 2;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or FormatException or System.Security.Cryptography.CryptographicException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
