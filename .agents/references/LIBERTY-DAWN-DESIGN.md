# Liberty Dawn Design and Strategy Reference

Liberty Dawn is a mod based on Command & Conquer running on the OpenRA engine.
It is an RTS—Real Time Strategy—game. Such games challenge the player's ability
to think fast, prioritize, solve problems, and creatively combine units, terrain,
and situations.

While Liberty Dawn can be fast-paced, its core philosophy focuses on creativity
and strategizing. A simple algorithmic game AI can never beat a human at this
alone, so AI design must also rely on fast reaction speed and pre-programmed,
approximate best-practice strategies, such as the air-AI module made for Skynet.

Many RTS games have simple technology trees and devolve into spamming whichever
unit happens to be best, or sometimes "spazzing" when a combination of units is
best. In Liberty Dawn this is practically impossible to pull off. Units are very
different and have distinct uses, strengths, and weaknesses; no unit or structure
is without limits. Overreliance on one strategy can be easily countered. Powerful
defenses or area attacks can shut down even the best spam attempt. It is often a
game of having the right unit or combination of units at the right time and place.

Liberty Dawn also innovates a technology system unlike the linear progression of
other RTS games: it is cyclical. As one player specializes, the enemy can
specialize to defeat that choice, after which the first player can specialize to
counter again. Timing those choices and fighting with or against mixed technology
armies is difficult. Sometimes a nominally weak specialization can be used well
enough to come out on top.

The economy is deceptively simple: build a harvester and get money. Tiberium,
however, is a living resource that grows across the map like a plant. Harvest too
much and nearby resources can collapse like over-hunted prey. Harvesters then
travel farther and suddenly become prey themselves. Green Tiberium is safe but
low-value; blue and red are increasingly dangerous, valuable, and fast to collect.
A Resonator grows resources faster but may block infantry paths in the base,
causing infantry to mutate and wreak havoc. Harvesters carrying red Tiberium can
become unstable when they cannot unload and explode violently. There is nothing
simple about it.

The goal of a game AI is not to master this chaos—even a human cannot—but to
survive. The AI must execute rules of thumb that are sensible in most situations
and compensate with fast reactions and precise management. A human may manage one
army at a time and set a few attack orders, while an AI can command five to ten
armies and scale an economy simultaneously. Rules of thumb are allowed; excessive
blunders by micro-managed armies and economy are not.

Cost-adjusted matchup notes use the cached combat crossover estimate for current
rules, baseline veterancy, and isolated actors. Ordinary matchups are ordered by
the cost of the crossover force divided by the subject actor's cost. `xN` means N
opponents are needed to match one subject; `/N` means N subjects are needed to
match one opponent. One-sided categorical and range matchups are retained for
mobile and immobile subjects. `immune` means only the subject can target the
opponent, `cannot engage` means only the opponent can target the subject, and
`outrange` means both can target each other but only one has effective engagement
range. One-sided entries are ordered by kill time because no crossover exists.
Each note states whether the subject can target ground, air, or both.

General class strengths and weaknesses use the median baseline kill time
normalized by target maximum health across all normally purchasable, targetable
actors in each available armor class, including unarmed buildings and
harvesters. This measures the time needed to remove a standard amount of health,
so fragile targets do not make a weapon appear strong against their entire
class. Offensive class scores are divided into best, middle, and worst thirds;
only the best and worst thirds are listed. A class that the subject cannot attack
but whose members can attack the subject participates as an infinite weakness.
Target cost does not affect class labels. Player-facing classes map to rules armor
as follows: Infantry = `None`, Buildings = `Wood`, Economy = `Tiberium` or
`TiberiumWood`, Light Armor = `Light`, and Heavy Armor = `Heavy`. Capture-only
specialists summarize their strength as Structures and their weakness as All
units. Dedicated anti-air actors that cannot attack ground summarize their
weakness as All ground units; immobile ground-only defenses similarly use All air
units. A valid but unreachable target has infinite normalized kill time. These
are strategic rules of thumb, not predictions of movement, terrain, focus fire,
detection, or other live battle conditions. Only normally purchasable
combat-capable units and defense towers receive matchup annotations; neutral,
hidden, and support-power actors are omitted. Two immobile actors are not included
in the individual matchup lists. An opponent's inability to retaliate is not
treated as a best matchup; these categorical immunity cases are retained only
when the subject itself cannot engage the opponent and therefore has a meaningful
worst matchup. Range-based one-sided matchups remain listed as outranging. Run
`./utility.sh cnc --combat-threat-annotations [ACTOR]` to regenerate the Markdown
annotations from current rules; the command prints output and does not edit this
document.

