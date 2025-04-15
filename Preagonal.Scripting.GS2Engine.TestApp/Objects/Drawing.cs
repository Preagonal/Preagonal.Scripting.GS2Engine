using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.TestApp.Objects;

public class Drawing : VariableCollection
{
	private string? _image = "";

	/*
	public Drawing ( string img, int x, int y ) : this ( img, x, y, null, null, null, null, null, null )
	{
	}

	public Drawing ( string img, int x, int y, int cropx, int cropy, int width, int height ) : this ( img, x, y, cropx, cropy, width, height, null, null )
	{
	}
	*/
	public Drawing(
		string? image,
		int x,
		int y,
		int? cropx = null,
		int? cropy = null,
		int? width = null,
		int? height = null,
		float scale = 1f,
		float rotation = 0f
	)
	{
		_image   = image;
		//Position = new(x, y);

		//if (cropx != null && cropy != null && width != null && height != null)
		//	Source = new(cropx.Value, cropy.Value, width.Value, height.Value);

		Rotation = rotation;
		//Scale    = scale;

		//Game1.GetInstance ().SpriteBatch.Draw ( this._img, _position, _source, Color.White, rotation, new Vector2 ( 0, 0 ), scale, null, 0 );
	}

	public double Rotation
	{
		get => GetVariable("rotation").GetValue<double>();
		private set => AddOrUpdate("rotation", value.ToStackEntry());
	}
	public ImgVis     Layer    { get; private set; } = ImgVis.DrawOverPlayer;

	public void ShowImg(string? image, int x, int y)
	{
		_image   = image;
	}

	public void ChangeImgVis(ImgVis imgVis) => Layer = imgVis;

	//public void ChangeImgPart(int x, int y, int w, int h) => Source = new Rectangle(x, y, w, h);
}

public enum ImgVis
{
	DrawUnderPlayer = 0,
	LikeThePlayer   = 1,
	DrawOverPlayer  = 2,
	OnScreen        = 4,
}