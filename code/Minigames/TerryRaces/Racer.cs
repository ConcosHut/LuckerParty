using LuckerGame.Entities;
using Sandbox;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;

public class Racer : LuckerGame.Entities.Racer
{
	public float Speed;
	public void ContinueRacing()
	{
		Position = Position.WithY( Position.y - Speed );
		SetAnimParameter( "move_x", Speed*500f );
	}

	public void StopRacing()
	{

	}

	public void GenerateSpeed(Random random, double minSpeed, double maxSpeed)
	{
		Speed = (float)(RandomExtensions.NextDouble( random, minSpeed, maxSpeed ));
	}
}