The cache treats Commando demolition as a zero-range instant defeat. Engineer
sabotage is a zero-range, consumed attack capped at half the target's maximum
health, so two Engineers must survive to reach the target. Contact specialists'
approach time accounts for exposure inside an armed defender's valid range. An
immobile actor cannot engage an opponent only when that opponent's valid weapon
outranges it; otherwise its normal range calculation is retained.

## Philosophy of Structures

### Construction Yard

The heart of the base and the player's ability to respond to changing
circumstances. It builds every normal structure and provides a large construction
radius. It is extremely durable, but losing it removes the ability to build until
an MCV can be deployed. Several Construction Yards increase build speed, support
expansion in several directions, and provide resilience against losing one base.

### Power Plant

Per point of power, the humble Power Plant grants more health than the Advanced
Power Plant. It is cheap enough to extend construction radius without risking an
important technology structure. Some AIs crawl defensive towers toward the enemy
base with Power Plants. Deadly.

### Advanced Power Plant

Produces twice as much power as a normal Power Plant for the same cash cost, but
has less health. It is economically and spatially efficient, but concentrating
power generation into a few plants creates an obvious target. It is available
through the Economy specialization.

### Refinery

Grants only a tiny amount of resource storage. It accepts harvesters quickly if
silos can store the excess dumped cash. Placing refineries near resources lowers
travel time, while placing them too far forward exposes both the building and
returning harvesters. Having no refinery is very bad.

### Tiberium Silo

Very cheap compared with the 4,000 credits it can store. Refineries hold almost
nothing, so a functioning economy needs silos before several harvesters return at
once. A player who ignores storage may have abundant Tiberium in transit but no
capacity to receive it.

### Barracks and Hand of Nod

Build infantry. They are faster and cheaper to establish than vehicle production
and can quickly turn spare cash into squishy but deadly infantry. They perform the
same basic job and differ by starting faction.

### Airstrip

Provides the standard vehicle-production queue and is available to both factions.
Vehicles arrive by cargo aircraft, making delivery visible and revealing the
approximate latitude of the base. The Airstrip is especially important for early
harvesters, Supply Trucks, and replacement MCVs.

### Weapons Factory

Produces vehicles locally without a cargo aircraft announcing every delivery.
Unlike the Airstrip, it is a Covert I structure, making inconspicuous local
vehicle production a technological choice.

### Helipad

Produces and repairs helicopters, making it the logistical foundation of an air
force rather than merely a factory. Aircraft carry ammunition and reload slowly
on their own. A Helipad is often a poor harassment target because it does not
directly influence economy and is frequently guarded by aircraft being built or
repaired.

### Communications Center

Provides radar, detects nearby stealth units, and contains the technology-upgrade
queue. It is also a prerequisite for rebuilding MCVs. Losing every MCV is not
automatically fatal if a Communications Center, Advanced Communications Center,
or Temple of Nod survives. Low power disables radar and stealth detection.

### Repair Facility

Repairs units and projects repairs around itself. It can sustain an entire
defensive formation. It is cheap but fragile enough for artillery or aircraft to
remove the healing advantage quickly, so place it behind defenses.

### Advanced Communications Center

The peak of Recon technology. It provides radar and stealth detection and grants
the Ion Cannon and A-10 Air Strike. The Ion Cannon is precise and immediate; the
Air Strike attacks a line and is better against infantry, buildings, and clustered
targets. It is expensive, consumes 200 power, and is worth it.

### Temple of Nod

The peak of Covert technology and source of the Nuclear Strike. It is extremely
durable and can serve as an alternative headquarters for MCV production. Its
missile recharges much more slowly than other support powers but devastates a
large area, especially on small, resource-scarce maps.

