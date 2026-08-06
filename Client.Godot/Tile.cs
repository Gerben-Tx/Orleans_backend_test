using Client.Godot.Classes;
using Godot;

namespace Client.Godot;

public partial class Tile : MeshInstance3D
{
	public Vector2I WorldPosition { get; set; }
	public WorldChunk WorldChunk { get; set; }
	public static Vector2I TileSize { get; } = new(5, 5);
	[Export]
	private CollisionShape3D _collisionShape3D = null!;
	
	public override void _Ready() {
		this.SetSize();
		
		Area3D area3D = GetNode<Area3D>("Area3D");
		area3D.InputRayPickable = true;
		area3D.InputEvent += OnArea3DInputEvent;
	}

	private void SetSize() {
		PlaneMesh planeMesh = (PlaneMesh) Mesh;
		planeMesh.Size = TileSize;
		((BoxShape3D)_collisionShape3D.Shape).Size = new Vector3(TileSize.X, 0, TileSize.Y);
	}

	public override void _Process(double delta)
	{
	}

	private void OnArea3DInputEvent(
		Node camera,
		InputEvent @event,
		Vector3 eventPosition,
		Vector3 normal,
		long shapeIndex
	) {
		// GD.Print($"OnArea3DInputEvent: {eventPosition}, {normal}, {shapeIndex}");

		if (@event is InputEventMouseButton mouseButton 
		    && mouseButton.ButtonIndex == MouseButton.Left
		    && mouseButton.Pressed
		) {
			GD.Print($"Tile clicked, position: {WorldPosition}, chunk: {WorldChunk.ChunkId}");
			
			// TODO: Send tile click event to server
		}
	}
}