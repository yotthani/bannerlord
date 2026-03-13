# DualWieldPrototype

Frischer Bannerlord-Testmod fuer Dual-Wield-Prototyping ohne Abhaengigkeit vom bestehenden `HoN/DualWield`.

Aktuelle Idee:

- zweite 1h-Nahkampfwaffe wird an den linken Hand-Bone angeheftet
- linke Native-Actions werden direkt ausgelost
- Steuerung und Playback laufen ueber MCM
- das Dateilog enthaelt jetzt zusaetzlich Mainhand-/Offhand-Usage und `GetIsLeftStance()`
  zur Eingrenzung, ob der fehlende Trigger im Engine-Kontext oder in den Actions liegt

MCM:

- `Enable Prototype`
- `Live Messages`
- `Debug File Logging`
- `Deep Action Logging`
- `Trace Native Channel Calls`
- `Unarmed Trace Mode`
- `Control Mode`
- `Playback Mode`
- `Off-Hand Cooldown`
- `RMB Trigger Mode`
- `Off-Hand Test Action`
- `Ignore Action Priority`
- `Fallback To Overlay`
- `Rotation Preset`
- `Offset X/Y/Z`

Modi:

- `SplitMouse`: `LMB` bleibt main hand, `RMB` versucht linken Angriff, `LMB+RMB` laesst natives Blocken in Ruhe
- `AutoAlternate`: jeder zweite `LMB` versucht einen linken Angriff
- aktueller Vergleichspfad fuer `SplitMouse`:
- `RMB Trigger Mode`:
- `DirectSlash`: `RMB` feuert den Offhand-Slash direkt, sobald das linke Fenster offen ist
- `ReleaseFollowUp`: `RMB` versucht den alten Timing-Pfad nachzubauen: RH-Release erkennen, kurzer Delay, `ch1` clear, dann LH auf `ch0`
- `PrimedSlashLeft`: `RMB` versucht zuerst einen LH-Thrust als Primer auf `ch0`, dann nach kurzem Delay `slashleft`
- `LegacyCycle`: `RMB` stellt den alten Testzustand wieder her und iteriert pro Klick durch `slashright probe -> thrust -> slashleft`, inklusive Historien-Logging
- `RMB` erzwingt im Test weiter nur `act_quick_release_slashleft_1h_left_stance`
- `V` erzwingt nur `act_quick_release_thrust_1h_left_stance`
- `Off-Hand Test Action`:
- `Sequence`: nutzt das aktuelle Offhand-Profil
- `SlashLeftOnly`: erzwingt nur `act_quick_release_slashleft_1h_left_stance`
- `ThrustOnly`: erzwingt nur `act_quick_release_thrust_1h_left_stance`
- `FistLeftOnly`: erzwingt nur `act_quick_release_swingleft_fist_left_stance`

- Playback:
- `Channel0Combat`: aktiver Testpfad fuer die bestaetigten linken Angriffe
- `Channel1Overlay`: fuer die aktuellen `*_left_stance`-Actions praktisch unbrauchbar und wird zur Laufzeit auf `Channel0Combat` zurueckgebogen

- Logging:
- bei aktiviertem `Debug File Logging` schreibt das Modul nach `Modules/DualWieldPrototype/dualwieldprototype.log`
- beim Missionsstart werden `mission_init supported=... mode=... scene=...` geloggt
- `attack_diag ...` schreibt bei aktiviertem `Deep Action Logging` den genauen Vor-/Nachzustand
  pro erzwungenem Offhand-Angriff ins Dateilog
- `legacy_cycle_probe ...` und `legacy_cycle_resolved ...` loggen im Legacy-Zyklus zusaetzlich die letzten beiden erzwungenen Offhand-Actions
- `Trace Native Channel Calls` loggt zusaetzlich jeden `Agent.SetActionChannel`-Call fuer den Main-Agent in supported missions
- `Unarmed Trace Mode` deaktiviert die Dual-Wield-Eingriffslogik und dient nur dazu, nativen Faustkampf sauber zu tracen

Anmerkung:

Diese erste Version ist absichtlich ein Testbett. Fuer den aktuellen Stand gelten nur drei bestaetigte Linkshand-Patterns als brauchbar:

- `act_quick_release_slashleft_1h_left_stance`
- `act_quick_release_thrust_1h_left_stance`
- `act_quick_release_swingleft_fist_left_stance`

Bei den anderen `*_left_stance`-Actions ist in 1.3 oft nur die Fussstellung links, nicht der eigentliche Waffenangriff.

Die XML-Testdateien bleiben im Repo als Referenz fuer den alten `left_stance`-Bootstrap,
sind aber aktuell nicht mehr als aktive Test-Items im Modul registriert.