### Turret

A durable anti-tank defense. It is poor against infantry but punishes tanks and
light vehicles attacking without support. It detects stealth at very short range.
Mammoth Tanks can crush it, so an unsupported turret is not absolute. It cannot
lose power.

Cost-adjusted matchups — Cannot target: Air. Best against: Light Armor, Heavy
Armor. Worst against: All air units. Best: Hum-Vee (x21), Nod Buggy (x24), APC
(x11), Recon Bike (x9), Light Tank (x5). Worst: Stealth Tank
(outrange), Artillery (outrange), Rocket Launcher (outrange), Orca (cannot
engage), Apache Longbow (cannot engage).

### Guard Tower

The dedicated anti-infantry defense. It has good vision and short-range stealth
detection but only a small ammunition reserve before reloading. It turns infantry
to mist and damages light vehicles quickly. It cannot lose power.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Light
Armor. Worst against: All air units. Best: Rocket Soldier (x26), Minigunner
(x44), Engineer (x21), Commando (x5), Nod Buggy (x13). Worst: Stealth
Tank (outrange), Artillery (outrange), Rocket Launcher (outrange), Orca (cannot
engage), Apache Longbow (cannot engage).

### Advanced Guard Tower

Long-ranged but not exceptionally powerful. It attacks ground and air, detects
stealth over a useful distance, and provides vision for other weapons. It is
strongest behind a frontline. Low power disables its weapon and detector. Large
coverage areas make stealth assets difficult to slip through.

Cost-adjusted matchups — Can target: Ground and Air. Best against: Infantry,
Light Armor. Worst against: Buildings, Economy, Heavy Armor. Best: Apache
Longbow (x8), Hum-Vee (x20), Nod Buggy (x23), Rocket Soldier (x27), APC (x10).
Worst: Rocket Launcher (outrange), Mammoth Tank (/4), Medium Tank (x2),
Flamethrower (x14), Chemical Warrior (x15).

### SAM Site

A specialized anti-air defense unable to attack ground targets. Long vision makes
it an early-warning position, but tanks can drive past unharmed. It becomes useless
without power. Enough SAM Sites can make even the A-10 support power ineffective.

Cost-adjusted matchups — Cannot target: Ground. Best against: Light Armor. Worst
against: All ground units. Best: Apache Longbow (x12), Orca (x6). Worst:
Commando (cannot engage), Flame Tank (cannot engage), Chemical Tank (cannot
engage), Mammoth Tank (cannot engage), Stealth Tank (cannot engage).

### Obelisk of Light

An Economy III ground defense with enormous single-target damage against nearly
every ground unit, including Mammoth Tanks. It cannot fire at aircraft and uses
substantial power. It must charge before firing, so many cheap targets or attacks
from several directions waste less value than feeding it one expensive unit at a
time. Infantry can overwhelm it; artillery and Stealth Tanks outrange it. Several
Obelisks together remain a formidable barrier.

Cost-adjusted matchups — Cannot target: Air. Best against: Buildings, Heavy
Armor. Worst against: All air units. Best: APC (x20), Hum-Vee (x30), Light Tank
(x12), Nod Buggy (x30), Medium Tank (x10). Worst: Stealth Tank
(outrange), Artillery (outrange), Rocket Launcher (outrange), Orca (cannot
engage), Apache Longbow (cannot engage).

### Stealth Generator

Cloaks nearby structures and units. It can hide production, harvesters, and
defensive preparations, but costs substantial power and has little health.
Detectors expose the force. It enables harassment-focused play because the enemy
does not know where to strike. Hide all buildings when possible.

### Tiberium Resonator

Accelerates Tiberium spread by 750 percent and suppresses spontaneous red-
Tiberium instability events. It can create an extraordinary economy, but may
cover movement corridors, mutate infantry, and surround the base with dangerous
resources. It is not an automatic economic upgrade; it is a controlled ecological
disaster. Walling off the boosted resource area can help.

### Sandbag Barrier

Stops infantry and light vehicles, blocks some projectiles, has surprisingly high
health, and repairs itself. Tanks can crush it. Sandbags guide infantry, protect
vulnerable firing lines, and channel light vehicles into predictable approaches.

