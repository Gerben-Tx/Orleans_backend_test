using System.Collections.Generic;

namespace Client.Godot.Classes;

public class PlayerList : List<Player> {
    public new void Add(Player player) {
        if (Find(p => p.Id == player.Id) != null) {
            // Player already exists
            return;
        }
        
        base.Add(player);
    }
}