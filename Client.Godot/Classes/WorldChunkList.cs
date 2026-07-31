using System.Collections.Generic;

namespace Client.Godot.Classes;

public class WorldChunkList : List<WorldChunk> {
    public new void Add(WorldChunk chunk) {
        if (Find(c => c.ChunkId == chunk.ChunkId) != null) {
            // Chunk already exists
            return;
        }
        
        base.Add(chunk);
    }
}