### Mined Chain-Link Barrier

Has little health but stops normal movement and cannot simply be crushed. It
explodes like a grenade when destroyed. It is a disposable minefield arranged as
a wall. When stealthed, it can redirect scouts without revealing the base.

### Concrete Barrier

The strongest conventional barrier. It stops infantry and most tanks, blocks some
projectiles, repairs itself, and can be crushed only by Mammoth Tanks. It protects
defenses and production exits but can also trap friendly vehicles or block a base.

## Capturable Structures

### Oil Derrick

Provides continuous cash. It requires only a cheap Engineer to capture but, over a
long game, can finance a significant army like a harvester gathering green
Tiberium. Its buildable area can establish a remote forward position.

### Hospital

Grants infantry self-healing. This greatly improves forces able to disengage,
especially experienced Commandos, Rocket Soldiers, and Chemical Warriors.

### Biological Laboratory

Grants infantry immunity to Tiberium terrain. Tiberium fields become concealed
routes instead of barriers and defending near a Resonator becomes less dangerous.

### Tech Center

Grants a very large area of vision and build radius. It normally produces no new
technology, but makes artillery, aircraft, and long-range defenses more effective.

These are neutral strategic objectives rather than normal construction options.

## Philosophy of Units

### Minigunner

Can look like a trash unit, but tanks struggle to hit him and infantry have large
vision. He is an excellent scout and strong against infantry. Unlike Flamethrowers,
Chemical Warriors, and Grenadiers, he carries no volatile ammunition. A few at an
enemy barracks exit can suppress infantry production while Rocket Soldiers wreck
the base. Even late, he protects tanks from anti-tank infantry.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Light
Armor. Worst against: Buildings, Economy, Heavy Armor. Best: Rocket Launcher
(outrange), Rocket Soldier (x4), Commando (/4), Stealth Tank (/5), Recon Bike
(/3). Worst: Apache Longbow (cannot engage), Orca (cannot engage), Guard
Tower (/44), Mammoth Tank (/93), Advanced Guard Tower (/48).

### Grenadier

Fast infantry with a powerful arcing attack against buildings and slow targets.
Grenades pass over some obstructions and punish dense forces, but Grenadiers may
explode when killed. Large formations may destroy themselves under area damage.
Available at Covert I.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Buildings,
Economy. Worst against: Light Armor, Heavy Armor. Best: Rocket Launcher
(outrange), Rocket Soldier (x6), Recon Bike (x2), Medium Tank (/2), Light Tank
(/2). Worst: Apache Longbow (cannot engage), Orca (cannot engage), Guard
Tower (/12), Nod Buggy (/5), Hum-Vee (/5).

### Rocket Soldier

The basic infantry answer to tanks. Slow, fragile, and poor against infantry and
buildings, but inexpensive relative to the vehicles he can destroy. He carries
anti-air rockets, though aircraft are not his preferred target.

Cost-adjusted matchups — Can target: Ground and Air. Best against: Light Armor,
Heavy Armor. Worst against: Infantry, Buildings, Economy. Best: Light Tank (x2),
Recon Bike (x2), Medium Tank (/2), APC (/2), Orca (/5). Worst: Chemical Warrior
(/6), Guard Tower (/26), Flamethrower (/5),
Grenadier (/6), Minigunner (/4).

### Flamethrower

Recon I anti-infantry infantry. Cheap, fast enough to accompany assaults, and
effective against infantry and buildings. His fuel tank may explode on death, so
dense formations can chain-react.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Buildings,
Economy. Worst against: Light Armor, Heavy Armor. Best: Rocket Launcher
(outrange), Rocket Soldier (x5), Recon Bike (x2), Stealth Tank (/2), APC (/2).
Worst: Apache Longbow (cannot engage), Orca (cannot engage), Guard
Tower (/13), Mammoth Tank (/31), Chemical Warrior (/2).

### Chemical Warrior

