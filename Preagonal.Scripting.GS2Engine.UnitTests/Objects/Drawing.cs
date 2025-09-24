using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests.Objects;

public class Drawing : ScriptVariable
{
	public new static readonly DrawingProperties PropertiesInstance = [];
	public override            IScriptProperties Properties => PropertiesInstance;
	private protected          string?           _image = "";

	public Drawing(
		string? image,
		int x,
		int y,
		int? cropx = null,
		int? cropy = null,
		int? width = null,
		int? height = null,
		float zoom = 1f,
		float rotation = 0f
	)
	{
		_image   = image;
		//Position = new(x, y);

		//if (cropx != null && cropy != null && width != null && height != null)
		//	Source = new(cropx.Value, cropy.Value, width.Value, height.Value);

		Rotation = rotation;
		Zoom     = zoom;
		Hidden  = false;
	}

	//public virtual Texture2D? Image    => TextureSystem.GetInstance().GetImage(_image);
	//public         Vector2    Position { get; private protected set; }
	//public         Rectangle? Source   { get; private set; }
	public         bool       Hidden   { get; private protected set; }
	//public         Color      Color    { get; private protected set; } = Color.White;

	public double Rotation { get; set; }
	public ImgVis Layer    { get; private set; } = ImgVis.DrawOverPlayer;

	public void ShowImg(string? image, int x, int y)
	{
		_image   = image;
		//Position = new(x, y);
		Hidden  = false;
	}

	public void ChangeImgVis(ImgVis imgVis) => Layer = imgVis;
	//public void ChangeImgPart(int x, int y, int w, int h) => Source = new Rectangle(x, y, w, h);
	public void ChangeImgZoom(double zoom)                => Zoom = (float)zoom;
	//public void ChangeImgColors(int r, int g, int b, int a) => Color = new(r, g, b, a);
	public void Hide() => Hidden = true;
	public void Show() => Hidden = false;
	public float Zoom { get; private set; } = 1.00f;
}