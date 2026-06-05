using UnityEngine;

// Anything that can take a hit. The reaction is the implementer's choice — the player flashes the screen red
// and gets knocked back (PlayerHurt); a creature tints its own mesh red (MobHurt). An attacker just calls Hit()
// on whatever it struck and the right feedback happens, so player-vs-mob and mob-vs-mob share one path.
public interface IHittable
{
    void Hit(int damage, Vector3 from);
}
