using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace Client.Godot.Classes;

public class Player {
    public required string Id { get; init; }
    public Queue<Vector2>? Path { get; set; }
    
    public Vector2? GetNextPathPoint() {
        return Path?.Dequeue();
    }

    public void AddPathFromArray(
        Array<Array<int>> path
    ) {
       Path = new Queue<Vector2>(path.ToList().ConvertAll(x => new Vector2(x[0], x[1]))); 
    }
}