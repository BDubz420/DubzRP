using Sandbox;
using Sandbox.Citizen;
using System.Collections.Generic;

[Title("DubzRP Player Component")]
public sealed class DubzPlayer : Component
{
    //
    // --- Core Stats ---
    //

    [Sync, Property] public int Wallet { get; set; } = 100;
    [Sync, Property] public int Salary { get; set; } = 45;
    [Sync, Property] public string Job { get; set; } = "Citizen";

    [Property] public float MaxArmor { get; set; } = 100f;
    [Sync, Property] public int Armor { get; set; }

    [Property] public float MaxHealth { get; set; } = 100f;
    [Sync, Property] public float Health { get; set; } = 100f;

    [Sync, Property] public int Ammo { get; set; }
    [Property] public int MaxAmmo { get; set; } = 100;

    [Sync, Property] public string Name { get; set; } = "Unknown";
    [Sync, Property] public int Ping { get; set; }
    [Sync, Property] public bool Disconnected { get; set; }

    //
    // --- Global Registry ---
    //
    public static List<DubzPlayer> AllPlayers { get; } = new();

    //
    // --- Lifecycle ---
    //
    protected override void OnStart()
    {
        base.OnStart();

        if ( GameObject.Components.TryGet<PlayerController>( out var controller ) )
        {
            controller.EnablePressing = true;
        }

        Initialize();
    }

    public void Initialize()
    {
        Log.Info($"[DubzPlayer] Initialized on {GameObject.Name}");

        Health = MaxHealth;
        Armor = 0;
        GiveDefaultLoadout();

        if ( !AllPlayers.Contains( this ) )
        {
            AllPlayers.Add( this );
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        RemoveFromList();
    }

    //
    // --- Connectivity ---
    //
    public void SetConnected( string playerName )
    {
        Name = playerName;
        Disconnected = false;

        if ( !AllPlayers.Contains( this ) )
            AllPlayers.Add( this );
    }

    public void SetDisconnected()
    {
        Disconnected = true;
    }

    public void RemoveFromList()
    {
        AllPlayers.Remove( this );
    }

    //
    // --- Combat & Damage ---
    //
    public void TakeDamage( float damage )
    {
        float remaining = damage;

        if ( Armor > 0 )
        {
            if ( Armor >= remaining )
            {
                Armor -= (int)remaining;
                remaining = 0;
            }
            else
            {
                remaining -= Armor;
                Armor = 0;
            }
        }

        Health -= remaining;

        if ( Health <= 0f )
        {
            Die();
        }
    }

    private void Die()
    {
        Log.Info($"[DubzPlayer] {GameObject.Name} died.");

        Health = MaxHealth;
        Armor = 0;

        // TODO: add respawn / ragdoll logic here
    }

    private void GiveDefaultLoadout()
    {
        Log.Info($"[DubzPlayer] Giving default weapons to {GameObject.Name}");
    }

    //
    // --- Interaction Helpers ---
    //
    public void AddMoney( int amount )
    {
        Wallet += amount;
    }

    public void SpendMoney( int amount )
    {
        Wallet = System.Math.Max( 0, Wallet - amount );
    }
}
