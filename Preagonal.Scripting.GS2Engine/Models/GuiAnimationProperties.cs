using Preagonal.Scripting.GS2Engine.Models.Properties;

namespace Preagonal.Scripting.GS2Engine.Models;

public class GuiAnimationProperties : ScriptProperties<GuiAnimation>
{
	public GuiAnimationProperties() : base(typeof(ScriptVariable))
	{
		var properties = new PropertyDefinitions<GuiAnimation>
		{
			{ "currenttime", "The animation's current playback time.", animation => animation.CurrentTime, (animation, value) => animation.CurrentTime = value },
			{ "alpha", "The target opacity for a transform transition.", animation => animation.Alpha, (animation, value) => animation.Alpha = value },
			{ "amplitude", "The movement or zoom amplitude for moveupdown, moveleftright, and zoominout.", animation => animation.Amplitude, (animation, value) => animation.Amplitude = value },
			{ "bounds", "The target {x,y,width,height} rectangle for a transform transition.", animation => animation.Bounds, (animation, value) => animation.Bounds = value },
			{ "delay", "The delay in seconds before animation playback begins.", animation => animation.Delay, (animation, value) => animation.Delay = value },
			{ "duration", "The animation's running time in seconds.", animation => animation.Duration, (animation, value) => animation.Duration = value },
			{ "interval", "The cycle interval for moveupdown, moveleftright, and zoominout.", animation => animation.Interval, (animation, value) => animation.Interval = value },
			{ "rotation", "The target rotation for a transform transition.", animation => animation.Rotation, (animation, value) => animation.Rotation = value },
			{ "sound", "The sound to play when the animation begins.", animation => animation.Sound, (animation, value) => animation.Sound = value },
			{ "tabfirstonshow", "Requests tab focus after showing the control; enabled by default.", animation => animation.TabFirstOnShow, (animation, value) => animation.TabFirstOnShow = value },
			{ "timing", "The timing curve: linear or sinus, with sinus easing the transition.", animation => animation.Timing, (animation, value) => animation.Timing = value },
			{ "transition", "The transition name, selecting a transform, fade, directional move or flip, zoom, grow, shrink, or rotation animation.", animation => animation.Transition, (animation, value) => animation.Transition = value }
		};

		AddProperties(this, properties);
		Compile();
	}
}