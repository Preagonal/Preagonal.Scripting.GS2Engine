using Microsoft.Extensions.Logging.Testing;
using Preagonal.Scripting.GS2Engine.Enums;
using Preagonal.Scripting.GS2Engine.GS2.Script;
using Preagonal.Scripting.GS2Engine.Models;

namespace Preagonal.Scripting.GS2Engine.UnitTests;

public class ScriptTimeoutTests
{
	[Fact]
	public void Recurring_fifty_millisecond_timer_runs_twenty_times_across_sixty_frames()
	{
		var manager = new ScriptManager(new FakeLogger<ScriptManager>());
		var script  = new Script(manager, ScriptType.Weapon);
		var start   = DateTime.UtcNow.AddMinutes(-1);
		manager.BeginFrame(start);
		script.SetTimer(0.05);
		var ticks = 0;
		for (var frame = 1; frame <= 60; frame++)
		{
			var now = start.AddSeconds(frame / 60d);
			manager.BeginFrame(now);
			if (!script.TryConsumeDueTimer(now)) continue;
			ticks++;
			script.SetTimer(0.05);
		}

		Assert.Equal(20, ticks);
	}

	[Fact]
	public void Timer_and_scheduled_event_deadlines_use_the_shared_frame_time()
	{
		var manager  = new ScriptManager(new FakeLogger<ScriptManager>());
		var script   = new Script(manager, ScriptType.Weapon);
		var receiver = new ScriptVariable("receiver");
		var frame    = DateTime.UtcNow.AddMinutes(-1);
		manager.BeginFrame(frame);
		script.SetTimer(0.05);
		script.SetTimer(receiver, 0.05);
		script.ScheduleEvent(receiver, 0.05, "Ready");
		Assert.Equal(frame.AddSeconds(0.05), script.Timer);
		Assert.Empty(script.TakeDueScriptScheduledEvents(frame.AddSeconds(0.04)));
		Assert.Equal(2, script.TakeDueScriptScheduledEvents(frame.AddSeconds(0.05)).Count);
	}

	[Fact]
	public void Given_due_timer_When_timer_is_consumed_Then_timer_is_cleared()
	{
		var script = CreateScript();
		script.Timer = DateTime.UtcNow.AddSeconds(-1);

		var result = script.TryConsumeDueTimer(DateTime.UtcNow);

		Assert.True(result);
		Assert.Null(script.Timer);
	}

	[Fact]
	public void Given_future_timer_When_timer_is_consumed_Then_timer_remains_scheduled()
	{
		var script = CreateScript();
		script.Timer = DateTime.UtcNow.AddSeconds(1);

		var result = script.TryConsumeDueTimer(DateTime.UtcNow);

		Assert.False(result);
		Assert.NotNull(script.Timer);
	}

	[Fact]
	public void Given_zero_timer_When_timer_is_set_Then_timer_is_disabled()
	{
		var script = CreateScript();
		script.SetTimer(1);

		script.SetTimer(0);

		Assert.Null(script.Timer);
	}

	[Fact]
	public void Given_joined_class_receiver_timer_When_timer_is_due_Then_event_keeps_receiver()
	{
		var script   = CreateScript();
		var receiver = new ScriptVariable("actor");
		script.SetTimer(receiver, 0.001);

		var events = script.TakeDueScriptScheduledEvents(DateTime.UtcNow.AddSeconds(1));

		var scheduledEvent = Assert.Single(events);
		Assert.Equal("onTimeout", scheduledEvent.EventName);
		Assert.Same(receiver, scheduledEvent.Receiver);
	}

	[Fact]
	public void Given_joined_class_receiver_timer_When_timer_is_reset_Then_only_latest_timer_remains()
	{
		var script   = CreateScript();
		var receiver = new ScriptVariable("actor");
		script.SetTimer(receiver, 0.001);
		script.SetTimer(receiver, 10);

		var events = script.TakeDueScriptScheduledEvents(DateTime.UtcNow.AddSeconds(1));

		Assert.Empty(events);
	}

	[Fact]
	public void CancelEvents_is_receiver_scoped_and_does_not_cancel_timeout()
	{
		var script = CreateScript();
		var first  = new ScriptVariable("first");
		var second = new ScriptVariable("second");
		script.ScheduleEvent(first, 0, "Ready");
		script.ScheduleEvent(second, 0, "Ready");
		script.SetTimer(first, 0.001);
		script.CancelEvents(first, "ready");
		var events = script.TakeDueScriptScheduledEvents(DateTime.UtcNow.AddSeconds(1));
		Assert.Equal(2, events.Count);
		Assert.Contains(events, e => ReferenceEquals(e.Receiver, second) && e.EventName == "Ready");
		Assert.Contains(events, e => ReferenceEquals(e.Receiver, first) && e.EventName == "onTimeout");
		Assert.Empty(script.TakeDueScriptScheduledEvents(DateTime.UtcNow.AddSeconds(1)));
	}

	private static Script CreateScript() => new(new ScriptManager(new FakeLogger<ScriptManager>()), ScriptType.Weapon);
}