using System;

namespace Preagonal.Scripting.GS2Engine.Models;

public class GuiAnimation(GuiControl owner) : ScriptVariable("animation")
{
	private string? _bounds;
	private double? _originalAlpha;
	private string  _transition = string.Empty;

	public new static readonly GuiAnimationProperties PropertiesInstance = [];
	public override            IScriptProperties      Properties => PropertiesInstance;

	public GuiControl Owner { get; } = owner;

	public double CurrentTime { get; set; }
	public double Alpha       { get; set; } = 1;
	public double Amplitude   { get; set; } = 32;

	public string Bounds
	{
		get => _bounds ?? Owner.Bounds;
		set => _bounds = value;
	}

	public double Delay          { get; set; }
	public double Duration       { get; set; } = 1;
	public double Interval       { get; set; } = 1;
	public double Rotation       { get; set; }
	public string Sound          { get; set; } = string.Empty;
	public bool   TabFirstOnShow { get; set; } = true;
	public string Timing         { get; set; } = string.Empty;

	public string Transition
	{
		get => _transition;
		set
		{
			RestoreAlpha();
			_transition = value.ToLowerInvariant();
			if (_transition is "fadein" or "fadeout")
			{
				_originalAlpha = Owner.Alpha;
				if (_transition == "fadein") Owner.Alpha = 0;
			}
		}
	}

	internal bool Advance(double elapsedSeconds)
	{
		CurrentTime += Math.Max(0, elapsedSeconds);
		if (CurrentTime < Delay) return true;
		var progress   = Duration > 0 ? Math.Clamp((CurrentTime - Delay) / Duration, 0, 1) : 1;
		var transition = Transition.ToLowerInvariant();
		if (transition is "fadein" or "fadeout")
		{
			_originalAlpha ??= Owner.Alpha;
			Owner.Alpha    =   _originalAlpha.Value * (transition == "fadein" ? progress : 1 - progress);
		}

		return progress < 1;
	}

	internal void Complete()
	{
		RestoreAlpha();
		if (!string.IsNullOrEmpty(Transition) && Transition != "transform")
			Owner.SetVisible(!Transition.Contains("out", StringComparison.OrdinalIgnoreCase), false);
	}

	internal void RestoreAlpha()
	{
		if (_originalAlpha is not { } alpha) return;
		Owner.Alpha    = alpha;
		_originalAlpha = null;
	}
}