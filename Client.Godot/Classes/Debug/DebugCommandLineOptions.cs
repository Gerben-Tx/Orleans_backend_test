using CommandLine;

namespace Client.Godot.Classes.Debug;

// ReSharper disable once ClassNeverInstantiated.Global
// This class is dynamically instantiated by the CommandLineParser package
public class DebugCommandLineOptions {
    [Option("randomLogin", Required = false, Default = false,
        HelpText = "Automatically logs the client in with a random name")]
    public bool RandomLogin { get; set; }
    
    [Option("loginName", Required = false, Default = null,
        HelpText = "Automatically logs the client in with the given name")]
    public string LoginName { get; set; }
    
    [Option("chunk", Required = false, Default = null, HelpText = "Automatically joins the specified chunk")]
    public int? Chunk { get; set; }
}