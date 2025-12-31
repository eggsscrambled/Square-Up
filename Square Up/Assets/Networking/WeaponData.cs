using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Combat/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string weaponID = "Unnamed";
    public float fireRate = 1f;
    public float bulletSpeed = 1f;
    public int bulletAmount = 1;
    public float spreadAmount = 1;
    public float maxSpreadDegrees = 45;
    public GameObject bulletPrefab;
    public GameObject bulletVisualPrefab; // Visual-only bullet (no NetworkObject)
    public int damage = 10;
    public float knockbackForce = 5f;
    public bool isAutomatic = false;
    public float recoilForce = 2f;
    public float bulletLifetime = 5f; // How long the bullet lives in seconds
}