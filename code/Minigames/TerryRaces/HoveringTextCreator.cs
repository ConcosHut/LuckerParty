using LuckerGame.UI;
using Sandbox;

public class HoveringTextCreator : Entity
{
	private string Text { get; set; }
	private Entity TargetEntity { get; set; }

	public HoveringTextCreator()
	{

	}
	public HoveringTextCreator(string text, Entity targetEntity)
	{
		Text = text;
		TargetEntity = targetEntity;
	}
	public override void ClientSpawn()
	{
		base.ClientSpawn();
		var hoveringText = new HoveringText(Text, TargetEntity);
	}
}