Covert I infantry with more health than ordinary soldiers and immunity to
Tiberium terrain. Chemical spray is useful against infantry and Tiberium/economy
structures. The unit retains infantry vulnerabilities and may explode on death.
Tiberium fields can become its safest route to bases or harvesters.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Economy.
Worst against: Light Armor, Heavy Armor. Best: Rocket Launcher (outrange),
Rocket Soldier (x6), Recon Bike (x2), Stealth Tank (/2), APC (/2). Worst: Apache
Longbow (cannot engage), Orca (cannot engage), Guard Tower
(/15), Mammoth Tank (/38), Advanced Guard Tower (/15).

### Engineer

Unarmed but strategically devastating. Engineers capture or sabotage structures,
repair bridges, and restore vehicle husks. Inside a transport, an Engineer is a
threat far beyond his direct cost and may capture missing technology.

Cost-adjusted matchups — Cannot target: Air. Best against: Structures. Worst
against: All units. Best: Obelisk of Light (/4), Turret (/3), Advanced Guard
Tower (/16), Guard Tower (/21). Worst: Chemical Tank (cannot
engage), Apache Longbow (cannot engage), Hum-Vee (cannot engage), Nod Buggy
(cannot engage), Flame Tank (cannot engage).

### Commando

An expensive Recon III sniper with enormous vision. He kills infantry at range
and demolishes buildings after planting explosives. Vehicles counter him only if
they find and reach him. Transports, terrain, and distractions multiply his value.

Cost-adjusted matchups — Cannot target: Air. Best against: Buildings. Worst
against: Light Armor, Heavy Armor. Best: Turret (x23), Obelisk of Light (x8),
Advanced Guard Tower (x11), Guard Tower (x5), Rocket Soldier (x10). Worst: Chemical
Tank (cannot engage), Flame Tank (cannot engage), Mammoth Tank (cannot engage),
Apache Longbow (cannot engage), Hum-Vee (cannot engage).

### Sheep

A hidden joke unit not normally produced. It can test how quickly an AI learns to
spam a new unit. It is highly vulnerable to area attacks and anti-infantry weapons.

### Mobile Construction Vehicle

Deploys into a Construction Yard and takes a long time to build. It has poor
vision and no weapon but enables recovery from base loss and new expansions. At
Covert III it can cloak, allowing stealth bases along otherwise suicidal routes.

### Harvester

The economy's foundation. It is unarmed, has little vision, and carries twenty
loads of green, blue, or red Tiberium. Red cargo makes it explosive when killed.
An unstable red-loaded Harvester flashes and can detonate with almost tactical-
nuclear force if it cannot unload.

### Stealth Harvester

Costs only slightly more than a normal Harvester and unlocks at Covert II. It
remains cloaked during normal operation, making distant fields safer. Damage,
repair, instability warnings, infantry, or detectors can expose it. Aircraft can
find it. Stealth reduces risk but does not remove it.

### Hum-Vee

A fast Recon I scout with a machine gun. Cheap and reasonably durable for its
class, it chases infantry but loses to tanks. Its real value is revealing targets
and killing infantry without wasting expensive tank shots.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Light
Armor. Worst against: Buildings, Economy, Heavy Armor. Best: Rocket Launcher
(outrange), Artillery (x3), APC (x2), Recon Bike (x2), Rocket Soldier (x4).
Worst: Orca (cannot engage), Apache Longbow (cannot engage), Turret (/21),
Advanced Guard Tower (/20), Obelisk of Light (/30).

### Nod Buggy

The Covert I Hum-Vee counterpart. Cheaper and much faster but less durable. It
raids Rocket Soldiers, Engineers, and exposed economy units if it avoids tanks and
defensive fire.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Light
Armor. Worst against: Buildings, Economy, Heavy Armor. Best: Rocket Launcher
(outrange), Artillery (outrange), APC (x2), Rocket Soldier (x4), Minigunner
(x7). Worst: Orca (cannot engage), Apache Longbow (cannot engage), Turret (/24),
Advanced Guard Tower (/23), Guard Tower (/13).

### Recon Bike

One of the fastest units. Rockets attack vehicles and, inaccurately, aircraft,
making it a flexible raider and emergency anti-air unit. Its health is extremely
low. Bikes win by choosing engagements, concentrating fire, and leaving quickly.

