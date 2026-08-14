FixtureProducerCell = CPos.New(35, 40)

WorldLoaded = function()
	local owner = Player.GetPlayer("CNC89Fixture")
	local producer = Actor.Create("pyle", true, { Owner = owner, Location = FixtureProducerCell })

	Trigger.AfterDelay(1, function()
		local queued = producer.Build({ "e1" }) and producer.IsProducing("e1")
		if queued then
			Media.Debug("CNC89 BUILD producer=fixture-barracks type=" .. producer.Type ..
				" owner=" .. producer.Owner.InternalName .. " location=" .. producer.Location.X ..
				"," .. producer.Location.Y .. " queue=Infantry item=e1 state=queued tick=" .. DateTime.GameTime)
			Trigger.AfterDelay(1250, function()
				Media.Debug("CNC89 READY tick=" .. DateTime.GameTime)
			end)
		end
	end)
end
