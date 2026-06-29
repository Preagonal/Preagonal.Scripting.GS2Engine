using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
public class GuiControl : ScriptVariable, IGuiControl, IDisposable
{
	public new static readonly GuiControlProperties PropertiesInstance = [];
	public override IScriptProperties Properties => PropertiesInstance;

	protected readonly string  Id;
	protected readonly Script Script;

	public GuiControl(string id, Script script)
	{
		Script?.ScriptManager.GlobalVariables.AddOrUpdate(id.ToLower(), this.ToStackEntry());
		Console.WriteLine($"Creating control with ID {id}");
		Id           = id;
		Script       = script;
		Active       = true;
		CanMove      = true;
		CanResize    = true;
		ClipMove     = false;
		ClipChildren = false;
		HorizSizing  = "";
		VertSizing   = "";
		MinSize      = "";
		MinExtent    = "";
		Hint         = "";
		X            = -1;
		Y            = -1;
		Width        = -1;
		Height       = -1;
	}

	public bool         Active        { get; set; }
	public bool         Awake         { get; set; }
	public bool         CanMove       { get; set; }
	public bool         CanResize     { get; set; }
	public bool         ClipChildren  { get; set; }
	public bool         ClipMove      { get; set; }
	public bool         ClipToBounds  { get; set; }
	public int          Cursor        { get; set; }
	public bool         Editing       { get; set; }
	public IGuiControl? Parent        { get; set; }
	public bool         Flickering    { get; set; }
	public int          FlickerTime   { get; set; }
	public string       Hint          { get; set; }
	public string       HorizSizing   { get; set; }
	public string       VertSizing    { get; set; }
	public int          Layer         { get; }
	public string       MinExtent     { get; set; }
	public string       MinSize       { get; set; }
	public IGuiControl? Profile       { get; set; }
	public bool         ResizeWidth   { get; set; }
	public bool         ResizeHeight  { get; set; }
	public int          ScrollLineX   { get; set; }
	public int          ScrollLineY   { get; set; }
	public bool         ShowHint      { get; set; }
	public bool         UseOwnProfile { get; set; }
	public bool         Visible       { get; set; }
	public int          Width         { get; set; }
	public int          Height        { get; set; }
	public int          X             { get; set; }
	public int          Y             { get; set; }

	public string Extent
	{
		get => GetExtentCallback();
		set => SetExtentCallback(value);
	}

	public string ClientExtent
	{
		get => GetClientExtentCallback();
		set => SetClientExtentCallback(value);
	}

	public string Position
	{
		get => GetPositionCallback();
		set => SetPositionCallback(value);
	}

	public HashSet<IGuiControl?> Controls { get; } = [];

	public void Dispose() => Active = false;

	public void Destroy()
	{
		//lock (controls)
		{
			foreach (var control in Controls) control?.Destroy();
			Controls.Clear();
		}
		Dispose();
	}

	public void AddControl(IGuiControl? obj)
	{
		if (obj == null) return;
		obj.Parent = this;
		lock (Controls)
		{
			Controls.Add(obj);
		}
	}

	// ReSharper disable once UnusedMember.Global
	protected void CallAction() => Script?.Call($"{Id}.onAction", []).ConfigureAwait(false).GetAwaiter().GetResult();


	public virtual void Draw()
	{
		//lock (controls)
		{
			foreach (var control in Controls) control?.Draw();
		}
	}

	protected void SetClientExtentCallback(object? posVar)
	{
		switch (posVar)
		{
			case List<object> var:
				Width  = (int)(double)(var[0] ?? "-1");
				Height = (int)(double)(var[1] ?? "-1");
				break;
			case TString posVarString:
			{
				string? positionString = posVarString;
				if (positionString?.Length <= 0 || positionString == null) return;
				var p = positionString.Split(' ');

				if (double.TryParse(p[0], out var p0)) Width  = (int)p0;
				if (double.TryParse(p[1], out var p1)) Height = (int)p1;
				break;
			}
		}
	}
	private string GetClientExtentCallback() => $"{Width} {Height}";

	protected void SetExtentCallback(object? posVar)
	{
		switch (posVar)
		{
			case List<object> var:
				Width  = (int)(double)(var[0] ?? "-1");
				Height = (int)(double)(var[1] ?? "-1");
				break;
			case string posVarString:
			{
				if (posVarString.Length <= 0) return;
				var p = posVarString.Split(' ');

				if (double.TryParse(p[0], out var p0)) Width  = (int)p0;
				if (double.TryParse(p[1], out var p1)) Height = (int)p1;
				break;
			}
			case TString posVarString:
			{
				var positionString = posVarString.ToString();
				if (positionString.Length <= 0) return;
				var p = positionString.Split(' ');

				if (double.TryParse(p[0], out var p0)) Width  = (int)p0;
				if (double.TryParse(p[1], out var p1)) Height = (int)p1;
				break;
			}
		}
	}

	private string GetExtentCallback() => $"{Width} {Height}";

	private string GetPositionCallback() => $"{X} {Y}";

	private void SetPositionCallback(object? posVar)
	{
		switch (posVar)
		{
			case List<object> var:
				X = (int)(double)(var[0] ?? "-1");
				Y = (int)(double)(var[1] ?? "-1");
				break;
			case TString posVarString:
			{
				string? positionString = posVarString;
				if (positionString?.Length <= 0 || positionString == null) return;
				var p = positionString.Split(' ');

				if (double.TryParse(p[0], out var p0)) X = (int)p0;
				if (double.TryParse(p[1], out var p1)) Y = (int)p1;
				break;
			}
		}
	}
}