Cost-adjusted matchups — Can target: Ground and Air. Best against: Light Armor,
Heavy Armor. Worst against: Infantry, Buildings, Economy. Best: Rocket Launcher
(outrange), Artillery (outrange), APC (x2), Nod Buggy (x2), Stealth Tank (/2).
Worst: Chemical Warrior (/2), Flamethrower (/2), Turret (/9), Advanced Guard
Tower (/9), Guard Tower (/5).

### APC

A Covert I armored transport carrying ten infantry with permanent stealth. It is
fast and lightly armed, but its cargo is the threat. Engineers, Rocket Soldiers,
Chemical Warriors, or a Commando can turn one opening into a destroyed base.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Light
Armor. Worst against: Buildings, Economy, Heavy Armor. Best: Rocket Launcher
(outrange), Artillery (x2), Recon Bike (x2), Minigunner (x5), Nod Buggy (x2).
Worst: Orca (cannot engage), Apache Longbow (cannot engage), Turret (/11),
Obelisk of Light (/20), Advanced Guard Tower (/10).

### Light Tank

A fast Recon I tank with heavy armor and a respectable cannon. Weaker than a
Medium Tank head-on, but much better at scouting, flanking, retreating, and hunting
light vehicles. Trading stationary shots like a Mammoth wastes it.

Cost-adjusted matchups — Cannot target: Air. Best against: Buildings, Economy,
Light Armor. Worst against: Infantry, Heavy Armor. Best: Rocket Launcher
(outrange), Artillery (x7), Hum-Vee (x7), APC (x4), Nod Buggy (x8). Worst: Orca
(cannot engage), Apache Longbow (cannot engage), Turret (/5), Obelisk of Light
(/12), Rocket Soldier (/2).

### Medium Tank

Faster and cheaper than the Mammoth while retaining heavy armor and a serious
cannon. It is the Economy I workhorse, excellent against light vehicles and able
to chase raiders, dynamically protect fields, or attack weak points.

Cost-adjusted matchups — Cannot target: Air. Best against: Buildings, Economy,
Light Armor. Worst against: Infantry, Heavy Armor. Best: Rocket Launcher
(outrange), Artillery (x7), Hum-Vee (x9), APC (x5), Nod Buggy (x10). Worst: Orca
(cannot engage), Apache Longbow (cannot engage), Obelisk of Light (/10), Turret
(/3), Grenadier (x2).

### Mammoth Tank

The strongest conventional unit. It self-heals, has massive health, and attacks
ground and air. It is extremely slow, turns slowly, and sees little. Long-range
weapons outside its vision can attack safely. It is a mobile fortification that
needs scouts, detectors, and cheaper screens. Enough Mammoths can overwhelm Recon
economically.

Cost-adjusted matchups — Can target: Ground and Air. Best against: Buildings,
Economy, Light Armor. Worst against: Infantry, Heavy Armor. Best: Apache Longbow
(x12), Minigunner (x93), Hum-Vee (x28), Nod Buggy (x31), Artillery (x15). Worst:
Obelisk of Light (/3), Turret (x2), Grenadier (x15), Stealth Tank (x3), Rocket
Soldier (x11).

### Flame Tank

A fast Recon II assault vehicle with good health, twin flamethrowers, and a violent
death explosion. It destroys infantry, buildings, and light vehicles but struggles
against true tanks. Covert II disables its production, trading it for other tools.
After aircraft clears an area, it removes buildings quickly.

Cost-adjusted matchups — Cannot target: Air. Best against: Buildings, Economy.
Worst against: Light Armor, Heavy Armor. Best: Rocket Launcher (outrange),
Artillery (x5), APC (x4), Stealth Tank (x2), Minigunner (x13). Worst: Orca
(cannot engage), Apache Longbow (cannot engage), Turret (/3), Mammoth Tank (/8),
Obelisk of Light (/6).

### Chemical Tank

The Covert II Flame Tank alternative. It is cloaked, fast, and effective against
infantry and Tiberium/nuclear targets. Its armor is light and it is poor against
tanks and aircraft. Detection makes it expensive prey. It often turns enemy
infantry into Visceroids.

