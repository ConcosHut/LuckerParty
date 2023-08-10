using LuckerGame.Entities;
using LuckerGame.UI;
using Sandbox;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

public class Racer : Pawn
{
	public float Speed;
	private float SpeedModifier = 1f;
	private Random Random { get; set; }
	private HoveringText NameTag { get; set; }
	
	public Racer()
	{

	}

	public Racer( Random random )
	{
		Random = random;
	}

	public override void ClientSpawn()
	{
		base.ClientSpawn();
		NameTag = new( Name, this );
	}

	public void ContinueRacing()
	{
		SetAnimParameter( "move_x", Speed * 500f * SpeedModifier );
		ModifySpeed();
		Position = Position.WithY( Position.y - (Speed * SpeedModifier) );
		
	}

	private void ModifySpeed()
	{
		var roll = Random.NextDouble();
		switch ( roll )
		{
			case >= .90 and < .95:
				SpeedModifier = 2f;
				break;
			case >= .95:
				SpeedModifier = 0.5f;
				break;
		}
	}

	public void StopRacing()
	{
		SetAnimParameter( "move_x", 0 );
	}

	public void GenerateSpeed( double minSpeed, double maxSpeed )
	{
		Speed = (float)(RandomExtensions.NextDouble( Random, minSpeed, maxSpeed ));
	}
}
