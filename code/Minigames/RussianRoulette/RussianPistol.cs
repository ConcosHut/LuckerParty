using System;
using System.Collections.Generic;
using LuckerGame.Entities.Weapons;
using Sandbox;

namespace LuckerGame.Minigames.RussianRoulette;

public partial class RussianPistol : Weapon
{
	public override string ModelPath => "weapons/rust_pistol/rust_pistol.vmdl";
	public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";
	
	public override string WeaponName => "Russian Pistol";

	protected override List<string> HitTags => new List<string> { "victim" };

	public override CitizenAnimationHelper.HoldTypes HoldType => CitizenAnimationHelper.HoldTypes.Pistol;

	public override int MaxAmmo => 6;

	protected override float Damage => 1000f;

	[ClientRpc]
	protected virtual void ShootEffects()
	{
		Game.AssertClient();

		Particles.Create( "particles/pistol_muzzleflash.vpcf", EffectEntity, "muzzle" );

		Pawn.SetAnimParameter( "b_attack", true );
		ViewModelEntity?.SetAnimParameter( "fire", true );
	}

	public override void PrimaryAttack()
	{
		if ( Random.Shared.Next( 1, Ammo + 1 ) == 1)
		{
			ShootEffects();
			Pawn.PlaySound( "rust_pistol.shoot" );
			ShootBullet( 0.01f, 100, Damage, 10 );
		}
		else
		{
			Pawn.PlaySound( "player_use_fail" );
		}
		Ammo--;
	}

	protected override void Animate()
	{
		Pawn.SetAnimParameter( "holdtype", (int)CitizenAnimationHelper.HoldTypes.Pistol );
	}
}
