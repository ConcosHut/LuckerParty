using System.Collections.Generic;
using System.Linq;
using LuckerGame.Entities.Weapons;
using Sandbox;

namespace LuckerGame.Components.Pawn;

public partial class PawnInventory : EntityComponent<Entities.Pawn>
{
	[Net] private List<Weapon> Weapons { get; set; } = new List<Weapon>();
	[Net, Predicted] public Weapon ActiveWeapon { get; private set; }

	public List<Weapon> GetWeapons()
	{
		return Weapons.ToList();
	}

	public bool AddWeapon( Weapon weapon )
	{
		if ( Weapons.Any( w => w.GetType() == weapon.GetType() ) )
		{
			return false;
		}

		Weapons.Add( weapon );
		if ( Weapons.Count == 1 )
		{
			SetActiveWeapon( weapon );
		}

		return true;
	}

	public void NextWeapon()
	{
		if ( ActiveWeapon == null )
		{
			ActiveWeapon = Weapons.FirstOrDefault();
			return;
		}

		var weaponIndex = Weapons.IndexOf( ActiveWeapon );
		var nextIndex = (weaponIndex + 1) % Weapons.Count;
		var weapon = Weapons[nextIndex];
		SetActiveWeapon( weapon );
	}

	public void SetActiveWeapon( Weapon weapon )
	{
		ActiveWeapon?.OnHolster();
		ActiveWeapon = weapon;
		ActiveWeapon.OnEquip( Entity );
	}
}
