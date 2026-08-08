using System;
using Client.Godot.Classes;
using Godot;

namespace Client.Godot;

public partial class Tile : MeshInstance3D {
    [Export] private CollisionShape3D _collisionShape3D = null!;
    public Vector2I WorldPosition { get; set; }
    public WorldChunk WorldChunk { get; set; }
    // @formatter:off
    public static Vector2I TileSize { get; } = new(5, 5); // TODO: new(1, 1) with chunk size 30 and
                                                          //  ChunkVisibilityRadius 2 takes long to load.
                                                          //  Nothing is slow, it's just a lot of tiles to instantiate
    // @formatter:on 

    public override void _EnterTree() {
        base._EnterTree();

        SetSize();
    }

    public override void _Ready() {
        Area3D area3D = GetNode<Area3D>("Area3D");
        area3D.InputRayPickable = true;
        area3D.InputEvent += OnArea3DInputEvent;
    }

    private void SetSize() {
        PlaneMesh planeMesh = (PlaneMesh)Mesh;
        planeMesh.Size = TileSize;
        ((BoxShape3D)_collisionShape3D.Shape).Size = new Vector3(TileSize.X, 0, TileSize.Y);
    }

    public override void _Process(
        double delta
    ) { }

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