using CommandLine;

namespace Client.Godot.Classes.Debug;

public class DebugCommandLineOptions {
    // --randomLogin --chunk=1
    [Option("randomLogin", Required = false, Default = false,
        HelpText = "Automatically logs the client in with a random name")]
    public bool RandomLogin { get; set; }
    [Option("chunk", Required = false, Default = null, HelpText = "Automatically joins the specified chunk")]
    public int? Chunk { get; set; }
}