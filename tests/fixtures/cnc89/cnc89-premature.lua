FixtureActorCell = CPos.New(32, 40)

WorldLoaded = function()
	local owner = Player.GetPlayer("CNC89Fixture")
	local actor = Actor.Create("e1", true, { Owner = owner, Location = FixtureActorCell })
	Media.Debug("CNC89 ACTOR label=fixture-scout type=" .. actor.Type ..
		" owner=" .. actor.Owner.InternalName .. " location=" .. actor.Location.X ..
		"," .. actor.Location.Y .. " tick=" .. DateTime.GameTime)
	Trigger.AfterDelay(1250, function()
		Media.Debug("CNC89 READY tick=" .. DateTime.GameTime)
	end)
end
