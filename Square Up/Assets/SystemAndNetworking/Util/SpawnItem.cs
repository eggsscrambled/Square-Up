using UnityEngine;
using Fusion;

public class SpawnItem : NetworkBehaviour
{
    public NetworkObject spawnObject;

    public override void Spawned()
    {
        base.Spawned();

        if (Object.HasStateAuthority)
        {
            Runner.Spawn(spawnObject, this.transform.position, this.transform.rotation);
        }
    }
}