using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Preagonal.Scripting.GS2Engine.Extensions;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.GS2.Script;

namespace Preagonal.Scripting.GS2Engine.Models;

[SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
public class GuiControl : VariableCollection, IGuiControl, IDisposable
{
	protected readonly string  Id;
	protected readonly Script? Script;

	public GuiControl(string id, Script? script)
	{
		Console.WriteLine($"Creating control with ID {id}");
		Id     = id;
		Script = script;
		Script.GlobalVariables.AddOrUpdate(id.ToLower(), this.ToStackEntry());

		Active       = true;
		CanMove      = true;
		CanResize    = true;
		ClipMove     = false;
		ClipChildren = false;
		X            = -1;
		Y            = -1;
		Width        = -1;
		Height       = -1;
		AddOrUpdate(
			"addcontrol",
			new Script.Command(
				(m, a) =>
				{
					if (!(a?.Length > 0)) return 0.ToStackEntry();
					
					var control = m.GetEntry(a[0], StackEntryType.Variable).GetValue<IGuiControl>();
					if (control != null)
						AddControl(control);

					return 0.ToStackEntry();
				}
			).ToStackEntry()
		);

		SetCallback("extent", SetExtentCallback);
		GetCallback("extent", GetExtentCallback);
		SetCallback("clientextent", SetClientExtentCallback);
		GetCallback("clientextent", GetClientExtentCallback);
		SetCallback("position", PositionCallback);
	}

	public bool Active
	{
		get => GetVariable("active").GetValue<bool>();
		private set => AddOrUpdate("active", value.ToStackEntry());
	}

	public bool Awake => GetVariable("awake").GetValue<bool>();

	public bool CanMove
	{
		get => GetVariable("canmove").GetValue<bool>();
		set => AddOrUpdate("canmove", value.ToStackEntry());
	}

	public bool CanResize
	{
		get => GetVariable("canresize").GetValue<bool>();
		set => AddOrUpdate("canresize", value.ToStackEntry());
	}

	public bool ClipChildren
	{
		get => GetVariable("clipchildren").GetValue<bool>();
		set => AddOrUpdate("clipchildren", value.ToStackEntry());
	}

	public bool ClipMove
	{
		get => GetVariable("clipmove").GetValue<bool>();
		set => AddOrUpdate("clipmove", value.ToStackEntry());
	}

	public bool ClipToBounds
	{
		get => GetVariable("cliptobounds").GetValue<bool>();
		set => AddOrUpdate("cliptobounds", value.ToStackEntry());
	}

	public HashSet<IGuiControl?> Controls { get; } = [];

	public int Cursor
	{
		get => GetVariable("cursor").GetValue<int>();
		set => AddOrUpdate("cursor", value.ToStackEntry());
	}

	public bool Editing
	{
		get => GetVariable("editing").GetValue<bool>();
		set => AddOrUpdate("editing", value.ToStackEntry());
	}

	public string Extent
	{
		get => GetVariable("extent").GetValue<TString>() ?? string.Empty;
		set => AddOrUpdate("extent", value.ToStackEntry());
	}

	public bool Flickering
	{
		get => GetVariable("flickering").GetValue<bool>();
		set => AddOrUpdate("flickering", value.ToStackEntry());
	}

	public int FlickerTime
	{
		get => GetVariable("flickertime").GetValue<int>();
		set => AddOrUpdate("flickertime", value.ToStackEntry());
	}

	public string Hint
	{
		get => GetVariable("hint").GetValue<TString>() ?? string.Empty;
		set => AddOrUpdate("hint", value.ToStackEntry());
	}
	public string HorizSizing
	{
		get => GetVariable("horizsizing").GetValue<TString>() ?? string.Empty;
		set => AddOrUpdate("horizsizing", value.ToStackEntry());
	}
	public int layer { get; }
	public string MinExtent
	{
		get => GetVariable("minextent").GetValue<TString>() ?? string.Empty;
		set => AddOrUpdate("minextent", value.ToStackEntry());
	}
	public string MinSize
	{
		get => GetVariable("minsize").GetValue<TString>() ?? string.Empty;
		set => AddOrUpdate("minsize", value.ToStackEntry());
	}
	public string position
	{
		get => GetVariable("position").GetValue<TString>() ?? string.Empty;
		set => AddOrUpdate("position", value.ToStackEntry());
	}
	public IGuiControl? profile
	{
		get => GetVariable("profile").GetValue<IGuiControl>();
		set => AddOrUpdate("profile", value.ToStackEntry());
	}

	public bool ResizeHeight
	{
		get => GetVariable("resizeheight").GetValue<bool>();
		set => AddOrUpdate("resizeheight", value.ToStackEntry());
	}

	public bool ResizeWidth
	{
		get => GetVariable("resizewidth").GetValue<bool>();
		set => AddOrUpdate("resizewidth", value.ToStackEntry());
	}

	public int scrolllinex { get; set; }
	public int scrollliney { get; set; }

	public bool ShowHint
	{
		get => GetVariable("showhint").GetValue<bool>();
		set => AddOrUpdate("showhint", value.ToStackEntry());
	}

	public bool UseOwnProfile
	{
		get => GetVariable("useownprofile").GetValue<bool>();
		set => AddOrUpdate("useownprofile", value.ToStackEntry());
	}

	public string vertsizing
	{
		get => GetVariable("vertsizing").GetValue<TString>() ?? string.Empty;
		set => AddOrUpdate("vertsizing", value.ToStackEntry());
	}

	public bool Visible
	{
		get => GetVariable("visible").GetValue<bool>();
		set => AddOrUpdate("visible", value.ToStackEntry());
	}

	public int Height
	{
		get => (int)GetVariable("height").GetValue<double>();
		set => AddOrUpdate("height", value.ToStackEntry());
	}

	public int Width
	{
		get => (int)GetVariable("width").GetValue<double>();
		set => AddOrUpdate("width", value.ToStackEntry());
	}

	public int X
	{
		get => (int)GetVariable("x").GetValue<double>();
		set => AddOrUpdate("x", value.ToStackEntry());
	}

	public int Y
	{
		get => (int)GetVariable("y").GetValue<double>();
		set => AddOrUpdate("y", value.ToStackEntry());
	}

	public void Dispose()
	{
		Active = false;
	}

	public void Destroy()
	{
		//lock (controls)
		{
			foreach (var control in Controls) control?.Destroy();
			Controls.Clear();
		}
		Dispose();
	}

	public IGuiControl? parent
	{
		get => GetVariable("parent").GetValue<IGuiControl>();
		set => AddOrUpdate("parent", value.ToStackEntry());
	}

	public void AddControl(IGuiControl? obj)
	{
		if (obj == null) return;
		obj.parent = this;
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
				string[] p = positionString.Split(' ');

				if (double.TryParse(p[0], out var p0)) Width  = (int)p0;
				if (double.TryParse(p[1], out var p1)) Height = (int)p1;
				break;
			}
		}
	}
	private object? GetClientExtentCallback() => $"{Width} {Height}";

	protected void SetExtentCallback(object? posVar)
	{
		switch (posVar)
		{
			case List<object> var:
				Width  = (int)(double)(var[0] ?? "-1");
				Height = (int)(double)(var[1] ?? "-1");
				break;
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

	private object? GetExtentCallback() => $"{Width} {Height}";

	protected void PositionCallback(object? posVar)
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
				string[] p = positionString.Split(' ');

				if (double.TryParse(p[0], out var p0)) X = (int)p0;
				if (double.TryParse(p[1], out var p1)) Y = (int)p1;
				break;
			}
		}
	}
}