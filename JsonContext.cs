using fuck;
using System.Text.Json;
using System.Text.Json.Serialization;
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Config))]
public partial class JsonContext : JsonSerializerContext
{

}
public class Config
{
    public int delay { get; set; } = 0;
    public string mainProgram { get; set; } = string.Empty;
    public string mainProgram2 { get; set; } = string.Empty;
    public string startGate { get; set; } = "AND";
    public string exitGate { get; set; } = "AND";
    public List<string> onStart { get; set; } = new List<string>();
    public List<string> keepAlive { get; set; } = new List<string>();
    public List<string> killOnExit { get; set; } = new List<string>();
}