Cost-adjusted matchups — Cannot target: Air. Best against: Infantry, Economy.
Worst against: Light Armor, Heavy Armor. Best: Rocket Launcher (outrange),
Artillery (x4), APC (x3), Stealth Tank (x2), Recon Bike (x3). Worst: Orca
(cannot engage), Apache Longbow (cannot engage), Mammoth Tank (/13), Turret
(/4), Medium Tank (/4).

### Artillery

A Covert II mid-range weapon with little health. Faster than the Rocket Launcher
and effective against infantry, vehicles, and buildings. It fires continuously
and supports normal squads. It needs forward vision because it shoots farther
than it sees.

Cost-adjusted matchups — Cannot target: Air. Best against: Buildings, Economy.
Worst against: Infantry, Heavy Armor. Best: Rocket Launcher (outrange), Guard
Tower (outrange), Turret (outrange), Obelisk of Light (outrange), Stealth Tank
(x2). Worst: Orca (cannot engage), Apache Longbow (cannot engage), Nod Buggy
(outrange), Recon Bike (outrange), Light Tank (/7).

### Rocket Launcher

Economy II artillery with extreme range and devastating ground fire. It is slow,
fragile, and has finite ammunition. Protected by tanks and scouts it dismantles a
base outside defensive range; unsupported, it may die before turning.

Cost-adjusted matchups — Cannot target: Air. Best against: Buildings, Economy,
Heavy Armor. Worst against: Infantry, Light Armor. Best: Guard Tower (outrange),
Turret (outrange), Advanced Guard Tower (outrange), Obelisk of Light (outrange),
Rocket Soldier (x5). Worst: Orca (cannot engage), Chemical Tank (outrange),
Flame Tank (outrange), Apache Longbow (cannot engage), Light Tank (outrange).

### Mobile SAM

Economy II anti-air vehicle with good speed and vision but only two missiles before
reloading and no ground attack. It belongs behind the army. Even a Minigunner can
destroy an unsupported Mobile SAM. It is essential against air harassment.

Cost-adjusted matchups — Cannot target: Ground. Best against: Light Armor. Worst
against: All ground units. Best: Apache Longbow (x4), Orca (x2). Worst: Obelisk
of Light (cannot engage), Mammoth Tank (cannot engage), Turret (cannot engage),
Flame Tank (cannot engage), Chemical Tank (cannot engage).

### Stealth Tank

The peak Covert combat vehicle: fast, long-ranged, cloaked, and effective against
harvesters, tanks, and buildings. Its armor is extremely weak. Recon detectors
expose it, after which even riflemen kill it quickly. Its classic role is hit-and-
run anti-armor raiding.

Cost-adjusted matchups — Can target: Ground and Air. Best against: Buildings,
Economy, Heavy Armor. Worst against: Infantry, Light Armor. Best: Rocket
Launcher (outrange), Guard Tower (outrange), Turret (outrange), Obelisk of Light
(outrange), Artillery (x4). Worst: Chemical Warrior (x2), Flamethrower (x2),
Advanced Guard Tower (/4), Orca (/6), Chemical Tank (/2).

### Mobile HQ

An unarmed Recon III detection vehicle with enormous vision and stealth-detection
range. It is fragile, but turns every weapon behind it into an anti-stealth weapon
and prevents cloaked assets from dismantling an army unseen.

### Supply Truck

Costs 1,000 credits and transfers 1,000 credits to another player. It creates no
profit. Losing it loses both vehicle and money.

### Chinook Transport

A fast, unarmed, fragile helicopter carrying infantry and vehicles up to ten cargo
points. It bypasses cliffs, Tiberium, and chokepoints, but losing a loaded Chinook
may cost more than losing a Mammoth Tank.

### Apache Longbow

A Recon II gunship with substantial health and ammunition. Its guns excel against
infantry, light vehicles, and aircraft but are weak against tanks. It clears light
units and harvesters but wastes ammunition on buildings.

Cost-adjusted matchups — Can target: Ground and Air. Best against: Infantry,
Light Armor. Worst against: Buildings, Heavy Armor. Best: Rocket Soldier (x14),
Stealth Tank (x3), Recon Bike (x4), Orca (/2), Mobile S.A.M. (/4). Worst: SAM
Site (/12), Advanced Guard Tower (/8), Mammoth Tank (/12), Mobile S.A.M. (/4),
Orca (/2).

