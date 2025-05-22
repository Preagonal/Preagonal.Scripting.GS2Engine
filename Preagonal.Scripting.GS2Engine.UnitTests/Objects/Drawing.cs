using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests.Objects;

public class Drawing : VariableCollection
{
	private string? _image;

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
		Rotation = rotation;
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

}