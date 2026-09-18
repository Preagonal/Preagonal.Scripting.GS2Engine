namespace Preagonal.Scripting.GS2Engine.Models;

public interface IGuiControl
{
	public IGuiControl? Parent { get; set; }
	public void         Draw();
	void                Destroy();
	public void         AddControl(IGuiControl? obj);
	public void         SetSize(int width, int height);
}