### Orca

A Recon III missile gunship and the fastest normal combat unit. It attacks ground
and air, especially tanks and buildings, but has little health and only eight
missiles. Orcas concentrate damage and leave before anti-air responds. They can
dodge missiles but remain vulnerable to splash; hovering empty over a base is
fatal.

Cost-adjusted matchups — Can target: Ground and Air. Best against: Light Armor,
Heavy Armor. Worst against: Infantry, Buildings. Best: Stealth Tank (x6), Recon
Bike (x8), Apache Longbow (x2), Rocket Soldier (x5), Mammoth Tank (/2). Worst:
SAM Site (/6), Mobile S.A.M. (/2), Advanced Guard Tower (/3), Mammoth Tank
(/2), Rocket Soldier (x5).

### A-10 Bomber

Not normally built. The Advanced Communications Center calls groups of three for
an Air Strike along a selected line. Guns and napalm punish infantry and
structures. The aircraft are real and may be shot down by prepared anti-air. Their
flight also scouts much of the map.

### Supply Aircraft

The normally invulnerable Airstrip cargo aircraft. It delivers vehicles from the
map edge. This is a logistical advantage and an intelligence leak: opponents can
see the latitude of deliveries.

### Visceroid

Usually a neutral mutant or crate result rather than a trained unit. It spits
Tiberium, attacks infantry and buildings, and heals rapidly on Tiberium. Tanks
counter it. Chemical Tanks may create Visceroids from enemy infantry.

## Playing Economy

A good Economy player defends with Obelisks, Sandbags, and SAM Sites sustained by
Repair Facilities. They wall off Tiberium fields and use Resonators to survive
heavy harvesting. Cheaper health and armor let them drown enemies in tanks and
artillery. Tanks survive Recon defenses; artillery outranges them; Mobile SAMs
protect harvesters and artillery from aircraft. A strong player uses mobile Medium
Tank and Mobile SAM groups to defend fields and exploit weak points while Mammoths
and artillery lock the frontline. Minigunners protect tanks from Rocket Soldiers.

## Playing Covert

Covert has strong infantry and many stealth advantages. One Chemical Warrior may
destroy an early economy; a careful Grenadier rush may overwhelm a base. Fast
units attack weak points. A strong Covert player's base is difficult to find:
stealthed MCVs, power, and refineries spread across the map while defensible normal
production remains concentrated. Mid-game APCs deliver dangerous infantry into
unexpected positions. Later—or sooner with fast tech—Stealth Tanks hunt
harvesters to extinction. Stealthed Chemical Tanks create chaos and remove Recon
infantry. Against Economy's limited normal detection, Covert is nowhere and
everywhere.

## Playing Recon

Recon sees all. Support powers strike revealed targets and aircraft mop up the
rest. Most Recon assets detect stealth. Aircraft scour the map and destroy stealth
harvesters and lightly armored units. Long-ranged towers secure sectors. Mobile
HQs reveal huge areas. Skilled Orcas evade missiles. A good Recon player clears
harvesters with aircraft, destroys raids with Ion Cannons, bombs strategic
buildings with A-10s, and sends flame and nimble tanks to finish. Concrete walls
strengthen defensive positions. Recon's weakness is being overwhelmed by the
Economy player's stream of tanks and anti-air.

## Main AI Types

### Brutalis

Uses the Economy branch and should play accordingly. Its AI-scale construction
management can build bases and then crawl Obelisks into the enemy base.

### VIKI

Uses the Covert branch and should play accordingly. Inhuman management of many
harassment squads and Stealth Tanks should make it deadly.

### Skynet

Uses the Recon branch and should play accordingly. Its inhuman air-AI module lets
the air force quickly and effectively eliminate anything exposed without anti-air.
Even against mediocre Economy players with decent anti-air, it can hold its own.

### Iron Reaper

Like a dark-robed reaper in the shape of a Terminator, it exists to end suffering.
It uses whichever technology the enemy is weak against or, when allowed, all of
them. It should find the shortest path to the enemy's destruction.

### Other AIs

The other AIs are intended for less experienced players.
