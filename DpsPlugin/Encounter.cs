
using System.Collections.Generic;

public class Encounter {
    struct abilityCast {
        uint actionId;
        uint actor;
        uint target;
    };

    Dictionary<uint, List<abilityCast>> abilitiesCast;

    public Encounter() {
        this.abilitiesCast = new Dictionary<uint, List<abilityCast>>();
    }

    public void RecordPacketAndGetPotency(Dictionary<uint, List<abilityCast>> abilitiesCast, System.IntPtr ptr, uint actorId) {
        
    }

}