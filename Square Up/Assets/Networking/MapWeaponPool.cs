using UnityEngine;
using Fusion;

[System.Serializable]

[CreateAssetMenu(fileName = "MapWeaponPool", menuName = "Combat/MapWeaponPool")]
public class MapWeaponPool : ScriptableObject
{
    public string poolName;
    public NetworkPrefabRef[] weapons;
}